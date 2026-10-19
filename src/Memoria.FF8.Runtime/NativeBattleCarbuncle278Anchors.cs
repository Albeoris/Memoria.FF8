using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>World-space creature anchors using its current local skeleton and cached model matrix.</summary>
public static class NativeBattleCarbuncle278Anchors
{
    public static NativeBattleVector ReadBone502170(NativeBattleModulePoolTask task, NativeBattleCarbuncleExecutableMemory executable, int bone, short fraction)
    {
        if ((task.ReadByte(0x20) & 2) == 0)
        {
            return new(unchecked((short)task.ReadWord(0x3C)), unchecked((short)task.ReadWord(0x3E)), unchecked((short)task.ReadWord(0x40)));
        }
        int count = executable.Read(NativeBattleCarbuncleExecutableMemory.Address, 1)[0];
        if ((uint)bone >= count)
        {
            throw new InvalidDataException("Native Carbuncle bone anchor leaves its current skeleton.");
        }
        int definition = checked(16 + bone * 48);
        byte[] words = executable.Skeleton.Read(definition, 4);
        int distance = BinaryPrimitives.ReadInt16LittleEndian(words) < 0 ? 0 : unchecked(fraction * BinaryPrimitives.ReadInt16LittleEndian(words.AsSpan(2))) >> 12;
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(WorldMatrix(task), NativeBattleArenaPose.ReadMatrix(executable.Skeleton, definition + 16));
        return new(unchecked((short)(matrix.X + ((long)matrix.Rotation[2] * distance >> 12))), unchecked((short)(matrix.Y + ((long)matrix.Rotation[5] * distance >> 12))), unchecked((short)(matrix.Z + ((long)matrix.Rotation[8] * distance >> 12))));
    }

    public static NativeBattleVector ReadVertex682270(NativeBattleModulePoolTask task, NativeBattleCarbuncleExecutableMemory executable, NativeBattleEffectOwnedModelResource resource, short mesh, short vertex)
    {
        NativeBattleActionMatrix world = WorldMatrix(task);
        int count = executable.Read(NativeBattleCarbuncleExecutableMemory.Address, 1)[0];
        ImmutableArray<NativeBattleActionMatrix>.Builder bones = ImmutableArray.CreateBuilder<NativeBattleActionMatrix>(count);
        for (int index = 0; index < count; index++)
        {
            bones.Add(NativeBattleGfMatrices.Compose56C2F0(world, NativeBattleArenaPose.ReadMatrix(executable.Skeleton, 32 + index * 48)));
        }
        int start = resource.Section(1);
        return NativeBattleGfMeshAnchors.Resolve684BA0(resource.Bytes.Slice(start, resource.Section(2) - start), mesh, vertex, index => bones[index]);
    }

    private static NativeBattleActionMatrix WorldMatrix(NativeBattleModulePoolTask task)
    {
        return new(Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(0x60 + index * 2))).ToImmutableArray(), unchecked((int)task.ReadDword(0x74)), unchecked((int)task.ReadDword(0x78)), unchecked((int)task.ReadDword(0x7C)), 4096);
    }
}
