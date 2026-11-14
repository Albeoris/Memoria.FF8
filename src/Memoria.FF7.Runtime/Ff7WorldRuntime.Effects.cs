using System.Numerics;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldEffectParticle(long Id, int X, int Y, int Z, int VelocityX, int VelocityY, int VelocityZ, int Angle, int Sprite, int Remaining, bool GroundAligned, int Gravity, int Intensity, bool Falling, uint Color);
public sealed record Ff7WorldEffectEmitter(int Model, int Type, int Delay, int Countdown = 0);
public sealed record Ff7WorldEffectsState(Ff7WorldEffectParticle[] Particles, Ff7WorldEffectEmitter?[] Emitters, byte[] Widths, byte[] Heights, long NextId = 1, uint Counter = 0, int RetiredCount = 0)
{
    public Ff7WorldEffectParticle[]? DisplayParticles { get; init; }
    public byte[]? DisplayWidths { get; init; }
    public byte[]? DisplayHeights { get; init; }
}

public sealed partial class Ff7WorldRuntime
{
    private Ff7WorldEffectTable? effectTable;
    private Ff7WorldEffectsState? effects;

    private void EnsureWorldEffects()
    {
        effectTable ??= Ff7PcExecutableData.LoadWorldEffects(inputInstallation);
        effects ??= new([], new Ff7WorldEffectEmitter?[2], effectTable.Sprites.Select(sprite => (byte)sprite.Width).ToArray(), effectTable.Sprites.Select(sprite => (byte)sprite.Height).ToArray(), Counter: unchecked((uint)tick));
    }

    private static Ff7WorldEffectsState? CopyWorldEffects(Ff7WorldEffectsState? state)
    {
        return state is null ? null : state with { Particles = state.Particles.ToArray(), Emitters = state.Emitters.ToArray(), Widths = state.Widths.ToArray(), Heights = state.Heights.ToArray(), DisplayParticles = state.DisplayParticles?.ToArray(), DisplayWidths = state.DisplayWidths?.ToArray(), DisplayHeights = state.DisplayHeights?.ToArray() };
    }

    private bool ExecuteWorldEffect(WorldHostRequest request)
    {
        WorldOperation operation = request.Command.Operation;
        if (operation is not (WorldOperation.Effect or WorldOperation.EffectStart or WorldOperation.EffectStop))
        {
            return false;
        }
        EnsureWorldEffects();
        int[] arguments = request.Command.Arguments;
        if (operation == WorldOperation.Effect)
        {
            EmitWorldEffect(ResolveHostEntity(request), arguments[0]);
            return true;
        }
        int slot = arguments[0];
        if ((uint)slot >= 2)
        {
            throw new InvalidDataException("FFVII world effect emitter index is outside its two native slots.");
        }
        effects!.Emitters[slot] = operation == WorldOperation.EffectStart ? new(unchecked((sbyte)request.ActiveModel), unchecked((sbyte)arguments[1]), unchecked((sbyte)arguments[2])) : null;
        return true;
    }

    private void SpawnWorldEffect(Ff7WorldEntity entity, int sequence, Vector3 offset, Vector3 velocity, int positionAngle = 0, int velocityAngle = 0, int gravity = 0, int? groundAngle = null, Vector3? origin = null)
    {
        EnsureWorldEffects();
        if (effects!.Particles.Length + effects.RetiredCount == 32)
        {
            return;
        }
        Ff7WorldEffectSequence start = effectTable!.Sequences[sequence];
        Ff7WorldEffectSprite sprite = effectTable.Sprites[start.FirstSprite];
        Vector3 position = origin ?? new(entity.PreviousX, entity.PreviousY ?? entity.Y, entity.PreviousZ);
        Vector3 translated = inputMath.TransformRotation(offset, 0, positionAngle, 0);
        Vector3 motion = inputMath.TransformRotation(velocity, 0, velocityAngle, 0);
        bool fadeIn = (sprite.Flags & 0x1C) == 8;
        Ff7WorldEffectParticle particle = new(effects.NextId, (int)position.X + (int)translated.X, (int)position.Y + (int)translated.Y, (int)position.Z + (int)translated.Z, unchecked((short)(int)motion.X), unchecked((short)(int)motion.Y), unchecked((short)(int)motion.Z), unchecked((short)((groundAngle ?? entity.Direction) ^ 2048)), start.FirstSprite, sprite.Duration & 127, start.GroundAligned, unchecked((byte)gravity), fadeIn ? 0 : 128, false, fadeIn ? 0x80000000u : 0x80808080u);
        effects = effects with { Particles = [particle, .. effects.Particles], NextId = checked(effects.NextId + 1) };
    }

    private void EmitWorldEffect(Ff7WorldEntity entity, int type)
    {
        int direction = entity.Direction;
        switch (type)
        {
            case 0x14:
            case 0x15:
            case 0x16:
            case 0x17:
                SpawnWorldEffect(entity, type, new(0, viewMode != 0 ? 300 : 600, -200), new(0, type == 0x17 ? 50 : 0, 0), direction);
                break;
            case 1:
                foreach (var ray in new[] { (0, 500, 0, 30, 0x1C), (0, -500, 0, -30, 0x20), (353, 353, 21, 21, 0x23), (-353, 353, -21, 21, 0x1D), (-353, -353, -21, -21, 0x1F), (353, -353, 21, -21, 0x21), (500, 0, 30, 0, 0x22), (-500, 0, -30, 0, 0x1E) })
                {
                    SpawnWorldEffect(entity, ray.Item5, new(ray.Item1, 0, ray.Item2), new(ray.Item3, 0, ray.Item4), groundAngle: 0);
                }
                break;
            case 2:
                int angle = WorldRandom.Next() << 4;
                SpawnWorldEffect(entity, 0x31, new(0, 0, 500), new(inputMath.Cosine(angle) >> 6, 120, 0), angle, gravity: 20);
                break;
            case 3:
                SpawnWorldEffect(entity, 0x38, new(0, 300, -1000), new(0, 20, -10), direction, direction + WorldRandom.Next() * 8 - 1024, 1);
                break;
            case 4:
                SpawnWorldEffect(entity, 0x39, new(30, 400, 500), Vector3.Zero, direction);
                SetWorldEffectSize(0x39, 11);
                break;
            case 5:
                SpawnWorldEffect(entity, 0x3A, new(30, 400, 500), Vector3.Zero, direction);
                break;
            case 6:
                SpawnWorldEffect(entity, 7, Vector3.Zero, new(0, 50, 0), groundAngle: 0);
                SetWorldEffectSize(7, 16);
                break;
        }
    }

    private void SetWorldEffectSize(int sequence, int size)
    {
        int sprite = effectTable!.Sequences[sequence].FirstSprite;
        effects!.Widths[sprite] = (byte)size;
        effects.Heights[sprite] = (byte)size;
    }

    private void EmitHighwindTakeoffEffect()
    {
        Ff7WorldEntity aircraft = RequireEntity(controlledModel);
        SpawnWorldEffect(aircraft, 7, Vector3.Zero, new(0, 50, 0), groundAngle: 0, origin: new(aircraft.X, aircraft.Y, aircraft.Z));
        SetWorldEffectSize(7, 16);
    }

    private void AdvanceWorldEffects()
    {
        EmitWorldTerrainEffects();
        if (effects is null)
        {
            return;
        }
        effects = effects with { DisplayParticles = effects.Particles.ToArray(), DisplayWidths = effects.Widths.ToArray(), DisplayHeights = effects.Heights.ToArray() };
        int retired = 0;
        List<Ff7WorldEffectParticle> live = [];
        foreach (Ff7WorldEffectParticle particle in effects!.Particles)
        {
            Ff7WorldEffectSprite sprite = effectTable!.Sprites[particle.Sprite];
            int intensity = particle.Intensity;
            bool falling = particle.Falling;
            uint color = particle.Color;
            int vy = unchecked((short)(particle.VelocityY - particle.Gravity));
            int gravity = particle.Gravity;
            if (sprite.Flags == 2)
            {
                intensity = unchecked((short)(intensity - 1));
                if (particle.Sprite == 0x7C)
                {
                    uint channel = unchecked((byte)((byte)color - 1));
                    color = color & 0xFF000000 | channel * 0x010101;
                }
                else
                {
                    color = (uint)unchecked((byte)((color >> 24) - 1)) << 24;
                }
            }
            else
            {
                switch (sprite.Flags & 0x1C)
                {
                    case 4:
                        intensity = Math.Max((short)0, unchecked((short)(intensity - 12)));
                        color = color & 0xFF000000 | (uint)unchecked((byte)intensity) * 0x010101;
                        effects.Widths[particle.Sprite] = effects.Heights[particle.Sprite] = unchecked((byte)(effects.Heights[particle.Sprite] + 16));
                        break;
                    case 8:
                        intensity = falling ? Math.Max((short)0, unchecked((short)(intensity - 8))) : unchecked((short)(intensity + 8));
                        if (falling)
                        {
                            color = color & 0xFF000000 | (uint)unchecked((byte)intensity) * 0x010101;
                        }
                        falling |= unchecked((byte)intensity) >= 128;
                        break;
                    case 12 when !falling:
                        falling = true;
                        vy = 200;
                        gravity = 30;
                        break;
                    case 16:
                    case 20:
                        intensity = Math.Max((short)0, unchecked((short)(intensity - ((sprite.Flags & 0x1C) == 16 ? 16 : 8))));
                        color = color & 0xFF000000 | (uint)unchecked((byte)intensity) * 0x010101;
                        break;
                    case 24:
                        int size = effects.Heights[particle.Sprite];
                        size += effects.Widths[particle.Sprite] < 32 ? (effects.Widths[particle.Sprite] & 15) != 0 ? 3 : -5 : 0;
                        effects.Widths[particle.Sprite] = effects.Heights[particle.Sprite] = unchecked((byte)size);
                        break;
                }
            }
            int frame = particle.Sprite;
            int remaining = unchecked((byte)(particle.Remaining - 1));
            if (remaining == 0)
            {
                frame++;
                if (frame >= effectTable.Sprites.Count)
                {
                    throw new InvalidDataException("FFVII world effect stepped outside its original sprite sequence.");
                }
                remaining = effectTable.Sprites[frame].Duration;
                if ((remaining & 128) != 0)
                {
                    if ((sprite.Flags & 0x1C) != 24)
                    {
                        retired++;
                        continue;
                    }
                    do
                    {
                        frame--;
                        remaining = effectTable.Sprites[frame].Duration;
                    }
                    while ((remaining & 128) == 0);
                    remaining &= 127;
                }
            }
            live.Add(particle with { X = unchecked(particle.X + particle.VelocityX), Y = unchecked(particle.Y + particle.VelocityY), Z = unchecked(particle.Z + particle.VelocityZ), VelocityY = vy, Gravity = gravity, Intensity = intensity, Falling = falling, Color = color, Sprite = frame, Remaining = remaining });
        }
        effects = effects with { Particles = live.ToArray(), Counter = unchecked(effects.Counter + 1), RetiredCount = retired };
        for (int slot = 0; slot < effects.Emitters.Length; slot++)
        {
            if (effects.Emitters[slot] is not { Delay: > 0 } emitter)
            {
                continue;
            }
            int countdown = unchecked((sbyte)(emitter.Countdown - 1));
            if (emitter.Countdown <= 1)
            {
                if (Machine.IsModelAttached(emitter.Model))
                {
                    countdown = emitter.Delay;
                    EmitWorldEffect(RequireEntity(emitter.Model), emitter.Type);
                }
                else
                {
                    effects.Emitters[slot] = null;
                    continue;
                }
            }
            effects.Emitters[slot] = emitter with { Countdown = countdown };
        }
    }

    private static void ValidateWorldEffects(Ff7WorldEffectsState? state)
    {
        if (state is null)
        {
            return;
        }
        if (state.Particles is null || state.Particles.Length > 32 || state.RetiredCount < 0 || state.RetiredCount + state.Particles.Length > 32 || state.Emitters is not { Length: 2 } || state.Widths is not { Length: 145 } || state.Heights is not { Length: 145 } || state.NextId < 1 || state.Particles.Any(particle => !ValidWorldEffectParticle(particle, state.NextId)) || state.Particles.Select(particle => particle.Id).Distinct().Count() != state.Particles.Length || state.DisplayParticles is { } displayed && (displayed.Length > 32 || displayed.Any(particle => !ValidWorldEffectParticle(particle, state.NextId)) || displayed.Select(particle => particle.Id).Distinct().Count() != displayed.Length) || state.DisplayWidths is { Length: not 145 } || state.DisplayHeights is { Length: not 145 } || state.Emitters.Any(emitter => emitter is { } entry && (entry.Model is < sbyte.MinValue or > sbyte.MaxValue || entry.Type is < sbyte.MinValue or > sbyte.MaxValue || entry.Delay is < sbyte.MinValue or > sbyte.MaxValue || entry.Countdown is < sbyte.MinValue or > sbyte.MaxValue)))
        {
            throw new InvalidDataException("FFVII world effects have corrupt native particle or emitter storage.");
        }
    }

    private static bool ValidWorldEffectParticle(Ff7WorldEffectParticle? particle, long nextId)
    {
        return particle is not null && particle.Id >= 1 && particle.Id < nextId && particle.Sprite is >= 0 and < 145 && particle.Remaining is >= 0 and <= 255 && particle.Gravity is >= 0 and <= 255 && particle.Angle is >= short.MinValue and <= short.MaxValue && particle.VelocityX is >= short.MinValue and <= short.MaxValue && particle.VelocityY is >= short.MinValue and <= short.MaxValue && particle.VelocityZ is >= short.MinValue and <= short.MaxValue && particle.Intensity is >= short.MinValue and <= short.MaxValue;
    }

    private void RestoreWorldEffects(Ff7WorldEffectsState? state)
    {
        effectTable = state is null ? effectTable : effectTable ?? Ff7PcExecutableData.LoadWorldEffects(inputInstallation);
        effects = CopyWorldEffects(state);
    }
}
