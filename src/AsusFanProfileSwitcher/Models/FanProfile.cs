namespace AsusFanProfileSwitcher.Models;

internal sealed record FanProfile(
    string Name,
    string DisplayName,
    string FilePath,
    long Size,
    DateTime ModifiedAt,
    string Hash);

internal sealed record FanCurvePoint(float Temperature, float Duty);

internal sealed record FanCurve(
    string Id,
    string Name,
    IReadOnlyList<FanCurvePoint> Points);
