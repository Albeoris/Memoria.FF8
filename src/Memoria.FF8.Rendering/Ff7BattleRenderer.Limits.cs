using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private Ff7BattleBillboardRenderer? beatRushHit;
    private PcPolygonModel? bigShotCharge;
    private PcTexture[]? bigShotChargeTextures;
    private Ff7BattleBillboardRenderer? bigShotInflow;
    private Ff7BattleBillboardRenderer? bigShotFragments;
    private Ff7BattleBillboardRenderer? bigShotFadingFragments;
    private Ff7BattleBillboardRenderer? bigShotBurst;

    private void DrawBigShot(ITriangleRenderer renderer, Ff7BattleBigShotState effect, CameraView view)
    {
        if (effect.Charge is { } charge)
        {
            bigShotCharge ??= PcPolygonModel.Read(Read(MagicArchive(), "hs.p"));
            bigShotChargeTextures ??= [PcTexture.Read(Read(MagicArchive(), "hvst.tex"))];
            Matrix4x4 weapon = new(charge.XAxis.X / 4096f, charge.XAxis.Y / 4096f, charge.XAxis.Z / 4096f, 0, charge.YAxis.X / 4096f, charge.YAxis.Y / 4096f, charge.YAxis.Z / 4096f, 0, charge.ZAxis.X / 4096f, charge.ZAxis.Y / 4096f, charge.ZAxis.Z / 4096f, 0, charge.Origin.X, charge.Origin.Y, charge.Origin.Z, 1);
            float scale = (512 + effect.Frame * 3584 / 22) / 4096f;
            Matrix4x4 first = BattleRigidPose.Rotation(new(0, 0, effect.ChargeRotation)) * Matrix4x4.CreateScale(scale) * weapon;
            Matrix4x4 second = BattleRigidPose.Rotation(new(0, 0, effect.CounterRotation)) * Matrix4x4.CreateScale(scale * 1.125f) * weapon;
            DrawModel(renderer, bigShotCharge, bigShotChargeTextures, first, view, new() { Cull = true });
            DrawModel(renderer, bigShotCharge, bigShotChargeTextures, second, view, new() { Cull = true });
        }

        foreach (Ff7BattleBigShotParticle particle in effect.Particles ?? [])
        {
            int age = effect.Frame - particle.StartedAt;
            float brightness = particle.Kind == Ff7BattleBigShotParticleKind.Charge ? Math.Min(age * 32, 128) : age < 14 ? 128 : 128 - (age - 14) * 16;
            brightness = brightness >= 128 ? 1 : brightness * 2 / 255;
            Vector3 anchor = view.Transform(particle.Position.Position);
            if (particle.Kind == Ff7BattleBigShotParticleKind.Charge)
            {
                bigShotInflow ??= LoadBigShotParticles(false, TriangleBlend.Add);
                bigShotInflow.Draw(renderer, particle.Variant, OffsetTowardCamera(anchor, 2000), view.Width, view.Height, view.Focal, brightness: brightness);
            }
            else if (particle.Kind == Ff7BattleBigShotParticleKind.Burst)
            {
                bigShotBurst ??= LoadBigShotParticles(false, TriangleBlend.Add);
                bigShotBurst.Draw(renderer, particle.Variant, anchor, view.Width, view.Height, view.Focal, particle.Scale / 4096f, brightness: brightness);
            }
            else
            {
                Ff7BattleBillboardRenderer fragments;
                if (age < 14)
                {
                    fragments = bigShotFragments ??= LoadBigShotParticles(true, TriangleBlend.Replace);
                }
                else
                {
                    fragments = bigShotFadingFragments ??= LoadBigShotParticles(true, TriangleBlend.Add);
                }
                Matrix4x4 rotation = BattleRigidPose.Rotation(particle.Rotation);
                fragments.Draw(renderer, particle.Variant, anchor, view.Width, view.Height, view.Focal, particle.Scale / 4096f, brightness: brightness, transform: point => view.Transform(Vector3.Transform(point, rotation) + particle.Position.Position));
            }
        }
    }

    private Ff7BattleBillboardRenderer LoadBigShotParticles(bool fragment, TriangleBlend blend)
    {
        IReadOnlyList<BattleBillboardQuad>[] frames = new IReadOnlyList<BattleBillboardQuad>[3];
        int[] sizes = [32, 24, 16];
        short[] v = [0, 32, 56];
        short[] u = fragment ? [160, 152, 144] : [128, 128, 128];
        for (int index = 0; index < frames.Length; index++)
        {
            frames[index] = [new(0, 0, (short)(-sizes[index] / 2), (short)(-sizes[index] / 2), u[index], v[index], 0x2C, fragment ? (ushort)0x7848 : (ushort)0x7808, sizes[index], sizes[index])];
        }
        PcTexture texture = PcTexture.Read(Read(MagicArchive(), "hvshot.tex"));
        return new(frames, [texture], _ => 0, blend);
    }

    private void DrawBeatRush(ITriangleRenderer renderer, Ff7BattleBeatRushState effect, CameraView view)
    {
        if (effect.Sprites is { Count: > 0 } sprites)
        {
            if (beatRushHit is null)
            {
                IReadOnlyList<IReadOnlyList<BattleBillboardQuad>> frames = BattleBillboard.Read(Read(MagicArchive(), "ff7/data/battle/limit2/tifa1/hit_1.s"));
                PcTexture texture = PcTexture.Read(Read(MagicArchive(), "ff7/data/battle/limit2/tifa1/hit_1.tex"));
                if (frames.Count != 8)
                {
                    throw new InvalidDataException("FFVII Beat Rush has an unexpected native hit sprite frame count.");
                }
                beatRushHit = new(frames, [texture], _ => 0, TriangleBlend.Add, paletteOverride: 2);
            }
            foreach (Ff7BattleBeatRushSprite sprite in sprites)
            {
                Vector3 anchor = OffsetTowardCamera(view.Transform(sprite.Anchor.Position), sprite.Scale >> 3);
                beatRushHit.Draw(renderer, effect.Frame - sprite.StartedAt, anchor, view.Width, view.Height, view.Focal, sprite.Scale / 4096f);
            }
        }

        TriangleMaterial material = new(null, TriangleBlend.Add, TriangleDepthComparison.GreaterEqual, false);
        foreach (Ff7BattleBeatRushTrail trail in effect.Trails ?? [])
        {
            int segments = trail.Samples.Count - 1;
            for (int index = 0; index < segments; index++)
            {
                Ff7BattleBeatRushSample previous = trail.Samples[index];
                Ff7BattleBeatRushSample current = trail.Samples[index + 1];
                Vector4 previousOuter = Color(0x201010, 0x060000, index, segments);
                Vector4 currentOuter = Color(0x201010, 0x060000, index + 1, segments);
                Vector4 previousInner = Color(0x785858, 0x280202, index, segments);
                Vector4 currentInner = Color(0x785858, 0x280202, index + 1, segments);
                DrawQuad(previous.Left, previous.Center, current.Left, current.Center, previousOuter, previousInner, currentOuter, currentInner);
                DrawQuad(previous.Center, previous.Right, current.Center, current.Right, previousInner, previousOuter, currentInner, currentOuter);
            }
        }

        void DrawQuad(Ff7BattleBraverPoint a, Ff7BattleBraverPoint b, Ff7BattleBraverPoint c, Ff7BattleBraverPoint d, Vector4 ca, Vector4 cb, Vector4 cc, Vector4 cd)
        {
            CameraSpaceVertex va = new(view.Transform(a.Position), Vector2.Zero, ca);
            CameraSpaceVertex vb = new(view.Transform(b.Position), Vector2.Zero, cb);
            CameraSpaceVertex vc = new(view.Transform(c.Position), Vector2.Zero, cc);
            CameraSpaceVertex vd = new(view.Transform(d.Position), Vector2.Zero, cd);
            CameraSpaceTriangleSubmission.Draw(renderer, va, vb, vc, view.Width, view.Height, view.Focal, material);
            CameraSpaceTriangleSubmission.Draw(renderer, vb, vd, vc, view.Width, view.Height, view.Focal, material);
        }

        static Vector4 Color(int start, int end, int index, int segments)
        {
            int fraction = 4096 / segments * index;
            Vector4 first = new((start & 255) / 255f, (start >> 8 & 255) / 255f, (start >> 16 & 255) / 255f, 1);
            Vector4 last = new((end & 255) / 255f, (end >> 8 & 255) / 255f, (end >> 16 & 255) / 255f, 1);
            Vector4 color = Vector4.Lerp(last, first, fraction / 4096f) * 255;
            return new(MathF.Round(color.X) / 255, MathF.Round(color.Y) / 255, MathF.Round(color.Z) / 255, 1);
        }
    }
}
