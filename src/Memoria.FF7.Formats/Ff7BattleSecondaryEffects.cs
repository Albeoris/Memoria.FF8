using System.Numerics;
using System.Text.Json.Serialization;

namespace Memoria.FF7.Formats;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Callback")]
[JsonDerivedType(typeof(Ff7BattleGunEmitter), "GunEmitter")]
[JsonDerivedType(typeof(Ff7BattleGunMuzzleCallback), "GunMuzzle")]
[JsonDerivedType(typeof(Ff7BattleGunCasingCallback), "GunCasing")]
[JsonDerivedType(typeof(Ff7BattleGunSparkCallback), "GunSpark")]
[JsonDerivedType(typeof(Ff7BattleGunDustCallback), "GunDust")]
[JsonDerivedType(typeof(Ff7BattleHitmarkOwnerCallback), "HitmarkOwner")]
[JsonDerivedType(typeof(Ff7BattleHitmarkSpriteCallback), "HitmarkSprite")]
[JsonDerivedType(typeof(Ff7BattleDelayedSoundCallback), "Sound")]
[JsonDerivedType(typeof(Ff7BattleBossDeathShakeCallback), "BossDeathShake")]
[JsonDerivedType(typeof(Ff7BattleLimitPreparationEntry), "LimitPreparationEntry")]
[JsonDerivedType(typeof(Ff7BattleLimitPreparationEmitter), "LimitPreparationEmitter")]
[JsonDerivedType(typeof(Ff7BattleLimitPreparationMesh), "LimitPreparationMesh")]
[JsonDerivedType(typeof(Ff7BattleWaveSpark), "WaveSpark")]
public abstract record Ff7BattleSecondaryCallback
{
    public int Age { get; init; }
}

public sealed record Ff7BattleGunEmitter(int Actor, int Bone, short Offset, byte Delay, byte Duration) : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleGunMuzzleCallback(int Actor, int Bone, short Offset) : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleGunCasingCallback(Ff7BattleBraverPoint Position, Ff7BattleBraverPoint Velocity, short Yaw, int Bounces = 0) : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleGunSparkCallback(Ff7BattleBraverPoint Position) : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleGunDustCallback(Ff7BattleBraverPoint Position, int MinimumScale, int MaximumScale) : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleHitmarkOwnerCallback(int Target, Ff7BattleHitmarkDefinition Definition, Ff7BattleBraverPoint Anchor, int CollisionRadius, bool Flipped) : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleHitmarkSpriteCallback(int Target, Ff7BattleHitmarkCue Cue, Ff7BattleBraverPoint Anchor, int CollisionRadius, bool Flipped) : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleDelayedSoundCallback(ushort Sound, int Delay) : Ff7BattleSecondaryCallback;

public sealed record Ff7BattleGunMuzzle(Ff7BattleBraverPoint Origin, Ff7BattleBraverPoint XAxis, Ff7BattleBraverPoint YAxis, Ff7BattleBraverPoint ZAxis);
public sealed record Ff7BattleGunCasing(Ff7BattleBraverPoint Position, short Yaw);
public sealed record Ff7BattleGunSpark(Ff7BattleBraverPoint Position, short Yaw);
public sealed record Ff7BattleGunDust(Ff7BattleBraverPoint Position, int Frame, int MinimumScale, int MaximumScale);
public enum Ff7BattleSecondaryDrawKind
{
    Muzzle,
    Casing,
    Spark,
    Dust,
    Hitmark,
    ScreenPulse,
    LimitPreparation,
    WaveSpark
}
public sealed record Ff7BattleSecondaryDraw(Ff7BattleSecondaryDrawKind Kind, int Index);

public sealed record Ff7BattleSecondaryState
{
    public IReadOnlyList<Ff7BattleSecondaryCallback?> Slots { get; init; } = new Ff7BattleSecondaryCallback?[Ff7BattleSecondaryEffects.Capacity];
    public IReadOnlyList<Ff7BattleGunMuzzle> Muzzles { get; init; } = [];
    public IReadOnlyList<Ff7BattleGunCasing> Casings { get; init; } = [];
    public IReadOnlyList<Ff7BattleGunSpark> Sparks { get; init; } = [];
    public IReadOnlyList<Ff7BattleGunDust> Dust { get; init; } = [];
    public IReadOnlyList<Ff7BattleHitmarkState> Hitmarks { get; init; } = [];
    public IReadOnlyList<ushort> SoundIds { get; init; } = [];
    public IReadOnlyList<Ff7BattleActorModelWrite> ActorWrites { get; init; } = [];
    public IReadOnlyList<Ff7BattleActorScreenPulse> ScreenPulses { get; init; } = [];
    public IReadOnlyList<Ff7BattleLimitPreparationDraw> LimitPreparations { get; init; } = [];
    public IReadOnlyList<Ff7BattleWaveSparkDraw> WaveSparks { get; init; } = [];
    public IReadOnlyList<Ff7BattleSecondaryDraw> DrawOrder { get; init; } = [];
    [JsonIgnore]
    public bool HasCallbacks => Slots.Any(slot => slot is not null);
}

public static partial class Ff7BattleSecondaryEffects
{
    public const int Capacity = 60;

    public static Ff7BattleSecondaryState ScheduleGun(Ff7BattleSecondaryState state, int actor, int resolvedBone, short offset, byte delay, byte duration)
    {
        ValidateActor(actor);
        ArgumentOutOfRangeException.ThrowIfLessThan(resolvedBone, -1);
        return Schedule(state, new Ff7BattleGunEmitter(actor, resolvedBone, offset, delay, duration));
    }

    public static Ff7BattleSecondaryState ScheduleSound(Ff7BattleSecondaryState state, ushort sound, int delay)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(delay);
        return Schedule(state, new Ff7BattleDelayedSoundCallback(sound, delay));
    }

    public static Ff7BattleSecondaryState ScheduleHitmark(Ff7BattleSecondaryState state, int target, Ff7BattleHitmarkDefinition definition, Vector3 frozenAnchor, int collisionRadius, bool flipped = false)
    {
        ValidateActor(target);
        ArgumentNullException.ThrowIfNull(definition);
        return Schedule(state, new Ff7BattleHitmarkOwnerCallback(target, definition, Ff7BattleBraverPoint.At(frozenAnchor), collisionRadius, flipped));
    }

    public static Ff7BattleSecondaryState ImportHitmark(Ff7BattleSecondaryState state, Ff7BattleHitmarkState previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (previous.Anchor is not { } anchor)
        {
            throw new InvalidDataException("FFVII hitmark continuation requires its recorded frozen anchor.");
        }
        if (previous.Definition.Cues.Any(cue => cue.Frame > previous.Frame))
        {
            state = Schedule(state, new Ff7BattleHitmarkOwnerCallback(previous.Target, previous.Definition, anchor, previous.CollisionRadius, previous.Flipped) { Age = previous.Frame + 1 });
        }
        foreach (Ff7BattleHitmarkCue cue in previous.Definition.Cues)
        {
            int age = previous.Frame - cue.Frame;
            if (age >= 0 && age + 1 < cue.FrameCount)
            {
                state = Schedule(state, new Ff7BattleHitmarkSpriteCallback(previous.Target, cue, anchor, previous.CollisionRadius, previous.Flipped) { Age = age + 1 });
            }
        }
        return state with { Hitmarks = [.. state.Hitmarks, previous], DrawOrder = [.. state.DrawOrder, new(Ff7BattleSecondaryDrawKind.Hitmark, state.Hitmarks.Count)] };
    }

    public static Ff7BattleSecondaryState Advance(Ff7BattleSecondaryState state, Func<int, int, Vector3, Vector3> actorPoint, Func<int, BattleRotationWords> actorRotation, Func<int> nextCrt)
    {
        ArgumentNullException.ThrowIfNull(actorPoint);
        ArgumentNullException.ThrowIfNull(actorRotation);
        ArgumentNullException.ThrowIfNull(nextCrt);
        Ff7BattleSecondaryCallback?[] slots = CopySlots(state);
        List<Ff7BattleGunMuzzle> muzzles = [];
        List<Ff7BattleGunCasing> casings = [];
        List<Ff7BattleGunSpark> sparks = [];
        List<Ff7BattleGunDust> dust = [];
        List<Ff7BattleHitmarkState> hitmarks = [];
        List<ushort> sounds = [];
        List<Ff7BattleSecondaryDraw> drawOrder = [];
        List<Ff7BattleActorModelWrite> actorWrites = [];
        List<Ff7BattleActorScreenPulse> screenPulses = [];
        List<Ff7BattleLimitPreparationDraw> limitPreparations = [];
        List<Ff7BattleWaveSparkDraw> waveSparks = [];
        for (int cursor = 0; cursor < Capacity; cursor++)
        {
            switch (slots[cursor])
            {
                case null:
                    continue;
                case Ff7BattleBossDeathShakeCallback shake:
                    Ff7BattleBossDeathShakeUpdate shakeUpdate = Ff7BattleActorDeath.AdvanceShake(shake.State);
                    slots[cursor] = shakeUpdate.State is { } shakeState ? shake with { State = shakeState, Age = shake.Age + 1 } : null;
                    if (shakeUpdate.Write is { } actorWrite)
                    {
                        actorWrites.Add(actorWrite);
                    }
                    if (shakeUpdate.Flash)
                    {
                        drawOrder.Add(new(Ff7BattleSecondaryDrawKind.ScreenPulse, screenPulses.Count));
                        screenPulses.Add(new(250, 250, 250));
                    }
                    break;
                case Ff7BattleWaveSpark waveSpark:
                    slots[cursor] = ExecuteWaveSpark(waveSpark, EmitWaveSpark);
                    break;
                case Ff7BattleLimitPreparationEntry preparationEntry:
                    sounds.Add(0x24);
                    Allocate(new Ff7BattleLimitPreparationEmitter(Ff7BattleBraverPoint.At(actorPoint(preparationEntry.Actor, preparationEntry.Bone, Vector3.Zero))));
                    slots[cursor] = null;
                    break;
                case Ff7BattleLimitPreparationEmitter preparationEmitter:
                    slots[cursor] = ExecuteLimitPreparation(preparationEmitter, Allocate, EmitPreparation);
                    break;
                case Ff7BattleLimitPreparationMesh preparationMesh:
                    slots[cursor] = ExecuteLimitPreparation(preparationMesh, Allocate, EmitPreparation);
                    break;
                case Ff7BattleGunEmitter emitter:
                    int age = emitter.Age - emitter.Delay;
                    if (age >= (emitter.Duration & 127))
                    {
                        slots[cursor] = null;
                        break;
                    }
                    slots[cursor] = emitter with { Age = emitter.Age + 1 };
                    if (age < 0)
                    {
                        break;
                    }
                    Allocate(new Ff7BattleGunMuzzleCallback(emitter.Actor, emitter.Bone, emitter.Offset));
                    if ((emitter.Duration & 128) == 0)
                    {
                        Ff7BattleBraverPoint origin = Ff7BattleBraverPoint.At(actorPoint(emitter.Actor, emitter.Bone, Vector3.Zero));
                        Vector3 velocity = new(-60 - (nextCrt() & 15), (nextCrt() & 31) - 150, (nextCrt() & 15) + 20);
                        BattleRotationWords rotation = actorRotation(emitter.Actor);
                        Ff7BattleBraverPoint rotated = Ff7BattleBraverPoint.At(Vector3.Transform(velocity, BattleRigidPose.Rotation(rotation)));
                        Allocate(new Ff7BattleGunCasingCallback(origin, rotated, rotation.Y));
                        Allocate(new Ff7BattleGunDustCallback(origin, 1024, 2048));
                    }
                    break;
                case Ff7BattleGunMuzzleCallback muzzle:
                    if (muzzle.Age != 0)
                    {
                        slots[cursor] = null;
                        break;
                    }
                    Vector3 muzzleOrigin = actorPoint(muzzle.Actor, muzzle.Bone, new(0, 0, -muzzle.Offset));
                    drawOrder.Add(new(Ff7BattleSecondaryDrawKind.Muzzle, muzzles.Count));
                    muzzles.Add(new(Ff7BattleBraverPoint.At(muzzleOrigin), Axis(new(4096, 0, 0)), Axis(new(0, 4096, 0)), Axis(new(0, 0, 4096))));
                    Allocate(new Ff7BattleGunSparkCallback(new((short)muzzleOrigin.X, 0, (short)muzzleOrigin.Z)));
                    slots[cursor] = muzzle with { Age = 1 };
                    break;

                    Ff7BattleBraverPoint Axis(Vector3 axis) => Ff7BattleBraverPoint.At(actorPoint(muzzle.Actor, muzzle.Bone, axis + new Vector3(0, 0, -muzzle.Offset)) - muzzleOrigin);
                case Ff7BattleGunCasingCallback casing:
                    drawOrder.Add(new(Ff7BattleSecondaryDrawKind.Casing, casings.Count));
                    casings.Add(new(casing.Position, casing.Yaw));
                    Ff7BattleBraverPoint position = Add(casing.Position, casing.Velocity);
                    Ff7BattleBraverPoint movement = casing.Velocity with { Y = (short)(casing.Velocity.Y + 30) };
                    int bounces = casing.Bounces;
                    if (position.Y >= 0)
                    {
                        short bounceY = (short)((-movement.Y >> 2) - (nextCrt() & 31));
                        short bounceX = (short)((movement.X >> 2) + (nextCrt() & 31) - 16);
                        short bounceZ = (short)((movement.Z >> 2) + (nextCrt() & 31) - 16);
                        movement = new(bounceX, bounceY, bounceZ);
                        position = position with { Y = (short)(-position.Y >> 2) };
                        bounces++;
                        Allocate(new Ff7BattleGunDustCallback(position, 512, 1024));
                    }
                    slots[cursor] = bounces >= 2 ? null : casing with { Age = casing.Age + 1, Position = position, Velocity = movement, Bounces = bounces };
                    break;
                case Ff7BattleGunSparkCallback spark:
                    drawOrder.Add(new(Ff7BattleSecondaryDrawKind.Spark, sparks.Count));
                    sparks.Add(new(spark.Position, (short)(nextCrt() & 1023)));
                    slots[cursor] = null;
                    break;
                case Ff7BattleGunDustCallback particle:
                    drawOrder.Add(new(Ff7BattleSecondaryDrawKind.Dust, dust.Count));
                    dust.Add(new(particle.Position, particle.Age, particle.MinimumScale, particle.MaximumScale));
                    slots[cursor] = particle.Age >= 7 ? null : particle with { Age = particle.Age + 1 };
                    break;
                case Ff7BattleHitmarkOwnerCallback owner:
                    foreach (Ff7BattleHitmarkCue cue in owner.Definition.Cues)
                    {
                        if (cue.Frame == owner.Age)
                        {
                            Allocate(new Ff7BattleHitmarkSpriteCallback(owner.Target, cue, owner.Anchor, owner.CollisionRadius, owner.Flipped));
                        }
                    }
                    slots[cursor] = owner.Definition.Cues.Any(cue => cue.Frame > owner.Age) ? owner with { Age = owner.Age + 1 } : null;
                    break;
                case Ff7BattleHitmarkSpriteCallback sprite:
                    Ff7BattleHitmarkDefinition definition = new(-1, [sprite.Cue with { Frame = 0 }]);
                    drawOrder.Add(new(Ff7BattleSecondaryDrawKind.Hitmark, hitmarks.Count));
                    hitmarks.Add(new(sprite.Target, definition, sprite.Age, sprite.Anchor, sprite.CollisionRadius, sprite.Flipped));
                    slots[cursor] = sprite.Age + 1 >= sprite.Cue.FrameCount ? null : sprite with { Age = sprite.Age + 1 };
                    break;
                case Ff7BattleDelayedSoundCallback sound:
                    if (sound.Age >= sound.Delay)
                    {
                        sounds.Add(sound.Sound);
                        slots[cursor] = null;
                    }
                    else
                    {
                        slots[cursor] = sound with { Age = sound.Age + 1 };
                    }
                    break;
                default:
                    throw new NotSupportedException("FFVII secondary callback has no admitted executor.");
            }

            void EmitWaveSpark(Ff7BattleWaveSparkDraw draw)
            {
                drawOrder.Add(new(Ff7BattleSecondaryDrawKind.WaveSpark, waveSparks.Count));
                waveSparks.Add(draw);
            }

            void EmitPreparation(Ff7BattleLimitPreparationDraw draw)
        {
            drawOrder.Add(new(Ff7BattleSecondaryDrawKind.LimitPreparation, limitPreparations.Count));
            limitPreparations.Add(draw);
        }

        void Allocate(Ff7BattleSecondaryCallback callback) => Insert(slots, cursor, callback);
        }
        return new() { Slots = slots, Muzzles = muzzles, Casings = casings, Sparks = sparks, Dust = dust, Hitmarks = hitmarks, SoundIds = sounds, ActorWrites = actorWrites, ScreenPulses = screenPulses, LimitPreparations = limitPreparations, WaveSparks = waveSparks, DrawOrder = drawOrder };
    }

    private static Ff7BattleBraverPoint Add(Ff7BattleBraverPoint a, Ff7BattleBraverPoint b) => new((short)(a.X + b.X), (short)(a.Y + b.Y), (short)(a.Z + b.Z));

    private static Ff7BattleSecondaryState Schedule(Ff7BattleSecondaryState state, Ff7BattleSecondaryCallback callback)
    {
        Ff7BattleSecondaryCallback?[] slots = CopySlots(state);
        Insert(slots, 0, callback);
        return state with { Slots = slots };
    }

    private static Ff7BattleSecondaryCallback?[] CopySlots(Ff7BattleSecondaryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Slots.Count != Capacity)
        {
            throw new InvalidDataException("FFVII secondary callback continuation requires exactly 60 native slots.");
        }
        return state.Slots.ToArray();
    }

    private static void Insert(Ff7BattleSecondaryCallback?[] slots, int cursor, Ff7BattleSecondaryCallback callback)
    {
        for (int index = cursor; index < Capacity; index++)
        {
            if (slots[index] is null)
            {
                slots[index] = callback;
                return;
            }
        }
        throw new InvalidOperationException("FFVII secondary callback pool exhausted its remaining native slots.");
    }

    private static void ValidateActor(int actor)
    {
        if (actor is < 0 or > 9 or 3)
        {
            throw new ArgumentOutOfRangeException(nameof(actor));
        }
    }
}
