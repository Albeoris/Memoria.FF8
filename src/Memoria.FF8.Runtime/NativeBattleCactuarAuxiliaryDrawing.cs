using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCactuarParticleDraw(NativeBattleActionMatrix CameraMatrix, short FrameResult, NativeBattleModulePacketBatch Packets);
public sealed record NativeBattleCactuarMeshDraw(NativeBattleStaticMeshResource Resource, NativeBattleActionMatrix Matrix, NativeBattleModuleMeshContext Context);

/// <summary>Cactuar's retained particle and three ordinary mesh submissions into the common packet bank.</summary>
public static class NativeBattleCactuarAuxiliaryDrawing
{
    public static NativeBattleCactuarParticleDraw? Particle5A9290(ImmutableArray<byte> particle, NativeBattleCactuar199SpriteResource sprite, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (particle.IsDefault || particle.Length < 0x12)
        {
            throw new InvalidDataException("Native Cactuar particle requires its retained flags, frame, scale and position.");
        }
        if ((BinaryPrimitives.ReadUInt32LittleEndian(particle.AsSpan()) & 1) == 0)
        {
            return null;
        }
        short frameIndex = Word(4);
        short scale = Word(6);
        NativeBattleVector position = new(Word(0xC), Word(0xE), Word(0x10));
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(view, position);
        NativeBattleActionMatrix camera = new([scale, 0, 0, 0, scale, 0, 0, 0, scale], unchecked(x + view.X), unchecked(y + view.Y), unchecked(z + view.Z), 4096);
        NativeBattleModuleResource resource = new(0, sprite.Address, 0, 0, sprite.Bytes);
        NativeBattleModuleSpriteFrame frame = resource.ProjectedFrame571C80(frameIndex);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            PilotIdentity = pilotIdentity,
            Resource = resource,
            SpriteFrame = frame,
            Phase = unchecked((ushort)frameIndex),
            Anchor = new(0, 0, 0, 0),
            Scale = scale,
            SpriteFlags = 0
        };
        NativeBattleActionMatrix spriteView = camera with { Rotation = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096] };
        return new(camera, frame.NextOffset, NativeBattleModulePackets.EmitSprite571DC0(draw, projection, 0, 17, 2, NativeBattlePoseMatrices.ModuleSpriteCamera(draw, spriteView, 0)));

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(particle.AsSpan().Slice(offset));
    }

    public static NativeBattleGfGeometryBatch Mesh(NativeBattleModulePoolTask task, NativeBattleCactuar199Resources resources, int modelY, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleStaticMeshResource resource = task.Callback switch
        {
            0x5A9140 => resources.MeshC,
            0x5A9570 => resources.MeshA,
            0x5A9730 => resources.MeshB,
            _ => throw new NotSupportedException("Native Cactuar mesh requires its actual drawing callback.")
        };
        NativeBattleCactuarMeshDraw draw = DescribeMesh(task, resource, modelY, view);
        return NativeBattleModulePackets.EmitBatch572200(resource.Bytes, 0, default, draw.Matrix, projection, draw.Context, colors, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(199, task));
    }

    public static NativeBattleCactuarMeshDraw DescribeMesh(NativeBattleModulePoolTask task, NativeBattleStaticMeshResource resource, int modelY, NativeBattleActionMatrix view)
    {
        int counter = Word(0xC);
        int scale = Word(0x1C);
        int scaleY = scale;
        NativeBattleVector angles;
        uint flags = 0x33;
        int fade = 0;
        uint address;
        switch (task.Callback)
        {
            case 0x5A9140:
                address = 0xCF26F8;
                angles = new(0, Word(0x18), 0);
                scaleY = VerticalScale5A9140(modelY);
                if (counter >= 6)
                {
                    flags = 0xF3;
                    fade = unchecked((counter - 6) * 682);
                }
                break;
            case 0x5A9570:
                address = 0xCF08EC;
                angles = default;
                if (counter < 4)
                {
                    flags = 0xF3;
                    fade = unchecked((4 - counter) << 10);
                }
                break;
            case 0x5A9730:
                address = 0xCF11BC;
                angles = new(0, 0, Word(0x18));
                if (counter < 8 || counter >= 14)
                {
                    flags = 0xF3;
                    fade = counter < 8 ? unchecked((8 - counter) << 9) : unchecked((counter - 14) * 409);
                }
                break;
            default:
                throw new NotSupportedException("Native Cactuar mesh requires its actual drawing callback.");
        }
        if (resource.Address != address)
        {
            throw new InvalidDataException("Native Cactuar mesh changed its original resource owner.");
        }
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(angles), scale, scaleY, scale) with { X = Word(0x10), Y = Word(0x12), Z = Word(0x14) };
        return new(resource, NativeBattlePoseMatrices.Compose56C2F0(view, local), new(flags, 0, fade, null, null, null, 17, 2));

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
    }

    public static int VerticalScale5A9140(int modelY)
    {
        int q = unchecked(-modelY << 12);
        int high = (int)(((long)q * unchecked((int)0xA4DED52D)) >> 32);
        int t = unchecked(high + q) >> 10;
        return unchecked(t + (int)((uint)t >> 31));
    }
}
