using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7BattlePrimaryPoolTests
{
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

    private static Ff7BattlePrimaryContext Context(Func<int> random)
    {
        return new((actor, bone, point) => Bone(actor, bone) + point, _ => default, _ => 350, random, _ => 5, (actor, _) => Bone(actor, 0));
    }

    private static Vector3 Bone(int actor, int bone) => new(actor * 1000 + bone * 100, -bone * 20, -300);
}
