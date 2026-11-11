using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7BattleTailLaserTests
{
    [Fact]
    public void OriginalSweepUsesBothPhysicalPoolsAndStaggersContacts()
    {
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.ScheduleTailLaser(new(), new(900, -900, 1100), new(0, 0, -1050), 3);
        Ff7BattleSecondaryState secondary = new();
        Ff7BattlePrimaryState? restoredPrimary = null;
        Ff7BattleSecondaryState? restoredSecondary = null;
        int random = 0;
        int restoredRandom = 0;
        int peakPrimary = 0;
        int peakSecondary = 0;
        Dictionary<int, int> meshes = [];
        Dictionary<int, int> sprites = [];
        List<(int Frame, int Target)> contacts = [];
        List<(int Frame, Ff7BattlePrimaryPositionedSound Cue)> sounds = [];
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new InvalidOperationException(), _ => default, _ => 0, () => ++random, JointPoint: (slot, _) => slot == 0 ? new(-1200, -450, -1000) : new(1200, -500, -1100));
        for (int frame = 0; frame < 54; frame++)
        {
            (primary, secondary) = Step(primary, secondary, context);
            if (restoredPrimary is not null)
            {
                (restoredPrimary, restoredSecondary) = Step(restoredPrimary, restoredSecondary!, context with { NextCrt = () => ++restoredRandom });
                Assert.Equal(random, restoredRandom);
                Assert.Equal(JsonSerializer.Serialize(primary), JsonSerializer.Serialize(restoredPrimary));
                Assert.Equal(JsonSerializer.Serialize(secondary), JsonSerializer.Serialize(restoredSecondary));
            }
            else if (frame == 4)
            {
                restoredPrimary = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(primary));
                restoredSecondary = JsonSerializer.Deserialize<Ff7BattleSecondaryState>(JsonSerializer.Serialize(secondary));
                restoredRandom = random;
            }
            if (frame == 0)
            {
                Ff7BattleTailLaserBeam beam = Assert.Single(primary.Slots.OfType<Ff7BattleTailLaserBeam>());
                Assert.Equal(new(4096, 0, -1050), beam.End);
                Ff7BattleTailLaserFragment fragment = Assert.Single(primary.Slots.OfType<Ff7BattleTailLaserFragment>());
                Assert.Equal(new(3649, -102, -1050), fragment.Position);
                Assert.Equal(7, fragment.Frame);
                Ff7BattleTailLaserMeshDraw beamDraw = Assert.Single(primary.Draws.OfType<Ff7BattleTailLaserMeshDraw>(), draw => draw.Resource == 2);
                Assert.Equal(new Ff7BattleSearchScopeBasis(new(-2286, 0, -3398), new(-5032, -1416, 3385), new(-772, 3988, 519)), beamDraw.Basis);
            }
            if (frame == 4)
            {
                Ff7BattleTailLaserSecondarySpark spark = secondary.Slots.OfType<Ff7BattleTailLaserSecondarySpark>().First();
                Assert.Equal(new(1200, 0, -1100), spark.Position);
                Assert.Equal(new(159, -5, 0), spark.Velocity);
                Assert.Equal(-20, spark.Pitch);
            }
            foreach (Ff7BattleTailLaserMeshDraw mesh in primary.Draws.OfType<Ff7BattleTailLaserMeshDraw>())
            {
                meshes[mesh.Resource] = meshes.GetValueOrDefault(mesh.Resource) + 1;
            }
            foreach (Ff7BattleTailLaserSprite sprite in primary.Draws.OfType<Ff7BattleTailLaserSpriteDraw>().Select(draw => draw.Sprite).Concat(secondary.TailLaserDraws))
            {
                sprites[sprite.Resource] = sprites.GetValueOrDefault(sprite.Resource) + 1;
            }
            contacts.AddRange(primary.TargetReactions.Select(reaction => (frame, reaction.Target)));
            sounds.AddRange(primary.PositionedSounds.Select(cue => (frame, cue)));
            peakPrimary = Math.Max(peakPrimary, primary.OccupiedSlotCount);
            peakSecondary = Math.Max(peakSecondary, secondary.Slots.Count(callback => callback is not null));
        }
        Assert.Equal([(24, 1), (28, 0)], contacts);
        Assert.Equal([(0, new Ff7BattlePrimaryPositionedSound(0x173, -1, 64)), (4, new Ff7BattlePrimaryPositionedSound(0x176, -1, 64, 2)), (8, new Ff7BattlePrimaryPositionedSound(0x176, -1, 64, 2))], sounds);
        Assert.Equal((326, 43, 32), (random, peakPrimary, peakSecondary));
        Assert.Equal(180, meshes[0]);
        Assert.Equal(15, meshes[1]);
        Assert.Equal(60, meshes[2]);
        Assert.Equal(15, sprites[3]);
        Assert.Equal(120, sprites[4]);
        Assert.Equal(176, sprites.GetValueOrDefault(5) + sprites.GetValueOrDefault(6));
        Assert.Equal(176, sprites.GetValueOrDefault(7) + sprites.GetValueOrDefault(8));
        Assert.Equal(120, sprites[10]);
        Assert.Equal(192, sprites[11]);
        Assert.Equal(576, sprites[12]);
        Assert.False(primary.HasCallbacks);
        Assert.False(secondary.HasCallbacks);
        Assert.Equal(0, primary.StageTransparency);

        static (Ff7BattlePrimaryState Primary, Ff7BattleSecondaryState Secondary) Step(Ff7BattlePrimaryState primary, Ff7BattleSecondaryState secondary, Ff7BattlePrimaryContext context)
        {
            secondary = Ff7BattleSecondaryEffects.Advance(secondary, context.ActorPoint, context.ActorRotation, () => throw new InvalidOperationException());
            primary = Ff7BattlePrimaryEffects.Advance(primary, context);
            foreach (Ff7BattleTailLaserSecondary callback in primary.TailLaserSecondaries.Cast<Ff7BattleTailLaserSecondary>())
            {
                secondary = Ff7BattleSecondaryEffects.ScheduleTailLaser(secondary, callback);
            }
            return (primary, secondary);
        }
    }
}
