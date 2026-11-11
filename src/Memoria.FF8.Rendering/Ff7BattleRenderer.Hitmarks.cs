using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private readonly Dictionary<(int Sprite, int Palette), Ff7BattleBillboardRenderer> hitmarks = [];

    private void DrawHitmark(ITriangleRenderer renderer, Ff7BattleHitmarkState state, CameraView view)
    {
        if (state.Completed || state.Anchor is not { } point)
        {
            return;
        }
        foreach (Ff7BattleHitmarkCue cue in state.Definition.Cues)
        {
            int frame = state.Frame - cue.Frame;
            if (frame < 0 || frame >= cue.FrameCount)
            {
                continue;
            }
            if (!hitmarks.TryGetValue((cue.Sprite, cue.Palette), out Ff7BattleBillboardRenderer? sprites))
            {
                PcTexture texture = grenadeTexture ?? throw new InvalidOperationException("FFVII hitmark presentation requires its original battle effect texture.");
                int source = cue.Sprite == 8 ? 5 : cue.Sprite;
                IReadOnlyList<IReadOnlyList<BattleBillboardQuad>> frames = Ff7BattleHitmarkResources.ReadSprite(MagicArchive(), source);
                if (frames.Count != cue.FrameCount)
                {
                    throw new InvalidDataException("FFVII hitmark continuation has a different native frame count.");
                }
                sprites = new(frames, [texture], _ => 0, paletteOverride: cue.Palette, useColorKey: true);
                hitmarks.Add((cue.Sprite, cue.Palette), sprites);
            }
            Vector3 anchor = SpriteAnchor(point.X, point.Y, point.Z, state.CollisionRadius, view) + new Vector3(state.Flipped ? -cue.OffsetX : cue.OffsetX, -cue.OffsetY, 0);
            float scale = cue.Scale / 4096f;
            if (cue.Sprite == 8)
            {
                Matrix4x4 rotation = BattleRigidPose.Rotation(new(0, 0, 512));
                sprites.Draw(renderer, frame, anchor, view.Width, view.Height, view.Focal, scale, state.Flipped ? 3 : 0, transform: local =>
                {
                    Vector3 rotated = Vector3.Transform(local, rotation);
                    return anchor + new Vector3(rotated.X, -rotated.Y, 0);
                });
            }
            else
            {
                sprites.Draw(renderer, frame, anchor, view.Width, view.Height, view.Focal, scale, state.Flipped ? 3 : 0);
            }
        }
    }
}
