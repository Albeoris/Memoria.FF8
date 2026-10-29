using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFiragaDrawResult(ImmutableArray<NativeBattleModulePacketBatch> Sprites, NativeBattleGfGeometryBatch? Mesh);
internal sealed record NativeBattleFiragaMeshDraw(NativeBattleStaticMeshResource Resource, NativeBattleActionMatrix Matrix, NativeBattleModuleMeshContext Context);

/// <summary>Firaga's original571BC0/571C80 and572200 child draw submissions.</summary>
public static class NativeBattleFiragaDrawing
{
    public static NativeBattleFiragaDrawResult Draw(NativeBattleModulePoolTask task, NativeBattleFiragaState state, NativeBattleFiragaResources resources, NativeBattleCameraProjection projection, NativeBattleActionMatrix view, short cameraRoll, NativeBattleSummonColors colors)
    {
        if (task.Callback == 0x61C6B0)
        {
            (NativeBattleModuleDraw billboard, NativeBattleModuleDraw ground) = Surface(task, resources);
            NativeBattleCameraProjection camera = projection with { View = view };
            return new([NativeBattleModulePackets.EmitSprite571DC0(billboard, camera, cameraRoll, 17, 2), NativeBattleModulePackets.EmitSprite571DC0(ground, camera, cameraRoll, 17, 2)], null);
        }
        NativeBattleFiragaMeshDraw mesh = Mesh(task, state, resources, view);
        return new([], NativeBattleModulePackets.EmitBatch572200(mesh.Resource.Bytes, 0, default, mesh.Matrix, projection, mesh.Context, colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(143, task)));
    }

    internal static (NativeBattleModuleDraw Billboard, NativeBattleModuleDraw Ground) Surface(NativeBattleModulePoolTask task, NativeBattleFiragaResources resources)
    {
        if (task.Callback != 0x61C6B0)
        {
            throw new NotSupportedException("Native Firaga surface sprites require their original61C6B0 callback.");
        }
        short scale = Word(task, 28);
        int frame = Word(task, 12);
        NativeBattleModuleDraw billboard = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(143, task),
            Resource = resources.Sprite16,
            SpriteFrame = resources.Sprite16.SpriteFrame(frame),
            Phase = unchecked((ushort)frame),
            Anchor = Position(task),
            Scale = scale,
            DepthOffset = -(scale >> 4),
            Direction = 4096,
            SpriteFlags = 0
        };
        // Native nested signed IDIVs truncate independently; only the final BYTE store wraps.
        int height = Math.Max((int)Word(task, 18), -2000);
        byte intensity = unchecked((byte)(160 + ((height << 12) / 2000 * 160 / 4096)));
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(1024, 0, 0));
        NativeBattleModuleDraw ground = billboard with
        {
            PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(143, task, 1),
            Anchor = new(Word(task, 16), Word(task, 22), Word(task, 20), 0),
            DepthOffset = 0,
            SpriteWorldRotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, 4096).Rotation,
            SpriteFlags = 0xC,
            SpriteColor = (uint)intensity * 0x10101
        };
        return (billboard, ground);
    }

    internal static NativeBattleFiragaMeshDraw Mesh(NativeBattleModulePoolTask task, NativeBattleFiragaState state, NativeBattleFiragaResources resources, NativeBattleActionMatrix view)
    {
        int counter = Word(task, 12);
        short scale = Word(task, 28);
        NativeBattleStaticMeshResource resource;
        NativeBattleActionMatrix matrix;
        uint flags = 0x30;
        int? opacity = null;
        uint? farColor = null;
        switch (task.Callback)
        {
            case 0x61C870:
                resource = resources.RingMesh;
                matrix = World(view, NativeBattleGfMatrices.Euler56CE30(new(1024, Word(task, 24), 0)), Position(task), scale, scale, scale);
                flags = 0x33;
                if (counter >= 10)
                {
                    flags = 0xF3;
                    opacity = (counter - 10) * 409;
                    farColor = 0;
                }
                break;
            case 0x61C9C0:
                resource = resources.FlareMesh;
                matrix = World(view, NativeBattleGfMatrices.Euler56CE30(new(0, Word(task, 24), 0)), Position(task), scale, scale, scale);
                if (counter >= 14)
                {
                    flags = 0xF0;
                    opacity = (counter - 14) * 409;
                    farColor = 0;
                }
                break;
            case 0x61CB10:
                resource = resources.PortalMesh;
                // 571BC0 leaves a diagonal billboard matrix. Counterroll belongs to571C80,
                // which this direct572200 path never calls.
                NativeBattleModuleDraw billboard = new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = Position(task), Scale = scale, DepthOffset = -(scale >> 5), SpriteFlags = 0 };
                matrix = NativeBattlePoseMatrices.ModuleSpriteCamera(billboard, view, 0);
                break;
            case 0x61CBB0:
                resource = resources.OrbitMesh;
                NativeBattleActionMatrix rotation = new(NativeBattleModuleAlignment.Rotation5714F0(Word(task, 22), new(Word(task, 24), Word(task, 26), Word(task, 28))), 0, 0, 0, 4096);
                (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(rotation, Word(task, 16), Word(task, 18), Word(task, 20));
                NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, -4096, 0, x, y, z);
                NativeBattleActionMatrix oriented = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), 0, 0, 0, 4096);
                matrix = World(view, oriented, state.Read(Word(task, 14)), 8192, Word(task, 34), 8192);
                if (counter < 6 || counter >= 14)
                {
                    flags = 0xF0;
                    opacity = counter < 6 ? 4096 - 682 * counter : 682 * (counter - 14);
                    farColor = 0;
                }
                break;
            default:
                throw new NotSupportedException("Native Firaga drawing requires its surface, ring, flare, portal or orbit child callback.");
        }
        return new(resource, matrix, new(flags, farColor, opacity, null, null, null, 17, 2));
    }

    private static NativeBattleActionMatrix World(NativeBattleActionMatrix view, NativeBattleActionMatrix rotation, NativeBattleModuleAnchor position, int x, int y, int z)
    {
        NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(rotation, x, y, z) with { X = position.X, Y = position.Y, Z = position.Z };
        return NativeBattlePoseMatrices.Compose56C2F0(view, world);
    }

    private static NativeBattleModuleAnchor Position(NativeBattleModulePoolTask task) => new(Word(task, 16), Word(task, 18), Word(task, 20), Word(task, 22));
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
