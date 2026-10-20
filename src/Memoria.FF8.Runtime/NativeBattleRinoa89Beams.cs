using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89BeamBirth(NativeBattleRinoa89Frame Frame, NativeBattleEffectTaskPool Pool, int? Slot);
public sealed record NativeBattleRinoa89EmitterAllocation(NativeBattleRinoa89Frame Frame, int? Offset);

public static partial class NativeBattleRinoa89Controller
{
    public static NativeBattleRinoa89BeamBirth SpawnBeam6FD4D0(NativeBattleRinoa89Frame frame, NativeBattleEffectTaskPool pool, short angleX, short angleY, NativeBattleGfAnimationSource source) => SpawnBeam(frame, pool, angleX, angleY, source, false);

    public static NativeBattleRinoa89BeamBirth SpawnBeam6FDB80(NativeBattleRinoa89Frame frame, NativeBattleEffectTaskPool pool, short angleX, short angleY, NativeBattleGfAnimationSource source) => SpawnBeam(frame, pool, angleX, angleY, source, true);

    public static NativeBattleRinoa89EmitterAllocation AllocateEmitter6FDB20(NativeBattleRinoa89Frame frame)
    {
        NativeBattleEffectArena arena = RequireArena(frame);
        uint baseAddress = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2556600, 4));
        uint cursor = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2556594, 4));
        if (baseAddress != ArenaAddress || cursor < baseAddress + 16960 || cursor > baseAddress + 23056 || (cursor - baseAddress - 16960) % 48 != 0)
        {
            throw new InvalidDataException("Native6FDB20 requires its actual retained 128-row emitter cursor.");
        }
        int offset = checked((int)(cursor - baseAddress));
        if (arena.Read(offset, 1)[0] != 0)
        {
            offset = 16960;
            int remaining = 128;
            while (arena.Read(offset, 1)[0] != 0)
            {
                offset += 48;
                if (--remaining == 0)
                {
                    return new(frame, null);
                }
            }
        }
        int next = offset < 23056 ? offset + 48 : 16960;
        return new(frame with { Staging = Dword(frame.Staging, 0x2556594, checked(baseAddress + (uint)next)) }, offset);
    }

    private static NativeBattleRinoa89BeamBirth SpawnBeam(NativeBattleRinoa89Frame frame, NativeBattleEffectTaskPool pool, short angleX, short angleY, NativeBattleGfAnimationSource source, bool narrow)
    {
        if (pool.DescriptorAddress != Beams || pool.BankAddress != ArenaAddress || pool.Stride != 392 || pool.Slots.Length != 8 || source.Address != 0x14A3EB0)
        {
            throw new InvalidDataException("Native Wishing Star beam requires its actual pool dimensions and programme.");
        }
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return new(frame, pool, null);
        }
        (pool, int slot) = pool.Append(narrow ? 0x6FDC20u : 0x6FD580u);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        NativeBattleGfAnimationWorkspace workspace = new(task.ReadBytes(48, 344).ToImmutableArray());
        task = task.WriteBytes(48, workspace.Initialize7016B0(source, 344).Data.AsSpan());
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CD50(new(angleX, angleY, 0));
        task = WriteBeamRotation(task, matrix);
        int distance = narrow ? 2 : 6;
        short centerX = BeamCenterWord(frame, 0);
        short centerY = BeamCenterWord(frame, 2);
        short centerZ = BeamCenterWord(frame, 4);
        task = task.WriteDword(32, unchecked((uint)(centerX + distance * matrix.Rotation[2]))).WriteDword(36, unchecked((uint)(centerY + distance * matrix.Rotation[5]))).WriteDword(40, unchecked((uint)(centerZ + distance * matrix.Rotation[8]))).WriteWord(44, 0);
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        return new(frame, pool, slot);
    }

    private static NativeBattleModulePoolTask WriteBeamRotation(NativeBattleModulePoolTask task, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(12 + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return task.MarkPayloadUnknown(30, 2);
    }

    private static NativeBattleActionMatrix ReadBeamPose(NativeBattleModulePoolTask task)
    {
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(12 + index * 2))).ToImmutableArray();
        return new(rotation, unchecked((int)task.ReadDword(32)), unchecked((int)task.ReadDword(36)), unchecked((int)task.ReadDword(40)), 4096);
    }

    private static short BeamCenterWord(NativeBattleRinoa89Frame frame, int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Staging.Read(checked(0x25565F8u + (uint)offset), 2));
}
