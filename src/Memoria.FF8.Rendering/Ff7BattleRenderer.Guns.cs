using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private PcPolygonModel? gunMuzzle;
    private PcPolygonModel? gunCasing;
    private Ff7BattleBillboardRenderer? gunSpark;
    private Ff7BattleBillboardRenderer? gunDust;

    private void DrawSecondaryEffects(ITriangleRenderer renderer, Ff7BattleSecondaryState state, CameraView view)
    {
        foreach (Ff7BattleSecondaryDraw draw in state.DrawOrder)
        {
            switch (draw.Kind)
            {
                case Ff7BattleSecondaryDrawKind.LimitPreparation:
                    DrawLimitPreparation(renderer, state.LimitPreparations[draw.Index], view);
                    break;
                case Ff7BattleSecondaryDrawKind.ScreenPulse:
                    Ff7BattleActorScreenPulse pulse = state.ScreenPulses[draw.Index];
                    DrawScreenPulse(renderer, pulse.Red, pulse.Green, pulse.Blue, view);
                    break;
                case Ff7BattleSecondaryDrawKind.Muzzle:
                    DrawMuzzle(state.Muzzles[draw.Index]);
                    break;
                case Ff7BattleSecondaryDrawKind.Casing:
                    DrawCasing(state.Casings[draw.Index]);
                    break;
                case Ff7BattleSecondaryDrawKind.Spark:
                    DrawSpark(state.Sparks[draw.Index]);
                    break;
                case Ff7BattleSecondaryDrawKind.Dust:
                    DrawDust(state.Dust[draw.Index]);
                    break;
                case Ff7BattleSecondaryDrawKind.Hitmark:
                    DrawHitmark(renderer, state.Hitmarks[draw.Index], view);
                    break;
                default:
                    throw new InvalidDataException("FFVII secondary packet has an unsupported draw kind.");
            }
        }

        void DrawMuzzle(Ff7BattleGunMuzzle muzzle)
        {
            gunMuzzle ??= PcPolygonModel.Read(Read(MagicArchive(), "ff7/data/battle/magic/mgun/jo_fir.p"));
            Matrix4x4 transform = new(muzzle.XAxis.X / 4096f, muzzle.XAxis.Y / 4096f, muzzle.XAxis.Z / 4096f, 0, muzzle.YAxis.X / 4096f, muzzle.YAxis.Y / 4096f, muzzle.YAxis.Z / 4096f, 0, muzzle.ZAxis.X / 4096f, muzzle.ZAxis.Y / 4096f, muzzle.ZAxis.Z / 4096f, 0, muzzle.Origin.X, muzzle.Origin.Y, muzzle.Origin.Z, 1);
            DrawModel(renderer, gunMuzzle, [], transform, view, new() { Cull = true });
        }
        void DrawCasing(Ff7BattleGunCasing casing)
        {
            gunCasing ??= PcPolygonModel.Read(Read(MagicArchive(), "jo_tam.p"));
            Matrix4x4 transform = BattleRigidPose.Rotation(new(0, casing.Yaw, 0)) * Matrix4x4.CreateTranslation(casing.Position.Position);
            DrawModel(renderer, gunCasing, [], transform, view, new() { Cull = true });
        }
        void DrawSpark(Ff7BattleGunSpark spark)
        {
            if (gunSpark is null)
            {
                PcTexture texture = cureTexture ?? throw new InvalidOperationException("FFVII ground spark presentation requires its original shared effect texture.");
                gunSpark = new([new BattleBillboardQuad[] { new(0, 0, 0, 0, 0, 192, 0x3A, 0x78C7, 64, 64) }], [texture], _ => 0, TriangleBlend.Add, paletteOverride: 3);
            }
            Matrix4x4 rotation = BattleRigidPose.Rotation(new(0, spark.Yaw, 0));
            gunSpark.Draw(renderer, 0, Vector3.Zero, view.Width, view.Height, view.Focal, transform: point => view.Transform(Vector3.Transform(new(point.X * 2000 / 512 - 1000, -2, point.Y * 2000 / 512 - 1000), rotation) + spark.Position.Position));
        }
        void DrawDust(Ff7BattleGunDust dust)
        {
            if (gunDust is null)
            {
                PcTexture texture = cureTexture ?? throw new InvalidOperationException("FFVII casing dust presentation requires its original shared effect texture.");
                IReadOnlyList<IReadOnlyList<BattleBillboardQuad>> frames = Enumerable.Range(0, 8).Select(frame => (IReadOnlyList<BattleBillboardQuad>)new BattleBillboardQuad[] { new(0, 0, -16, -32, (short)(frame * 32), 160, 0x3A, 0x7A07, 31, 31) }).ToArray();
                gunDust = new(frames, [texture], _ => 0, TriangleBlend.Add);
            }
            Vector3 anchor = OffsetTowardCamera(view.Transform(dust.Position.Position), -dust.MinimumScale / 16);
            gunDust.Draw(renderer, dust.Frame, anchor, view.Width, view.Height, view.Focal, dust.MinimumScale / 4096f);
        }
    }
}
