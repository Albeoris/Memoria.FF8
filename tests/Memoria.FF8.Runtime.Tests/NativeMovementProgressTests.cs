using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeMovementProgressTests
{
    private static NativeSavedBattleFlags Flags(byte value = 0) => new(NativeInputConfiguration.OriginalInitHash, value);
    private static NativeEventMemory Memory(uint total = 0, uint recovery = 0, uint angelo = 0) => NativeEventMemory.FromDWords(new Dictionary<int, int> { [4] = unchecked((int)total), [12] = unchecked((int)recovery), [244] = unchecked((int)angelo) });
    private static uint Read(NativeEventMemory memory, int address) => unchecked((uint)memory.Read(address, 4).RequireExact());
    private static NativeLimitProgress Limits(byte remaining = 0) => new(new string('0', 64), 0, 0, 0, 3, ImmutableArray.CreateRange(new byte[] { 0, 0, 0, remaining, 0, 0, 0, 0 }));

    [Fact]
    public void CountersWrapBeforeUnsignedThresholdsAndDiscardThresholdExcess()
    {
        NativeMovementProgressResult wrapped = NativeMovementProgress.Sample(Memory(uint.MaxValue - 1, uint.MaxValue - 1, uint.MaxValue - 1), null, null, null, null, 3);
        Assert.Equal(1u, Read(wrapped.Memory, 4));
        Assert.Equal(1u, Read(wrapped.Memory, 12));
        Assert.Equal(1u, Read(wrapped.Memory, 244));
        NativeMovementProgressResult threshold = NativeMovementProgress.Sample(Memory(0, 7), MagicSystemFixture.Create(), null, null, null, 5);
        Assert.Equal(5u, Read(threshold.Memory, 4));
        Assert.Equal(0u, Read(threshold.Memory, 12));
        Assert.Equal(5u, Read(threshold.Memory, 244));
    }

    [Fact]
    public void RecoveryUsesEquippedStatusBit17AndPhysicalCombatRows()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals = vitals with { CombatParty = [2, 1, 255], Definitions = vitals.Definitions with { CharacterStatusPassiveFlags = ImmutableArray.CreateRange(new uint[] { 0x20000, 1 }.Concat(new uint[18])) } };
        vitals.Records[2] = vitals.Records[2] with { HitPoints = new(5, 6), Junction = vitals.Records[2].Junction with { Abilities = 58 } };
        vitals.Records[1] = vitals.Records[1] with { HitPoints = new(5, 5), Junction = vitals.Records[1].Junction with { Abilities = 59 } };
        vitals.Records[0] = vitals.Records[0] with { HitPoints = new(5, 5), Junction = vitals.Records[0].Junction with { Abilities = 58 } };
        vitals.GuardianForces[0] = vitals.GuardianForces[0] with { HitPoints = 5, MaximumHitPoints = 10 };
        vitals.GuardianForces[1] = vitals.GuardianForces[1] with { HitPoints = 0, MaximumHitPoints = 10 };
        vitals.GuardianForces[2] = vitals.GuardianForces[2] with { HitPoints = 5, Flags = 0, MaximumHitPoints = 10 };
        NativeCharacterVitals recovered = NativeMovementProgress.Sample(Memory(recovery: 7), vitals, null, null, null, 1).Vitals!;
        Assert.Equal(new NativeHpWord(6, 7), recovered.Records[2].HitPoints);
        Assert.Equal(new NativeHpWord(5, 5), recovered.Records[1].HitPoints);
        Assert.Equal(new NativeHpWord(5, 5), recovered.Records[0].HitPoints);
        Assert.Equal(6, recovered.GuardianForces[0].HitPoints);
        Assert.Equal(0, recovered.GuardianForces[1].HitPoints);
        Assert.Equal(5, recovered.GuardianForces[2].HitPoints);
        Assert.Equal(new NativeHpWord(5, 6), vitals.Records[2].HitPoints);
    }

    [Fact]
    public void DeadAndFullCharactersDoNotRecover()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        vitals.Records[0] = vitals.Records[0] with { HitPoints = new(0, 0), Junction = vitals.Records[0].Junction with { Abilities = 58 } };
        vitals.Records[1] = vitals.Records[1] with { HitPoints = new(10, 10), Junction = vitals.Records[1].Junction with { Abilities = 58 } };
        NativeCharacterVitals recovered = NativeMovementProgress.Sample(Memory(recovery: 8), vitals, null, null, null, 0).Vitals!;
        Assert.Same(vitals, recovered);
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(true, 0, 594)]
    [InlineData(false, 16, 594)]
    public void AngeloGatesProcessingButNeverAccumulation(bool dream, byte flags, uint expected)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create() with { DreamEnabled = dream };
        NativeMovementProgressResult sampled = NativeMovementProgress.Sample(Memory(angelo: 591), vitals, Limits(), [0, 255, 255], Flags(flags), 3);
        Assert.Equal(expected, Read(sampled.Memory, 244));
        Assert.False(sampled.LearnedAngelo);
    }

    [Fact]
    public void AngeloDecrementsThenLearnsOnTheNextThresholdUsingFieldIdentity()
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        NativeMovementProgressResult decremented = NativeMovementProgress.Sample(Memory(angelo: 591), vitals, Limits(1), [4, 255, 255], Flags(), 1);
        Assert.Equal(0, decremented.Limits!.TrainingCounters[3]);
        Assert.Equal(0, decremented.Limits.AngeloFlags);
        Assert.False(decremented.LearnedAngelo);
        NativeMovementProgressResult learned = NativeMovementProgress.Sample(Memory(angelo: 592), vitals, decremented.Limits, [4, 255, 255], Flags(), 0);
        Assert.Equal(8, learned.Limits!.AngeloFlags);
        Assert.True(learned.LearnedAngelo);
        Assert.False(NativeMovementProgress.Sample(Memory(angelo: 592), vitals, learned.Limits, [4, 255, 255], Flags(), 0).LearnedAngelo);
        Assert.Equal(0, NativeMovementProgress.Sample(Memory(angelo: 592), vitals, Limits(), [0, 255, 255], Flags(), 0).Limits!.AngeloFlags);
    }

    [Fact]
    public void UnknownRequiredOwnersRefuseWithoutMutatingInput()
    {
        NativeEventMemory memory = Memory(angelo: 591);
        Assert.Throws<NotSupportedException>(() => NativeMovementProgress.Sample(memory, MagicSystemFixture.Create(), Limits(), [4, 255, 255], null, 1));
        Assert.Equal(591u, Read(memory, 244));
        Assert.Throws<NotSupportedException>(() => NativeMovementProgress.Sample(Memory(angelo: 592), MagicSystemFixture.Create(), Limits() with { TrainingSelection = 8 }, [4, 255, 255], Flags(), 0));
    }

    [Fact]
    public void WorldTransactionRetainsProgressAcrossCallbacksAndColdSnapshots()
    {
        GameState initial = new() { Scene = SceneKind.WorldMap, World = new(), EventMemory = Memory(recovery: 7), CharacterVitals = MagicSystemFixture.Create(), LimitProgress = Limits(), SavedBattleFlags = Flags(), OriginalSave = null };
        NativeWorldSalaryState batch = NativeWorldSalaryState.Begin(initial).PrepareMovementProgress(3).PrepareMovementProgress(3);
        GameState result = batch.ApplyTo(initial, initial.World);
        Assert.Equal(6u, Read(result.EventMemory, 4));
        Assert.Equal(3u, Read(result.EventMemory, 12));
        GameState restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(result))!;
        Assert.Equal(Read(result.EventMemory, 244), Read(restored.EventMemory, 244));
        Assert.Equal(JsonSerializer.Serialize(result.LimitProgress), JsonSerializer.Serialize(restored.LimitProgress));
    }
}
