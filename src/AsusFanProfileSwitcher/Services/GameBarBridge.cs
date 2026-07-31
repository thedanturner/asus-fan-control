using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace AsusFanProfileSwitcher.Services;

internal sealed record GameBarRequest(string Command, string? ProfileName);

internal sealed record GameBarProfile(string Name, string DisplayName, bool IsActive);

internal sealed record GameBarState(
    bool Connected,
    string Status,
    IReadOnlyList<GameBarProfile> Profiles);

internal sealed record GameBarResponse(
    bool Success,
    string Message,
    GameBarState? State = null);

/// <summary>
/// Hosts the small, local-only IPC surface consumed by the packaged Game Bar widget.
/// The pipe grants access to the current interactive user and UWP app containers, but
/// all privileged profile validation and switching remains in the desktop process.
/// </summary>
internal sealed class GameBarBridge : IDisposable
{
    public const string PipeName = @"LOCAL\AsusFanProfileSwitcher.GameBar.v1";
    // Keep this synchronized with Package.appxmanifest Identity Name. Partner
    // Center may assign a different value when the package is associated.
    private const string ExpectedPackageFamilyPrefix =
        "AsusFanProfileSwitcher.GameBar_";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Func<GameBarRequest, Task<GameBarResponse>> _handler;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _serverTask;

    public GameBarBridge(Func<GameBarRequest, Task<GameBarResponse>> handler)
    {
        _handler = handler;
        _serverTask = Task.Run(RunAsync);
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        try
        {
            _serverTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Cancellation and a client disconnect are normal during application shutdown.
        }
        _shutdown.Dispose();
    }

    private async Task RunAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                await using var pipe = CreatePipe();
                await pipe.WaitForConnectionAsync(_shutdown.Token);
                if (!IsExpectedWidgetClient(pipe))
                {
                    continue;
                }
                await ProcessClientAsync(pipe, _shutdown.Token);
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
                return;
            }
            catch (IOException)
            {
                // A widget can disappear when Game Bar closes. Accept the next connection.
            }
            catch
            {
                // Keep the controller usable even if the optional widget bridge fails.
                await Task.Delay(250, _shutdown.Token).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessClientAsync(Stream pipe, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            pipe,
            new UTF8Encoding(false),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true);
        using var writer = new StreamWriter(
            pipe,
            new UTF8Encoding(false),
            bufferSize: 4096,
            leaveOpen: true)
        {
            AutoFlush = true
        };

        GameBarResponse response;
        try
        {
            var payload = await reader.ReadLineAsync(cancellationToken);
            var request = string.IsNullOrWhiteSpace(payload)
                ? null
                : JsonSerializer.Deserialize<GameBarRequest>(payload, JsonOptions);
            response = request is null
                ? new GameBarResponse(false, "The widget sent an invalid request.")
                : await _handler(request);
        }
        catch (Exception exception)
        {
            response = new GameBarResponse(false, exception.Message);
        }

        await writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static NamedPipeServerStream CreatePipe()
    {
        var security = new PipeSecurity();
        var currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser is not null)
        {
            security.AddAccessRule(new PipeAccessRule(
                currentUser,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
        }

        // ALL APPLICATION PACKAGES: required for a UWP view hosted by Game Bar.
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier("S-1-15-2-1"),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }

    private static bool IsExpectedWidgetClient(NamedPipeServerStream pipe)
    {
        if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var processId))
        {
            return false;
        }

        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            uint length = 0;
            _ = GetPackageFamilyName(process, ref length, null);
            if (length == 0)
            {
                return false;
            }

            var familyName = new StringBuilder((int)length);
            return GetPackageFamilyName(process, ref length, familyName) == 0 &&
                familyName.ToString().StartsWith(
                    ExpectedPackageFamilyPrefix,
                    StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(
        SafePipeHandle pipe,
        out uint clientProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetPackageFamilyName(
        IntPtr process,
        ref uint packageFamilyNameLength,
        StringBuilder? packageFamilyName);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
