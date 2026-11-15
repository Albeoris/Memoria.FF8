namespace Memoria.FF7.Resources.Tests;

public sealed class FieldProjectionResourcesTests
{
    [Fact]
    public void InstalledPartyFilterKeepsItsNativeFieldAndAllocatedSlotBoundsSeparateFromStaticMasks()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for native party presentation checks.");
        Ff7PcFieldPresentationData presentation = Ff7PcExecutableData.Load(Ff7Installation.Discover(root!)).NativePresentation;
        int field = Assert.Single(Enumerable.Range(0, 1200), field => presentation.PartyFilter(field) is not null);
        Assert.Equal(502, field);
        Assert.Equal(8, presentation.PartyFilter(field)!.LastSlotInclusive);
        Assert.All(Enumerable.Range(0, 18), slot => Assert.Equal(new Ff7NativeModelPresentation(0, 0, 1), presentation.Model(field, slot)));
        Assert.Throws<NotSupportedException>(() => presentation.PartyFilter(-1));
        Assert.Throws<NotSupportedException>(() => presentation.PartyFilter(1200));
    }

    [Fact]
    public void AuthoredPresentationFixturesRetainAnOptionalPartyFilterCatalog()
    {
        Ff7NativeModelPresentation[][] fields = [[new(0, 0, 1)]];
        Dictionary<string, int> roles = [];
        Ff7PcFieldPresentationData ordinary = new(fields, roles, [], [50]);
        Assert.Null(ordinary.PartyFilter(0));
        Ff7PcFieldPresentationData filtered = new(fields, roles, [], [50], new Dictionary<int, Ff7NativePartyPresentationFilter> { [0] = new(0) });
        Assert.Equal(0, filtered.PartyFilter(0)!.LastSlotInclusive);
        Assert.Equal(ordinary.Model(0, 0), filtered.Model(0, 0));
    }

    [Fact]
    public void InstalledInitializerProvidesEveryNativeFieldNearPlaneIncludingTheReactor()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII resources for field projection checks.");
        Ff7PcExecutableData data = Ff7PcExecutableData.Load(Ff7Installation.Discover(root!));
        Assert.Equal(50, data.NativePresentation.NearPlane(116));
        Assert.Equal(512, data.NativePresentation.NearPlane(120));
        Assert.Equal(16, data.NativePresentation.NearPlane(138));
        Assert.Equal(30, data.NativePresentation.NearPlane(139));
        Assert.Equal(56, data.NativePresentation.NearPlane(253));
        Assert.Equal(1, data.NativePresentation.NearPlane(466));
        Dictionary<float, int> distribution = Enumerable.Range(0, 1200).Select(data.NativePresentation.NearPlane).GroupBy(value => value).ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal(1141, distribution[50]);
        Assert.Equal(55, distribution[512]);
        Assert.Equal(1, distribution[1]);
        Assert.Equal(1, distribution[16]);
        Assert.Equal(1, distribution[30]);
        Assert.Equal(1, distribution[56]);
        Assert.Throws<NotSupportedException>(() => data.NativePresentation.NearPlane(-1));
        Assert.Throws<NotSupportedException>(() => data.NativePresentation.NearPlane(1200));
    }
}
