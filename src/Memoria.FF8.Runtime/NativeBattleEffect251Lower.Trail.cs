using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect251Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect251Lower
{
    private static NativeBattleEffect251Step Trail(NativeBattleEffect251Frame frame, uint address, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        frame = frame with { State = frame.State with { CallbackStackWord58 = unchecked((ushort)address) } };
        (frame, uint temporary) = Allocate(frame, 64);
        NativeBattleEffectArena scratch = ReadBytes(frame, temporary, 64);
        NativeBattleEffect251BoneStep bone = inputs.BoneWorld5FC0(frame, 0x1D97530, 1, CopyBytes(scratch, 0x30, NativeBattleEffectArena.Clear().MarkUnknown(0, 12), 0, 12), CopyBytes(scratch, 0x20, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8));
        frame = bone.Frame;
        scratch = CopyBytes(bone.Direction12, 0, scratch, 0x30, 12);
        scratch = CopyBytes(bone.Position8, 0, scratch, 0x20, 8);
        for (int axis = 0; axis < 3; axis++)
        {
            int direction = unchecked((int)Dword251(scratch, 0x30 + axis * 4));
            int product = unchecked(3 * (direction + 2 * unchecked((direction << 7) + direction)));
            scratch = PutWord251(scratch, 0x28 + axis * 2, Word251(scratch, 0x20 + axis * 2) + (product >> 12));
        }
        frame = WriteBytes(frame, temporary, scratch, 64);
        int age = Signed(Read(frame, address), 12);
        int ring = age % 7;
        if (ring < 0)
        {
            throw new InvalidDataException("Effect251 trail requires its live nonnegative history age.");
        }
        NativeBattleEffectArena record = ReadBytes(frame, address, 0x34C);
        record = Put251(record, 0x10 + ring * 20, 1);
        record = CopyBytes(scratch, 0x20, record, 0x14 + ring * 20, 8);
        record = CopyBytes(scratch, 0x28, record, 0x1C + ring * 20, 8);
        int desired = age < 13 ? 7 : 20 - age;
        int count = 0;
        for (; count < desired; count++)
        {
            if (Dword251(record, 0x10 + ring * 20) == 0)
            {
                break;
            }
            record = CopyBytes(record, 0x14 + ring * 20, record, 0x9C + count * 8, 8);
            record = CopyBytes(record, 0x1C + ring * 20, record, 0xDC + count * 8, 8);
            ring = ring == 0 ? 6 : ring - 1;
        }
        frame = WriteBytes(frame, address, record, 0x34C);
        if (count > 1)
        {
            (frame, uint workAddress) = Allocate(frame, 400);
            NativeBattleEffectArena work = ReadBytes(frame, workAddress, 400);
            for (int side = 0; side < 2; side++)
            {
                work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, record, side == 0 ? 0x9C : 0xDC, work);
                for (int sample = 0; sample < 32; sample++)
                {
                    record = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, sample * 4096 / 31, record, (side == 0 ? 0x11C : 0x21C) + sample * 8);
                }
                frame = WriteBytes(frame, workAddress, work, 400);
                frame = WriteBytes(frame, address, record, 0x34C);
            }
            frame = Release(frame, 400);
            record = CopyBytes(record, 0x21C, record, 0x31C, 8);
            record = CopyBytes(record, 0x11C, record, 0x324, 8);
            record = NormalizeDifference251(record, 0x21C, 0x224, 0x32C);
            record = NormalizeDifference251(record, 0x31C, 0x324, 0x33C);
            frame = WriteBytes(frame, address, record, 0x34C);
            frame = LoadMatrix(frame, inputs.ReadView(frame));
            NativeBattleRibbonPair72Step drawn = NativeBattleRibbonPair72.DrawLoaded(frame.Memory, CopyBytes(record, 0x11C, NativeBattleEffectArena.Clear().MarkUnknown(0, 256), 0, 256), CopyBytes(record, 0x21C, NativeBattleEffectArena.Clear().MarkUnknown(0, 256), 0, 256), scratch, inputs.ReadProjection(frame), inputs.Colors, 0x3A185820);
            frame = WriteBytes(frame with { Memory = drawn.Memory }, temporary, drawn.Temporary64, 64);
            frame = inputs.Execute(frame, new NativeBattleEffect251DrawBatch(drawn.Batch));
        }
        frame = Release(frame, 64);
        if (Paused251(frame))
        {
            return new(frame, 0);
        }
        short next = unchecked((short)(age + 1));
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next))), next >= 19 ? 2 : 0);
    }

    private static NativeBattleEffectArena NormalizeDifference251(NativeBattleEffectArena record, int first, int second, int destination)
    {
        var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(Word251(record, first) - Word251(record, second), Word251(record, first + 2) - Word251(record, second + 2), Word251(record, first + 4) - Word251(record, second + 4));
        return Put251(Put251(Put251(record, destination, unchecked((uint)normalized.X)), destination + 4, unchecked((uint)normalized.Y)), destination + 8, unchecked((uint)normalized.Z));
    }

    private static NativeBattleEffect251Step ActChild(NativeBattleEffect251Frame frame, uint address, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix local = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], Signed(task, 12), Signed(task, 14), Signed(task, 16), 4096);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        NativeBattleEffectArena caller = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 56), 0, 32);
        caller = Put251(PutWord251(Put251(caller, 0x20, 0x1438778), 0x24, -128), 0x34, checked(frame.State.ReadDword(0x2556450) + 0xA880));
        caller = PutWord251(caller, 0x28, frame.State.ReadCallbackStackWord58());
        uint source = Dword251(ReadBytes(frame, address + 0x14, 4), 0);
        NativeBattleEffect251Step result = NativeBattleEffect251Programme.Advance(frame, address + 0x14, source, caller, resources, inputs);
        return new(result.Frame, result.Result == 0 ? 2 : 0);
    }
}
