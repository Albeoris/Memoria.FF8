using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTonberry90ProgramDrawServices(Func<NativeBattleTonberry90Frame, uint, short> ReadWord, Func<NativeBattleTonberry90Frame, NativeBattleGfGeometryBatch, uint?, NativeBattleTonberry90Frame> Submit);
public sealed record NativeBattleTonberry90PreparedNode(NativeBattleGfGeometryBatch Packets, ImmutableArray<NativeBattleVector> Morph, uint? FarColor, bool Active = true);

public static class NativeBattleTonberry90ProgramDrawing
{
    public static NativeBattleTonberry90ActorDraw Advance764130(NativeBattleTonberry90Frame frame, uint address, NativeBattleGfAnimationSource source, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleTonberry90ProgramDrawServices services)
    {
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        if (task.ReadDword(0x74) != source.Address || task.PayloadLength < 0x94 - 12)
        {
            throw new InvalidDataException("Native Tonberry programme requires its actual source pointer and retained workspace.");
        }
        NativeBattleActionMatrix view = (frame.Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Native Tonberry programme requires its actual shared view copy.")).ReadMatrix();
        NativeBattleActionMatrix context = NativeBattleSearchProgramDrawing.ComposeContext758D70(task, view);
        NativeBattleTonberry90State shared = frame.Memory.Tonberry90 ?? throw new NotSupportedException("Native Tonberry programme requires its current module state.");
        int frozen = unchecked((int)shared.ReadDword(0x25A2118));
        NativeBattleGfAnimationWorkspace workspace = new(task.ReadRawPayloadSpan().Slice(0x94 - 12).ToArray().ToImmutableArray());
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, frozen, (current, node) =>
        {
            if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
            {
                return current;
            }
            short bias = unchecked((short)task.ReadWord(0x88));
            if (task.ReadWord(0x80) == 0)
            {
                NativeBattleTonberry90State actual = frame.Memory.Tonberry90 ?? throw new NotSupportedException("Native Tonberry depth selection requires its current module state.");
                uint table = actual.ReadDword(0x25A4C04);
                bias = services.ReadWord(frame, unchecked(table + (uint)(2 * unchecked((short)node.GeometryIndex))));
            }
            NativeBattleTonberry90PreparedNode prepared = Prepare764260(source, node, context, bias, projection, colors);
            if (!prepared.Morph.IsEmpty)
            {
                NativeBattleTonberry90State actual = frame.Memory.Tonberry90 ?? throw new NotSupportedException("Native Tonberry morph requires its actual shared scratch owner.");
                uint destination = 0x25A4008;
                foreach (NativeBattleVector vector in prepared.Morph)
                {
                    byte[] xyz = new byte[6];
                    BinaryPrimitives.WriteInt16LittleEndian(xyz, vector.X);
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), vector.Y);
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), vector.Z);
                    actual = actual.Write(destination, xyz);
                    destination = checked(destination + 8);
                }
                frame = frame with { Memory = frame.Memory with { Tonberry90 = actual } };
            }
            if (prepared.Active)
            {
                frame = services.Submit(frame, prepared.Packets, prepared.FarColor);
            }
            return current;
        });
        task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        task = task.WriteRetainedBytes(0x94, advanced.Workspace.Data.AsSpan());
        frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
        return new(frame, advanced.Remaining);
    }

    public static NativeBattleTonberry90PreparedNode Prepare764260(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleActionMatrix context, short depthBias, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        if (node.Data.IsDefault || node.Data.Length != 44)
        {
            throw new InvalidDataException("Native Tonberry mesh requires its complete decoded callback record.");
        }
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(new([], 0), [], null, false);
        }
        int geometry = Int(checked(8 + 4 * unchecked((short)node.GeometryIndex)));
        int count = Int(checked(geometry + 4));
        if (count < 0 || count > source.Data.Length / 8)
        {
            throw new InvalidDataException("Native Tonberry vertex extent leaves its supplied current source.");
        }
        ImmutableArray<NativeBattleVector> morph = [];
        int selected = 0;
        if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            selected = checked(geometry + 12 + shape * count * 8);
        }
        else
        {
            morph = NativeBattleGfMorphGeometry.MorphVertices701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38);
            if ((long)morph.Length * 8 > 0x25A4BF0 - 0x25A4008)
            {
                throw new InvalidDataException("Native Tonberry morph exceeds its supplied shared scratch window.");
            }
        }
        NativeBattleActionMatrix matrix = Compose764260(node, context);
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        if ((node.Header & 0x2000) != 0)
        {
            flags |= 0xC;
        }
        bool cue = node.Scalar36 != 0;
        if (cue)
        {
            flags |= 0xC0;
        }
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleEsunaMeshContext operands = new(flags, cue ? node.Color32 : null, node.Scalar36, depthBias, 0, 0, full, full, 17, 2);
        NativeBattleGfGeometryBatch packets = NativeBattleTonberry90MeshPackets.EmitBatch764540(source.Data.AsSpan().Slice(geometry).ToArray().ToImmutableArray(), matrix, projection, operands, colors, offset =>
        {
            if (offset < 0 || (offset & 7) != 0 || offset / 8 >= count)
            {
                throw new InvalidDataException("Native Tonberry selected vertex leaves its current shape.");
            }
            if (!morph.IsEmpty)
            {
                return morph[offset / 8];
            }
            ReadOnlySpan<byte> xyz = Read(checked(selected + offset), 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.Slice(4)));
        });
        return new(packets, morph, cue ? node.Color32 : null);

        int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(offset, 4));
        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (source.Data.IsDefault || offset < 0 || length < 0 || offset > source.Data.Length - length)
            {
                throw new InvalidDataException("Native Tonberry mesh operand leaves its supplied source bytes.");
            }
            return source.Data.AsSpan().Slice(offset, length);
        }
    }

    public static NativeBattleActionMatrix Compose764260(NativeBattleGfAnimationNode node, NativeBattleActionMatrix context)
    {
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler701310(node.Vector16);
        int x = node.Vector8.X;
        int y = node.Vector8.Y;
        int z = node.Vector8.Z;
        if ((node.Header & 0x200) == 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(context, node.Vector8);
            matrix = NativeBattleGfMatrices.Multiply56C090(context, matrix);
        }
        if (node.Vector24 is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, node.Vector24.X, node.Vector24.Y, node.Vector24.Z);
        }
        return matrix with { X = unchecked(context.X + x), Y = unchecked(context.Y + y), Z = unchecked(context.Z + z) };
    }
}
