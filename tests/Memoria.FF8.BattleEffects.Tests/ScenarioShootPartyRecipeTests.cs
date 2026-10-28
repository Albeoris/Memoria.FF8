using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects.Tests;

public sealed class ScenarioShootPartyRecipeTests
{
    [Fact]
    public void Installed_initialization_supplies_three_original_saved_records_without_vital_changes()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for original Shoot party preparation.");
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        NativeCharacterVitals initial = NativeCharacterInitialization.Read(resources.Read(new ResourceKey("main/init.out")), resources.Read(new ResourceKey("main/kernel.bin")));
        Assert.True(ScenarioDiscovery.TrySelectShootParty(initial.Records, initial.CombatParty, out byte[]? party, out string? blocker), blocker);
        Assert.Equal(3, party!.Length);
        Assert.Equal(3, party.Select(row => initial.Records[row].CharacterId).Distinct().Count());
        Assert.All(party, row => Assert.True(initial.Records[row].HitPoints.IsPositive));
    }

    [Fact]
    public void Shoot_prepares_three_distinct_original_records_and_keeps_existing_members_in_order()
    {
        NativeCharacterVital[] records = Records();
        Assert.True(ScenarioDiscovery.TrySelectShootParty(records, new byte[] { 0, 5, 255 }, out byte[]? party, out string? blocker), blocker);
        Assert.Equal(new byte[] { 0, 5, 1 }, party);
        Assert.All(records, record => Assert.Equal(new NativeHpWord(100, 100), record.HitPoints));
        Assert.Equal(Enumerable.Range(0, 8).Select(index => (byte)index), records.Select(record => record.CharacterId));
    }

    [Fact]
    public void Shoot_does_not_revive_replace_or_duplicate_unavailable_saved_records()
    {
        NativeCharacterVital[] records = Records();
        for (int row = 2; row < records.Length; row++)
        {
            records[row] = records[row] with { HitPoints = new(0, 0) };
        }
        Assert.False(ScenarioDiscovery.TrySelectShootParty(records, new byte[] { 0, 255, 255 }, out byte[]? party, out string? blocker));
        Assert.Null(party);
        Assert.StartsWith("SHOOT_PARTY_PRECONDITION:", blocker);
        records[2] = records[2] with { HitPoints = new(1, 100), Status = 1 };
        Assert.False(ScenarioDiscovery.TrySelectShootParty(records, new byte[] { 0, 255, 255 }, out _, out _));
    }

    [Fact]
    public void Duplicate_character_identities_are_not_used_to_fill_an_empty_party_slot()
    {
        NativeCharacterVital[] records = Records();
        records[1] = records[1] with { CharacterId = 0 };
        Assert.True(ScenarioDiscovery.TrySelectShootParty(records, new byte[] { 0, 1, 255 }, out byte[]? party, out string? blocker), blocker);
        Assert.Equal(new byte[] { 0, 2, 3 }, party);
    }

    [Theory]
    [InlineData(136)]
    [InlineData(1000)]
    public void Stage147IntroPreparesItsRequiredPhysicalPartySlotsFromOriginalLivingRecords(int encounter)
    {
        NativeCharacterVital[] records = Records();

        Assert.True(ScenarioDiscovery.TrySelectStageEntryParty(147, encounter, records, new byte[] { 0, 255, 255 }, out byte[]? party, out string? blocker), blocker);

        Assert.Equal(new byte[] { 0, 1, 2 }, party);
        Assert.All(records, record => Assert.Equal(new NativeHpWord(100, 100), record.HitPoints));
    }

    [Theory]
    [InlineData(147, 137)]
    [InlineData(34, 136)]
    public void OrdinaryStagesDoNotAddPartyMembers(int stage, int encounter)
    {
        Assert.True(ScenarioDiscovery.TrySelectStageEntryParty(stage, encounter, Records(), new byte[] { 0, 255, 255 }, out byte[]? party, out string? blocker));
        Assert.Null(party);
        Assert.Null(blocker);
    }

    [Fact]
    public void Stage147IntroDoesNotInventARequiredLivingPartyRecord()
    {
        NativeCharacterVital[] records = Records();
        for (int row = 2; row < records.Length; row++)
        {
            records[row] = records[row] with { HitPoints = new(0, 0) };
        }

        Assert.False(ScenarioDiscovery.TrySelectStageEntryParty(147, 136, records, new byte[] { 0, 255, 255 }, out byte[]? party, out string? blocker));
        Assert.Null(party);
        Assert.StartsWith("STAGE_ENTRY_PARTY_PRECONDITION:", blocker);
    }

    private static NativeCharacterVital[] Records() => Enumerable.Range(0, 8).Select(index => new NativeCharacterVital((byte)index, new(100, 100), 0, null!, 0, 0, 0, [], [], 0, 0)).ToArray();
}
