using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The alternate spinner's two FT4 records with an inline draw-mode command.</summary>
public static class NativeBattleScanAltMeshPackets
{
    public static NativeBattleGfGeometryBatch Emit8DE9D0(NativeBattleStaticMeshResource source, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleGfGeometryContext context, short zsf4, short dqa, int dqb, NativeBattleSummonColors? colors = null, Action<ushort>? observeDepth = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (source.Address != 0x15CF674 || source.Bytes.IsDefault || source.Bytes.Length != 0x88 || context.Flags != 0xF0 || context.TexturePage is not null || context.Clut is not null)
        {
            throw new NotSupportedException("Native Scan alternate geometry requires its original two-face mesh and unmodified F0 context.");
        }
        matrix.Validate();
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan()));
        int allocation = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>(2);
        for (int group = 0; group < 8; group++)
        {
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan().Slice(cursor, 4));
            cursor = checked(cursor + 4);
            if (count != (group == 3 ? 2u : 0u))
            {
                throw new InvalidDataException("Native Scan alternate geometry requires its original FT4-only group layout.");
            }
            for (int index = 0; index < count; index++)
            {
                byte[] face = source.Bytes.AsSpan().Slice(cursor, 24).ToArray();
                NativeBattleGfGeometryAttempt attempt = EmitFace8DF200(face, corner => Project(face, corner), context, zsf4, colors, allocation, observeDepth);
                if (pilotIdentity is { } identity && attempt.Primitive is { } primitive)
                {
                    System.Numerics.Vector3 World(int corner) => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, Vertex(face, corner)), projection);
                    attempt = attempt with { Primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + index) }, World(0), World(1), World(2), World(3), 4) } };
                }
                attempts.Add(attempt);
                if (attempt.Primitive is not null)
                {
                    allocation = checked(allocation + 44);
                }
                cursor = checked(cursor + 24);
            }
        }
        return new(attempts.MoveToImmutable(), allocation);

        NativeBattleVector Vertex(byte[] face, int corner)
        {
            int offset = checked(8 + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4);
            ReadOnlySpan<byte> xyz = source.Bytes.AsSpan().Slice(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.Slice(4)));
        }
        NativeBattleModuleProjectedVertex Project(byte[] face, int corner)
        {
            NativeBattleVector vertex = Vertex(face, corner);
            return corner == 3 ? NativeBattleModuleProjection.Project45FAA0(matrix, vertex, projection, dqa, dqb) : NativeBattleModuleProjection.Project45FE10(matrix, vertex, projection);
        }
    }

    public static NativeBattleGfGeometryAttempt EmitFace8DF200(ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleGfGeometryContext context, short zsf4, NativeBattleSummonColors? colors = null, int byteOffsetRelative = 0, Action<ushort>? observeDepth = null)
    {
        NativeBattleGfGeometryAttempt attempt = NativeBattleShivaMeshPackets.EmitFace5C1BB0(face, readCorner, context, zsf4, colors, byteOffsetRelative, observeDepth);
        byte[] bytes = new byte[attempt.Primitive is null ? 40 : 44];
        attempt.Data.AsSpan().CopyTo(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x0A000000);
        ImmutableArray<NativeBattleGfPacketByteState>.Builder mask = attempt.WriteMask.ToBuilder();
        if (attempt.Primitive is not { } primitive)
        {
            return attempt with { Data = bytes.ToImmutableArray() };
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40), 0xE1000220);
        for (int index = 0; index < 4; index++)
        {
            mask.Add(NativeBattleGfPacketByteState.Known);
        }
        ImmutableArray<uint> words = primitive.Words.SetItem(0, 0x0A000000).Add(0xE1000220);
        ImmutableArray<NativeBattleGfPacketByteState> written = mask.ToImmutable();
        return attempt with { Data = bytes.ToImmutableArray(), WriteMask = written, Primitive = primitive with { Words = words, PhysicalWriteMask = written } };
    }
}
