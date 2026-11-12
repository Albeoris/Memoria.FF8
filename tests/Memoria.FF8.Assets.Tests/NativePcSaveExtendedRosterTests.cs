using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed partial class NativePcSaveConverterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_export_removes_mod_only_members_and_preserves_native_story_members(bool withTargetSource)
    {
        GameState defaults = Defaults();
        NativeCharacterVitals original = defaults.CharacterVitals!;
        NativeCharacterVitals vitals = original.WithExtendedRoster(true) with { CombatParty = [8, 6, 7] };
        vitals.Records[6] = vitals.Records[6] with { MemberFlags = 0 };
        vitals.Records[7] = vitals.Records[7] with { Experience = 3400 };
        vitals.Records[8] = vitals.Records[8] with { Experience = 15000, Junction = vitals.Records[8].Junction with { GuardianForces = 1, Commands = 0x33 } };
        GameState source = defaults with { CharacterVitals = vitals };

        byte[] save = withTargetSource ? NativePcSaveConverter.Export(source, original) : NativePcSaveConverter.Export(source);
        GameState imported = NativePcSaveConverter.Import(save, defaults, _ => "fixture", "en");

        Assert.Equal(NativeCharacterVitals.NativeRecordCount, imported.CharacterVitals!.Records.Length);
        Assert.False(imported.CharacterVitals.ExtendedRosterEnabled);
        Assert.Equal(new byte[] { 7, 255, 255 }, imported.CharacterVitals.CombatParty);
        Assert.Equal(new byte[] { 7, 255, 255 }, imported.OriginalSave!.FieldParty);
        Assert.Equal(3400u, imported.CharacterVitals.Records[7].Experience);
        Assert.Equal((ushort)0, imported.CharacterVitals.Records[0].Junction.GuardianForces);
        Assert.Equal(1, imported.CharacterVitals.GuardianForces[0].Flags);
        Assert.Equal(new byte[] { 8, 6, 7 }, vitals.CombatParty);
        Assert.Equal(15000u, vitals.Records[8].Experience);
        Assert.Equal((ushort)1, vitals.Records[8].Junction.GuardianForces);
    }

    [Fact]
    public void Original_export_preserves_story_field_party_when_permanent_guests_fill_combat_slots()
    {
        GameState defaults = Defaults();
        NativeCharacterVitals vitals = defaults.CharacterVitals!.WithExtendedRoster(true) with { CombatParty = [8, 9, 10] };
        JsonElement scene = JsonSerializer.SerializeToElement(new { Scene = new { Party = new[] { 0, 1, 255 } } });
        GameState source = defaults with { CharacterVitals = vitals, EventState = scene };

        GameState imported = NativePcSaveConverter.Import(NativePcSaveConverter.Export(source), defaults, _ => "fixture", "en");

        Assert.Equal(new byte[] { 0, 255, 255 }, imported.CharacterVitals!.CombatParty);
        Assert.Equal(new byte[] { 0, 1, 255 }, imported.OriginalSave!.FieldParty);
        Assert.Equal(new byte[] { 8, 9, 10 }, vitals.CombatParty);
    }

    [Fact]
    public void Original_export_round_trips_native_dream_aliases_and_projects_the_party_restored_afterward()
    {
        GameState defaults = Defaults();
        NativeCharacterVitals extended = defaults.CharacterVitals!.WithExtendedRoster(true) with { CombatParty = [0, 8, 6] };
        extended.Records[1] = extended.Records[1] with { MemberFlags = 9 };
        NativeJunctionResult dream = extended.PrepareJunction(1, [0, 1, 255], 7);
        JsonElement scene = JsonSerializer.SerializeToElement(new { Scene = new { Party = dream.FieldParty.Select(identity => (int)identity).ToArray() } });
        GameState source = defaults with { CharacterVitals = dream.Vitals, EventState = scene };

        GameState imported = NativePcSaveConverter.Import(NativePcSaveConverter.Export(source), defaults, _ => "fixture", "en");

        Assert.True(imported.CharacterVitals!.DreamEnabled);
        Assert.Equal(NativeCharacterVitals.NativeRecordCount, imported.CharacterVitals.Records.Length);
        Assert.Equal(new byte[] { 8, 9, 255 }, imported.OriginalSave!.FieldParty);
        Assert.Equal(dream.Vitals.CombatParty, imported.CharacterVitals.CombatParty);
        Assert.Equal((byte)0, imported.CharacterVitals.FindPhysicalRow(8));
        Assert.Equal((byte)1, imported.CharacterVitals.FindPhysicalRow(9));
        NativeJunctionResult restored = imported.CharacterVitals.PrepareJunction(0, [8, 9, 255], 7);
        Assert.Equal(new byte[] { 0, 6, 255 }, restored.Vitals.CombatParty);
        Assert.Equal(new byte[] { 0, 1, 255 }, restored.FieldParty);
        Assert.Equal(new byte[] { 0, 8, 6 }, dream.Vitals.ExtendedRosterPartyBeforeDream);
    }
}
