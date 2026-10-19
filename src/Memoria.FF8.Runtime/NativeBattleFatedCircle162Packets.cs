using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFatedCircle162MeshStep(NativeBattleFileStaging Staging, NativeBattleGfGeometryBatch Batch);

public static partial class NativeBattleFatedCircle162Packets
{
    public static NativeBattleFatedCircle162MeshStep Emit5FB980(NativeBattleFileStaging staging, uint mesh, uint? selectedVertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleModuleMeshContext context, int scroll, NativeBattleSummonColors colors)
    {
        uint cursor = checked(mesh + ReadDword(mesh) + 24);
        int count = unchecked((int)ReadDword(cursor));
        cursor += 4;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int offset = scroll % 128;
        for (int index = 0; index < count; index++)
        {
            byte[] face = staging.Read(cursor, 28);
            uint command = BinaryPrimitives.ReadUInt32LittleEndian(face) | 0x02000000;
            BinaryPrimitives.WriteUInt32LittleEndian(face, command);
            staging = staging.Write(cursor, face.AsSpan(0, 4));
            NativeBattleModuleProjectedVertex[] corners = new NativeBattleModuleProjectedVertex[3];
            for (int corner = 0; corner < 3; corner++)
            {
                uint vertex = checked((selectedVertices ?? mesh + 8) + 4u * BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)));
                byte[] xyz = staging.Read(vertex, 6);
                NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
                corners[corner] = NativeBattleModuleProjection.Project45FE10(matrix, point, projection);
            }
            NativeBattleGfGeometryAttempt common = NativeBattleModulePackets.EmitFace572200(face, corners, 6, context, colors, allocated);
            byte[] data = new byte[48];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[48];
            common.Data.AsSpan(0, 4).CopyTo(data);
            common.WriteMask.AsSpan(0, 4).CopyTo(mask);
            common.Data.AsSpan().Slice(4).CopyTo(data.AsSpan(8));
            common.WriteMask.AsSpan().Slice(4).CopyTo(mask.AsSpan(8));
            BinaryPrimitives.WriteUInt32LittleEndian(data, 0x0B000000);
            NativeBattleGeometryPrimitive? primitive = null;
            if (common.Primitive is { } admitted)
            {
                if (offset != 0)
                {
                    ImmutableArray<byte> shifted = NativeBattleEffectTextureWindows.OffsetAxisSigned([data[17], data[29], data[41]], offset, 128);
                    data[17] = shifted[0];
                    data[29] = shifted[1];
                    data[41] = shifted[2];
                }
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), NativeBattleEffectTextureWindows.Encode(new(64, 0, 64, 128)));
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(44), NativeBattleEffectTextureWindows.Encode(new(0, 0, 255, 255)));
                mask.AsSpan(4, 4).Fill(NativeBattleGfPacketByteState.Known);
                mask.AsSpan(44, 4).Fill(NativeBattleGfPacketByteState.Known);
                ImmutableArray<uint> words = Enumerable.Range(0, 12).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(word * 4))).ToImmutableArray();
                primitive = admitted with { Words = words };
            }
            attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            if (primitive is not null)
            {
                allocated += 48;
            }
            cursor += 28;
        }
        return new(staging, new(attempts.ToImmutable(), allocated));

        uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));
    }
}
