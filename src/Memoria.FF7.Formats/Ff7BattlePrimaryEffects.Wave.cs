using System.Numerics;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleWaveGlobals(Ff7BattleSearchScopeBasis? Basis = null, short CenterStrength = 16, short BeamStrength = 8, short OrbStrength = 8);
public sealed record Ff7BattlePrimaryPositionedSound(ushort Sound, int Actor, byte FallbackPan, int Channel = 1, bool RawPan = false);
public sealed record Ff7BattlePrimaryWaveController(int Target, Ff7BattleBraverPoint Source, Ff7BattleBraverPoint Destination, short Length) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryWaveOptionalFlash : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryWaveBeam(Ff7BattleBraverPoint Source, short LengthScale) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryWaveOrb(Ff7BattleBraverPoint Source, short Length) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryWaveCenter(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryWaveRing(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryWaveExplosion(Ff7BattleBraverPoint Position) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryWaveFragment(Ff7BattleBraverPoint Position, Ff7BattleBraverPoint Velocity, int Frame, short Roll = 0) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryWaveMeshDraw(int Model, Ff7BattleBraverPoint Position, Ff7BattleSearchScopeBasis Basis, byte Alpha) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimaryWaveSpriteDraw(int Resource, Ff7BattleBraverPoint Position, int Frame, int Scale, byte Brightness, short Roll = 0) : Ff7BattlePrimaryDraw;

public static partial class Ff7BattlePrimaryEffects
{
    private static readonly Ff7BattleSearchScopeBasis WaveTurn = Ff7BattleEffectMatrix.RotateXyz(new(-1024, 0, 0));
    private static readonly Ff7BattleSearchScopeBasis WaveIdentity = Ff7BattleEffectMatrix.RotateXyz(default);

    public static Ff7BattlePrimaryState ScheduleWave(Ff7BattlePrimaryState state, int source, ushort targets, int owner = 0)
    {
        ValidateTargets(targets);
        return ScheduleGroup(state with { WaveGlobals = new(state.WaveGlobals.Basis) }, new(Ff7BattlePrimaryFamily.Wave, targets, 4, Source: source) { Owner = owner });
    }

    private static void InitializeWaveTarget(int source, int target, int owner, PrimaryPass pass, WavePass wave)
    {
        Ff7BattlePrimaryContext context = pass.Context ?? throw new InvalidOperationException("FFVII Wave requires its original actor owners.");
        Func<int, int, Vector3> joint = context.JointPoint ?? throw new InvalidOperationException("FFVII Wave requires original model joint selectors.");
        Ff7BattleBraverPoint origin = Ff7BattleBraverPoint.At(joint(source, 9));
        Ff7BattleBraverPoint destination = Ff7BattleBraverPoint.At(joint(target, 0));
        Ff7BattleSearchScopeBasis basis = Ff7BattleSearchScopeEffect.CreateState((ushort)(1 << target), origin.Position, destination.Position, 0).Basis;
        wave.Globals = wave.Globals with { Basis = basis };
        pass.Allocate(new Ff7BattlePrimaryWaveController(target, origin, destination, Length(origin, destination)) { Owner = owner });
        pass.StageRed = 0;
        pass.StageGreen = 0;
        pass.StageBlue = 0;
        pass.FadeTo(3072, 10);
        pass.PositionedSounds.Add(new(0xFA, source, 0x40));
    }

    private static Ff7BattlePrimaryCallback? ExecuteWave(Ff7BattlePrimaryCallback callback, PrimaryPass pass, WavePass wave)
    {
        switch (callback)
        {
            case Ff7BattlePrimaryWaveController controller:
                if (controller.Age == 0)
                {
                    pass.Allocate(new Ff7BattlePrimaryWaveOptionalFlash { Owner = controller.Owner });
                    pass.Allocate(new Ff7BattlePrimaryWaveBeam(controller.Source, (short)(controller.Length * 0x1200 / 3000)) { Owner = controller.Owner });
                    pass.Allocate(new Ff7BattlePrimaryWaveOrb(controller.Source, controller.Length) { Owner = controller.Owner });
                }
                if (controller.Age is >= 7 and < 14)
                {
                    Func<int> random = WaveRandom(pass);
                    for (int spawn = 0; spawn < 2; spawn++)
                    {
                        Ff7BattleBraverPoint target = controller.Destination;
                        Ff7BattleBraverPoint point = new((short)(target.X + random() % 1200 - 600), (short)(target.Y + random() % 1200 - 600), (short)(target.Z + random() % 1200 - 600));
                        pass.Allocate(new Ff7BattlePrimaryWaveExplosion(point) { Owner = controller.Owner });
                    }
                }
                if (controller.Age == 10)
                {
                    Ff7BattleBraverPoint center = controller.Destination with { Y = 0 };
                    pass.Allocate(new Ff7BattlePrimaryWaveCenter(center) { Owner = controller.Owner });
                    pass.Allocate(new Ff7BattlePrimaryWaveRing(center with { Y = -300 }) { Owner = controller.Owner });
                    Func<int> random = WaveRandom(pass);
                    for (int spawn = 0; spawn < 20; spawn++)
                    {
                        Ff7BattleBraverPoint lift = new(0, (short)(-350 + 2 * (random() % 100)), 0);
                        BattleRotationWords rotation = new((short)(random() % 800 - 400), 0, (short)(random() % 800 - 400));
                        Ff7BattleBraverPoint velocity = Ff7BattleEffectMatrix.Transform(lift, Ff7BattleEffectMatrix.RotateXyz(rotation));
                        pass.Allocate(new Ff7BattlePrimaryWaveFragment(center, velocity, random() % 7) { Owner = controller.Owner });
                    }
                    pass.PositionedSounds.Add(new(0x176, controller.Target, 0x40));
                }
                if (controller.Age == 35)
                {
                    pass.Contact(controller.Target);
                }
                if (controller.Age == 44)
                {
                    pass.FadeTo(0, 10);
                    return null;
                }
                return controller with { Age = controller.Age + 1 };
            case Ff7BattlePrimaryWaveOptionalFlash flash:
                return flash.Age == 9 ? null : flash with { Age = flash.Age + 1 };
            case Ff7BattlePrimaryWaveBeam beam:
                Ff7BattleSearchScopeBasis aimed = Ff7BattleEffectMatrix.Compose(WaveTurn, wave.Basis);
                int width = 0x2000 - (beam.Age << 9);
                Ff7BattleSearchScopeBasis stretched = Ff7BattleEffectMatrix.Scale(aimed, width, beam.LengthScale * beam.Age / 6, width);
                byte beamAlpha = Ff7BattleStageFade.Alpha((short)(4096 - (wave.Globals.BeamStrength << 8)));
                pass.Emit(new Ff7BattlePrimaryWaveMeshDraw(1, beam.Source, stretched, beamAlpha));
                pass.Emit(new Ff7BattlePrimaryWaveMeshDraw(2, beam.Source, stretched, beamAlpha));
                if (beam.Age > 6)
                {
                    wave.Globals = wave.Globals with { BeamStrength = (short)(wave.Globals.BeamStrength - 2) };
                }
                return beam.Age == 9 ? null : beam with { Age = beam.Age + 1 };
            case Ff7BattlePrimaryWaveOrb orb:
                Ff7BattleBraverPoint offset = Ff7BattleEffectMatrix.Transform(new(0, 0, (short)(orb.Length * orb.Age / 6)), wave.Basis);
                Ff7BattleBraverPoint orbPosition = Ff7BattleEffectMatrix.Add(orb.Source, offset);
                Ff7BattleSearchScopeBasis orbBasis = Ff7BattleEffectMatrix.Scale(Ff7BattleEffectMatrix.Compose(WaveTurn, wave.Basis), 8192, 8192, 8192);
                byte orbAlpha = ExpandEffectBrightness(wave.Globals.OrbStrength << 4);
                for (int model = 3; model <= 5; model++)
                {
                    pass.Emit(new Ff7BattlePrimaryWaveMeshDraw(model, orbPosition, orbBasis, orbAlpha));
                }
                if (orb.Age > 2)
                {
                    wave.Globals = wave.Globals with { OrbStrength = (short)(wave.Globals.OrbStrength - 1) };
                }
                return orb.Age == 9 ? null : orb with { Age = orb.Age + 1 };
            case Ff7BattlePrimaryWaveCenter center:
                int scale = 4096 + center.Age * 300;
                short spin = (short)(center.Age << 6);
                Ff7BattleSearchScopeBasis outer = Ff7BattleEffectMatrix.Scale(Ff7BattleEffectMatrix.RotateXyz(new(0, spin, 0)), scale, scale, scale);
                byte centerAlpha = ExpandEffectBrightness(wave.Globals.CenterStrength << 3);
                pass.Emit(new Ff7BattlePrimaryWaveMeshDraw(7, center.Position, outer, centerAlpha));
                pass.Emit(new Ff7BattlePrimaryWaveMeshDraw(8, center.Position, outer, Ff7BattleStageFade.Alpha((short)(4096 - (wave.Globals.CenterStrength << 9)))));
                int innerScale = 2048 + center.Age * 280;
                Ff7BattleSearchScopeBasis inner = Ff7BattleEffectMatrix.Scale(Ff7BattleEffectMatrix.RotateXyz(new(0, (short)-spin, 0)), innerScale, innerScale, innerScale);
                pass.Emit(new Ff7BattlePrimaryWaveMeshDraw(7, center.Position, inner, centerAlpha));
                if (center.Age < 22)
                {
                    Func<int> random = WaveRandom(pass);
                    int radius = center.Age * 80;
                    for (int spawn = 0; spawn < 6; spawn++)
                    {
                        int angle = random() & 4095;
                        short x = (short)(center.Position.X + (WaveCosine(angle) * radius >> 12));
                        short z = (short)(center.Position.Z + (Ff7BattleSearchScopeEffect.Sine(angle) * radius >> 12));
                        wave.Sparks.Add(new(x, -150, z));
                    }
                }
                pass.CameraFocusYDelta = (short)(pass.CameraFocusYDelta + ((center.Age & 1) << 4) - 8);
                if (center.Age > 14)
                {
                    wave.Globals = wave.Globals with { CenterStrength = (short)(wave.Globals.CenterStrength - 1) };
                }
                return center.Age == 29 ? null : center with { Age = center.Age + 1 };
            case Ff7BattlePrimaryWaveRing ring:
                int ringScale = ring.Age * 1500;
                Ff7BattleSearchScopeBasis ringBasis = Ff7BattleEffectMatrix.Scale(WaveIdentity, ringScale, ringScale, ringScale);
                pass.Emit(new Ff7BattlePrimaryWaveMeshDraw(6, ring.Position, ringBasis, Ff7BattleStageFade.Alpha((short)(ring.Age << 8))));
                return ring.Age == 14 ? null : ring with { Age = ring.Age + 1 };
            case Ff7BattlePrimaryWaveExplosion explosion:
                pass.Emit(new Ff7BattlePrimaryWaveSpriteDraw(13, explosion.Position, explosion.Age, 8192, 255));
                return explosion.Age == 15 ? null : explosion with { Age = explosion.Age + 1 };
            case Ff7BattlePrimaryWaveFragment fragment:
                int brightness = fragment.Age < 22 ? 8 : 30 - fragment.Age;
                pass.Emit(new Ff7BattlePrimaryWaveSpriteDraw(brightness == 8 ? 11 : 12, fragment.Position, fragment.Frame, 8192, ExpandEffectBrightness(brightness << 4), fragment.Roll));
                Ff7BattleBraverPoint moved = Ff7BattleEffectMatrix.Add(fragment.Position, fragment.Velocity);
                short vy = fragment.Velocity.Y;
                if (moved.Y > 0)
                {
                    moved = moved with { Y = 0 };
                    vy = (short)-vy;
                }
                return fragment.Age == 29 ? null : fragment with { Age = fragment.Age + 1, Position = moved, Velocity = fragment.Velocity with { Y = (short)(vy + 20) }, Roll = (short)(fragment.Roll + 512) };
            default:
                throw new NotSupportedException("FFVII Wave callback has no admitted executor.");
        }
    }

    private static Func<int> WaveRandom(PrimaryPass pass)
    {
        return pass.Context?.NextCrt ?? throw new InvalidOperationException("FFVII Wave requires its original random owner.");
    }

    private static byte ExpandEffectBrightness(int value)
    {
        return value >= 128 ? (byte)255 : (byte)(value << 1);
    }

    private static int WaveCosine(int angle)
    {
        return (int)(Math.Cos(angle * 0.087890625f * (Math.PI / 180)) * 4096);
    }

    private sealed class WavePass(Ff7BattleWaveGlobals globals)
    {
        public Ff7BattleWaveGlobals Globals = globals;
        public readonly List<Ff7BattleBraverPoint> Sparks = [];
        public Ff7BattleSearchScopeBasis Basis => Globals.Basis ?? throw new InvalidDataException("FFVII Wave has no original shared aiming matrix.");
    }
}
