using System.Numerics;
using System.Text.Json.Serialization;

namespace Memoria.FF7.Formats;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Callback")]
[JsonDerivedType(typeof(Ff7BattlePrimaryTargetReaction), "TargetReaction")]
[JsonDerivedType(typeof(Ff7BattlePrimaryActionTitle), "ActionTitle")]
[JsonDerivedType(typeof(Ff7BattlePrimaryLaserCannonController), "LaserCannonController")]
[JsonDerivedType(typeof(Ff7BattlePrimaryLaserCannonBeam), "LaserCannonBeamCallback")]
[JsonDerivedType(typeof(Ff7BattlePrimaryLaserCannonImpact), "LaserCannonImpactCallback")]
[JsonDerivedType(typeof(Ff7BattlePrimaryLaserCannonFlare), "LaserCannonFlare")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBigBomberController), "BigBomberController")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBigBomberMuzzle), "BigBomberMuzzle")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBigBomberDebris), "BigBomberDebris")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBigBomberSmoke), "BigBomberSmoke")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBigBomberWave), "BigBomberWave")]
[JsonDerivedType(typeof(Ff7BattleTailLaserStage), "TailLaserStage")]
[JsonDerivedType(typeof(Ff7BattleTailLaserSweep), "TailLaserSweep")]
[JsonDerivedType(typeof(Ff7BattleTailLaserBeam), "TailLaserBeam")]
[JsonDerivedType(typeof(Ff7BattleTailLaserFragment), "TailLaserFragment")]
[JsonDerivedType(typeof(Ff7BattleTailLaserImpact), "TailLaserImpact")]
[JsonDerivedType(typeof(Ff7BattleTailLaserBlast), "TailLaserBlast")]
[JsonDerivedType(typeof(Ff7BattleTailLaserSmoke), "TailLaserSmoke")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBeamGunController), "BeamGunController")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBeamGunFlash), "BeamGunFlash")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBeamGunShot), "BeamGunShot")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBeamGunBurst), "BeamGunBurst")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBubbleGroup), "BubbleGroup")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBubbleController), "BubbleController")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBubbleStream), "BubbleStream")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBubbleCloud), "BubbleCloud")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBubbleBurst), "BubbleBurst")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBubbleRibbon), "BubbleRibbon")]
[JsonDerivedType(typeof(Ff7BattlePrimaryStageFade), "StageFade")]
[JsonDerivedType(typeof(Ff7BattlePrimaryScopeController), "ScopeController")]
[JsonDerivedType(typeof(Ff7BattlePrimaryScopeEmitter), "ScopeEmitter")]
[JsonDerivedType(typeof(Ff7BattlePrimaryScopeSprite), "ScopeSprite")]
[JsonDerivedType(typeof(Ff7BattlePrimarySilkGroup), "SilkGroup")]
[JsonDerivedType(typeof(Ff7BattlePrimarySilkController), "SilkController")]
[JsonDerivedType(typeof(Ff7BattlePrimarySilkLines), "SilkLines")]
[JsonDerivedType(typeof(Ff7BattlePrimarySilkSprite), "SilkSprite")]
[JsonDerivedType(typeof(Ff7BattlePrimarySilkStrand), "SilkStrand")]
[JsonDerivedType(typeof(Ff7BattlePrimaryTargetGroup), "TargetGroup")]
[JsonDerivedType(typeof(Ff7BattlePrimaryCureController), "CureController")]
[JsonDerivedType(typeof(Ff7BattlePrimaryCureParticle), "CureParticle")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBoltController), "BoltController")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBoltSpark), "BoltSpark")]
[JsonDerivedType(typeof(Ff7BattlePrimaryDestroyedController), "DestroyedController")]
[JsonDerivedType(typeof(Ff7BattlePrimaryDestroyedSprite), "DestroyedSprite")]
[JsonDerivedType(typeof(Ff7BattlePrimaryFireController), "FireController")]
[JsonDerivedType(typeof(Ff7BattlePrimaryFireSprite), "FireSprite")]
[JsonDerivedType(typeof(Ff7BattlePrimaryIceInitializer), "IceInitializer")]
[JsonDerivedType(typeof(Ff7BattlePrimaryIceMesh), "IceMesh")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBraverGroup), "BraverGroup")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBraverController), "BraverController")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBraverShine), "BraverShine")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBraverTrail), "BraverTrail")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBraverImpact), "BraverImpact")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBeatRushController), "BeatRushController")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBeatRushTrail), "BeatRushTrail")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBeatRushSprite), "BeatRushSprite")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBigShotGroup), "BigShotGroup")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBigShotController), "BigShotController")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBigShotCharge), "BigShotCharge")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBigShotParticle), "BigShotParticle")]
[JsonDerivedType(typeof(Ff7BattlePrimarySmokeShotController), "SmokeShotController")]
[JsonDerivedType(typeof(Ff7BattlePrimarySmokeShotSprite), "SmokeShotSprite")]
[JsonDerivedType(typeof(Ff7BattlePrimarySmogController), "SmogController")]
[JsonDerivedType(typeof(Ff7BattlePrimarySmogCloud), "SmogCloud")]
[JsonDerivedType(typeof(Ff7BattlePrimarySmogSprite), "SmogSprite")]
[JsonDerivedType(typeof(Ff7BattlePrimaryDeathController), "ActorDeath")]
[JsonDerivedType(typeof(Ff7BattlePrimaryRocketController), "RocketController")]
[JsonDerivedType(typeof(Ff7BattlePrimaryRocketMissile), "RocketMissile")]
[JsonDerivedType(typeof(Ff7BattlePrimaryRocketExplosion), "RocketExplosion")]
[JsonDerivedType(typeof(Ff7BattlePrimaryWaveController), "WaveController")]
[JsonDerivedType(typeof(Ff7BattlePrimaryWaveOptionalFlash), "WaveOptionalFlash")]
[JsonDerivedType(typeof(Ff7BattlePrimaryWaveBeam), "WaveBeam")]
[JsonDerivedType(typeof(Ff7BattlePrimaryWaveOrb), "WaveOrb")]
[JsonDerivedType(typeof(Ff7BattlePrimaryWaveCenter), "WaveCenter")]
[JsonDerivedType(typeof(Ff7BattlePrimaryWaveRing), "WaveRing")]
[JsonDerivedType(typeof(Ff7BattlePrimaryWaveExplosion), "WaveExplosion")]
[JsonDerivedType(typeof(Ff7BattlePrimaryWaveFragment), "WaveFragment")]
[JsonDerivedType(typeof(Ff7BattlePrimaryEnergyBallController), "EnergyBallController")]
[JsonDerivedType(typeof(Ff7BattlePrimaryEnergyBallSourceLight), "EnergyBallSourceLight")]
[JsonDerivedType(typeof(Ff7BattlePrimaryEnergyBallCharge), "EnergyBallCharge")]
[JsonDerivedType(typeof(Ff7BattlePrimaryEnergyBallRing), "EnergyBallRing")]
[JsonDerivedType(typeof(Ff7BattlePrimaryEnergyBallArc), "EnergyBallArc")]
[JsonDerivedType(typeof(Ff7BattlePrimaryEnergyBallOrb), "EnergyBallOrb")]
[JsonDerivedType(typeof(Ff7BattlePrimaryEnergyBallBlast), "EnergyBallBlast")]
public abstract record Ff7BattlePrimaryCallback
{
    public int Owner { get; init; }
    public int Age { get; init; }
}

public sealed record Ff7BattlePrimaryStageFade(Ff7BattleStageFadeRamp Ramp) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryScopeController(ushort Targets, Ff7BattleBraverPoint Source, Ff7BattleSearchScopeBasis Basis, short Length) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryScopeEmitter(Ff7BattleBraverPoint Source, Ff7BattleSearchScopeBasis Basis, short Length, int Index) : Ff7BattlePrimaryCallback;
public sealed record Ff7BattlePrimaryScopeSprite(Ff7BattleBraverPoint Position, int Stage = 0) : Ff7BattlePrimaryCallback;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Packet")]
[JsonDerivedType(typeof(Ff7BattlePrimaryLaserCannonBeamDraw), "LaserCannonBeam")]
[JsonDerivedType(typeof(Ff7BattlePrimaryLaserCannonImpactDraw), "LaserCannonImpact")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBigBomberMeshDraw), "BigBomberMesh")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBigBomberSpriteDraw), "BigBomberSprite")]
[JsonDerivedType(typeof(Ff7BattleTailLaserMeshDraw), "TailLaserMesh")]
[JsonDerivedType(typeof(Ff7BattleTailLaserSpriteDraw), "TailLaserSprite")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBeamGunMeshDraw), "BeamGunMesh")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBeamGunSpriteDraw), "BeamGunSprite")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBubbleSpriteDraw), "BubbleSprite")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBubbleRibbonDraw), "BubbleRibbon")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBubbleCloudDraw), "BubbleCloud")]
[JsonDerivedType(typeof(Ff7BattlePrimaryScopeMeshDraw), "ScopeMesh")]
[JsonDerivedType(typeof(Ff7BattlePrimaryScopeSpriteDraw), "ScopeSprite")]
[JsonDerivedType(typeof(Ff7BattlePrimarySilkLineDraw), "SilkLine")]
[JsonDerivedType(typeof(Ff7BattlePrimarySilkSpriteDraw), "SilkSprite")]
[JsonDerivedType(typeof(Ff7BattlePrimaryCureDraw), "Cure")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBoltStrikeDraw), "BoltStrike")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBoltSparkDraw), "BoltSpark")]
[JsonDerivedType(typeof(Ff7BattlePrimaryDestroyedSpriteDraw), "DestroyedSprite")]
[JsonDerivedType(typeof(Ff7BattlePrimaryDestroyedPulseDraw), "DestroyedPulse")]
[JsonDerivedType(typeof(Ff7BattlePrimaryFireDraw), "Fire")]
[JsonDerivedType(typeof(Ff7BattlePrimaryIceDraw), "Ice")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBraverDraw), "Braver")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBeatRushDraw), "BeatRush")]
[JsonDerivedType(typeof(Ff7BattlePrimaryBigShotDraw), "BigShot")]
[JsonDerivedType(typeof(Ff7BattlePrimarySmokeShotDraw), "SmokeShot")]
[JsonDerivedType(typeof(Ff7BattlePrimarySmogSpriteDraw), "SmogSprite")]
[JsonDerivedType(typeof(Ff7BattlePrimarySmogCloudDraw), "SmogCloud")]
[JsonDerivedType(typeof(Ff7BattlePrimaryRocketDraw), "Rocket")]
[JsonDerivedType(typeof(Ff7BattlePrimaryWaveMeshDraw), "WaveMesh")]
[JsonDerivedType(typeof(Ff7BattlePrimaryWaveSpriteDraw), "WaveSprite")]
[JsonDerivedType(typeof(Ff7BattlePrimaryEnergyBallMeshDraw), "EnergyBallMesh")]
[JsonDerivedType(typeof(Ff7BattlePrimaryEnergyBallSpriteDraw), "EnergyBallSprite")]
public abstract record Ff7BattlePrimaryDraw
{
    public int Slot { get; init; }
    public int Owner { get; init; }
}

public sealed record Ff7BattlePrimaryScopeMeshDraw(Ff7BattleBraverPoint Source, Ff7BattleSearchScopeBasis Basis, Ff7BattleSearchScopeMesh Mesh) : Ff7BattlePrimaryDraw;
public sealed record Ff7BattlePrimaryScopeSpriteDraw(Ff7BattleBraverPoint Position, int Stage, int Frame) : Ff7BattlePrimaryDraw;

public sealed record Ff7BattlePrimaryContext(Func<int, int, Vector3, Vector3> ActorPoint, Func<int, BattleRotationWords> ActorRotation, Func<int, int> CollisionRadius, Func<int> NextCrt, Func<int, int>? BoneCount = null, Func<int, int, Vector3>? JointPoint = null, Func<int, int, int>? JointSelector = null, Func<int, Vector3, Vector3>? WeaponPoint = null, Func<int, Ff7BattleActorModelModifiers>? ActorModel = null, Func<int, short>? ActorRootZ = null, Func<int, short>? ActorHomeZ = null);
public sealed record Ff7BattlePrimaryHitmark(int Target, int Style);
public sealed record Ff7BattlePrimaryReactionReceipt(int Target, bool RespectDeathMask);

public sealed record Ff7BattlePrimaryState
{
    public IReadOnlyList<Ff7BattlePrimaryCallback?> Slots { get; init; } = new Ff7BattlePrimaryCallback?[Ff7BattlePrimaryEffects.Capacity];
    public IReadOnlyList<Ff7BattlePrimaryDraw> Draws { get; init; } = [];
    public IReadOnlyList<ushort> SoundIds { get; init; } = [];
    public IReadOnlyList<Ff7BattlePrimaryHitmark> Hitmarks { get; init; } = [];
    public IReadOnlyList<Ff7BattlePrimaryReactionReceipt> TargetReactions { get; init; } = [];
    public Ff7BattleSearchScopeBasis? BeamGunBasis { get; init; }
    public Ff7BattleBubblePool Bubbles { get; init; } = new();
    public Ff7BattleWaveGlobals WaveGlobals { get; init; } = new();
    public Ff7BattleEnergyBallGlobals EnergyBallGlobals { get; init; } = new();
    public IReadOnlyList<Ff7BattlePrimaryPositionedSound> PositionedSounds { get; init; } = [];
    public IReadOnlyList<Ff7BattleBraverPoint> WaveSparks { get; init; } = [];
    public IReadOnlyList<Ff7BattleSecondaryCallback> TailLaserSecondaries { get; init; } = [];
    public short CameraFocusYDelta { get; init; }
    public IReadOnlyList<Ff7BattleActorModelWrite> ActorWrites { get; init; } = [];
    public IReadOnlyList<Ff7BattleActorModelSound> ActorSounds { get; init; } = [];
    public IReadOnlyList<Ff7BattleBossDeathShakeRequest> BossDeathShakes { get; init; } = [];
    public Ff7BattleActionTitle? ActionTitle { get; init; }
    public ushort ImpactTargets { get; init; }
    public int Tick { get; init; } = -1;
    public short SearchScopeCounter { get; init; }
    public byte StageRed { get; init; }
    public byte StageGreen { get; init; }
    public byte StageBlue { get; init; }
    public short StageTransparency { get; init; }
    public Ff7BattleSilkLinePool SilkLines { get; init; } = new();
    [JsonIgnore]
    public Ff7BattleStageFadeState StageFade => new(StageRed, StageGreen, StageBlue, StageTransparency, Slots.OfType<Ff7BattlePrimaryStageFade>().FirstOrDefault()?.Ramp);
    [JsonIgnore]
    public bool HasCallbacks => Slots.Any(slot => slot is not null);
    [JsonIgnore]
    public int OccupiedSlotCount => Slots.Count(slot => slot is not null);

    public bool HasOwner(int owner) => Slots.Any(slot => slot?.Owner == owner);
}

public static partial class Ff7BattlePrimaryEffects
{
    public const int Capacity = 100;

    private static Ff7BattlePrimaryState Schedule(Ff7BattlePrimaryState state, Ff7BattlePrimaryCallback callback)
    {
        PrimaryPass pass = new(state, null);
        pass.Allocate(callback);
        return pass.Finish(state.Tick, preserveReceipts: true);
    }


    public static Ff7BattlePrimaryState ScheduleSearchScope(Ff7BattlePrimaryState state, ushort targets, Vector3 source, Vector3 targetCenter, int owner = 0)
    {
        Ff7BattleSearchScopeState scope = Ff7BattleSearchScopeEffect.CreateState(targets, source, targetCenter, state.SearchScopeCounter);
        short length = Length(scope.Source, scope.TargetCenter);
        PrimaryPass pass = new(state, null);
        pass.Allocate(new Ff7BattlePrimaryScopeController(targets, scope.Source, scope.Basis, length) { Owner = owner });
        pass.StageRed = 0;
        pass.StageGreen = 0;
        pass.StageBlue = 0;
        pass.FadeTo(1024, 10);
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    public static Ff7BattlePrimaryState FadeTo(Ff7BattlePrimaryState state, int target, int duration)
    {
        PrimaryPass pass = new(state, null);
        pass.FadeTo(target, duration);
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    public static Ff7BattlePrimaryState ImportStageFade(Ff7BattlePrimaryState state, Ff7BattleStageFadeState previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        PrimaryPass pass = new(state, null) { StageRed = previous.Red, StageGreen = previous.Green, StageBlue = previous.Blue, StageTransparency = previous.Transparency };
        if (previous.Ramp is { } ramp)
        {
            pass.Allocate(new Ff7BattlePrimaryStageFade(ramp) { Owner = -1 });
        }
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    public static Ff7BattlePrimaryState ImportSearchScope(Ff7BattlePrimaryState state, Ff7BattleSearchScopeState previous, int owner = 0)
    {
        ArgumentNullException.ThrowIfNull(previous);
        PrimaryPass pass = new(state, null) { SearchScopeCounter = previous.ScaleCounter };
        short length = Length(previous.Source, previous.TargetCenter);
        if (previous.Frame < 39)
        {
            pass.Allocate(new Ff7BattlePrimaryScopeController(previous.Targets, previous.Source, previous.Basis, length) { Owner = owner, Age = previous.Frame + 1 });
        }
        if (previous.Frame < 31)
        {
            for (int index = 0; index < 4; index++)
            {
                pass.Allocate(new Ff7BattlePrimaryScopeEmitter(previous.Source, previous.Basis, length, index) { Owner = owner, Age = previous.Frame + 1 });
            }
        }
        foreach (Ff7BattleSearchScopeSprite sprite in previous.Sprites)
        {
            int age = previous.Frame - sprite.StartedAt + 1;
            if (age < Ff7BattleSearchScopeEffect.FirstSpriteFrames + Ff7BattleSearchScopeEffect.SecondSpriteFrames)
            {
                bool second = age >= Ff7BattleSearchScopeEffect.FirstSpriteFrames;
                pass.Allocate(new Ff7BattlePrimaryScopeSprite(sprite.Position, second ? 1 : 0) { Owner = owner, Age = second ? age - Ff7BattleSearchScopeEffect.FirstSpriteFrames : age });
            }
        }
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    public static Ff7BattlePrimaryState Advance(Ff7BattlePrimaryState state, Ff7BattlePrimaryContext? context = null)
    {
        PrimaryPass pass = new(state, context);
        for (int index = 0; index < Capacity; index++)
        {
            pass.Cursor = index;
            if (pass.Slots[index] is not { } callback)
            {
                continue;
            }
            pass.CurrentOwner = callback.Owner;
            pass.Slots[index] = Execute(callback, pass);
        }
        return pass.Finish(state.Tick + 1);
    }

    private static Ff7BattlePrimaryCallback? Execute(Ff7BattlePrimaryCallback callback, PrimaryPass pass)
    {
        switch (callback)
        {
            case Ff7BattlePrimaryRocketController:
            case Ff7BattlePrimaryRocketMissile:
            case Ff7BattlePrimaryRocketExplosion:
                return ExecuteRocket(callback, pass);
            case Ff7BattlePrimaryDeathController death:
                return ExecuteDeath(death, pass);
            case Ff7BattlePrimarySmogController:
            case Ff7BattlePrimarySmogCloud:
            case Ff7BattlePrimarySmogSprite:
                return ExecuteSmog(callback, pass);
            case Ff7BattlePrimarySmokeShotController:
            case Ff7BattlePrimarySmokeShotSprite:
                return ExecuteSmokeShot(callback, pass);
            case Ff7BattlePrimaryBigShotGroup:
            case Ff7BattlePrimaryBigShotController:
            case Ff7BattlePrimaryBigShotCharge:
            case Ff7BattlePrimaryBigShotParticle:
                return ExecuteBigShot(callback, pass);
            case Ff7BattlePrimaryBeatRushController:
            case Ff7BattlePrimaryBeatRushTrail:
            case Ff7BattlePrimaryBeatRushSprite:
                return ExecuteBeatRush(callback, pass);
            case Ff7BattlePrimaryBraverGroup:
            case Ff7BattlePrimaryBraverController:
            case Ff7BattlePrimaryBraverShine:
            case Ff7BattlePrimaryBraverTrail:
            case Ff7BattlePrimaryBraverImpact:
                return ExecuteBraver(callback, pass);
            case Ff7BattlePrimaryEnergyBallController:
            case Ff7BattlePrimaryEnergyBallSourceLight:
            case Ff7BattlePrimaryEnergyBallCharge:
            case Ff7BattlePrimaryEnergyBallRing:
            case Ff7BattlePrimaryEnergyBallArc:
            case Ff7BattlePrimaryEnergyBallOrb:
            case Ff7BattlePrimaryEnergyBallBlast:
                return ExecuteEnergyBall(callback, pass, ref pass.EnergyBallGlobals);
            case Ff7BattleTailLaserCallback tailLaser:
                return ExecuteTailLaser(tailLaser, pass);
            case Ff7BattlePrimaryWaveController:
            case Ff7BattlePrimaryWaveOptionalFlash:
            case Ff7BattlePrimaryWaveBeam:
            case Ff7BattlePrimaryWaveOrb:
            case Ff7BattlePrimaryWaveCenter:
            case Ff7BattlePrimaryWaveRing:
            case Ff7BattlePrimaryWaveExplosion:
            case Ff7BattlePrimaryWaveFragment:
                return ExecuteWave(callback, pass, pass.Wave);
            case Ff7BattlePrimaryLaserCannonController:
            case Ff7BattlePrimaryLaserCannonBeam:
            case Ff7BattlePrimaryLaserCannonImpact:
            case Ff7BattlePrimaryLaserCannonFlare:
                return ExecuteLaserCannon(callback, pass);
            case Ff7BattlePrimaryBigBomberController:
            case Ff7BattlePrimaryBigBomberMuzzle:
            case Ff7BattlePrimaryBigBomberDebris:
            case Ff7BattlePrimaryBigBomberSmoke:
            case Ff7BattlePrimaryBigBomberWave:
                return ExecuteBigBomber(callback, pass);
            case Ff7BattlePrimaryBeamGunController:
            case Ff7BattlePrimaryBeamGunFlash:
            case Ff7BattlePrimaryBeamGunShot:
            case Ff7BattlePrimaryBeamGunBurst:
                return ExecuteBeamGun(callback, pass);
            case Ff7BattlePrimaryBubbleGroup:
            case Ff7BattlePrimaryBubbleController:
            case Ff7BattlePrimaryBubbleStream:
            case Ff7BattlePrimaryBubbleCloud:
            case Ff7BattlePrimaryBubbleBurst:
            case Ff7BattlePrimaryBubbleRibbon:
                return ExecuteHellBubbles(callback, pass);
            case Ff7BattlePrimaryActionTitle title:
                return ExecuteActionTitle(title, pass);
            case Ff7BattlePrimaryTargetReaction reaction:
                return ExecuteTargetReaction(reaction, pass);
            case Ff7BattlePrimaryStageFade fade:
                Ff7BattleStageFadeState updated = Ff7BattleStageFade.Advance(new(pass.StageRed, pass.StageGreen, pass.StageBlue, pass.StageTransparency, fade.Ramp));
                pass.StageTransparency = updated.Transparency;
                return updated.Ramp is { } ramp ? fade with { Ramp = ramp, Age = fade.Age + 1 } : null;
            case Ff7BattlePrimaryScopeController scope:
                if (scope.Age == 0)
                {
                    for (int index = 0; index < 4; index++)
                    {
                        pass.Allocate(new Ff7BattlePrimaryScopeEmitter(scope.Source, scope.Basis, scope.Length, index) { Owner = scope.Owner });
                    }
                }
                if (scope.Age == 32)
                {
                    pass.ContactMask(scope.Targets);
                }
                if (scope.Age == 39)
                {
                    pass.FadeTo(0, 10);
                    return null;
                }
                return scope with { Age = scope.Age + 1 };
            case Ff7BattlePrimaryScopeEmitter emitter:
                int sign = (emitter.Index >> 1) * 2 - 1;
                short pitch = (short)(Ff7BattleSearchScopeEffect.Sine(emitter.Age << 8) / 40 * sign);
                short yaw = (short)((Ff7BattleSearchScopeEffect.Sine(emitter.Age << 7) >> 4) * sign);
                bool inner = (emitter.Index & 1) == 0;
                Ff7BattleSearchScopeMesh mesh = new(inner ? 1 : 0, new((short)(pitch - 1024), yaw, 0), (short)(emitter.Length * 5376 / 2800), inner ? 12288 : 16384, (short)(4096 - pass.SearchScopeCounter * (inner ? 32 : 24)));
                pass.Emit(new Ff7BattlePrimaryScopeMeshDraw(emitter.Source, emitter.Basis, mesh));
                if (inner)
                {
                    Matrix4x4 transform = BattleRigidPose.Rotation(new(pitch, yaw, 0)) * emitter.Basis.Matrix;
                    Ff7BattleBraverPoint offset = Ff7BattleBraverPoint.At(Vector3.Transform(new(0, 0, emitter.Length), transform));
                    Ff7BattleBraverPoint point = new((short)(emitter.Source.X + offset.X), (short)(emitter.Source.Y + offset.Y), (short)(emitter.Source.Z + offset.Z));
                    pass.Allocate(new Ff7BattlePrimaryScopeSprite(point) { Owner = emitter.Owner });
                }
                if (emitter.Age < 8)
                {
                    pass.SearchScopeCounter = (short)(pass.SearchScopeCounter + 1);
                }
                if (emitter.Age > 24)
                {
                    pass.SearchScopeCounter = (short)(pass.SearchScopeCounter - 1);
                }
                return emitter.Age == 31 ? null : emitter with { Age = emitter.Age + 1 };
            case Ff7BattlePrimaryScopeSprite sprite:
                pass.Emit(new Ff7BattlePrimaryScopeSpriteDraw(sprite.Position, sprite.Stage, sprite.Age));
                int count = sprite.Stage == 0 ? Ff7BattleSearchScopeEffect.FirstSpriteFrames : Ff7BattleSearchScopeEffect.SecondSpriteFrames;
                if (sprite.Age + 1 < count)
                {
                    return sprite with { Age = sprite.Age + 1 };
                }
                return sprite.Stage == 0 ? sprite with { Stage = 1, Age = 0 } : null;
            case Ff7BattlePrimarySilkGroup:
            case Ff7BattlePrimarySilkController:
            case Ff7BattlePrimarySilkLines:
            case Ff7BattlePrimarySilkSprite:
            case Ff7BattlePrimarySilkStrand:
                return ExecuteSilk(callback, pass);
            case Ff7BattlePrimaryTargetGroup:
            case Ff7BattlePrimaryCureController:
            case Ff7BattlePrimaryCureParticle:
            case Ff7BattlePrimaryBoltController:
            case Ff7BattlePrimaryBoltSpark:
            case Ff7BattlePrimaryDestroyedController:
            case Ff7BattlePrimaryDestroyedSprite:
                return ExecuteParticles(callback, pass);
            case Ff7BattlePrimaryFireController:
            case Ff7BattlePrimaryFireSprite:
            case Ff7BattlePrimaryIceInitializer:
            case Ff7BattlePrimaryIceMesh:
                return ExecuteFireIce(callback, pass);
            default:
                throw new NotSupportedException("FFVII primary callback has no admitted executor.");
        }
    }

    private static short Length(Ff7BattleBraverPoint from, Ff7BattleBraverPoint to)
    {
        int x = (short)(to.X - from.X);
        int y = (short)(to.Y - from.Y);
        int z = (short)(to.Z - from.Z);
        return (short)(int)Math.Sqrt(x * x + y * y + z * z);
    }

    private sealed partial class PrimaryPass
    {
        private readonly Ff7BattlePrimaryState previous;
        public readonly Ff7BattlePrimaryCallback?[] Slots;
        public readonly List<Ff7BattlePrimaryDraw> Draws = [];
        public readonly List<ushort> SoundIds = [];
        public readonly List<Ff7BattlePrimaryHitmark> Hitmarks = [];
        public readonly List<Ff7BattlePrimaryReactionReceipt> TargetReactions = [];
        public readonly Ff7BattlePrimaryContext? Context;
        public int Cursor;
        public int CurrentOwner;
        public Ff7BattleActionTitle? ActionTitle;
        public Ff7BattleBubblePool Bubbles;
        public Ff7BattleSearchScopeBasis? BeamGunBasis;
        public readonly WavePass Wave;
        public Ff7BattleEnergyBallGlobals EnergyBallGlobals;
        public readonly List<Ff7BattlePrimaryPositionedSound> PositionedSounds = [];
        public readonly List<Ff7BattleSecondaryCallback> TailLaserSecondaries = [];
        public short CameraFocusYDelta;
        public ushort ImpactTargets;
        public short SearchScopeCounter;
        public byte StageRed;
        public byte StageGreen;
        public byte StageBlue;
        public short StageTransparency;
        public Ff7BattleSilkLinePool SilkLines;
        public int Tick => previous.Tick + 1;

        public PrimaryPass(Ff7BattlePrimaryState state, Ff7BattlePrimaryContext? context)
        {
            ArgumentNullException.ThrowIfNull(state);
            if (state.Slots.Count != Capacity)
            {
                throw new InvalidDataException("FFVII primary continuation requires exactly 100 native slots.");
            }
            previous = state;
            Slots = state.Slots.ToArray();
            Context = context;
            SearchScopeCounter = state.SearchScopeCounter;
            StageRed = state.StageRed;
            StageGreen = state.StageGreen;
            StageBlue = state.StageBlue;
            StageTransparency = state.StageTransparency;
            SilkLines = state.SilkLines;
            Wave = new(state.WaveGlobals);
            EnergyBallGlobals = state.EnergyBallGlobals;
            Bubbles = state.Bubbles;
            BeamGunBasis = state.BeamGunBasis;
        }

        public int Allocate(Ff7BattlePrimaryCallback callback)
        {
            for (int index = Cursor; index < Capacity; index++)
            {
                if (Slots[index] is null)
                {
                    Slots[index] = callback;
                    return index;
                }
            }
            throw new InvalidOperationException("FFVII primary callback pool exhausted its remaining native slots.");
        }

        public void Emit(Ff7BattlePrimaryDraw draw)
        {
            Draws.Add(draw with { Slot = Cursor, Owner = CurrentOwner });
        }

        public void Contact(int target)
        {
            ImpactTargets |= (ushort)(1 << target);
            TargetReactions.Add(new(target, true));
        }

        public void ContactMask(ushort targets)
        {
            for (int target = 0; target < 10; target++)
            {
                if ((targets & (1 << target)) != 0)
                {
                    Contact(target);
                }
            }
        }

        public void FadeTo(int target, int duration)
        {
            if (Slots.Any(callback => callback is Ff7BattlePrimaryStageFade))
            {
                return;
            }
            Ff7BattleStageFadeState fade = Ff7BattleStageFade.FadeTo(new(StageRed, StageGreen, StageBlue, StageTransparency), target, duration);
            Allocate(new Ff7BattlePrimaryStageFade(fade.Ramp!) { Owner = -1 });
        }

        public Ff7BattlePrimaryState Finish(int tick, bool preserveReceipts = false)
        {
            return new() { Slots = Slots, BeamGunBasis = BeamGunBasis, Bubbles = Bubbles, WaveGlobals = Wave.Globals, EnergyBallGlobals = EnergyBallGlobals, PositionedSounds = preserveReceipts ? [.. previous.PositionedSounds, .. PositionedSounds] : PositionedSounds, WaveSparks = preserveReceipts ? [.. previous.WaveSparks, .. Wave.Sparks] : Wave.Sparks, TailLaserSecondaries = preserveReceipts ? [.. previous.TailLaserSecondaries, .. TailLaserSecondaries] : TailLaserSecondaries, CameraFocusYDelta = preserveReceipts ? previous.CameraFocusYDelta : CameraFocusYDelta, Tick = tick, Draws = preserveReceipts ? [.. previous.Draws, .. Draws] : Draws, SoundIds = preserveReceipts ? [.. previous.SoundIds, .. SoundIds] : SoundIds, Hitmarks = preserveReceipts ? [.. previous.Hitmarks, .. Hitmarks] : Hitmarks, TargetReactions = preserveReceipts ? [.. previous.TargetReactions, .. TargetReactions] : TargetReactions, ActionTitle = ActionTitle ?? (preserveReceipts ? previous.ActionTitle : null), ImpactTargets = preserveReceipts ? previous.ImpactTargets : ImpactTargets, SearchScopeCounter = SearchScopeCounter, StageRed = StageRed, StageGreen = StageGreen, StageBlue = StageBlue, StageTransparency = StageTransparency, SilkLines = SilkLines, ActorWrites = preserveReceipts ? [.. previous.ActorWrites, .. ActorWrites] : ActorWrites, ActorSounds = preserveReceipts ? [.. previous.ActorSounds, .. ActorSounds] : ActorSounds, BossDeathShakes = preserveReceipts ? [.. previous.BossDeathShakes, .. BossDeathShakes] : BossDeathShakes };
        }
    }
}
