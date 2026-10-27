using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa89BeamBirthTests
{
    [Theory]
    [InlineData(false, 24876)]
    [InlineData(true, 8492)]
    public void BeamBirthKeepsItsExactTranslationDistanceWorkspaceAndUntouchedSuffix(bool narrow, int expectedZ)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleRinoa89Controller.Beams, 0x20DFAB8, 392, 8);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteBytes(12, Enumerable.Repeat((byte)0xA5, 380).ToArray())) });
        NativeBattleRinoa89Frame frame = Frame(pool);
        NativeBattleGfAnimationSource source = new(0x14A3EB0, new byte[8].ToImmutableArray());
        NativeBattleRinoa89BeamBirth born = narrow ? NativeBattleRinoa89Controller.SpawnBeam6FDB80(frame, pool, 0, 0, source) : NativeBattleRinoa89Controller.SpawnBeam6FD4D0(frame, pool, 0, 0, source);
        Assert.Equal((int?)0, born.Slot);
        NativeBattleModulePoolTask task = born.Pool.Slots[0];
        Assert.Equal(narrow ? 0x6FDC20u : 0x6FD580u, task.Callback);
        Assert.Equal(100u, task.ReadDword(32));
        Assert.Equal(200u, task.ReadDword(36));
        Assert.Equal(unchecked((uint)expectedZ), task.ReadDword(40));
        Assert.Equal((ushort)0, task.ReadWord(44));
        Assert.Equal((ushort)0xA5A5, task.ReadWord(46));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(30));
        Assert.Equal(0x14A3EB0u, task.ReadDword(48));
        Assert.All(task.ReadBytes(52, 340), value => Assert.Equal((byte)0, value));
        born.Pool.Validate();
    }

    [Fact]
    public void FullBeamPoolDoesNotInitializeOrAdvanceRandom()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleRinoa89Controller.Beams, 0x20DFAB8, 392, 8);
        for (int index = 0; index < 8; index++)
        {
            (pool, _) = pool.Append(0x6FD580);
        }
        NativeBattleRinoa89Frame frame = Frame(pool);
        NativeBattleRinoa89BeamBirth born = NativeBattleRinoa89Controller.SpawnBeam6FD4D0(frame, pool, 0, 0, new(0x14A3EB0, []));
        Assert.Null(born.Slot);
        Assert.Same(pool, born.Pool);
        Assert.Same(frame, born.Frame);
    }

    [Fact]
    public void EmitterAllocatorWrapsAtTheActualLastRowAndDoesNotReadItsUnusedPadding()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(NativeBattleRinoa89Controller.Beams, 0x20DFAB8, 392, 8);
        NativeBattleRinoa89Frame frame = Frame(pool);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(23056 + 34, 2) }, Staging = frame.Staging.Write(0x2556594, BitConverter.GetBytes(0x20DFAB8u + 23056u)) };
        NativeBattleRinoa89EmitterAllocation allocated = NativeBattleRinoa89Controller.AllocateEmitter6FDB20(frame);
        Assert.Equal((int?)23056, allocated.Offset);
        Assert.Equal(0x20DFAB8u + 16960u, BinaryPrimitives.ReadUInt32LittleEndian(allocated.Frame.Staging.Read(0x2556594, 4)));
        NativeBattleEffectArenaWriter writer = new(allocated.Frame.Memory.Arena!);
        for (int index = 0; index < 128; index++)
        {
            writer.Write(16960 + index * 48, new byte[] { 1 });
        }
        frame = allocated.Frame with { Memory = allocated.Frame.Memory with { Arena = writer.Freeze() } };
        NativeBattleRinoa89EmitterAllocation full = NativeBattleRinoa89Controller.AllocateEmitter6FDB20(frame);
        Assert.Null(full.Offset);
        Assert.Same(frame, full.Frame);
    }

    private static NativeBattleRinoa89Frame Frame(NativeBattleEffectTaskPool pool)
    {
        byte[] center = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(center, 100);
        BinaryPrimitives.WriteInt16LittleEndian(center.AsSpan(2), 200);
        BinaryPrimitives.WriteInt16LittleEndian(center.AsSpan(4), 300);
        NativeBattleOriginalEffectState effect = new(89, 88, new(89, 0x6F9CF0, 0x5755E0), new(0, 0, 8, 0, 0, 0), NativeBattleRinoa89Controller.Beams) { Pools = [pool] };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x2556600, BitConverter.GetBytes(0x20DFAB8u)).Write(0x2556594, BitConverter.GetBytes(0x20DFAB8u + 16960u)).Write(0x25565F8, center);
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, staging, NativeCrtRandomState.FromColdProcess());
    }
}
