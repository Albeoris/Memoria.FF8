using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The instruction-identical Double/Triple mesh, oriented, ground and billboard draw adapters.</summary>
public static class NativeBattleEnhancementDraws
{
    public static NativeBattleGfGeometryBatch? EmitMesh(NativeBattleModulePoolTask task, Func<uint, ImmutableArray<byte>> readCurrentMesh, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return null;
        }
        uint meshAddress = task.ReadDword(0x4C);
        ImmutableArray<byte> bytes = readCurrentMesh(meshAddress);
        // One task may draw simultaneous layers selected by distinct authored mesh resources.
        if (pilotIdentity is { } identity)
        {
            pilotIdentity = identity with { Primitive = unchecked(identity.Primitive * 397 + (int)meshAddress) };
        }
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CD50(new(Word(task, 0x44), Word(task, 0x46), Word(task, 0x48)));
        world = Scale(world, task) with { X = Word(task, 0x1C), Y = Word(task, 0x1E), Z = Word(task, 0x20) };
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native enhancement mesh draw requires the prior1D97778 view.");
        NativeBattleActionMatrix composed = NativeBattlePoseMatrices.Compose56C2F0(view, world);
        NativeBattleModuleMeshContext context = new(0xF0, task.ReadDword(0x40), Word(task, 0x50), null, null, null, 17, 2);
        return NativeBattleModulePackets.EmitBatch572200(bytes, 0, [], composed, projection, context, colors, pilotIdentity: pilotIdentity);
    }

    public static NativeBattleModuleDraw? OrientedSprite(NativeBattleModulePoolTask task, Func<uint, NativeBattleModuleResource> readResource, uint? groundResource, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return null;
        }
        bool flat = groundResource is not null;
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.Identity8DD770();
        if (flat)
        {
            rotation = NativeBattleCuragaMatrices.RotateX8DD7E0(rotation, 1024);
        }
        else
        {
            rotation = NativeBattleCuragaMatrices.RotateX8DD7E0(NativeBattleCuragaMatrices.RotateY8DD8A0(rotation, Word(task, 0x42)), Word(task, 0x40));
            rotation = Scale(rotation, task);
        }
        NativeBattleModuleResource resource = readResource(groundResource ?? task.ReadDword(0x4C));
        int phase = flat ? 0 : Word(task, 0x50);
        return new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.SpriteFrame(phase), Anchor = new(Word(task, 0x1C), flat ? (short)0 : Word(task, 0x1E), Word(task, 0x20), 0), Phase = unchecked((ushort)phase), Scale = 4096, Direction = 4096, SpriteFlags = 4, SpriteColor = task.ReadDword(0x48), PilotIdentity = pilotIdentity, SpriteWorldRotation = rotation.Rotation };
    }

    public static NativeBattleModuleDraw? Billboard(NativeBattleModulePoolTask task, Func<uint, NativeBattleModuleResource> readResource, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return null;
        }
        NativeBattleModuleResource resource = readResource(task.ReadDword(0x4C));
        int phase = Word(task, 0x50);
        return new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = resource.SpriteFrame(phase), Anchor = new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20), 0), Phase = unchecked((ushort)phase), Scale = 4096, Direction = 4096, DepthOffset = Word(task, 0x54), SpriteFlags = 0, PilotIdentity = pilotIdentity, SpriteColor = 0x808080 };
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleActionMatrix Scale(NativeBattleActionMatrix matrix, NativeBattleModulePoolTask task) => NativeBattlePoseMatrices.Scale56BEF0(matrix, unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38)));
}
