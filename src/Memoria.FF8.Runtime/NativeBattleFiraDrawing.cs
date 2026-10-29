namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFiraDrawResult(NativeBattleModulePacketBatch? Sprites, NativeBattleGfGeometryBatch? Mesh);
internal sealed record NativeBattleFiraMeshDraw(NativeBattleStaticMeshResource Resource, NativeBattleActionMatrix Matrix, NativeBattleModuleMeshContext Context);

/// <summary>Fira's six drawable child submissions through the existing571C80/572200 packet contracts.</summary>
public static class NativeBattleFiraDrawing
{
    public static NativeBattleFiraDrawResult Draw(NativeBattleModulePoolTask task, NativeBattleFiraResources resources, NativeBattleModuleResource commonResource5, NativeBattleCameraProjection projection, NativeBattleActionMatrix view, short cameraRoll, NativeBattleSummonColors colors)
    {
        if (task.Callback == 0x61DAF0)
        {
            return new(null, null);
        }
        if (task.Callback is 0x61D4C0 or 0x61D5D0 or 0x61D6B0)
        {
            NativeBattleModuleDraw sprite = Sprite(task, resources, commonResource5);
            return new(NativeBattleModulePackets.EmitSprite571DC0(sprite, projection with { View = view }, cameraRoll, 17, 2), null);
        }
        NativeBattleFiraMeshDraw mesh = Mesh(task, resources, view);
        return new(null, NativeBattleModulePackets.EmitBatch572200(mesh.Resource.Bytes, 0, default, mesh.Matrix, projection, mesh.Context, colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(142, task)));
    }

    internal static NativeBattleModuleDraw Sprite(NativeBattleModulePoolTask task, NativeBattleFiraResources resources, NativeBattleModuleResource commonResource5)
    {
        int counter = Word(task, 12);
        bool spark = task.Callback == 0x61D4C0;
        bool ring = task.Callback == 0x61D6B0;
        if (!spark && !ring && task.Callback != 0x61D5D0)
        {
            throw new NotSupportedException("Native Fira sprite submission requires its spark, smoke or ring callback.");
        }
        NativeBattleModuleResource resource = spark ? commonResource5 : ring ? resources.Sprite0 : resources.AnimatedSprite;
        int frame = spark ? 9 - (counter >> 1) : ring ? 0 : counter;
        short scale = Word(task, spark ? 32 : 28);
        ushort flags = spark ? (ushort)0x10 : (ushort)0;
        uint color = 0x808080;
        if (ring && (counter < 6 || counter >= 14))
        {
            byte intensity = unchecked((byte)(counter < 6 ? counter * 21 : 0x60 - (counter << 4)));
            color = (uint)intensity * 0x10101;
            flags = 4;
        }
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(142, task),
            Resource = resource,
            SpriteFrame = resource.SpriteFrame(frame),
            Phase = unchecked((ushort)frame),
            Anchor = new(Word(task, 16), Word(task, 18), Word(task, 20), Word(task, 22)),
            Scale = scale,
            DepthOffset = ring ? 0 : -(scale >> 3),
            Direction = 4096,
            SpriteFlags = flags,
            SpriteColor = color
        };
        if (spark)
        {
            // Only palette0 is read with flag10; the other array cells have no descriptor meaning here.
            draw = draw with { SpritePaletteOffsets = [4, 0, 0, 0] };
        }
        if (ring)
        {
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(1024, Word(task, 24), 0));
            draw = draw with { SpriteWorldRotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, scale).Rotation };
        }
        return draw;
    }

    internal static NativeBattleFiraMeshDraw Mesh(NativeBattleModulePoolTask task, NativeBattleFiraResources resources, NativeBattleActionMatrix view)
    {
        int counter = Word(task, 12);
        bool column = task.Callback == 0x61D7F0;
        bool flare = task.Callback == 0x61D9A0;
        bool tracer = task.Callback == 0x61DC20;
        if (!column && !flare && !tracer)
        {
            throw new NotSupportedException("Native Fira mesh submission requires its column, flare or tracer callback.");
        }
        int scale = Word(task, 28);
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CD50(new(0, Word(task, 24), 0));
        world = NativeBattlePoseMatrices.Scale56BEF0(world, scale, tracer ? scale : Word(task, 32), scale) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
        NativeBattleActionMatrix composed = NativeBattlePoseMatrices.Compose56C2F0(view, world);
        uint flags = tracer ? 0x1033u : 0x30u;
        int? opacity = null;
        uint? farColor = null;
        if (column && (counter < 4 || counter >= 8))
        {
            flags = 0xF0;
            opacity = counter < 4 ? (4 - counter) << 10 : (counter - 8) * 409;
            farColor = 0;
        }
        else if (flare && counter >= 14 || tracer && counter >= 8)
        {
            flags = tracer ? 0x10F3u : 0xF0u;
            opacity = (counter - (tracer ? 8 : 14)) << 9;
            farColor = 0;
        }
        uint? uvOffset = null;
        if (tracer)
        {
            if (resources.UvOffsets.IsDefault || resources.UvOffsets.Length != 16 || (uint)counter >= 16)
            {
                throw new InvalidDataException("Native Fira tracer requires its original sixteen-DWORD packed UV-offset table and current frame.");
            }
            uvOffset = resources.UvOffsets[counter];
        }
        NativeBattleModuleMeshContext context = new(flags, farColor, opacity, null, null, uvOffset, 17, 2);
        return new(column ? resources.Mesh2 : flare ? resources.Mesh1 : resources.Mesh0, composed, context);
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
