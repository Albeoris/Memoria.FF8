using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel216MeshPackets
{
    public static NativeBattleGfGeometryBatch EmitBatch697D10(uint meshAddress, ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, uint flags, uint? farColor, int? fade, int depthBias, NativeBattleSummonColors colors, Func<int, NativeBattleVector> readSelectedVertex, Action<int, uint>? observeContext = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleMeshPacketContext context = new(flags, farColor, fade, null, null, 0, depthBias, 17, 2);
        NativeBattleGfGeometryBatch batch = NativeBattleEffectMeshPackets.EmitBatch(mesh, matrix, projection, flags, readSelectedVertex, (group, face, readCorner, offset) => Emit(group, face, readCorner, context, colors, offset, observeContext), pilotIdentity: pilotIdentity);
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.AsSpan()));
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan().Slice(cursor, 4));
            cursor = checked(cursor + 4 + count * (group switch { 0 or 1 => 12, 2 or 4 => 20, 3 or 5 => 24, 6 => 28, _ => 36 }));
        }
        if (pilotIdentity.HasValue)
        {
            batch = batch with { Attempts = batch.Attempts.Select(attempt =>
            {
                if (attempt.Primitive is not { PilotGeometry: { } geometry } primitive || geometry.CornerCount != 4 || primitive.Words.Length != 6 || (primitive.Words[1] >> 24) != 0x2A)
                {
                    return attempt;
                }
                return attempt with { Primitive = primitive with { PilotGeometry = geometry with { A = geometry.B, B = geometry.C, C = geometry.C, D = geometry.D, SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true } } };
            }).ToImmutableArray() };
        }
        observeContext?.Invoke(32, checked(meshAddress + (uint)cursor));
        return batch;
    }

    internal static NativeBattleGfGeometryBatch Emit(int group, ReadOnlySpan<byte> face, Func<int, NativeBattleModuleProjectedVertex> readCorner, NativeBattleMeshPacketContext context, NativeBattleSummonColors colors, int byteOffset, Action<int, uint>? observeContext)
    {
        if (group is not (2 or 7))
        {
            throw new NotSupportedException("Native Duel216 window9 requires its reached FT3 or GT4 resource group.");
        }
        bool quad = group == 7;
        NativeBattleModuleProjectedVertex?[] captured = new NativeBattleModuleProjectedVertex?[quad ? 4 : 3];
        NativeBattleGfGeometryPackets.ProjectedCorners vertices = new(Enumerable.Range(0, captured.Length).Select(index => (ushort)index).ToArray(), index =>
        {
            if (captured[index] is { } retained)
            {
                return retained;
            }
            NativeBattleModuleProjectedVertex projected = readCorner(index);
            captured[index] = projected;
            return projected;
        });
        NativeBattleMeshPacketVariant observations = new(null, null, false, false)
        {
            ObserveFlags = value => observeContext?.Invoke(48, value),
            ObserveArea = value => observeContext?.Invoke(36, unchecked((uint)value)),
            ObserveAverageDepth = value => observeContext?.Invoke(44, unchecked((uint)Math.Max(unchecked((int)value + context.DepthBias), 0)))
        };
        NativeBattleGfGeometryAttempt primary = NativeBattleGfGeometryPackets.EmitAttempt(face, vertices, group, context with { Flags = context.Flags & 0xFF }, colors, byteOffset, variant: observations);
        if (primary.Primitive is not { } polygon)
        {
            return new([primary], 0);
        }
        bool faded = (context.Flags & (quad ? 0x80u : 0x40u)) != 0;
        if (!faded)
        {
            return new([primary], primary.PacketLength);
        }
        byte[] data = primary.Data.ToArray();
        data[7] |= 2;
        primary = primary with { Data = data.ToImmutableArray(), Primitive = polygon with { Words = polygon.Words.SetItem(1, polygon.Words[1] | 0x02000000) } };
        int factor = context.DepthCue ?? throw new NotSupportedException("Native Duel216 overlay requires its actual signed fade DWORD.");
        byte intensity = unchecked((byte)(128 - (factor >> 5)));
        uint color = (uint)(intensity | intensity << 8 | intensity << 16) | (quad ? 0x2A000000u : 0x22000000u);
        uint[] screens;
        if (quad)
        {
            observeContext?.Invoke(52, vertices[2].Screen);
            screens = [vertices[1].Screen, vertices[2].Screen, vertices[2].Screen, vertices[3].Screen];
        }
        else
        {
            screens = [vertices[0].Screen, vertices[1].Screen, vertices[2].Screen];
        }
        int overlayLength = quad ? 24 : 20;
        byte[] overlay = new byte[overlayLength];
        BinaryPrimitives.WriteUInt32LittleEndian(overlay, (uint)(overlayLength / 4 - 1) << 24);
        BinaryPrimitives.WriteUInt32LittleEndian(overlay.AsSpan(4), color);
        for (int index = 0; index < screens.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(overlay.AsSpan(8 + index * 4), screens[index]);
        }
        ImmutableArray<uint> overlayWords = Enumerable.Range(0, overlay.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleGfGeometryAttempt overlayAttempt = new(checked(byteOffset + primary.PacketLength), overlay.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, overlayLength).ToImmutableArray(), new(polygon.Bucket, overlayWords, polygon.VertexPayload4));
        byte[] mode = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(mode, 0x01000000);
        BinaryPrimitives.WriteUInt32LittleEndian(mode.AsSpan(4), 0xE1000240);
        NativeBattleGfGeometryAttempt after = new(checked(byteOffset + primary.PacketLength + overlayLength), mode.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), new(polygon.Bucket, [0x01000000, 0xE1000240], [0, 0, 0, 0]));
        return new([primary, overlayAttempt, after], primary.PacketLength + overlayLength + 8);
    }
}

