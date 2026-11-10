using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private Ff7BattleBillboardRenderer[]? destroyed;

    private void DrawDestroyed(ITriangleRenderer renderer, Ff7BattleDestroyedState effect, CameraView view)
    {
        if (effect.ScreenBlue != 0)
        {
            Vector4 color = new(0, 0, effect.ScreenBlue / 255f, 1);
            TriangleVertex a = new(new(0, 0), 0.1f, 1, Vector2.Zero, color);
            TriangleVertex b = new(new(view.Width, 0), 0.1f, 1, Vector2.Zero, color);
            TriangleVertex c = new(new(0, view.Height), 0.1f, 1, Vector2.Zero, color);
            TriangleVertex d = new(new(view.Width, view.Height), 0.1f, 1, Vector2.Zero, color);
            TriangleMaterial pulse = new(null, TriangleBlend.Add, TriangleDepthComparison.Always, false);
            renderer.Draw(a, b, c, pulse);
            renderer.Draw(b, d, c, pulse);
        }
        destroyed ??=
        [
            LoadBillboard("biribiri.s", ["biribiri.tex"], Ff7BattleDestroyedEffect.SpriteFrameCount, _ => 0),
            LoadBillboard("b_flip.s", ["biribiri.tex"], Ff7BattleDestroyedEffect.SpriteFrameCount, _ => 0)
        ];
        foreach (Ff7BattleDestroyedSprite sprite in effect.Sprites ?? [])
        {
            destroyed[sprite.Variant].Draw(renderer, effect.Frame - sprite.StartedAt, view.Transform(sprite.Position.Position), view.Width, view.Height, view.Focal, sprite.Scale / 4096f);
        }
    }
}
