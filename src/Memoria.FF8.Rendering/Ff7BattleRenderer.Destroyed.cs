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
            DrawDestroyedPulse(renderer, effect.ScreenBlue, view);
        }
        foreach (Ff7BattleDestroyedSprite sprite in effect.Sprites ?? [])
        {
            DrawDestroyedSprite(renderer, sprite, effect.Frame - sprite.StartedAt, view);
        }
    }

    private static void DrawDestroyedPulse(ITriangleRenderer renderer, byte blue, CameraView view)
    {
        DrawScreenPulse(renderer, 0, 0, blue, view);
    }

    private static void DrawScreenPulse(ITriangleRenderer renderer, byte red, byte green, byte blue, CameraView view)
    {
        Vector4 color = new(red / 255f, green / 255f, blue / 255f, 1);
        TriangleVertex a = new(new(0, 0), 0.1f, 1, Vector2.Zero, color);
        TriangleVertex b = new(new(view.Width, 0), 0.1f, 1, Vector2.Zero, color);
        TriangleVertex c = new(new(0, view.Height), 0.1f, 1, Vector2.Zero, color);
        TriangleVertex d = new(new(view.Width, view.Height), 0.1f, 1, Vector2.Zero, color);
        TriangleMaterial pulse = new(null, TriangleBlend.Add, TriangleDepthComparison.Always, false);
        renderer.Draw(a, b, c, pulse);
        renderer.Draw(b, d, c, pulse);
    }

    private void DrawDestroyedSprite(ITriangleRenderer renderer, Ff7BattleDestroyedSprite sprite, int frame, CameraView view)
    {
        destroyed ??=
        [
            LoadBillboard("biribiri.s", ["biribiri.tex"], Ff7BattleDestroyedEffect.SpriteFrameCount, _ => 0),
            LoadBillboard("b_flip.s", ["biribiri.tex"], Ff7BattleDestroyedEffect.SpriteFrameCount, _ => 0)
        ];
        destroyed[sprite.Variant].Draw(renderer, frame, view.Transform(sprite.Position.Position), view.Width, view.Height, view.Focal, sprite.Scale / 4096f);
    }
}
