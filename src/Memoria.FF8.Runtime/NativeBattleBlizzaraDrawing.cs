using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleBlizzaraMeshDraw(NativeBattleStaticMeshResource Resource, NativeBattleActionMatrix Matrix, NativeBattleModuleMeshContext Context);

/// <summary>Blizzara's mutable GT3 leaf/overlay and four original child draw submissions.</summary>
public static class NativeBattleBlizzaraDrawing
{
    public static NativeBattleGfGeometryBatch EmitMainMesh(ImmutableArray<byte> clone, ImmutableArray<byte> expandedVertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, uint descriptorFlags, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (clone.IsDefault || clone.Length != 0x1CBC || expandedVertices.IsDefault || expandedVertices.Length != 669 * 8)
        {
            throw new InvalidDataException("Native Blizzara main submission requires its full canonical cloned mesh and669 expanded8-byte vertices.");
        }
        if ((descriptorFlags & 0x2000) == 0 || (descriptorFlags & 0x1000) != 0)
        {
            throw new NotSupportedException("Native Blizzara main submission requires its proved expanded-vertex and zero packed-UV-offset descriptor contract.");
        }
        int faces = 0;
        NativeBattleGfGeometryBatch result = NativeBattleEffectMeshPackets.EmitBatch(clone, matrix, projection, descriptorFlags, offset =>
        {
            if (offset < 0 || (offset & 7) != 0 || offset > expandedVertices.Length - 8)
            {
                throw new InvalidDataException("Native Blizzara remapped selector leaves its actual expanded vertex owner.");
            }
            ReadOnlySpan<byte> bytes = expandedVertices.AsSpan().Slice(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
        }, (group, face, project, offset) =>
        {
            if (group != 6)
            {
                throw new NotSupportedException("Native Blizzara's initialized primary clone contains only its original GT3 group.");
            }
            faces++;
            NativeBattleGfGeometryBatch batch = EmitMainFace6DB5D0(face.ToArray().ToImmutableArray(), [project(0), project(1), project(2)], descriptorFlags);
            return batch with { Attempts = batch.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + offset) }).ToImmutableArray() };
        }, pilotIdentity: pilotIdentity);
        if (faces != 223)
        {
            throw new InvalidDataException("Native Blizzara main submission lost its223 original GT3 faces.");
        }
        return result;
    }

    public static NativeBattleGfGeometryBatch EmitMainFace6DB5D0(ImmutableArray<byte> source, ImmutableArray<NativeBattleModuleProjectedVertex> corners, uint descriptorFlags)
    {
        if (source.IsDefault || source.Length != 28 || corners.IsDefault || corners.Length != 3)
        {
            throw new InvalidDataException("Native Blizzara main drawing requires its current28-byte GT3 and three actual projected expanded vertices.");
        }
        if ((descriptorFlags & 0x2000) != 0 && source[27] != 0)
        {
            throw new NotSupportedException("Native Blizzara's initialized companion branch was mutated beyond its proved zero owner.");
        }
        byte alpha = source[23];
        NativeBattleModuleMeshContext context = new(alpha == 0 ? 0u : 0x20u, null, null, null, null, 0, 17, 2);
        NativeBattleGfGeometryAttempt main = NativeBattleModulePackets.EmitFace572200(source.AsSpan(), corners, 6, context);
        if (main.Primitive is not { } accepted)
        {
            return new([main], 0);
        }
        if ((descriptorFlags & 0x40) == 0 || alpha == 0)
        {
            return new([main], 40);
        }
        SkinWrites overlay = new(20, 40);
        overlay.Word(0, 0x04000000);
        overlay.Word(4, 0x22000000u | (uint)alpha * 0x10101);
        overlay.Word(8, accepted.Words[2]);
        overlay.Word(12, accepted.Words[5]);
        overlay.Word(16, accepted.Words[8]);
        NativeBattleGeometryPrimitive triangle = new(accepted.Bucket, overlay.Words(), accepted.VertexPayload4);
        SkinWrites mode = new(8, 60);
        mode.Word(0, 0x01000000);
        mode.Word(4, 0xE1000240);
        NativeBattleGeometryPrimitive command = new(accepted.Bucket, mode.Words(), [0, 0, 0, 0]);
        return new([main, overlay.Attempt(triangle), mode.Attempt(command)], 68);
    }

    public static NativeBattleModulePacketBatch Sprite(NativeBattleModulePoolTask task, NativeBattleBlizzaraResources resources, NativeBattleCameraProjection projection, NativeBattleActionMatrix view, short cameraRoll)
    {
        return NativeBattleModulePackets.EmitSprite571DC0(DescribeSprite(task, resources), projection with { View = view }, cameraRoll, 17, 2);
    }

    internal static NativeBattleModuleDraw DescribeSprite(NativeBattleModulePoolTask task, NativeBattleBlizzaraResources resources)
    {
        bool smoke = task.Callback == 0x6DBEA0;
        if (!smoke && task.Callback != 0x6DBFF0)
        {
            throw new NotSupportedException("Native Blizzara sprites require their smoke or trail callback.");
        }
        int start = smoke ? 0x14 : 0x10;
        short scale = Word(task, smoke ? 0x1A : 0x16);
        int frame = task.ReadByte(0xC) & 15;
        return new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(103, task),
            Resource = resources.Sprite16,
            SpriteFrame = resources.Sprite16.SpriteFrame(frame),
            Phase = (ushort)frame,
            Anchor = new(Word(task, start), Word(task, start + 2), Word(task, start + 4), Word(task, start + 6)),
            Scale = scale,
            DepthOffset = -(scale >> 2),
            SpriteFlags = 0,
            Direction = 4096
        };
    }

    public static NativeBattleGfGeometryBatch? Mesh(NativeBattleModulePoolTask task, NativeBattleBlizzaraResources resources, NativeBattleCameraProjection projection, NativeBattleActionMatrix view, NativeBattleSummonColors colors)
    {
        NativeBattleBlizzaraMeshDraw? draw = DescribeMesh(task, resources, view);
        return draw is null ? null : NativeBattleModulePackets.EmitBatch572200(draw.Resource.Bytes, 0, default, draw.Matrix, projection, draw.Context, colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(103, task));
    }

    internal static NativeBattleBlizzaraMeshDraw? DescribeMesh(NativeBattleModulePoolTask task, NativeBattleBlizzaraResources resources, NativeBattleActionMatrix view)
    {
        int counter = Word(task, 0xC);
        bool sky = task.Callback == 0x6DBB20;
        if (!sky && task.Callback != 0x6DBD20)
        {
            throw new NotSupportedException("Native Blizzara mesh drawing requires its sky or ring callback.");
        }
        int fade = counter < 6 ? 4096 - NativeBattleCameraMath.Sine((counter << 10) / 6) : counter < (sky ? 18 : 14) ? 0 : NativeBattleCameraMath.Sine(((counter - (sky ? 18 : 14)) << 10) / (sky ? 12 : 6));
        if (fade >= 4096)
        {
            return null;
        }
        int start = sky ? 0x14 : 0x10;
        short scale = Word(task, sky ? 0x22 : 0x1A);
        NativeBattleActionMatrix local;
        if (sky)
        {
            short yaw = Word(task, 0x1C);
            int cosine = NativeBattleCameraMath.Cosine(yaw) * scale >> 12;
            int sine = NativeBattleCameraMath.Sine(yaw) * scale >> 12;
            local = new([unchecked((short)cosine), 0, unchecked((short)sine), 0, scale, 0, unchecked((short)-sine), 0, unchecked((short)cosine)], Word(task, start), Word(task, start + 2), Word(task, start + 4), 4096);
        }
        else
        {
            local = new([scale, 0, 0, 0, scale, 0, 0, 0, scale], Word(task, start), Word(task, start + 2), Word(task, start + 4), 4096);
        }
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, local);
        return new(sky ? resources.SkyMesh : resources.RingMesh, matrix, new(0xF3, 0, fade, null, null, null, 17, 2));
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
