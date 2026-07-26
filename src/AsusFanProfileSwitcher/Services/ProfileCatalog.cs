using System.Security.Cryptography;
using System.Globalization;
using System.Xml.Linq;
using AsusFanProfileSwitcher.Models;

namespace AsusFanProfileSwitcher.Services;

internal sealed class ProfileCatalog
{
    public const string DefaultProfileDirectory =
        @"C:\ProgramData\ASUS\DIP\FanXpert\Profiles";

    public IReadOnlyList<FanProfile> Load(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(directory, "*.xml", SearchOption.TopDirectoryOnly)
            .Select(TryRead)
            .Where(profile => profile is not null)
            .Cast<FanProfile>()
            .OrderBy(ProfileOrder)
            .ThenBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static void ValidateXml(string filePath)
    {
        _ = GetRootName(filePath);
    }

    public static string GetRootName(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The selected profile no longer exists.", filePath);
        }

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var document = XDocument.Load(stream, LoadOptions.None);
        if (document.Root is null)
        {
            throw new InvalidDataException("The profile XML does not contain a root element.");
        }

        return document.Root.Name.LocalName;
    }

    public static string CalculateHash(string filePath)
    {
        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static FanProfile? TryRead(string filePath)
    {
        try
        {
            ValidateXml(filePath);
            var info = new FileInfo(filePath);
            return new FanProfile(
                Path.GetFileNameWithoutExtension(filePath),
                Path.GetFileNameWithoutExtension(filePath),
                filePath,
                info.Length,
                info.LastWriteTime,
                CalculateHash(filePath));
        }
        catch
        {
            // A malformed XML file is not a selectable fan profile.
            return null;
        }
    }

    private static int ProfileOrder(FanProfile profile) =>
        profile.Name.ToLowerInvariant() switch
        {
            var name when name.Contains("silent") => 0,
            var name when name.Contains("standard") => 1,
            var name when name.Contains("turbo") => 2,
            var name when name.Contains("full") => 3,
            _ => 4
        };

    public IReadOnlyList<FanCurve> LoadCurves(string filePath)
    {
        try
        {
            var document = XDocument.Load(filePath, LoadOptions.None);
            if (document.Root is null)
            {
                return [];
            }

            var fans = document
                .Descendants()
                .Where(element => IsNamed(element, "fan"))
                .Where(element => element.Descendants().Any(point => IsNamed(point, "point")))
                .ToArray();

            if (fans.Length == 0 &&
                document.Descendants().Any(element => IsNamed(element, "point")))
            {
                fans = [document.Root];
            }

            return fans
                .Select((fan, index) => ReadCurve(fan, index))
                .Where(curve => curve.Points.Count > 0)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    public FanProfile Duplicate(
        FanProfile source,
        string destinationDirectory,
        string fileName,
        string displayName)
    {
        ValidateXml(source.FilePath);
        Directory.CreateDirectory(destinationDirectory);
        var safeName = MakeSafeFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            throw new InvalidDataException("Enter a valid profile file name.");
        }

        var destination = Path.Combine(destinationDirectory, safeName + ".xml");
        if (File.Exists(destination))
        {
            throw new IOException($"A profile named “{safeName}” already exists.");
        }

        File.Copy(source.FilePath, destination, false);
        var info = new FileInfo(destination);
        return new FanProfile(
            safeName,
            displayName.Trim(),
            destination,
            info.Length,
            info.LastWriteTime,
            CalculateHash(destination));
    }

    private static FanCurve ReadCurve(XElement fan, int index)
    {
        var id = GetAttribute(fan, "key")
            ?? GetAttribute(fan, "id")
            ?? index.ToString(CultureInfo.InvariantCulture);
        var name = GetAttribute(fan, "name")
            ?? GetChildValue(fan, "name")
            ?? $"Fan {index + 1}";

        var points = fan
            .Descendants()
            .Where(element => IsNamed(element, "point"))
            .Select(ReadPoint)
            .Where(point => point is not null)
            .Cast<FanCurvePoint>()
            .OrderBy(point => point.Temperature)
            .ToArray();

        if (points.Length > 0 && points.Max(point => point.Duty) > 100)
        {
            points = points
                .Select(point => point with { Duty = Math.Clamp(point.Duty / 255F * 100F, 0, 100) })
                .ToArray();
        }

        return new FanCurve(id, name, points);
    }

    private static FanCurvePoint? ReadPoint(XElement point)
    {
        var xText = GetAttribute(point, "x") ?? GetChildValue(point, "x");
        var yText = GetAttribute(point, "y") ?? GetChildValue(point, "y");
        if (!float.TryParse(xText, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(yText, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            return null;
        }

        return new FanCurvePoint(Math.Clamp(x, 0, 100), Math.Max(0, y));
    }

    private static bool IsNamed(XElement element, string name) =>
        string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase);

    private static string? GetAttribute(XElement element, string name) =>
        element.Attributes()
            .FirstOrDefault(attribute =>
                string.Equals(attribute.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
            ?.Value;

    private static string? GetChildValue(XElement element, string name) =>
        element.Elements().FirstOrDefault(child => IsNamed(child, name))?.Value;

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Trim().Where(character => !invalid.Contains(character)).ToArray())
            .TrimEnd('.');
    }
}
