using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAeroNodeDraw(NativeBattleEffectMemory Memory, NativeBattleGfGeometryBatch Packets);

/// <summary>6C10A0's source shape selection, two-stage scaling and7043B0 submission.</summary>
public static class NativeBattleAeroDrawing
{
    public static NativeBattleAeroNodeDraw Node(NativeBattleEffectMemory memory, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleAeroNodeConfig config, NativeBattleActionMatrix view, NativeBattleActionMatrix billboard, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        ValidateNode(node);
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(memory, new([], 0));
        }
        int index = unchecked((short)node.GeometryIndex);
        if (index < 0 || index >= U32(0))
        {
            throw new InvalidDataException("Native Aero node selects an absent current source geometry.");
        }
        int geometry = checked((int)U32(8 + index * 4));
        int vertices = checked((int)U32(geometry + 4));
        int shapes = checked((int)U32(geometry + 8));
        int first = node.Word40;
        int second = node.Word42;
        if (vertices < 1 || shapes < 1 || first < 0 || first >= shapes || second < 0 || second >= shapes)
        {
            throw new InvalidDataException("Native Aero node requires its actual12-byte geometry header and in-range shape owners.");
        }
        ImmutableArray<byte> selected;
        int ratio = node.Scalar38;
        if (first == second || ratio is 0 or 4096)
        {
            int shape = first == second || ratio == 0 ? first : second;
            int offset = checked(geometry + 12 + vertices * shape * 8);
            selected = Read(offset, checked(vertices * 8)).ToArray().ToImmutableArray();
        }
        else
        {
            NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Aero morph requires its actual shared arena scratch owner.");
            if (vertices > 92)
            {
                throw new InvalidDataException("Native Aero morph exceeds its retained92-vertex shared scratch.");
            }
            ImmutableArray<byte> scratch = arena.Read(config.MorphArenaOffset, 0x2E0).ToImmutableArray();
            ImmutableArray<byte> changed = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, first, second, ratio, scratch);
            memory = memory with { Arena = arena.Write(config.MorphArenaOffset, changed.AsSpan()) };
            selected = changed.AsSpan().Slice(0, vertices * 8).ToArray().ToImmutableArray();
        }
        NativeBattleActionMatrix matrix = ComposeNodeMatrix(node, config, view, billboard);
        bool cue = node.Scalar36 != 0;
        NativeBattleGfGeometryBatch packets = NativeBattleGfGeometryPackets.EmitBatch7043B0(source.Data, geometry, selected, matrix, projection, new(cue ? 0x20F0u : 0x2030u, cue ? node.Color32 : 0, node.Scalar36, null, null, config.DepthBias, 17, 2), colors);
        return new(memory, packets);

        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (source.Data.IsDefault || offset < 0 || length < 0 || offset > source.Data.Length - length)
            {
                throw new InvalidDataException("Native Aero geometry access leaves its current complete source.");
            }
            return source.Data.AsSpan().Slice(offset, length);
        }
        uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Read(offset, 4));
    }

    internal static NativeBattleActionMatrix ComposeNodeMatrix(NativeBattleGfAnimationNode node, NativeBattleAeroNodeConfig config, NativeBattleActionMatrix view, NativeBattleActionMatrix billboard)
    {
        ValidateNode(node);
        view.Validate();
        billboard.Validate();
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector position = node.Vector8;
        if (config.GlobalScale)
        {
            position = new(unchecked((short)(unchecked(position.X * config.ScaleX) >> 12)), unchecked((short)(unchecked(position.Y * config.ScaleY) >> 12)), unchecked((short)(unchecked(position.Z * config.ScaleZ) >> 12)));
            local = NativeBattlePoseMatrices.Scale56BEF0(local, config.ScaleX, config.ScaleY, config.ScaleZ);
        }
        bool facing = (node.Header & 0x1000) != 0;
        int x;
        int y;
        int z;
        if (facing)
        {
            (int ax, int ay, int az) = NativeBattleGfMatrices.Rotate56C3D0(view, new(config.Anchor.X, config.Anchor.Y, config.Anchor.Z));
            (int px, int py, int pz) = NativeBattleGfMatrices.Rotate56C3D0(billboard, position);
            (x, y, z) = (unchecked(ax + px), unchecked(ay + py), unchecked(az + pz));
        }
        else
        {
            NativeBattleVector combined = new(unchecked((short)(config.Anchor.X + position.X)), unchecked((short)(config.Anchor.Y + position.Y)), unchecked((short)(config.Anchor.Z + position.Z)));
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, combined);
        }
        if ((node.Header & 0x8000) == 0)
        {
            local = NativeBattleGfMatrices.Multiply56C090(facing ? billboard : view, local);
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            local = (node.Header & 0x100) != 0
                ? NativeBattleGfMatrices.Multiply56C090(local, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096))
                : local with { Rotation = local.Rotation.Select((value, index) => unchecked((short)(value * (index / 3 == 0 ? scale.X : index / 3 == 1 ? scale.Y : scale.Z) >> 12))).ToImmutableArray() };
        }
        return local with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
    }

    public static NativeBattleGfGeometryBatch Particle(NativeBattleModulePoolTask task, NativeBattleAeroResources resources, NativeBattleCameraProjection projection, NativeBattleActionMatrix view, NativeBattleSummonColors colors)
    {
        if (task.Callback != 0x6C14B0)
        {
            throw new NotSupportedException("Native Aero particle drawing requires its original particle callback.");
        }
        short scale = Word(0x10);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(Word(0x2A), Word(0x2C), Word(0x2E))), scale, scale, scale) with { X = Word(0x1A), Y = Word(0x1C), Z = Word(0x1E) };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, local);
        return NativeBattleModulePackets.EmitBatch572200(resources.ParticleMesh.Bytes, 0, default, matrix, projection, new(0, null, null, null, null, null, 17, 2), colors);

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
    }

    private static void ValidateNode(NativeBattleGfAnimationNode node)
    {
        if (node.Data.IsDefault || node.Data.Length != 44)
        {
            throw new InvalidDataException("Native Aero drawing requires the complete decoded44-byte node record.");
        }
    }
}
