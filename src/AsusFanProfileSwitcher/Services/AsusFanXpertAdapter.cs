using System.Diagnostics;
using System.Text.RegularExpressions;
using AsusFanProfileSwitcher.Models;
using Microsoft.Win32;

namespace AsusFanProfileSwitcher.Services;

internal sealed record FanXpertConnection(
    bool IsConnected,
    string? ServiceName,
    string? ActiveStorePath,
    string Summary);

internal sealed record ApplyResult(string BackupPath, string ActiveStorePath);

internal sealed class AsusFanXpertAdapter
{
    private static readonly string[] KnownServiceNames =
    [
        "AsusFanControlService",
        "ASUSFanControlService"
    ];

    public FanXpertConnection Discover()
    {
        var services = DiscoverCandidateServices().ToArray();

        foreach (var service in services)
        {
            var store = FindStoreBesideExecutable(service.ImagePath);
            if (store is not null)
            {
                return Connected(service.Name, store);
            }
        }

        var fallbackStore = FindStoreInLegacyInstallDirectory();
        if (fallbackStore is not null)
        {
            var serviceName = (services.Length > 0 ? services[0].Name : null)
                ?? KnownServiceNames.FirstOrDefault(ServiceExists);

            if (serviceName is not null)
            {
                return Connected(serviceName, fallbackStore);
            }
        }

        if (services.Length > 0)
        {
            return new FanXpertConnection(
                false,
                services[0].Name,
                null,
                "ASUS fan service found, but its active FanStore.xml could not be located.");
        }

        return new FanXpertConnection(
            false,
            null,
            null,
            "Compatible Fan Xpert 2+/3 service not detected. Newer Fan Xpert 4 builds do not expose a supported switching API.");
    }

    public async Task<ApplyResult> ApplyAsync(
        FanProfile profile,
        FanXpertConnection connection,
        CancellationToken cancellationToken = default)
    {
        if (!connection.IsConnected ||
            string.IsNullOrWhiteSpace(connection.ServiceName) ||
            string.IsNullOrWhiteSpace(connection.ActiveStorePath))
        {
            throw new InvalidOperationException(connection.Summary);
        }

        ProfileCatalog.ValidateXml(profile.FilePath);

        var activeStore = connection.ActiveStorePath;
        var profileRoot = ProfileCatalog.GetRootName(profile.FilePath);
        var storeRoot = ProfileCatalog.GetRootName(activeStore);
        if (!string.Equals(profileRoot, storeRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"The selected XML root (“{profileRoot}”) does not match the active ASUS store (“{storeRoot}”).");
        }

        var backupDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AsusFanProfileSwitcher",
            "Backups");
        Directory.CreateDirectory(backupDirectory);

        var backupPath = Path.Combine(
            backupDirectory,
            $"FanStore-{DateTime.Now:yyyyMMdd-HHmmss-fff}.xml");

        if (File.Exists(activeStore))
        {
            File.Copy(activeStore, backupPath, false);
        }
        else
        {
            throw new FileNotFoundException("The active ASUS FanStore.xml disappeared.", activeStore);
        }

        var serviceWasRunning = await IsServiceRunningAsync(
            connection.ServiceName,
            cancellationToken);

        try
        {
            if (serviceWasRunning)
            {
                await SetServiceStateAsync(
                    connection.ServiceName,
                    "stop",
                    1,
                    "stopped",
                    cancellationToken);
            }

            // Copying over an existing file retains its access control list.
            File.Copy(profile.FilePath, activeStore, true);
            ProfileCatalog.ValidateXml(activeStore);

            await SetServiceStateAsync(
                connection.ServiceName,
                "start",
                4,
                "running",
                cancellationToken);

            return new ApplyResult(backupPath, activeStore);
        }
        catch
        {
            try
            {
                File.Copy(backupPath, activeStore, true);
                if (serviceWasRunning)
                {
                    await SetServiceStateAsync(
                        connection.ServiceName,
                        "start",
                        4,
                        "running",
                        CancellationToken.None);
                }
            }
            catch
            {
                // Preserve the original failure. The backup path is included by the UI.
            }

            throw;
        }
    }

    private static FanXpertConnection Connected(string serviceName, string store) =>
        new(
            true,
            serviceName,
            store,
            $"Connected to {serviceName}");

    private static IEnumerable<(string Name, string ImagePath)> DiscoverCandidateServices()
    {
        using var servicesKey = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Services");
        if (servicesKey is null)
        {
            yield break;
        }

        foreach (var name in servicesKey.GetSubKeyNames())
        {
            using var serviceKey = servicesKey.OpenSubKey(name);
            var displayName = serviceKey?.GetValue("DisplayName")?.ToString() ?? "";
            var isCandidate =
                KnownServiceNames.Contains(name, StringComparer.OrdinalIgnoreCase) ||
                (name.Contains("asus", StringComparison.OrdinalIgnoreCase) &&
                 name.Contains("fan", StringComparison.OrdinalIgnoreCase)) ||
                (displayName.Contains("asus", StringComparison.OrdinalIgnoreCase) &&
                 displayName.Contains("fan", StringComparison.OrdinalIgnoreCase));

            var imagePath = serviceKey?.GetValue("ImagePath")?.ToString();
            if (isCandidate && !string.IsNullOrWhiteSpace(imagePath))
            {
                yield return (name, imagePath);
            }
        }
    }

    private static string? FindStoreBesideExecutable(string rawImagePath)
    {
        var executable = ExtractExecutablePath(rawImagePath);
        if (executable is null)
        {
            return null;
        }

        var directory = Path.GetDirectoryName(executable);
        if (directory is null)
        {
            return null;
        }

        var store = Path.Combine(directory, "FanStore.xml");
        return File.Exists(store) ? store : null;
    }

    private static string? FindStoreInLegacyInstallDirectory()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };

        foreach (var root in roots.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var serviceRoot = Path.Combine(root, "ASUS", "AsusFanControlService");
            if (!Directory.Exists(serviceRoot))
            {
                continue;
            }

            try
            {
                var newestStore = Directory
                    .EnumerateFiles(serviceRoot, "FanStore.xml", SearchOption.AllDirectories)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(info => info.LastWriteTimeUtc)
                    .FirstOrDefault();
                if (newestStore is not null)
                {
                    return newestStore.FullName;
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Continue to the next known install root.
            }
        }

        return null;
    }

    private static string? ExtractExecutablePath(string rawImagePath)
    {
        var expanded = Environment.ExpandEnvironmentVariables(rawImagePath.Trim());
        if (expanded.StartsWith('"'))
        {
            var closingQuote = expanded.IndexOf('"', 1);
            return closingQuote > 1 ? expanded[1..closingQuote] : null;
        }

        var exeEnd = expanded.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeEnd >= 0 ? expanded[..(exeEnd + 4)] : expanded;
    }

    private static bool ServiceExists(string serviceName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{serviceName}");
        return key is not null;
    }

    private static async Task<bool> IsServiceRunningAsync(
        string serviceName,
        CancellationToken cancellationToken)
    {
        var output = await RunScAsync(["query", serviceName], cancellationToken);
        return HasServiceState(output, 4, "RUNNING");
    }

    private static async Task SetServiceStateAsync(
        string serviceName,
        string command,
        int expectedStateCode,
        string expectedStateName,
        CancellationToken cancellationToken)
    {
        await RunScAsync([command, serviceName], cancellationToken, allowNonZeroExit: true);

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = await RunScAsync(
                ["query", serviceName],
                cancellationToken,
                allowNonZeroExit: true);
            if (HasServiceState(
                    status,
                    expectedStateCode,
                    expectedStateName.ToUpperInvariant()))
            {
                return;
            }

            await Task.Delay(300, cancellationToken);
        }

        throw new InvalidOperationException(
            $"The ASUS service did not reach the {expectedStateName} state.");
    }

    private static bool HasServiceState(string output, int stateCode, string englishName) =>
        output.Contains(englishName, StringComparison.OrdinalIgnoreCase) ||
        Regex.IsMatch(output, $@":\s*{stateCode}\s", RegexOptions.CultureInvariant);

    private static async Task<string> RunScAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool allowNonZeroExit = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "sc.exe"),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows Service Control could not be started.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = (await outputTask) + (await errorTask);

        if (!allowNonZeroExit && process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Windows Service Control failed ({process.ExitCode}). {output.Trim()}");
        }

        return output;
    }
}
