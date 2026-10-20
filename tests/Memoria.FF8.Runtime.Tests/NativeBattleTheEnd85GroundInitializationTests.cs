using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTheEnd85GroundInitializationTests
{
    [Fact]
    public void Native_grounded_relocation_reuses_four_nullable_tables_and_thirty_two_unconditional_channels()
    {
        const uint source = 0x17151EC;
        byte[] data = new byte[1024];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 256);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(68), 912);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(456), 900);
        byte[] relocated = NativeBattleEffectNodeBankFixups.Relocate(data.ToImmutableArray(), source);
        uint D(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(relocated.AsSpan(offset));
        Assert.Equal(source + 256, D(4));
        Assert.Equal(source + 912, D(68));
        Assert.Equal(0u, D(8));
        Assert.Equal(source + 900, D(456));
        Assert.Equal(source, D(460));
        Assert.Equal(source, D(580));
        Assert.Equal(0u, D(584));
    }

    [Fact]
    public void Native_grounded_first_and_completed_calls_preserve_context_counts_and_accumulate_module_counts()
    {
        NativeBattleTheEnd85Frame frame = Frame();
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(0x300000, hierarchy.Read(0x300000).WriteWord(72, 5).WriteWord(74, 7)).Pools } };
        NativeBattleTheEnd85NodeStep first = NativeBattleTheEnd85GroundInitialization.Advance789700(frame, 0x300000, Services());
        Assert.False(first.Complete);
        Assert.Equal((ushort)1, new NativeBattleEffectHierarchy(first.Frame.Effect.Pools).Read(0x300000).ReadWord(84));
        Assert.Equal((ushort)5, first.Frame.Memory.TheEnd85!.Word(0x25C21B0));
        Assert.Equal((ushort)7, first.Frame.Memory.TheEnd85.Word(0x25C1CB4));
        Assert.Equal(0x300034u, first.Frame.Memory.TheEnd85.Dword(0x25C21DC));
        hierarchy = new(first.Frame.Effect.Pools);
        frame = first.Frame with { Effect = first.Frame.Effect with { Pools = hierarchy.Write(0x300000, hierarchy.Read(0x300000).WriteWord(84, 2)).Pools } };
        NativeBattleTheEnd85NodeStep completed = NativeBattleTheEnd85GroundInitialization.Advance789700(frame, 0x300000, Services());
        Assert.True(completed.Complete);
        Assert.Equal((ushort)10, completed.Frame.Memory.TheEnd85!.Word(0x25C21B0));
        Assert.Equal((ushort)14, completed.Frame.Memory.TheEnd85.Word(0x25C1CB4));
    }

    [Fact]
    public void Native_grounded_duration_comparison_is_signed_after_WORD_wrap()
    {
        NativeBattleTheEnd85Frame frame = Frame();
        NativeBattleTheEnd85State state = frame.Memory.TheEnd85!;
        state = (state with { ModelData = new byte[0xA0C4].ToImmutableArray() }).WriteDword(0x25C1CB8, 0x20DFAB8).WriteDword(0x25C21D0, 0x20DFAB8 + 1944);
        frame = frame with { Memory = frame.Memory with { TheEnd85 = state, Arena = NativeBattleEffectArena.Clear() } };
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(0x300000).WriteWord(84, 1).WriteWord(76, 32767).WriteWord(78, 1).WriteDword(600, 0x17151F0);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(0x300000, task).Pools } };
        NativeBattleTheEnd85NodeStep step = NativeBattleTheEnd85GroundInitialization.Advance789700(frame, 0x300000, Services());
        task = new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(0x300000);
        Assert.Equal((ushort)32768, task.ReadWord(76));
        Assert.Equal((ushort)1, task.ReadWord(84));
        Assert.False(step.Complete);
    }

    private static NativeBattleTheEnd85Frame Frame()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x25C1748, 0x300000, 676, 1);
        (pool, _) = pool.Append(0x788B90);
        NativeBattleOriginalEffectState effect = new(85, 85, new(85, 0x7844A0, 0x784480), null, null) { Pools = [pool] };
        return new(new NativeBattleEffectMemory(null, 0, 0, [], 0, 0, 0, 0) { TheEnd85 = NativeBattleTheEnd85State.FromColdProcess() }, effect, new(1));
    }
    private static NativeBattleTheEnd85NodeServices Services()
    {
        NativeBattleTheEnd85Resources resources = new(null!, [], [], [], null!, 0, 0, ImmutableDictionary<uint, uint>.Empty);
        return new(resources, null!, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), NativeBattleCuragaMatrices.Identity8DD770(), (_, _, _, _) => throw new InvalidOperationException());
    }
}
