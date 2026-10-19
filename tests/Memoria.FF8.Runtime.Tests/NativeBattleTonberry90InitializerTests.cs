using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTonberry90InitializerTests
{
    [Fact]
    public void Specialized_initializer_clears_arena_once_loads_three_files_and_publishes_before_entry()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(0).LoadIntoArena(new byte[] { 255, 254 });
        memory = memory with { Tonberry90 = memory.Tonberry90!.WriteDword(0x25A4D94, 0x12345678) };
        NativeBattleOriginalEffectState selected = Effect();
        var initialized = NativeBattleOriginalEffects.Initialize(memory, selected);
        Assert.Equal(12, initialized.Memory.Cursor);
        Assert.Equal(89, initialized.Memory.SelectedIndex);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, initialized.Memory.Arena!.Read(0, 12));
        Assert.Equal(new[] { 0, 3, 7 }, initialized.Effect.ResourcePointers.Select(pointer => pointer.ArenaOffset));
        Assert.Equal(new uint[] { 0x25A4C20, 0x259EEAC, 0x259EEB0 }, initialized.Effect.ResourcePointers.Select(pointer => pointer.GlobalAddress));
        NativeBattleTonberry90State state = initialized.Memory.Tonberry90!;
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress, state.ReadDword(0x25A4C20));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 3, state.ReadDword(0x259EEAC));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 7, state.ReadDword(0x259EEB0));
        Assert.Equal(0x12345678u, state.ReadDword(0x25A4D94));
        var fixture = NativeBattleTonberry90ControllerTests.Fixture();
        NativeBattleTonberry90Frame entry = NativeBattleTonberry90Controller.Initialize762360(new(initialized.Memory, initialized.Effect with { Parameters = fixture.Frame.Effect.Parameters }, new(1)), fixture.Inputs.Packets);
        Assert.Equal(12, entry.Memory.Cursor);
        Assert.Equal(initialized.Memory.Arena, entry.Memory.Arena);
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0xC000, entry.Memory.Tonberry90!.ReadDword(0x25A4C1C));
    }

    [Fact]
    public void Missing_or_reordered_initializer_inputs_do_not_bypass_the_native_loading_contract()
    {
        NativeBattleOriginalEffectState selected = Effect();
        NativeBattleTonberry90Resources resources = selected.Binding.Tonberry90Resources!;
        Assert.Throws<InvalidDataException>(() => NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected with { Binding = selected.Binding with { Tonberry90Resources = resources with { InitializerTims = [] } } }));
        Assert.Throws<InvalidDataException>(() => NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), selected with { Binding = selected.Binding with { Tonberry90Resources = resources with { InitializerTims = resources.InitializerTims.Reverse().ToImmutableArray() } } }));
        NativeBattleEffectMemory warm = NativeBattleEffectMemory.FromColdProcess() with { Tonberry90 = null };
        Assert.Throws<NotSupportedException>(() => NativeBattleOriginalEffects.Initialize(warm, selected));
    }

    private static NativeBattleOriginalEffectState Effect()
    {
        NativeBattleTonberry90Resources resources = new(0, new("unused", []), null!, [])
        {
            InitializerTims = [new("mag089_0.tim", [1, 2, 3]), new("mag089_1.tim", [4, 5, 6, 7]), new("mag089_2.tim", [8, 9, 10, 11, 12])]
        };
        return new(90, 89, new(90, 0x762360, 0x762320) { InitializerIsRet = false, Tonberry90Resources = resources }, null, null);
    }
}
