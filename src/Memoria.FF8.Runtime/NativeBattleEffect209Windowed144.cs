using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect209Windowed144
{
    public static NativeBattleWindowedMesh144Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors colors, int tableBaseBucket = 17, byte depthShift = 2)
    {
        if (mesh.Address != 0xE26340 || mesh.Bytes.IsDefaultOrEmpty || mesh.Bytes.Length < 8 || BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan(4, 4)) != 158 || BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0x20, 4)) != 0xF3)
        {
            throw new InvalidDataException("Effect209 windowed drawing requires its selected physical source and flags.");
        }
        return NativeBattleWindowedMesh144.DrawLoaded(memory, mesh, projection, context, colors, tableBaseBucket, depthShift, (1 << 2) | (1 << 3));
    }
}
