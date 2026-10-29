using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleThundaraMeshDraw(NativeBattleStaticMeshResource Resource, NativeBattleActionMatrix Matrix, NativeBattleModuleMeshContext Context);

/// <summary>The ordered Thundara controller's sprite, explicit-matrix mesh and private fullscreen submissions.</summary>
public static class NativeBattleThundaraDrawing
{
    public static NativeBattleModulePacketBatch Sprite(NativeBattleThundaraSprite operation, NativeBattleThundaraResources resources, NativeBattleCameraProjection projection, NativeBattleActionMatrix view, short cameraRoll)
    {
        NativeBattleModuleResource resource = resources.Sprite(operation.ResourceAddress);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            PilotIdentity = operation.PilotIdentity,
            Resource = resource,
            SpriteFrame = resource.SpriteFrame(operation.Frame),
            Phase = unchecked((ushort)operation.Frame),
            Anchor = operation.Position,
            Scale = operation.Scale,
            DepthOffset = operation.DepthOffset,
            SpriteFlags = operation.Flags,
            SpriteColor = operation.Color,
            Direction = 4096
        };
        return NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = view }, cameraRoll, 17, 2);
    }

    public static NativeBattleGfGeometryBatch Geometry(NativeBattleThundaraGeometry operation, NativeBattleThundaraResources resources, NativeBattleCameraProjection projection, NativeBattleActionMatrix view, NativeBattleSummonColors colors)
    {
        NativeBattleThundaraMeshDraw mesh = DescribeGeometry(operation, resources, view);
        return NativeBattleModulePackets.EmitBatch572200(mesh.Resource.Bytes, 0, default, mesh.Matrix, projection, mesh.Context, colors, pilotIdentity: operation.PilotIdentity);
    }

    public static NativeBattleModulePacketBatch Flash(NativeBattleThundaraResources resources, int arenaOffset)
    {
        byte intensity = (byte)resources.FlashColor;
        if (resources.FlashColor != (uint)intensity * 0x10101)
        {
            throw new NotSupportedException("The bound Thundara fullscreen resource is outside the existing grayscale flash packet contract.");
        }
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Flash, intensity)
        {
            NativePrimitiveCommand = 0x2A,
            NativeOrderingBucket = 8,
            NativeEffectArenaOffset = arenaOffset,
            NativeScreenVertices = [new(0, 0), new(320, 0), new(0, 216), new(320, 216)]
        };
        return NativeBattleFlashPackets.Emit(draw);
    }

    internal static NativeBattleThundaraMeshDraw DescribeGeometry(NativeBattleThundaraGeometry operation, NativeBattleThundaraResources resources, NativeBattleActionMatrix view)
    {
        if (operation.Duration <= 0)
        {
            throw new InvalidDataException("Native Thundara geometry requires the actual positive phase duration.");
        }
        NativeBattleActionMatrix local;
        NativeBattleStaticMeshResource resource;
        int fade;
        if (operation.Helper == 0x6DCD90)
        {
            int q = unchecked(operation.Phase << 12) / operation.Duration;
            short scale = unchecked((short)NativeBattleCameraMath.Sine(q >> 2));
            int cosine = NativeBattleCameraMath.Cosine(operation.AngleOrBaseY) * scale >> 12;
            int sine = NativeBattleCameraMath.Sine(operation.AngleOrBaseY) * scale >> 12;
            local = new([scale, 0, 0, 0, unchecked((short)cosine), unchecked((short)-sine), 0, unchecked((short)sine), unchecked((short)cosine)], operation.Position.X, operation.Position.Y, operation.Position.Z, 4096);
            fade = q < 1024 ? 0 : q - 1024 + (q - 1024) / 3;
            resource = resources.Mesh0;
        }
        else if (operation.Helper is 0x6DCEE0 or 0x6DCFE0)
        {
            int sine = NativeBattleCameraMath.Sine(unchecked(operation.Phase << 10) / operation.Duration);
            int x = operation.Helper == 0x6DCEE0 ? (sine >> 2) + 512 : sine;
            int y = operation.Helper == 0x6DCEE0 ? 2 * sine + 256 - (sine >> 3) - (sine >> 2) : sine;
            local = new([unchecked((short)x), 0, 0, 0, unchecked((short)y), 0, 0, 0, unchecked((short)x)], operation.Position.X, operation.AngleOrBaseY, operation.Position.Z, 4096);
            fade = sine;
            resource = operation.Helper == 0x6DCEE0 ? resources.Mesh1 : resources.Mesh2;
        }
        else
        {
            throw new NotSupportedException("Native Thundara geometry requires one of its three explicit matrix helpers.");
        }
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, local);
        return new(resource, matrix, new(0xF3, 0, fade, null, null, null, 17, 2));
    }
}
