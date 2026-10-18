using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlizzagaDrawing
{
    public static NativeBattleGfGeometryBatch AuxiliaryA(NativeBattleModulePoolTask task, int phase, NativeBattleBlizzagaResources resources, NativeBattleCameraProjection projection, NativeBattleActionMatrix view, NativeBattleSummonColors colors)
    {
        int u = phase - 46;
        if ((uint)u > 24)
        {
            throw new InvalidDataException("Native Blizzaga auxiliary A requires its reached phase46 through70.");
        }
        int fade = u < 4 ? 4096 - (u << 10) : u <= 17 ? 0 : 4096 - ((25 - u) << 9);
        short scale = unchecked((short)(3072 + (NativeBattleCameraMath.Sine((u << 10) / 25) >> 2)));
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, Oriented(task, scale, scale));
        NativeBattleEffectMeshWindow window = new(unchecked((ushort)((phase & 15) << 3)), 0, new(0, 0, 256, 256), new(0, 128, 128, 64), true);
        return NativeBattleEffectMeshPackets.EmitBatch(resources.Mesh1317088.Bytes, matrix, projection, 0, null, (group, face, project, offset) =>
        {
            bool gouraud = group is 6 or 7;
            if (group is not (3 or 6 or 7))
            {
                throw new NotSupportedException("Native Blizzaga auxiliary A requires its original FT4,GT3 and GT4 groups.");
            }
            NativeBattleEffectMeshPacketContext context = new(fade == 0 ? 0u : gouraud ? 0x80u : 0x40u, 0, fade, null, null, 0, false, null, 17, 2, window);
            return NativeBattleEffectMeshPackets.EmitTextured(face, project, group != 6, gouraud, context, colors, offset, NativeBattleEffectMeshClip.OutsideScreen);
        });
    }

    public static NativeBattleGfGeometryBatch AuxiliaryB(NativeBattleModulePoolTask task, int phase, NativeBattleBlizzagaResources resources, NativeBattleCameraProjection projection, NativeBattleActionMatrix view, NativeBattleSummonColors colors)
    {
        int u = phase - 31;
        if ((uint)u > 9)
        {
            throw new InvalidDataException("Native Blizzaga auxiliary B requires its reached phase31 through40.");
        }
        short scale = unchecked((short)(4096 + 4 * NativeBattleCameraMath.Sine((u << 10) / 10)));
        NativeBattleActionMatrix local = new([scale, 0, 0, 0, 4096, 0, 0, 0, scale], Word(task, 0x20), Word(task, 0x22), Word(task, 0x24), 4096);
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, local);
        int fade = u <= 6 ? 0 : 4096 - ((10 - u) << 10);
        return NativeBattleModulePackets.EmitBatch572200(resources.Mesh1318490.Bytes, 0, default, matrix, projection, new(0xF0, 0, fade, null, null, null, 17, 2), colors);
    }

    public static NativeBattleModulePacketBatch Sprite(NativeBattleModulePoolTask task, NativeBattleBlizzagaResources resources, NativeBattleCameraProjection projection, NativeBattleActionMatrix view, short cameraRoll, NativeBattleActionMatrix? sparkMatrix = null)
    {
        bool spark = task.Callback == 0x6D9DD0;
        bool sixth = task.Callback == 0x6D96A0;
        if (!spark && !sixth && task.Callback != 0x6D95E0)
        {
            throw new NotSupportedException("Native Blizzaga sprite drawing requires an original particle callback.");
        }
        NativeBattleModuleResource resource = sixth ? resources.Sprite14 : resources.Sprite10;
        int frame = Word(task, 0xC);
        NativeBattleModuleAnchor anchor;
        short scale;
        if (spark)
        {
            view = sparkMatrix ?? throw new NotSupportedException("Native Blizzaga spark requires its published identity matrix and full camera-space translation.");
            view.Validate();
            if (!view.Rotation.SequenceEqual(new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 }))
            {
                throw new InvalidDataException("Native Blizzaga spark requires the original identity rotation.");
            }
            anchor = new(0, 0, 0, 0);
            scale = 4096;
        }
        else
        {
            int start = sixth ? 0x14 : 0x10;
            anchor = new(Word(task, start), Word(task, start + 2), Word(task, start + 4), Word(task, start + 6));
            scale = Word(task, sixth ? 0x1A : 0x16);
        }
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Resource = resource,
            SpriteFrame = resource.SpriteFrame(frame),
            Phase = unchecked((ushort)frame),
            Anchor = anchor,
            Scale = scale,
            DepthOffset = spark ? 0 : -(scale >> 2),
            SpriteFlags = 0,
            Direction = 4096
        };
        return NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = view }, cameraRoll, 17, 2);
    }

    private static NativeBattleActionMatrix Oriented(NativeBattleModulePoolTask task, short horizontal, short vertical)
    {
        int angle = Word(task, 0x10);
        short cosine = unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * horizontal) >> 12));
        short sine = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * horizontal) >> 12));
        return new([cosine, 0, sine, 0, vertical, 0, unchecked((short)-sine), 0, cosine], Word(task, 0x20), Word(task, 0x22), Word(task, 0x24), 4096);
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
