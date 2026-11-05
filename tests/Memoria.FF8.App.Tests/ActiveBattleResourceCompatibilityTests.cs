using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ActiveBattleResourceCompatibilityTests
{
    [Fact]
    public void World_battle_graphics_profile_supplies_absent_field_profile_and_both_must_agree()
    {
        NativeBattleGraphicsRules rules = Definition(NativeFieldRuntimeProfile.FF8EnExecutableHash).NativeGraphics!.Rules;
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, ActiveBattleResourceCompatibility.AllocationImage(null, rules));
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, ActiveBattleResourceCompatibility.AllocationImage(NativeFieldRuntimeProfile.ForImage(NativeFieldRuntimeProfile.FF8EnExecutableHash), rules));
        Assert.Throws<InvalidDataException>(() => ActiveBattleResourceCompatibility.AllocationImage(NativeFieldRuntimeProfile.ForImage(NativeFieldRuntimeProfile.RemasteredEnImageHash), rules));
        Assert.Throws<InvalidDataException>(() => ActiveBattleResourceCompatibility.AllocationImage(null, null));
    }

    [Theory]
    [InlineData(1, 60, 4)]
    [InlineData(2, 132, 4)]
    [InlineData(3, 20, 2)]
    [InlineData(4, 12, 2)]
    [InlineData(5, 24, 4)]
    [InlineData(7, 24, 4)]
    [InlineData(9, 20, 2)]
    [InlineData(10, 16, 0)]
    [InlineData(18, 24, 4)]
    [InlineData(19, 16, 4)]
    [InlineData(21, 24, 4)]
    [InlineData(22, 32, 4)]
    [InlineData(25, 20, 2)]
    public void Native_kernel_text_offsets_bridge_but_first_behavioral_byte_remains_strict(int section, int length, int textBytes)
    {
        byte[] currentBytes = new byte[length];
        byte[] savedBytes = new byte[length];
        Array.Fill(savedBytes, (byte)9, 0, textBytes);
        BattleDefinition current = Definition(NativeFieldRuntimeProfile.RemasteredEnImageHash) with { NativeActions = [Action(section, currentBytes)] };
        BattleDefinition saved = Definition(NativeFieldRuntimeProfile.FF8EnExecutableHash) with { NativeActions = [Action(section, savedBytes)] };
        GameWorld.ValidateBattleDefinition(Bridge(current, saved), saved);
        currentBytes[textBytes] = 1;
        BattleDefinition changed = current with { NativeActions = [Action(section, currentBytes)] };
        Assert.Throws<InvalidDataException>(() => GameWorld.ValidateBattleDefinition(Bridge(changed, saved), saved));
    }

    [Fact]
    public void Additional_command_supplements_have_text_words_while_blue_magic_supplements_have_statuses()
    {
        BattleNativeAction additional = Action(10, new byte[16]) with { SupplementalKernelIndex = 7, SupplementalRecord = new byte[8].ToImmutableArray() };
        BattleNativeAction retained = additional with { SupplementalRecord = new byte[] { 1, 2, 3, 4, 0, 0, 0, 0 }.ToImmutableArray() };
        BattleDefinition current = Definition(NativeFieldRuntimeProfile.RemasteredEnImageHash) with { NativeActions = [additional] };
        BattleDefinition saved = Definition(NativeFieldRuntimeProfile.FF8EnExecutableHash) with { NativeActions = [retained] };
        GameWorld.ValidateBattleDefinition(Bridge(current, saved), saved);
        byte[] changed = retained.SupplementalRecord.ToArray();
        changed[4] = 1;
        BattleDefinition changedCommand = current with { NativeActions = [additional with { SupplementalRecord = changed.ToImmutableArray() }] };
        Assert.Throws<InvalidDataException>(() => GameWorld.ValidateBattleDefinition(Bridge(changedCommand, saved), saved));
        BattleNativeAction weapon = Action(4, new byte[12]) with { Command = 1, SupplementalKernelIndex = 43, SupplementalRecord = new byte[60].ToImmutableArray() };
        byte[] spell = new byte[60];
        spell[0] = 9;
        spell[2] = 11;
        current = current with { NativeActions = [weapon] };
        saved = saved with { NativeActions = [weapon with { SupplementalRecord = spell.ToImmutableArray() }] };
        GameWorld.ValidateBattleDefinition(Bridge(current, saved), saved);
        spell[38] = 1;
        saved = saved with { NativeActions = [weapon with { SupplementalRecord = spell.ToImmutableArray() }] };
        Assert.Throws<InvalidDataException>(() => GameWorld.ValidateBattleDefinition(Bridge(current, saved), saved));
        BattleNativeAction blue = Action(19, new byte[16]) with { SupplementalKernelIndex = 0, SupplementalRecord = new byte[8].ToImmutableArray() };
        current = current with { NativeActions = [blue] };
        saved = saved with { NativeActions = [blue with { SupplementalRecord = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }.ToImmutableArray() }] };
        Assert.Throws<InvalidDataException>(() => GameWorld.ValidateBattleDefinition(Bridge(current, saved), saved));
    }

    [Fact]
    public void Retained_visual_bytes_require_same_binding_and_bounded_original_DAT()
    {
        NativeBattleGraphicsResource currentResource = Model(1);
        NativeBattleGraphicsResource savedResource = Model(2);
        BattleDefinition current = Definition(NativeFieldRuntimeProfile.RemasteredEnImageHash) with { Combatants = [new() { Id = "party-0" }] };
        current = current with { NativeGraphics = current.NativeGraphics! with { Bindings = [new("party-0", 0, currentResource)] } };
        BattleDefinition saved = Definition(NativeFieldRuntimeProfile.FF8EnExecutableHash) with { Combatants = current.Combatants };
        saved = saved with { NativeGraphics = saved.NativeGraphics! with { Bindings = [new("party-0", 0, savedResource)] } };
        BattleDefinition bridged = Bridge(current, saved);
        Assert.Same(savedResource, bridged.NativeGraphics!.Bindings[0].Resource);
        GameWorld.ValidateBattleDefinition(bridged, saved);
        BattleDefinition wrongActor = current with { NativeGraphics = current.NativeGraphics with { Bindings = [new("party-0", 1, currentResource)] } };
        Assert.Throws<InvalidDataException>(() => Bridge(wrongActor, saved));
        byte[] damaged = savedResource.ModelLoadSources!.Value[0].Bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(damaged.AsSpan(4), 999);
        NativeBattleGraphicsResource invalid = savedResource with { ModelLoadSources = [savedResource.ModelLoadSources.Value[0] with { Bytes = damaged.ToImmutableArray() }] };
        BattleDefinition malformed = saved with { NativeGraphics = saved.NativeGraphics with { Bindings = [new("party-0", 0, invalid)] } };
        Assert.Throws<InvalidDataException>(() => Bridge(current, malformed));
        BattleDefinition changedHp = saved with { Combatants = [saved.Combatants[0] with { MaximumHp = 999 }] };
        Assert.Throws<InvalidDataException>(() => GameWorld.ValidateBattleDefinition(Bridge(current, changedHp), changedHp));
        Assert.Throws<InvalidDataException>(() => ActiveBattleResourceCompatibility.RetainAllocatedResources(current, saved, new string('A', 64), NativeFieldRuntimeProfile.RemasteredEnImageHash));
    }

    private static BattleDefinition Bridge(BattleDefinition current, BattleDefinition saved) => ActiveBattleResourceCompatibility.RetainAllocatedResources(current, saved, NativeFieldRuntimeProfile.FF8EnExecutableHash, NativeFieldRuntimeProfile.RemasteredEnImageHash);

    private static BattleDefinition Definition(string image) => new() { NativeGraphics = new(new(image, Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray(), Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray()), [], []) };

    private static NativeBattleGraphicsResource Model(byte sample)
    {
        byte[] bytes = new byte[13];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 12);
        bytes[12] = sample;
        return new([0, 0]) { AnimationFrames = [], BodyFileSelector129 = 0, ModelLoadSources = [new(0, false, 0, 1, "body.dat", bytes.ToImmutableArray())] };
    }

    private static BattleNativeAction Action(int section, byte[] bytes) => new(2, 0, section, 0) { MagicId = 0, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = 0, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = bytes.ToImmutableArray(), SupplementalRecord = [] };
}
