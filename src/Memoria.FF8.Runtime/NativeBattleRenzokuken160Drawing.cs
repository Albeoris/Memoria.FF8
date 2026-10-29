namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRenzokuken160SpritePackets(NativeBattleModulePacketBatch Packets, bool AnimationEnded);

public static class NativeBattleRenzokuken160Drawing
{
    public static NativeBattleRenzokuken160SpritePackets DrawSprite571C80(NativeBattleRenzokuken160Sprite sprite, NativeBattleCameraProjection projection, NativeBattleActionMatrix view, short cameraRoll, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleModuleResource resource = new(0, sprite.Resource.Address, 0, 0, sprite.Resource.Bytes);
        NativeBattleModuleSpriteFrame frame = resource.ProjectedFrame571C80(sprite.Frame);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            PilotIdentity = pilotIdentity,
            Resource = resource,
            SpriteFrame = frame,
            Phase = sprite.Frame,
            Anchor = new(sprite.Position.X, sprite.Position.Y, sprite.Position.Z, 0),
            Scale = sprite.Scale,
            DepthOffset = -(sprite.Scale >> 4),
            SpriteFlags = sprite.Flags,
            SpriteColor = sprite.Color ?? 0x808080
        };
        return new(NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = view }, cameraRoll, 17, 2), frame.NextOffset < 0);
    }
}
