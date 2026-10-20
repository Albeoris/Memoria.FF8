using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePain34DrawingStep(NativeBattlePain34Frame Frame, NativeBattleGfGeometryBatch Packets, uint? DrawMode2795BD0);

public static class NativeBattlePain34Drawing
{
    public static NativeBattlePain34DrawingStep Draw865AE0(NativeBattlePain34Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleVector anchor, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(frame, new([], 0), null);
        }
        uint physical = NativeBattlePain34Players.RequirePhysicalBlock40(frame.PlayerQueueStack);
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(checked(8 + unchecked((short)node.GeometryIndex) * 4)));
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(checked(geometry + 4)));
        if (count is < 0 or > 4096)
        {
            throw new InvalidDataException("Native Pain requires its bounded original vertex count.");
        }
        NativeBattlePain34State state = frame.Memory.Pain34 ?? throw new NotSupportedException("Native Pain drawing requires its retained module bytes.");
        ImmutableArray<byte> vertices;
        uint selected;
        if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            int offset = checked(geometry + 12 + shape * count * 8);
            vertices = source.Data.Slice(offset, checked(count * 8));
            selected = checked(source.Address + (uint)offset);
        }
        else
        {
            vertices = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38, state.Read(0x26C0298, checked(count * 8)).ToImmutableArray());
            for (int index = 0; index < count; index++)
            {
                state = state.Write(checked(0x26C0298u + (uint)index * 8), vertices.AsSpan().Slice(index * 8, 6));
            }
            selected = 0x26C0298;
        }
        NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 88);
        NativeBattleFileStaging staging = allocated.Staging;
        uint header = allocated.Address;
        Store(0, checked(source.Address + (uint)geometry));
        Store(4, selected);
        Store(12, unchecked((uint)(int)node.Scalar36));
        uint flags = node.Scalar36 == 0 ? 0x2030u : 0x20F0u;
        if (node.Scalar36 != 0)
        {
            Store(8, node.Color32);
        }
        bool glow = physical == 1;
        if (glow)
        {
            flags |= 0x400;
            Store(16, 0);
        }
        Store(28, flags);
        uint color = BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(checked(header + 8), 4));
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Pain requires its current renderer registers.");
        NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
        process = process with { ModelRendererRegisters = registers.PublishModelColor(color) };
        NativeBattleActionMatrix matrix = Compose(node, anchor, projection.View ?? throw new NotSupportedException("Native Pain requires its current camera matrix."));
        Store(24, 0);
        uint meshAddress = checked(source.Address + (uint)geometry);
        Store(32, checked(meshAddress + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(geometry))));
        NativeBattleMeshPacketVariant observations = new(null, null, false, glow)
        {
            ObserveFlags = value => Store(48, value),
            ObserveArea = value => Store(36, unchecked((uint)value)),
            ObserveAverageDepth = value => Store(44, value)
        };
        NativeBattleGfGeometryBatch packets = glow
            ? Emit8DE9D0(source.Data, geometry, vertices, matrix, projection, flags, color, node.Scalar36, colors, observations.ObserveFlags, observations.ObserveArea, observations.ObserveAverageDepth, value => Store(32, checked(source.Address + (uint)value)))
            : NativeBattleGfGeometryPackets.EmitBatch(source.Data, geometry, vertices, matrix, projection, new(flags, color, node.Scalar36, null, null, 0, 0, 17, 2), colors, observeSourceCursor: value => Store(32, checked(source.Address + (uint)value)), variant: observations);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88);
        frame = frame with { Memory = frame.Memory with { Pain34 = state, PrimitiveProcess = process }, Staging = staging };
        return new(frame, packets, glow ? 0xE1000220u : null);

        void Store(int offset, uint value) => staging = staging.Write(checked(header + (uint)offset), BitConverter.GetBytes(value));
    }

    public static NativeBattleActionMatrix Compose(NativeBattleGfAnimationNode node, NativeBattleVector anchor, NativeBattleActionMatrix camera)
    {
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler701310(node.Vector16);
        int x;
        int y;
        int z;
        if ((node.Header & 0x200) != 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(camera, anchor);
            x = unchecked(x + node.Vector8.X);
            y = unchecked(y + node.Vector8.Y);
            z = unchecked(z + node.Vector8.Z);
        }
        else
        {
            NativeBattleVector position = new(unchecked((short)(anchor.X + node.Vector8.X)), unchecked((short)(anchor.Y + node.Vector8.Y)), unchecked((short)(anchor.Z + node.Vector8.Z)));
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(camera, position);
            matrix = NativeBattleGfMatrices.Multiply56C090(camera, matrix);
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        return matrix with { X = unchecked(x + camera.X), Y = unchecked(y + camera.Y), Z = unchecked(z + camera.Z) };
    }

    public static NativeBattleGfGeometryBatch Emit8DE9D0(ImmutableArray<byte> source, int geometry, ImmutableArray<byte> selectedVertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, uint flags, uint farColor, short fade, NativeBattleSummonColors colors, Action<uint>? observeFlags = null, Action<int>? observeArea = null, Action<uint>? observeAverageDepth = null, Action<int>? observeSourceCursor = null)
    {
        int cursor = checked(geometry + BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(geometry)));
        int allocation = 0;
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int group = 0; group < lengths.Length; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(cursor));
            cursor = checked(cursor + 4);
            if (count < 0 || (long)cursor + (long)count * lengths[group] > source.Length)
            {
                throw new InvalidDataException("Native Pain glow geometry leaves its selected original source.");
            }
            for (int face = 0; face < count; face++, cursor += lengths[group])
            {
                ReadOnlySpan<byte> primitive = source.AsSpan().Slice(cursor, lengths[group]);
                int corners = (group & 1) == 0 ? 3 : 4;
                ushort[] indices = Enumerable.Range(0, corners).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan().Slice(cursor + 4 + index * 2))).ToArray();
                NativeBattleGfGeometryPackets.ProjectedCorners projected = new(indices, index =>
                {
                    ReadOnlySpan<byte> vertex = selectedVertices.AsSpan().Slice(index * 4, 6);
                    return NativeBattleModuleProjection.Project45FE10(matrix, new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex[2..]), BinaryPrimitives.ReadInt16LittleEndian(vertex[4..])), projection);
                });
                NativeBattleGfGeometryAttempt ordinary = NativeBattleGfGeometryPackets.EmitAttempt(primitive, projected, group, new(flags, farColor, fade, 0, null, 0, 0, 17, 2), colors, allocation, variant: new(null, null, false, true) { ObserveFlags = observeFlags, ObserveArea = observeArea, ObserveAverageDepth = observeAverageDepth });
                byte[] bytes = new byte[ordinary.Data.Length + 4];
                ordinary.Data.AsSpan().CopyTo(bytes);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, unchecked(BinaryPrimitives.ReadUInt32LittleEndian(bytes) + 0x01000000));
                ImmutableArray<NativeBattleGfPacketByteState> mask = ordinary.WriteMask.AddRange(Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 4));
                NativeBattleGeometryPrimitive? accepted = null;
                if (ordinary.Primitive is { } linked)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(ordinary.Data.Length), 0xE1000220);
                    for (int offset = ordinary.Data.Length; offset < bytes.Length; offset++)
                    {
                        mask = mask.SetItem(offset, NativeBattleGfPacketByteState.Known);
                    }
                    accepted = linked with { Words = linked.Words.SetItem(0, unchecked(linked.Words[0] + 0x01000000)).Add(0xE1000220) };
                    allocation = checked(allocation + bytes.Length);
                }
                attempts.Add(new(ordinary.ByteOffsetRelative, bytes.ToImmutableArray(), mask, accepted));
            }
        }
        observeSourceCursor?.Invoke(cursor);
        return new(attempts.ToImmutable(), allocation);
    }
}