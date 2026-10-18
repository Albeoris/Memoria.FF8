namespace Memoria.FF8.Runtime;

/// <summary>Bio's billboard and ordered world-matrix sprite submissions.</summary>
public static class NativeBattleBio22Sprites
{
    public static NativeBattleModulePacketBatch Emit89BD90(NativeBattleModulePoolTask task, short scale, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleModuleResource resource)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return new([], 0);
        }
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Resource = resource,
            SpriteFrame = resource.SpriteFrame(task.ReadWord(0x50)),
            Phase = task.ReadWord(0x50),
            Anchor = new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20), 0),
            Scale = scale,
            DepthOffset = Word(task, 0x54),
            SpriteFlags = 0
        };
        return NativeBattleModulePackets.EmitSprite571DC0(draw, projection, cameraRoll, 17, 2);
    }

    public static NativeBattleGfGeometryBatch Emit89BF80(NativeBattleModulePoolTask task, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleModuleResource resource)
    {
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return new([], 0);
        }
        NativeBattleActionMatrix world = NativeBattleCuragaMatrices.Identity8DD770();
        world = NativeBattleCuragaMatrices.RotateY8DD8A0(world, Word(task, 0x42));
        world = NativeBattleCuragaMatrices.RotateZ8DD960(world, Word(task, 0x44));
        world = NativeBattleCuragaMatrices.RotateX8DD7E0(world, Word(task, 0x40));
        world = NativeBattlePoseMatrices.Scale56BEF0(world, unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38))) with { X = Word(task, 0x1C), Y = Word(task, 0x1E), Z = Word(task, 0x20) };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(projection.View ?? throw new NotSupportedException("Native Bio planar sprites require the current camera matrix."), world);
        return NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource.Bytes, task.ReadWord(0x50), matrix, projection, cameraRoll, 17, 2);
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
