using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDispel109MeshContext(uint Mesh, uint SelectedVertices, uint? FarColor, short Fade, int DepthBias, uint Flags, uint SourceCursor);
public sealed record NativeBattleDispel109Draw(NativeBattleDispel109Frame Frame, NativeBattleGfGeometryBatch Packets, NativeBattleActionMatrix? Matrix, NativeBattleDispel109MeshContext? Context, NativeBattleFileStaging? Staging);

public static class NativeBattleDispel109Geometry
{
    public static bool Visible(NativeBattleGfAnimationNode node) => node.Vector24 is not { X: 0, Y: 0, Z: 0 } && (node.Scalar36 < 4096 || node.Color32 != 0);

    public static NativeBattleActionMatrix Compose6CF9A0(NativeBattleGfAnimationNode node, NativeBattleActionMatrix view, NativeBattleVector anchor, short targetScale, bool scaledStackInput)
    {
        NativeBattleActionMatrix matrix = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector offset = node.Vector8;
        if (scaledStackInput)
        {
            offset = new(unchecked((short)(unchecked(targetScale * offset.X) >> 12)), unchecked((short)(unchecked(3000 * offset.Y) >> 12)), unchecked((short)(unchecked(targetScale * offset.Z) >> 12)));
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, targetScale, 3000, targetScale);
        }
        offset = new(unchecked((short)(anchor.X + offset.X)), unchecked((short)(anchor.Y + offset.Y)), unchecked((short)(anchor.Z + offset.Z)));
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(view, offset);
        if ((node.Header & 0x8000) == 0)
        {
            matrix = NativeBattleGfMatrices.Multiply56C090(view, matrix);
        }
        if (node.Vector24 is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, node.Vector24.X, node.Vector24.Y, node.Vector24.Z);
        }
        return matrix with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
    }

    public static NativeBattleDispel109Draw Draw6CF9A0(NativeBattleDispel109Frame frame, NativeBattleModulePoolTask target, NativeBattleGfAnimationNode node, bool scaledStackInput, NativeBattleDispel109Resources resources, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleFileStaging? staging = null, uint? temporary = null)
    {
        if (!Visible(node))
        {
            return new(frame, new([], 0), null, null, staging);
        }
        NativeBattleGfAnimationSource source = resources.Animation;
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(checked(8 + unchecked((short)node.GeometryIndex) * 4), 4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(checked(geometry + 4), 4));
        bool morph = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        uint selected;
        ImmutableArray<byte> selectedBytes = default;
        Func<int, NativeBattleVector>? readSelected = null;
        if (morph)
        {
            int offset = checked((frame.Effect.Dispel109 ?? throw new NotSupportedException("Native Dispel requires its executed fixed-base initializer.")).BufferOffset + 4312);
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Dispel morph requires its actual retained arena.");
            ImmutableArray<NativeBattleVector> vertices = NativeBattleGfMorphGeometry.MorphVertices701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38);
            for (int index = 0; index < vertices.Length; index++)
            {
                byte[] xyz = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(xyz, vertices[index].X);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), vertices[index].Y);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), vertices[index].Z);
                arena = arena.Write(checked(offset + index * 8), xyz);
            }
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
            NativeBattleEffectArena current = arena;
            readSelected = address =>
            {
                byte[] xyz = current.Read(checked(offset + address), 6);
                return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
            };
            selected = checked(0x20DFAB8u + (uint)offset);
        }
        else
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            int offset = checked(geometry + 12 + shape * count * 8);
            selected = checked(source.Address + (uint)offset);
            selectedBytes = source.Data.AsSpan().Slice(offset).ToArray().ToImmutableArray();
        }
        NativeBattleVector anchor = new(unchecked((short)target.ReadWord(24)), unchecked((short)target.ReadWord(26)), unchecked((short)target.ReadWord(28)));
        NativeBattleActionMatrix matrix = Compose6CF9A0(node, projection.View ?? throw new NotSupportedException("Native Dispel requires its current view matrix."), anchor, unchecked((short)target.ReadWord(30)), scaledStackInput);
        uint flags = node.Scalar36 == 0 ? 0x2030u : 0x20F0u;
        uint? far = node.Scalar36 == 0 ? null : node.Color32;
        if ((staging is null) != (temporary is null))
        {
            throw new ArgumentException("Native Dispel temporary drawing requires both its physical staging owner and address.");
        }
        void Store(int offset, uint value)
        {
            staging = staging!.Write(checked(temporary!.Value + (uint)offset), BitConverter.GetBytes(value));
        }
        if (temporary is not null)
        {
            Store(0, checked(source.Address + (uint)geometry));
            Store(4, selected);
            Store(12, unchecked((uint)(int)node.Scalar36));
            Store(24, unchecked((uint)-256));
            Store(28, flags);
            if (far is uint color)
            {
                Store(8, color);
            }
        }
        int cursor = geometry;
        NativeBattleGfGeometryBatch packets;
        if (temporary is uint address)
        {
            NativeBattleVector ReadVertex(int offset)
            {
                if (readSelected is not null)
                {
                    return readSelected(offset);
                }
                ReadOnlySpan<byte> bytes = selectedBytes.AsSpan().Slice(offset, 6);
                return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(4)));
            }
            NativeBattleModuleMeshTemporaryResult drawn = NativeBattleModulePackets.EmitRetainedBatch7043B0(staging!, address, checked(source.Address + (uint)geometry), source.Data.Slice(geometry, source.Data.Length - geometry), matrix, projection, new(flags, node.Color32, node.Scalar36, null, null, -256, 17, 2), colors, ReadVertex);
            staging = drawn.Staging;
            packets = drawn.Batch;
            cursor = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address + 32, 4)) - source.Address));
        }
        else
        {
            NativeBattleMeshPacketContext context = new(flags, far, node.Scalar36, null, null, null, -256, 17, 2);
            packets = NativeBattleGfGeometryPackets.EmitBatch(source.Data, geometry, selectedBytes, matrix, projection, context, colors, readSelected, observeSourceCursor: value => cursor = value);
        }
        return new(frame, packets, matrix, new(checked(source.Address + (uint)geometry), selected, far, node.Scalar36, -256, flags, checked(source.Address + (uint)cursor)), staging);
    }
}
