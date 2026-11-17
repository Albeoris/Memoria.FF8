using Memoria.FF8.Mods;

namespace Memoria.Launcher.Tests;

public sealed class OfficialModNotificationsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-notification-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void AcknowledgementUsesStableComponentAndUpdateVersionForEachProfile()
    {
        OfficialModComponent added = Component("first", "1.0.0");
        OfficialModComponent updated = Component("second", "1.0.0-rc.2");
        var installed = Inventory(Installed(updated, "1.0.0-rc.1"));
        string classic = OfficialModNotifications.StateDirectory(root, root, root, "Classic", "en");
        string french = OfficialModNotifications.StateDirectory(root, root, root, "Classic", "fr");
        OfficialModNotifications initial = Build(classic, installed, [added, updated]);
        Assert.Contains("1 new official mod available", initial.Summary);
        Assert.Contains("1 mod update available", initial.Summary);
        Assert.Equal("Official mod changes reviewed", initial.Acknowledge().Summary);
        Assert.Equal("Official mod changes reviewed", Build(classic, installed, [added, updated], revision: 2).Summary);
        Assert.Contains("1 mod update available", Build(french, installed, [added, updated]).Summary);

        OfficialModNotifications later = Build(classic, installed, [Component("first", "2.0.0"), Component("second", "1.0.0")], revision: 3);
        Assert.Equal("1 mod update available", later.Summary);
        Assert.True(Assert.Single(later.Notices, notice => notice.Component.Id == "first").Reviewed);
    }

    [Fact]
    public void UnavailableSavedCatalogKeepsInstalledVersionAndExplainsNewEngineRequirement()
    {
        OfficialModComponent updated = Component("dependent", "2.0.0", "[2.0.0,)");
        OfficialModComponent omitted = Component("omitted", "1.0.0");
        OfficialModComponent unrelated = Component("unrelated", "1.0.0") with { Target = new("FFVIII", ["Remastered"], ["*"]) };
        OfficialModNotifications result = Build(root, Inventory(Installed(updated, "1.0.0"), Installed(omitted, "1.0.0")), [updated, unrelated], error: "Unavailable");
        Assert.Contains("Could not check official mods", result.Summary);
        Assert.Contains("1 mod update requires Memoria 2.0.0 or later", result.Summary);
        Assert.DoesNotContain("new official mod", result.Summary);
        Assert.Contains("Installed 1.0.0", Assert.Single(result.Notices, notice => notice.Component.Id == "dependent").Describe());
        Assert.Contains("No release listed", Assert.Single(result.Notices, notice => notice.Component.Id == "omitted").Describe());
        Assert.Null(Assert.Single(result.Notices, notice => notice.Component.Id == "unrelated").AcknowledgementKey);
    }

    [Theory]
    [InlineData("Memoria/Girl Next Door", "Girl Next Door", "1.0.1")]
    [InlineData("Memoria/girl-next-door", "girl-next-door", "1.0.3")]
    public void RenamedOfficialComponentShowsItsInstalledVersionAndUpdateInOneNotice(string oldRuntimeId, string oldSettingsKey, string oldVersion)
    {
        OfficialModComponent current = Component(OfficialModCatalog.GirlNextDoor.Id, "1.0.4");
        OfficialModComponent previous = current with { Id = "girl-next-door", RuntimePackageId = oldRuntimeId, SettingsKey = oldSettingsKey };
        OfficialModNotifications result = Build(root, Inventory(Installed(previous, oldVersion)), [current]);
        Assert.Equal("1 mod update available", result.Summary);
        OfficialModNotice notice = Assert.Single(result.Notices);
        Assert.Same(current, notice.Component);
        Assert.Contains($"Installed {oldVersion}", notice.Describe());
        Assert.Contains("Latest 1.0.4", notice.Describe());
    }

    private static OfficialModNotifications Build(string profile, IReadOnlyDictionary<string, ModPackageInstallResult> installed, IReadOnlyList<OfficialModComponent> components, long revision = 1, string? error = null)
    {
        return OfficialModNotifications.Create(new(revision, components, DateTimeOffset.UtcNow, error), profile, "1.0.0", "Classic", "en", installed);
    }

    private static OfficialModComponent Component(string id, string version, string range = "[1.0.0,)")
    {
        return new(id, "Memoria", "Memoria/" + id, id, id, "Description", "Test", "memoria-native", "download", true, new("FFVIII", ["Classic"], ["*"]), new(version, range, id + "-v" + version, "Release notes", new("https://example.invalid/package.zip", 3, 6, new string('a', 64))));
    }

    private static ModPackageInstallResult Installed(OfficialModComponent component, string version)
    {
        return new(Guid.NewGuid(), component.RuntimePackageId, version, false, true) { Official = component, EngineVersionRange = "[1.0.0,)" };
    }

    private static IReadOnlyDictionary<string, ModPackageInstallResult> Inventory(params ModPackageInstallResult[] values)
    {
        return values.ToDictionary(installation => installation.PackageId, StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
