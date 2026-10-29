using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleModulePackets
{
    /// <summary>572200's flags0/group0 stores and insertion match7043B0's shared F3 core, with no depth bias or color/material overrides.</summary>
    public static NativeBattleGfGeometryBatch EmitFlatTriangles572200(ImmutableArray<byte> source, int geometryOffset, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, int tableBaseBucket, byte depthShift, Action<ushort>? observeDepth = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        using NativeBattleGfSourceReader reader = new(source);
        return EmitFlatTriangles572200(reader, geometryOffset, matrix, projection, tableBaseBucket, depthShift, observeDepth, pilotIdentity);
    }

    internal static NativeBattleGfGeometryBatch EmitFlatTriangles572200(NativeBattleGfSourceReader source, int geometryOffset, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, int tableBaseBucket, byte depthShift, Action<ushort>? observeDepth = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (source.IsDefault || geometryOffset < 0 || geometryOffset > source.Length - 8)
        {
            throw new InvalidDataException("Native572200 requires its current bounded mesh header.");
        }
        int groups = checked(geometryOffset + Read32(geometryOffset));
        int triangles = Read32(groups);
        int cursor = checked(groups + 4 + Math.Max(triangles, 0) * 12);
        for (int group = 1; group < 8; group++)
        {
            if (Read32(cursor) > 0)
            {
                throw new NotSupportedException($"Native572200 flags0 F3 adapter does not own reached group{group}.");
            }
            cursor = checked(cursor + 4);
        }
        //Flags0 never consumes the context's far RGB, cue, CLUT or TPAGE scratch values.
        return NativeBattleGfGeometryPackets.EmitBatch7043B0(source, geometryOffset, default, matrix, projection, new(0, 0, 0, null, null, 0, tableBaseBucket, depthShift), observeDepth: observeDepth, pilotIdentity: pilotIdentity);

        int Read32(int offset)
        {
            if (offset < 0 || offset > source.Length - 4)
            {
                throw new InvalidDataException("Native572200 mesh group directory leaves its current bytes.");
            }
            return source.Int(offset);
        }
    }
}
