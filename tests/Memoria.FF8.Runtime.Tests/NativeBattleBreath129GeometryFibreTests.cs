using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBreath129Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBreath129GeometryFibreTests
{
    private const uint Arena = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Task = Arena + 0x690;

    [Fact]
    public void Fibre_preparation_preserves_sparse_records_consumes42_draws_and_releases_actual_temporary_memory()
    {
        NativeBattleBreath129Frame frame = Frame();
        NativeBattleBreath129FibrePreparation result = Prepare(frame);
        Assert.Equal(NativeBattleTemporaryMemory.Address, result.Address);
        Assert.Equal(result.Address, result.Stack.CrtSavedEdi);
        NativeBattleEffectArena records = Context(result.Frame, result.Address, 256);
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 4 }, records.Read(0, 6));
        Assert.Equal(new byte[] { 16, 0, 0, 0, 0, 4 }, records.Read(240, 6));
        for (int index = 0; index < 16; index++)
        {
            Assert.Equal((ushort)(7 + index * 10), BitConverter.ToUInt16(records.Read(index * 16 + 6, 2)));
            Assert.Equal((ushort)(200 - index), BitConverter.ToUInt16(records.Read(index * 16 + 14, 2)));
            Assert.All(records.ReadSnapshot(index * 16 + 8, 6).Known, value => Assert.False(value));
        }
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 42; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, result.Frame.Random);
        Assert.Equal(256u, result.Frame.Memory.GeometryRegisters!.Lzcs);
        Assert.Equal(23u, result.Frame.Memory.GeometryRegisters.Lzcr);
        Assert.Equal(3822u, result.Frame.Memory.ScriptedColorRegisters!.Ir0);
        Assert.Equal(result.Address + 256, BitConverter.ToUInt32(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        NativeBattleBreath129Frame released = Release(result.Frame, 256);
        Assert.Equal(result.Address, BitConverter.ToUInt32(released.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(records.Read(0, 8), Context(released, result.Address, 256).Read(0, 8));
    }

    [Fact]
    public void Fibre_jitter_uses_signed_shift_and_draws_even_when_the_other_amplitudes_are_zero()
    {
        NativeBattleBreath129Frame frame = Frame();
        NativeBattleBreath129FibrePreparation baseline = Prepare(frame);
        frame = WriteBytes(frame, Arena + 0x35C2, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)4096)), 2);
        NativeBattleBreath129FibrePreparation jitter = Prepare(frame);
        NativeBattleEffectArena before = Context(baseline.Frame, baseline.Address, 256);
        NativeBattleEffectArena after = Context(jitter.Frame, jitter.Address, 256);
        Assert.Equal(BitConverter.ToInt16(before.Read(16, 2)) - 1, BitConverter.ToInt16(after.Read(16, 2)));
        Assert.Equal(BitConverter.ToInt16(before.Read(18, 2)) - 1, BitConverter.ToInt16(after.Read(18, 2)));
        Assert.Equal(before.Read(20, 2), after.Read(20, 2));
        Assert.Equal(before.Read(32, 8), after.Read(32, 8));
        Assert.Equal(baseline.Frame.Random, jitter.Frame.Random);
    }

    private static NativeBattleBreath129FibrePreparation Prepare(NativeBattleBreath129Frame frame) => NativeBattleBreath129Geometry.PrepareFibre6B68C0(frame, Task, Point(0, 0, 1024), Point(0, 0, 4096), Point(16, 0, 1024), Point(0, 0, 4096), Resources(), Inputs());

    private static NativeBattleEffectArena Point(short x, short y, short z) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z));

    private static NativeBattleBreath129Frame Frame()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x25211A0, Task, 128, 1);
        (pool, _) = pool.Append(0x6B68C0);
        NativeBattleOriginalEffectState effect = new(129, 128, new(129, 0x6B4C00, 0x6B4C10), null, 0) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = new([0, 0, 0], [0, 0, 0], [0, 0, 0], 0) { Material = 0x33000000, Ir0 = 0 } };
        NativeBattleBreath129Frame frame = new(memory, effect, new(55), NativeBattleBreath129State.FromColdProcess().WriteDword(WorkingGlobal, Arena)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        NativeBattleModulePoolTask task = Read(frame, Task).WriteByte(18, 12);
        for (int index = 0; index < 16; index++)
        {
            task = task.WriteWord(32 + index * 6, checked((ushort)(7 + index * 10))).WriteWord(34 + index * 6, checked((ushort)(200 - index)));
        }
        return Write(frame, Task, task);
    }

    private static NativeBattleBreath129Resources Resources()
    {
        ushort[] sqrt = new ushort[256];
        sqrt[64] = 4096;
        return new(new("unused", []), new(0x12239B8, []), new(0x1225468, []), new(0x1226098, []), new(0x122688C, []), new(0, 0x12238C0, 0, 0, []), new("unused", []), [], [], [], [], [], sqrt.ToImmutableArray(), [], 0);
    }

    private static NativeBattleBreath129Inputs Inputs() => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException());
}
