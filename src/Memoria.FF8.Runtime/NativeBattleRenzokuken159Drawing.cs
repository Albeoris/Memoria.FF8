namespace Memoria.FF8.Runtime;

public static class NativeBattleRenzokuken159Drawing
{
    public static NativeBattleRenzokuken160SpritePackets DrawSprite571C80(NativeBattleRenzokuken159Sprite sprite, NativeBattleCameraProjection projection, NativeBattleActionMatrix view, short cameraRoll)
    {
        return NativeBattleRenzokuken160Drawing.DrawSprite571C80(new(sprite.Resource, sprite.Frame, sprite.Position, sprite.Scale, sprite.Color, sprite.Flags), projection, view, cameraRoll);
    }
}
