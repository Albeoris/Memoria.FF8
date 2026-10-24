using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRenzokukenSpecialSpriteDraw(NativeBattleModulePacketBatch Packets) : NativeBattleRenzokuken160Operation;

public static class NativeBattleRenzokukenSpecialSpriteDrawing
{
    public static NativeBattleRenzokuken160SpritePackets Draw64DD70(NativeBattleRenzokuken160Sprite sprite, NativeBattleActionMatrix transform, NativeBattleCameraProjection projection, short cameraRoll)
    {
        NativeBattleModuleResource resource = new(0, sprite.Resource.Address, 0, 0, sprite.Resource.Bytes);
        NativeBattleModuleSpriteFrame selected = resource.ProjectedFrame571C80(sprite.Frame);
        NativeBattleFullMacVertex camera = NativeBattleGfMatrices.TransformMac460840(transform, sprite.Position);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(camera.X, camera.Y, camera.Z);
        int offset = -(sprite.Scale >> 3);
        int x = unchecked(camera.X + (offset * direction.X >> 12));
        int y = unchecked(camera.Y + (offset * direction.Y >> 12));
        int z = unchecked(camera.Z + (offset * direction.Z >> 12));
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Resource = resource,
            SpriteFrame = selected,
            Phase = sprite.Frame,
            Anchor = new(0, 0, 0, 0),
            Scale = sprite.Scale,
            SpriteFlags = sprite.Flags,
            SpriteColor = sprite.Color ?? 0x808080
        };
        NativeBattleActionMatrix main = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), cameraRoll) with { X = x, Y = y, Z = z };
        return new(NativeBattleModulePackets.EmitSprite571DC0(draw, projection, cameraRoll, 17, 2, main), selected.NextOffset < 0);
    }
}
