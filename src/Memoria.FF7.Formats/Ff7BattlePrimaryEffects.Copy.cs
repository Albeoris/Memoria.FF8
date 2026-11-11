namespace Memoria.FF7.Formats;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState Copy(Ff7BattlePrimaryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state with
        {
            Slots = state.Slots.Select(callback => callback switch
            {
                Ff7BattlePrimaryBeatRushTrail trail => trail with { Samples = trail.Samples.ToArray() },
                _ => callback
            }).ToArray(),
            Draws = state.Draws.Select(draw => draw switch
            {
                Ff7BattlePrimaryBeatRushDraw beatRush => beatRush with { State = beatRush.State with { Trails = beatRush.State.Trails?.Select(trail => trail with { Samples = trail.Samples.ToArray() }).ToArray(), Sprites = beatRush.State.Sprites?.ToArray(), Primary = beatRush.State.Primary is { } beatRushPrimary ? Copy(beatRushPrimary) : null } },
                Ff7BattlePrimaryBigShotDraw bigShot => bigShot with { State = bigShot.State with { Particles = bigShot.State.Particles?.ToArray(), Primary = bigShot.State.Primary is { } bigShotPrimary ? Copy(bigShotPrimary) : null } },
                Ff7BattlePrimaryRocketDraw rocket => rocket with { State = rocket.State with { Targets = rocket.State.Targets.ToArray(), Explosions = rocket.State.Explosions.ToArray(), Missiles = rocket.State.Missiles.ToArray(), Sprites = rocket.State.Sprites.ToArray(), Points = rocket.State.Points.ToArray(), Primary = rocket.State.Primary is { } rocketPrimary ? Copy(rocketPrimary) : null } },
                Ff7BattlePrimaryBraverDraw braver => braver with { State = braver.State with { Primary = braver.State.Primary is { } braverPrimary ? Copy(braverPrimary) : null } },
                _ => draw
            }).ToArray(),
            PositionedSounds = state.PositionedSounds.ToArray(),
            WaveSparks = state.WaveSparks.ToArray(),
            ActorWrites = state.ActorWrites.ToArray(),
            ActorSounds = state.ActorSounds.ToArray(),
            BossDeathShakes = state.BossDeathShakes.ToArray(),
            SoundIds = state.SoundIds.ToArray(),
            Hitmarks = state.Hitmarks.ToArray(),
            TargetReactions = state.TargetReactions.ToArray(),
            Bubbles = state.Bubbles with { Particles = state.Bubbles.Particles.ToArray() },
            SilkLines = state.SilkLines with { Slots = state.SilkLines.Slots.ToArray() }
        };
    }
}
