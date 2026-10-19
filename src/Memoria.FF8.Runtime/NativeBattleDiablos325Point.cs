using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325PointChoice(NativeCrtRandomState Random, NativeBattleVector Position, uint Programme);

public static class NativeBattleDiablos325Point
{
    public static NativeBattleDiablos325PointChoice Choose6557E5(NativeBattleDiablos325Frame frame, NativeBattleModulePoolTask task)
    {
        NativeCrtRandomState random = (frame.Random ?? throw new NotSupportedException("Native Diablos point selection requires its current CRT random owner.")).Draw(out ushort selected);
        NativeBattleDiablos325Resources resources = frame.Effect.Binding.Diablos325Resources ?? throw new NotSupportedException("Native Diablos point selection requires its original vector table.");
        int index = selected * 10 >> 15;
        int offset = checked((int)(0xFDBDA8u - resources.OpeningProgramme.Address) + index * 8);
        NativeBattleVector point = Sample6F0CC0(frame.Memory, task, resources.OpeningProgramme.Data.AsSpan().Slice(offset, 8));
        random = random.Draw(out ushort programme);
        int kind = programme * 3 >> 15;
        uint address = checked(NativeBattleDiablos325Entry.RequireState(frame.Memory).ModelBuffer5208 + (kind == 0 ? 0x4AFACu : kind == 1 ? 0x4B558u : 0x4BBC4u));
        return new(random, point, address);
    }

    public static NativeBattleVector Sample6F0CC0(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, ReadOnlySpan<byte> record)
    {
        if (record.Length != 8)
        {
            throw new InvalidDataException("Native Diablos point selection requires its original packed vector and signed bone index.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Diablos point selection requires its current skeleton.");
        short selector = BinaryPrimitives.ReadInt16LittleEndian(record[6..]);
        int skeleton = NativeBattleWaterExecutionFields.ArenaOffset(task.ReadDword(0x244), 1);
        NativeBattleActionMatrix bone = NativeBattleArenaPose.ReadMatrix(arena, checked(skeleton + 32 + selector * 48));
        short[] rotation = new short[9];
        for (int index = 0; index < rotation.Length; index++)
        {
            rotation[index] = unchecked((short)task.ReadWord(0x14C + index * 2));
        }
        NativeBattleActionMatrix model = new(rotation.ToImmutableArray(), unchecked((int)task.ReadDword(0x160)), unchecked((int)task.ReadDword(0x164)), unchecked((int)task.ReadDword(0x168)), 4096);
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(model, bone);
        NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(record), BinaryPrimitives.ReadInt16LittleEndian(record[2..]), BinaryPrimitives.ReadInt16LittleEndian(record[4..]));
        return NativeBattleGfMatrices.Transform460840(composed, point);
    }
}
