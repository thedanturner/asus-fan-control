using AsusFanProfileSwitcher.Services;

var temporaryDirectory = Path.Combine(
    Path.GetTempPath(),
    $"asus-fan-profile-tests-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryDirectory);

try
{
    var profilePath = Path.Combine(temporaryDirectory, "Silent.xml");
    File.WriteAllText(
        profilePath,
        """
        <?xml version="1.0"?>
        <fanstore>
          <fan key="0" name="CPU Fan">
            <point key="0"><x>30</x><y>25</y></point>
            <point key="1"><x>65</x><y>70</y></point>
            <point key="2"><x>85</x><y>100</y></point>
          </fan>
        </fanstore>
        """);

    var catalog = new ProfileCatalog();
    var profiles = catalog.Load(temporaryDirectory);
    Require(profiles.Count == 1, "Expected one valid profile.");
    Require(profiles[0].Name == "Silent", "Expected the profile file name.");

    var curves = catalog.LoadCurves(profilePath);
    Require(curves.Count == 1, "Expected one fan curve.");
    Require(curves[0].Name == "CPU Fan", "Expected the fan name from XML.");
    Require(curves[0].Points.Count == 3, "Expected three curve points.");
    Require(curves[0].Points[1].Temperature == 65, "Expected temperature parsing.");
    Require(curves[0].Points[1].Duty == 70, "Expected duty parsing.");

    var duplicate = catalog.Duplicate(
        profiles[0],
        temporaryDirectory,
        "Gaming",
        "Gaming cooling");
    Require(File.Exists(duplicate.FilePath), "Expected a duplicated XML profile.");
    Require(ProfileCatalog.GetRootName(duplicate.FilePath) == "fanstore", "Expected XML validation.");

    Console.WriteLine("Profile catalog smoke tests passed.");
}
finally
{
    Directory.Delete(temporaryDirectory, true);
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
