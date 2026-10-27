using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect227Controller;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect227RecordsTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect227LowerTests
{
    [Theory]
    [InlineData(15, 0, 16)]
    [InlineData(16, 2, 16)]
    public void Terminal_callback_draws_before_retirement_and_preserves_terminal_spin(int age, int expectedResult, int nextAge)
    {
        NativeBattleEffect227Frame frame = Frame();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2508718, 0x20DFAB8, 24, 16);
        (pool, int slot) = pool.Append(0x694360);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteBytes(12, new byte[12]).WriteWord(12, (ushort)age).WriteWord(14, 20).WriteWord(16, 30).WriteWord(18, 7).WriteWord(20, unchecked((ushort)-3));
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        frame = frame with { Effect = frame.Effect with { Pools = [pool] } };
        frame = frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, frame.Effect.Pools) };
        byte[] mesh = new byte[40];
        BinaryPrimitives.WriteUInt32LittleEndian(mesh, 8);
        NativeBattleEffect227Resources resources = Resources() with { Meshes = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(0x112DAA8, new(0, 0x112DAA8, mesh.ToImmutableArray())) };
        Harness harness = new();
        NativeBattleEffect227Step result = NativeBattleEffect227Lower.Advance(frame, pool.BankAddress, resources, harness.Inputs());
        Assert.Equal(expectedResult, result.Result);
        NativeBattleModulePoolTask current = Read(result.Frame, pool.BankAddress);
        Assert.Equal(nextAge, current.ReadWord(12));
        Assert.Equal(age == 16 ? 30 : 37, current.ReadWord(16));
        Assert.Equal(age == 16 ? 20 : 17, current.ReadWord(14));
        Assert.IsType<NativeBattleEffect227DrawBatch>(Assert.Single(harness.Operations));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }
}
