using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModuleMeshTemporaryResult(NativeBattleFileStaging Staging, NativeBattleGfGeometryBatch Batch);

public static partial class NativeBattleModulePackets
{
    public static NativeBattleModuleMeshTemporaryResult EmitRetainedBatch7043B0(NativeBattleFileStaging staging, uint temporary, uint meshAddress, ImmutableArray<byte> source, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleGfGeometryContext context, NativeBattleSummonColors colors, Func<int, NativeBattleVector> readSelectedVertex)
    {
        staging = staging.Write(temporary + 32, BitConverter.GetBytes(checked(meshAddress + BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan()))));
        NativeBattleMeshPacketVariant observations = new(null, null, false, false)
        {
            ObserveFlags = flags => staging = staging.Write(temporary + 48, BitConverter.GetBytes(flags)),
            ObserveArea = area => staging = staging.Write(temporary + 36, BitConverter.GetBytes(area)),
            ObserveAverageDepth = depth => staging = staging.Write(temporary + 44, BitConverter.GetBytes(Math.Max(unchecked((int)depth + context.DepthBias), 0)))
        };
        NativeBattleMeshPacketContext operands = new(context.Flags, context.FarColor, context.DepthCue, context.TexturePage, context.Clut, 0, context.DepthBias, context.TableBaseBucket, context.DepthShift);
        NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch(source, 0, default, matrix, projection, operands, colors, readSelectedVertex, null, cursor => staging = staging.Write(temporary + 32, BitConverter.GetBytes(checked(meshAddress + (uint)cursor))), observations);
        return new(staging, batch);
    }

    public static NativeBattleModuleMeshTemporaryResult EmitRetainedBatch572200(NativeBattleFileStaging staging, uint temporary, uint meshAddress, ImmutableArray<byte> source, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleModuleMeshContext context, NativeBattleSummonColors colors, ImmutableArray<byte> selectedVertices = default, uint? selectedVertexAddress = null)
    {
        if (Read(0) != meshAddress || Read(28) != context.Flags)
        {
            throw new InvalidDataException("Native572200 requires its actual caller mesh and flags in temporary memory.");
        }
        if ((context.Flags & 0x2000) != 0)
        {
            if (selectedVertices.IsDefault || selectedVertexAddress is null || Read(4) != selectedVertexAddress)
            {
                throw new NotSupportedException("Native572200 retained selected vertices require their explicit physical pointer owner.");
            }
        }
        else
        {
            staging = Write(4, checked(meshAddress + 8));
        }
        if ((context.Flags & 0x1000) == 0)
        {
            staging = Write(24, 0);
        }
        staging = Write(32, checked(meshAddress + BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan())));
        NativeBattleMeshPacketVariant observations = new(null, null, false, false)
        {
            ObserveFlags = flags => staging = Write(48, flags),
            ObserveArea = area => staging = Write(36, unchecked((uint)area)),
            ObserveAverageDepth = depth => staging = Write(44, depth)
        };
        NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch(source, 0, selectedVertices, matrix, projection, Normalize572200(context), colors, null, null, cursor => staging = Write(32, checked(meshAddress + (uint)cursor)), observations);
        return new(staging, batch);

        uint Read(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(checked(temporary + (uint)offset), 4));
        NativeBattleFileStaging Write(int offset, uint value) => staging.Write(checked(temporary + (uint)offset), BitConverter.GetBytes(value));
    }
}
