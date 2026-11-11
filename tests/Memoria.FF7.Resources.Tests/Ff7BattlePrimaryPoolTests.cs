using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7BattlePrimaryPoolTests
{
    [Fact]
    public void RocketPreservesNativePhysicalCallbacksAndRandomOrderAcrossRestore()
    {
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleRocket(new(), 5, 0x70, 4);
        Ff7BattlePrimaryState? restored = null;
        int random = 0;
        int restoredRandom = 0;
        int missiles = 0;
        int points = 0;
        int exhaust = 0;
        int smoke = 0;
        int explosions = 0;
        List<int> contacts = [];
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new InvalidOperationException(), _ => default, _ => throw new InvalidOperationException(), () => ++random, JointPoint: (_, selector) => selector == 9 ? new(0, -500, 0) : new(900, -800, 1800));
        for (int frame = 0; frame <= 48; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            if (restored is not null)
            {
                restored = Ff7BattlePrimaryEffects.Advance(restored, context with { NextCrt = () => ++restoredRandom });
                Assert.Equal(random, restoredRandom);
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
            }
            Ff7BattleRocketState[] draws = state.Draws.OfType<Ff7BattlePrimaryRocketDraw>().Select(draw => draw.State).ToArray();
            missiles += draws.Sum(draw => draw.Missiles.Count);
            points += draws.Sum(draw => draw.Points.Count);
            exhaust += draws.Sum(draw => draw.Sprites.Count(sprite => sprite.Kind == 0));
            smoke += draws.Sum(draw => draw.Sprites.Count(sprite => sprite.Kind == 1));
            explosions += draws.Sum(draw => draw.Sprites.Count(sprite => sprite.Kind == 2));
            foreach (Ff7BattlePrimaryReactionReceipt receipt in state.TargetReactions)
            {
                Assert.Equal(20 + (receipt.Target - 4) * 4, frame);
                contacts.Add(receipt.Target);
            }
            if (frame == 12)
            {
                restored = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
                restoredRandom = random;
            }
            if (frame == 47)
            {
                Assert.False(state.HasCallbacks);
            }
        }
        Assert.Equal(1854, random);
        Assert.Equal(60, missiles);
        Assert.Equal(600, points);
        Assert.Equal(60, exhaust);
        Assert.Equal(504, smoke);
        Assert.Equal(288, explosions);
        Assert.Equal(new[] { 4, 5, 6 }, contacts);
    }

    [Fact]
    public void SmogRetainsControllerCloudAndLeafLifetimesInPhysicalSlots()
    {
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleSmog(new(), 5, 0x10, 8);
        Ff7BattlePrimaryState? restored = null;
        int random = 0;
        int projectiles = 0;
        int cloudParticles = 0;
        int cloudDraws = 0;
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new InvalidOperationException(), _ => default, _ => throw new InvalidOperationException(), () => { random++; return 0; }, JointPoint: (actor, selector) => selector == 9 ? new(0, -500, 0) : new(1200, -800, 1800));
        for (int frame = 0; frame <= 49; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            if (restored is not null)
            {
                restored = Ff7BattlePrimaryEffects.Advance(restored, context with { NextCrt = () => 0 });
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
            }
            Ff7BattlePrimarySmogSpriteDraw[] sprites = state.Draws.OfType<Ff7BattlePrimarySmogSpriteDraw>().ToArray();
            projectiles += sprites.Count(draw => draw.Sprite.Velocity is not null);
            cloudParticles += sprites.Count(draw => draw.Sprite.Velocity is null);
            cloudDraws += state.Draws.OfType<Ff7BattlePrimarySmogCloudDraw>().Count();
            Assert.Equal(frame == 12 ? (ushort)0x10 : (ushort)0, state.ImpactTargets);
            if (frame == 0)
            {
                Assert.Equal(new Ff7BattleBraverPoint(0, -500, 0), sprites[0].Sprite.Position);
                Assert.Equal(new Ff7BattleBraverPoint(-50, -488, -75), sprites[1].Sprite.Position);
            }
            if (frame == 12)
            {
                restored = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
            }
            if (frame == 39)
            {
                Assert.DoesNotContain(state.Slots, callback => callback is Ff7BattlePrimarySmogController);
            }
            if (frame == 41)
            {
                Assert.DoesNotContain(state.Slots, callback => callback is Ff7BattlePrimarySmogCloud);
            }
            if (frame == 48)
            {
                Assert.Single(sprites);
                Assert.False(state.HasCallbacks);
            }
        }
        Assert.Equal(90, random);
        Assert.Equal(216, projectiles);
        Assert.Equal(240, cloudParticles);
        Assert.Equal(30, cloudDraws);
    }

    [Fact]
    public void SmokeShotGroupsTargetsAndRegistersSpecializedMarkBeforeReaction()
    {
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleSmokeShot(new(), 5, 0x70, 3);
        Ff7BattlePrimaryState? restored = null;
        int draws = 0;
        List<int> contacts = [];
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new InvalidOperationException(), _ => default, _ => throw new InvalidOperationException(), () => throw new InvalidOperationException(), JointPoint: (actor, selector) => selector == 8 ? new(0, -500, 0) : new(actor * 160, -820, 1600));
        for (int frame = 0; frame <= 33; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            if (restored is not null)
            {
                restored = Ff7BattlePrimaryEffects.Advance(restored, context);
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
            }
            Ff7BattlePrimarySmokeShotDraw[] packets = state.Draws.OfType<Ff7BattlePrimarySmokeShotDraw>().ToArray();
            draws += packets.Length;
            if (frame == 0)
            {
                Assert.Equal(new Ff7BattleBraverPoint(40, -520, 100), Assert.Single(packets).Sprite.Position);
            }
            if (state.TargetReactions.Count > 0)
            {
                Ff7BattlePrimaryReactionReceipt receipt = Assert.Single(state.TargetReactions);
                Assert.Equal(15 + (receipt.Target - 4) * 4, frame);
                Assert.Equal(new Ff7BattlePrimaryHitmark(receipt.Target, 0x32), Assert.Single(state.Hitmarks));
                contacts.Add(receipt.Target);
            }
            if (frame == 12)
            {
                restored = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
            }
        }
        Assert.Equal(480, draws);
        Assert.Equal(new[] { 4, 5, 6 }, contacts);
        Assert.False(state.HasCallbacks);
    }

    [Fact]
    public void PrimaryCaptureCopiesNestedTrailAndParticleCollections()
    {
        Ff7BattleBeatRushSample original = new(new(1, 2, 3), new(4, 5, 6), new(7, 8, 9));
        Ff7BattleBeatRushSample[] samples = [original];
        Ff7BattleBigShotParticle particle = new(Ff7BattleBigShotParticleKind.Burst, 0, 0, new(1, 2, 3), new(4, 5, 6), 4096);
        Ff7BattleBigShotParticle[] particles = [particle];
        Ff7BattlePrimaryCallback?[] slots = new Ff7BattlePrimaryCallback?[100];
        slots[0] = new Ff7BattlePrimaryBeatRushTrail(2, 4, samples);
        Ff7BattlePrimaryState source = new() { Slots = slots, Draws = [new Ff7BattlePrimaryBeatRushDraw(new(4, 2, Trails: [new(1, 4, samples)])), new Ff7BattlePrimaryBigShotDraw(new(4, 22, Particles: particles))] };
        Ff7BattlePrimaryState copy = Ff7BattlePrimaryEffects.Copy(source);
        samples[0] = original with { Center = new(99, 99, 99) };
        particles[0] = particle with { Scale = 99 };
        Assert.Equal(original, Assert.IsType<Ff7BattlePrimaryBeatRushTrail>(copy.Slots[0]).Samples[0]);
        Assert.Equal(original, Assert.IsType<Ff7BattlePrimaryBeatRushDraw>(copy.Draws[0]).State.Trails![0].Samples[0]);
        Assert.Equal(particle, Assert.IsType<Ff7BattlePrimaryBigShotDraw>(copy.Draws[1]).State.Particles![0]);
    }

    [Fact]
    public void BigShotUsesNativeLeafSlotsAndDrawBeforeMovementThroughColdRestore()
    {
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleBigShot(new(), 1, 0x10, 23000, -14000, 5);
        Ff7BattlePrimaryState? restored = null;
        int random = 0;
        int charges = 0;
        int inflows = 0;
        int fragments = 0;
        int bursts = 0;
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new InvalidOperationException(), _ => default, _ => throw new InvalidOperationException(), () => { random++; return 0; }, JointPoint: (_, _) => new(900, -10000, 1800), WeaponPoint: (_, offset) => offset + new Vector3(0, -10000, 0));
        for (int frame = 0; frame <= 44; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            if (restored is not null)
            {
                restored = Ff7BattlePrimaryEffects.Advance(restored, context with { NextCrt = () => 0 });
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
            }
            Ff7BattleBigShotState[] draws = state.Draws.OfType<Ff7BattlePrimaryBigShotDraw>().Select(draw => draw.State).ToArray();
            charges += draws.Count(draw => draw.Charge is not null);
            Ff7BattleBigShotParticle[] particles = draws.SelectMany(draw => draw.Particles ?? []).ToArray();
            inflows += particles.Count(particle => particle.Kind == Ff7BattleBigShotParticleKind.Charge);
            fragments += particles.Count(particle => particle.Kind == Ff7BattleBigShotParticleKind.Fragment);
            bursts += particles.Count(particle => particle.Kind == Ff7BattleBigShotParticleKind.Burst);
            Assert.Equal(frame == 22 ? (ushort)0x10 : (ushort)0, state.ImpactTargets);
            if (frame == 22)
            {
                Assert.Equal(81, state.OccupiedSlotCount);
                Assert.Equal(80, particles.Length);
                Assert.All(particles, particle => Assert.Equal(new Ff7BattleBraverPoint(900, -10000, 1800), particle.Position));
                Assert.NotEqual(particles[0].Position, state.Slots.OfType<Ff7BattlePrimaryBigShotParticle>().First().Particle.Position);
                restored = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
            }
        }
        Assert.Equal(812, random);
        Assert.Equal(22, charges);
        Assert.Equal(224, inflows);
        Assert.Equal(1100, fragments);
        Assert.Equal(660, bursts);
        Assert.False(state.HasCallbacks);
    }

    [Fact]
    public void BeatRushDrawsRetainedStripBeforeRemovingOldestSample()
    {
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleBeatRush(new(), 2, 0x10, 9);
        Ff7BattlePrimaryState? restored = null;
        int frame = -1;
        int random = 0;
        int sprites = 0;
        int strips = 0;
        int[] expectedFirst = [0, 1, 2, 3, 3, 2, 1, 0];
        int[] expectedSecond = [0, 1, 2, 3, 4, 5, 5, 4, 3, 2, 1, 0];
        Ff7BattlePrimaryContext context = new((source, bone, offset) =>
        {
            Assert.Equal(2, source);
            Assert.Equal(32, bone);
            return offset + new Vector3(frame * 10, -500, 100);
        }, _ => default, _ => throw new InvalidOperationException(), () => { random++; return 0; });
        for (frame = 0; frame <= 20; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            if (restored is not null)
            {
                restored = Ff7BattlePrimaryEffects.Advance(restored, context with { NextCrt = () => 0 });
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
            }
            Ff7BattleBeatRushState[] draws = state.Draws.OfType<Ff7BattlePrimaryBeatRushDraw>().Select(draw => draw.State).ToArray();
            Ff7BattleBeatRushTrail[] trails = draws.SelectMany(draw => draw.Trails ?? []).ToArray();
            sprites += draws.Sum(draw => draw.Sprites?.Count ?? 0);
            strips += trails.Sum(trail => trail.Samples.Count - 1);
            if (frame is >= 1 and <= 8)
            {
                Assert.Equal(expectedFirst[frame - 1], trails.Where(trail => trail.Duration == 4).Sum(trail => trail.Samples.Count - 1));
            }
            if (frame is >= 7 and <= 18)
            {
                Assert.Equal(expectedSecond[frame - 7], trails.Where(trail => trail.Duration == 6).Sum(trail => trail.Samples.Count - 1));
            }
            Assert.Equal(frame == 12 ? (ushort)0x10 : (ushort)0, state.ImpactTargets);
            if (frame == 4)
            {
                restored = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
            }
        }
        Assert.Equal(12, random);
        Assert.Equal(24, sprites);
        Assert.Equal(42, strips);
        Assert.False(state.HasCallbacks);
    }

    [Fact]
    public void BraverRetainsSeparateNativeChildrenAndFirstTrailPacketAcrossJson()
    {
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleBraver(new(), 0, 0x10, 7);
        Ff7BattlePrimaryState? restored = null;
        int frame = -1;
        int shines = 0;
        int trails = 0;
        int impacts = 0;
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new InvalidOperationException(), _ => default, _ => 350, () => throw new InvalidOperationException(), JointPoint: (_, _) => new(900, -200, 1800), WeaponPoint: (_, offset) => offset + new Vector3(frame * 10, -500, 100));
        for (frame = 0; frame <= 37; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            if (restored is not null)
            {
                restored = Ff7BattlePrimaryEffects.Advance(restored, context);
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
            }
            Ff7BattleBraverState[] draws = state.Draws.OfType<Ff7BattlePrimaryBraverDraw>().Select(draw => draw.State).ToArray();
            shines += draws.Count(draw => draw.Shine is not null);
            trails += draws.Count(draw => draw.Trail is not null);
            impacts += draws.Count(draw => draw.Impact is not null);
            Assert.Equal(frame == 26 ? (ushort)0x10 : (ushort)0, state.ImpactTargets);
            if (frame == 17)
            {
                Assert.DoesNotContain(draws, draw => draw.Trail is not null);
                Assert.Equal(3, state.OccupiedSlotCount);
                restored = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
            }
            if (frame == 18)
            {
                Ff7BattleBraverState trail = Assert.Single(draws, draw => draw.Trail is not null);
                Assert.Equal((short)180, trail.Trail!.Base.X);
                Assert.Equal((short)170, trail.PreviousTrail!.Base.X);
            }
            if (frame == 27)
            {
                Assert.Equal(2, state.OccupiedSlotCount);
            }
        }
        Assert.Equal(10, shines);
        Assert.Equal(19, trails);
        Assert.Equal(7, impacts);
        Assert.False(state.HasCallbacks);
    }

    [Fact]
    public void NativeHundredSlotAllocatorCannotReuseEarlierRetiredLeaves()
    {
        Ff7BattlePrimaryCallback?[] slots = new Ff7BattlePrimaryCallback?[100];
        for (int index = 0; index < 99; index++)
        {
            slots[index] = new Ff7BattlePrimaryCureParticle(Ff7BattleCureKind.Cure, new(0, 0, 0, 4096)) { Age = 8 };
        }
        slots[99] = new Ff7BattlePrimaryScopeController(0x10, new(0, 0, 0), new(new(4096, 0, 0), new(0, 4096, 0), new(0, 0, 4096)), 2800);
        Ff7BattlePrimaryState state = new() { Slots = slots };
        Assert.Equal(100, state.OccupiedSlotCount);
        Assert.Throws<InvalidOperationException>(() => Ff7BattlePrimaryEffects.Advance(state, Context(() => throw new InvalidOperationException())));
    }

    [Fact]
    public void ScopeRetainsExternalOccupancyAndPhysicalLeafRecyclingAcrossJson()
    {
        Ff7BattlePrimaryCallback?[] slots = new Ff7BattlePrimaryCallback?[100];
        for (int index = 0; index < 4; index++)
        {
            slots[index] = new Ff7BattlePrimaryCureParticle(Ff7BattleCureKind.Cure, new((short)index, 0, 0, 4096)) { Owner = 91 };
        }
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleSearchScope(new() { Slots = slots }, 0x10, new(0, -500, 0), new(0, -500, 2800), 7);
        Ff7BattlePrimaryState? restored = null;
        for (int frame = 0; frame <= 51; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, Context(() => throw new InvalidOperationException()));
            if (restored is not null)
            {
                restored = Ff7BattlePrimaryEffects.Advance(restored, Context(() => throw new InvalidOperationException()));
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
            }
            if (frame == 0)
            {
                Assert.Equal(new[] { 0, 1, 2, 3, 6, 7, 8, 9, 10, 11 }, state.Draws.Select(draw => draw.Slot));
                Assert.Equal(4, state.Draws.Count(draw => draw.Owner == 91));
            }
            if (frame == 14)
            {
                restored = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
            }
            if (frame == 20)
            {
                Ff7BattlePrimaryScopeSpriteDraw[] sprites = state.Draws.OfType<Ff7BattlePrimaryScopeSpriteDraw>().ToArray();
                Assert.Equal(new[] { 10, 11, 12, 13 }, sprites.Take(4).Select(draw => draw.Slot));
                Assert.All(sprites.Take(2), draw => Assert.True(draw is { Stage: 0, Frame: 0 }));
                Assert.All(sprites.Skip(2).Take(2), draw => Assert.True(draw is { Stage: 1, Frame: 11 }));
            }
            if (frame == 32)
            {
                Assert.Equal(new[] { new Ff7BattlePrimaryReactionReceipt(4, true) }, state.TargetReactions);
            }
            if (frame == 50)
            {
                Assert.False(state.HasCallbacks);
                Assert.Equal(2, state.Draws.Count);
            }
        }
        Assert.Empty(state.Draws);
        Assert.Equal((short)4, state.SearchScopeCounter);
        Assert.Equal((short)0, state.StageTransparency);
    }

    [Fact]
    public void CureBoltAndDestroyedShareOneClockRandomOwnerAndOrderedReceipts()
    {
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleCure(new(), 1, owner: 11);
        state = Ff7BattlePrimaryEffects.ScheduleBolt(state, 0x10, 12);
        state = Ff7BattlePrimaryEffects.ScheduleDestroyed(state, 0x20, 13);
        Ff7BattlePrimaryState? restored = null;
        int random = 0;
        int restoredRandom = 0;
        int cure = 0;
        int sparks = 0;
        int strikes = 0;
        int destroyed = 0;
        int pulses = 0;
        for (int frame = 0; frame < 45; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, Context(() => ++random));
            if (restored is not null)
            {
                restored = Ff7BattlePrimaryEffects.Advance(restored, Context(() => ++restoredRandom));
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
                Assert.Equal(random, restoredRandom);
            }
            cure += state.Draws.OfType<Ff7BattlePrimaryCureDraw>().Count();
            sparks += state.Draws.OfType<Ff7BattlePrimaryBoltSparkDraw>().Count();
            strikes += state.Draws.OfType<Ff7BattlePrimaryBoltStrikeDraw>().Count();
            destroyed += state.Draws.OfType<Ff7BattlePrimaryDestroyedSpriteDraw>().Count();
            pulses += state.Draws.OfType<Ff7BattlePrimaryDestroyedPulseDraw>().Count();
            if (frame == 0)
            {
                Assert.Equal(16, random);
                Assert.Equal(new[] { new Ff7BattlePrimaryReactionReceipt(4, true) }, state.TargetReactions);
                Ff7BattlePrimaryDestroyedSpriteDraw first = Assert.Single(state.Draws.OfType<Ff7BattlePrimaryDestroyedSpriteDraw>());
                Assert.Equal(new Ff7BattleBraverPoint(4912, -207, -486), first.Sprite.Position);
                Assert.Equal(6159, first.Sprite.Scale);
            }
            if (frame == 10)
            {
                restored = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
                restoredRandom = random;
            }
            if (frame == 29)
            {
                Assert.Equal(new[] { new Ff7BattlePrimaryReactionReceipt(0, true), new Ff7BattlePrimaryReactionReceipt(5, true) }, state.TargetReactions);
            }
        }
        Assert.False(state.HasCallbacks);
        Assert.Equal(488, random);
        Assert.Equal(540, cure);
        Assert.Equal(112, sparks);
        Assert.Equal(16, strikes);
        Assert.Equal(264, destroyed);
        Assert.Equal(30, pulses);
    }

    [Fact]
    public void LegacyCureImportKeepsElapsedParticlesAndNeverReplaysRandomDraws()
    {
        int random = 0;
        Ff7BattleCureState previous = Ff7BattleCureEffect.Start(1);
        for (int frame = 0; frame <= 5; frame++)
        {
            previous = Ff7BattleCureEffect.Advance(previous, _ => 5, Bone, () => ++random).State;
        }
        Ff7BattlePrimaryState exact = previous.Primary!;
        Ff7BattlePrimaryState imported = Ff7BattlePrimaryEffects.ImportCure(new() { Tick = previous.Frame }, previous with { Primary = null });
        Assert.Equal(60, random);
        int importedRandom = random;
        for (int frame = 6; frame <= 38; frame++)
        {
            exact = Ff7BattlePrimaryEffects.Advance(exact, Context(() => ++random));
            imported = Ff7BattlePrimaryEffects.Advance(imported, Context(() => ++importedRandom));
            Assert.Equal(random, importedRandom);
            Assert.Equal(exact.ImpactTargets, imported.ImpactTargets);
            Assert.Equal(CurePackets(exact), CurePackets(imported));
        }
        Assert.Equal(300, random);
        Assert.False(imported.HasCallbacks);
    }

    private static string[] CurePackets(Ff7BattlePrimaryState state)
    {
        return state.Draws.OfType<Ff7BattlePrimaryCureDraw>().Select(draw => JsonSerializer.Serialize(draw.Particle)).Order(StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public void FireAndIceRetainTheirOriginalControllerAndMeshAllocationTrees()
    {
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleFire(new(), 0x30, 21);
        state = Ff7BattlePrimaryEffects.ScheduleIce(state, 0x70, 22);
        Ff7BattlePrimaryState? restored = null;
        int random = 0;
        int restoredRandom = 0;
        int fire = 0;
        int ice = 0;
        for (int frame = 0; frame <= 23; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, Context(() => ++random));
            if (restored is not null)
            {
                restored = Ff7BattlePrimaryEffects.Advance(restored, Context(() => ++restoredRandom));
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
                Assert.Equal(random, restoredRandom);
            }
            fire += state.Draws.OfType<Ff7BattlePrimaryFireDraw>().Count();
            ice += state.Draws.OfType<Ff7BattlePrimaryIceDraw>().Count();
            if (frame == 0)
            {
                Assert.Equal(6, state.OccupiedSlotCount);
                Assert.Empty(state.Slots.OfType<Ff7BattlePrimaryIceInitializer>());
                Assert.Single(state.Slots.OfType<Ff7BattlePrimaryIceMesh>());
                Assert.Equal(new[] { new Ff7BattlePrimaryReactionReceipt(4, true), new Ff7BattlePrimaryReactionReceipt(5, true), new Ff7BattlePrimaryReactionReceipt(4, true) }, state.TargetReactions);
                Assert.Equal(new short[] { 4200, 5300 }, state.Draws.OfType<Ff7BattlePrimaryFireDraw>().Select(draw => draw.Sprite.X));
            }
            if (frame == 6)
            {
                restored = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
                restoredRandom = random;
            }
            if (frame == 8)
            {
                Assert.Equal(new short[] { 6000, 4000, 5000 }, state.Draws.OfType<Ff7BattlePrimaryIceDraw>().Select(draw => draw.Crystal.X));
            }
        }
        Assert.Equal(6, random);
        Assert.Equal(84, fire);
        Assert.Equal(45, ice);
        Assert.False(state.HasCallbacks);
    }

    [Fact]
    public void IceImportUsesSavedStartIdentityAfterSlotRecyclingChangesDrawOrder()
    {
        Ff7BattlePrimaryState shared = Ff7BattlePrimaryEffects.ScheduleFire(new(), 0x30, 21);
        shared = Ff7BattlePrimaryEffects.ScheduleIce(shared, 0x70, 22);
        int random = 0;
        for (int frame = 0; frame <= 8; frame++)
        {
            shared = Ff7BattlePrimaryEffects.Advance(shared, Context(() => ++random));
        }
        Ff7BattleIceState previous = Ff7BattlePrimaryEffects.ProjectIce(shared, new(0x70, 7), 22, retainPrimary: true);
        Assert.Equal(new[] { 8, 0, 4 }, previous.Crystals!.Select(crystal => crystal.StartedAt));
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new InvalidOperationException(), _ => default, _ => throw new InvalidOperationException(), () => throw new InvalidOperationException(), JointPoint: (_, _) => throw new InvalidOperationException());
        Ff7BattlePrimaryState exact = previous.Primary!;
        Ff7BattlePrimaryState imported = Ff7BattlePrimaryEffects.ImportIce(new() { Tick = previous.Frame }, previous with { Primary = null }, context);
        for (int frame = 9; frame <= 23; frame++)
        {
            exact = Ff7BattlePrimaryEffects.Advance(exact, context);
            imported = Ff7BattlePrimaryEffects.Advance(imported, context);
            string[] first = exact.Draws.OfType<Ff7BattlePrimaryIceDraw>().Select(draw => $"{draw.Crystal.X},{draw.Crystal.Y},{draw.Crystal.Z},{draw.Frame}").Order(StringComparer.Ordinal).ToArray();
            string[] second = imported.Draws.OfType<Ff7BattlePrimaryIceDraw>().Select(draw => $"{draw.Crystal.X},{draw.Crystal.Y},{draw.Crystal.Z},{draw.Frame}").Order(StringComparer.Ordinal).ToArray();
            Assert.Equal(first, second);
        }
    }

    private static Ff7BattlePrimaryContext Context(Func<int> random)
    {
        return new((actor, bone, point) => Bone(actor, bone) + point, _ => default, _ => 350, random, _ => 5, (actor, _) => Bone(actor, 0));
    }

    private static Vector3 Bone(int actor, int bone) => new(actor * 1000 + bone * 100, -bone * 20, -300);
}
