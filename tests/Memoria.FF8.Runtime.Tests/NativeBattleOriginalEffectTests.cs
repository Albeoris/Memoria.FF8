using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleOriginalEffectTests
{
    [Fact]
    public void LoaderFallbackPreservesTheIndexWrittenBeforeItsMissingEntryFallback()
    {
        ImmutableArray<NativeBattleEffectBinding> catalog = Enumerable.Range(1, 400).Select(effect => new NativeBattleEffectBinding((ushort)effect, effect == 1 ? 0x8D6A00u : 0, 0)).ToImmutableArray();
        NativeBattleOriginalEffectState fallback = NativeBattleOriginalEffects.Select(17, catalog);
        Assert.Equal(16, fallback.SelectedIndex);
        Assert.Equal((ushort)1, fallback.Binding.Effect);
        Assert.Equal(0x8D6A00u, fallback.Binding.Entry);
        Assert.Null(fallback.Parameters);
        Assert.Null(fallback.ReturnedDescriptor);
        Assert.Equal(0, NativeBattleOriginalEffects.Select(0, catalog).SelectedIndex);
        Assert.Equal(0, NativeBattleOriginalEffects.Select(401, catalog).SelectedIndex);
        Assert.Equal(0, NativeBattleOriginalEffects.Select(ushort.MaxValue, catalog).SelectedIndex);
        Assert.Throws<InvalidDataException>(() => NativeBattleOriginalEffects.Select(1, catalog.RemoveAt(399)));
        Assert.Throws<InvalidDataException>(() => NativeBattleOriginalEffects.Select(1, catalog.SetItem(1, catalog[0])));
    }

    [Fact]
    public void ActualLoaderClearFreesOnlyTheTrackedPhysicalPrefixAndResetsCursor()
    {
        NativeBattleEffectHeapAllocation first = new("first", [1, 2]);
        NativeBattleEffectHeapAllocation retained = new("retained", [3, 4]);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with
        {
            Arena = NativeBattleEffectArena.Clear().Write(17, [9]),
            Cursor = 18,
            AllocationCount = 1,
            Allocations = NativeBattleEffectMemory.FromColdProcess().Allocations.SetItem(0, first).SetItem(5, retained),
            SoundPendingFlags = 0x10,
            SoundPublishedFlags = 0x80000004
        };
        NativeBattleEffectMemory loaded = memory.BeginLoad(16);
        Assert.Equal(0, loaded.Cursor);
        Assert.Equal(0, loaded.AllocationCount);
        Assert.Equal(1, loaded.Active);
        Assert.Equal(16, loaded.SelectedIndex);
        Assert.Null(loaded.Allocations[0]);
        Assert.Same(retained, loaded.Allocations[5]);
        Assert.Equal(new byte[] { 0, 0, 0 }, loaded.Arena!.Read(16, 3));
        Assert.Equal(0x80000004u, loaded.SoundPublishedFlags);
        loaded = loaded.LoadIntoArena([7, 8]).LoadIntoArena([9]);
        Assert.Equal(3, loaded.Cursor);
        Assert.Equal(new byte[] { 7, 8, 9, 0 }, loaded.Arena!.Read(0, 4));
        NativeBattleEffectMemory published = loaded.PublishSound();
        Assert.Equal(0x80000010u, published.SoundPublishedFlags);
        Assert.Equal(0u, published.SoundPendingFlags);
        Assert.Equal(0x80000000u, published.PublishSound().SoundPublishedFlags);
    }

    [Fact]
    public void OriginalCallbackDescriptorAndParametersRoundTripWithoutInventingCompletion()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x24CA828, 0x24CA818, 16, 1);
        (pool, _) = pool.Append(0x62A610);
        NativeBattleOriginalEffectState state = new(2, 1, new(2, 0x6298A0, 0x629880), new(3, 1, 5, 7, 2, 0), pool.DescriptorAddress) { Pools = [pool] };
        state.Validate();
        string json = JsonSerializer.Serialize(state);
        NativeBattleOriginalEffectState restored = JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal((byte)2, restored.Parameters!.Parameter81);
        Assert.Equal((uint?)0x24CA828, restored.ReturnedDescriptor);
        Assert.Throws<InvalidDataException>(() => (state with { Pools = [] }).Validate());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleOriginalEffectState>("{}"));
    }

    [Fact]
    public void ProcessMemorySurvivesBattleSnapshotReturnAndNextEntryWhileOldUnknownStaysUnknown()
    {
        BattleDefinition definition = new()
        {
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 100, CurrentHp = 100 }, new() { Id = "enemy", Name = "Enemy", MaximumHp = 100, CurrentHp = 100 }],
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess() with { EffectMemory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(5).LoadIntoArena([4, 5]) }
        };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        string json = JsonSerializer.Serialize(battle.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        Assert.Equal(json, JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(new byte[] { 4, 5 }, restored.NativeEffectMemory!.Arena!.Read(0, 2));
        GameSession process = GameSession.CreateColdProcess();
        process.SetBattle(restored.Capture());
        process.SetBattle(null);
        BattleSession next = new(definition with { NativeRetainedMemory = process.State.NativeBattleMemory }, BattleAtbMode.Normal);
        Assert.Equal(2, next.NativeEffectMemory!.Cursor);
        Assert.Equal(5, next.NativeEffectMemory.SelectedIndex);
        Assert.Null(new BattleSession(definition with { NativeRetainedMemory = null }, BattleAtbMode.Normal).NativeEffectMemory);
        Assert.Null(NativeBattleEffectMemory.FromColdProcess().Arena);
    }
}
