using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldSharedStateEqualityTests
{
    [Fact]
    public void GroundCacheComparesIndependentArraysAndAllAlternativeOwners()
    {
        NativeWorldGroundIdentity current = new(1, 2, 3, 4);
        NativeWorldGroundIdentity previous = new(5, 6, 7, 8);
        NativeWorldGroundAlternative alternative = new(1, [current, previous], current, previous) { FailedSelections = 3 };
        NativeWorldGroundCacheState original = NativeWorldGroundCacheState.CreateFresh(current) with { Alternatives = [alternative, alternative with { PreferredGroup = 0 }] };
        NativeWorldGroundCacheState copy = Copy(original);
        Assert.NotSame(original.Alternatives, copy.Alternatives);
        Assert.NotSame(original.Alternatives[0].Entries, copy.Alternatives[0].Entries);
        Assert.True(NativeWorldGroundCacheState.ContentEquals(original, copy));
        NativeWorldGroundCacheState[] changed =
        [
            copy with { Version = 2 },
            copy with { Rule = "different" },
            copy with { Alternatives = copy.Alternatives.Reverse().ToArray() },
            copy with { Alternatives = [alternative] },
            copy with { Alternatives = [alternative with { PreferredGroup = 0 }, copy.Alternatives[1]] },
            copy with { Alternatives = [alternative with { FailedSelections = null }, copy.Alternatives[1]] },
            copy with { Alternatives = [alternative with { CurrentGround = current with { PolygonIndex = 9 } }, copy.Alternatives[1]] },
            copy with { Alternatives = [alternative with { PreviousGround = previous with { BlockIndex = 9 } }, copy.Alternatives[1]] },
            copy with { Alternatives = [alternative with { Entries = [current with { SourceId = 9 }, previous] }, copy.Alternatives[1]] },
            copy with { Alternatives = [alternative with { Entries = [previous, current] }, copy.Alternatives[1]] },
            copy with { Alternatives = [alternative with { Entries = [] }, copy.Alternatives[1]] }
        ];
        foreach (NativeWorldGroundCacheState candidate in changed)
        {
            Assert.False(NativeWorldGroundCacheState.ContentEquals(original, candidate));
            Assert.False(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(original), JsonSerializer.SerializeToElement(candidate)));
        }
        Assert.True(NativeWorldGroundCacheState.ContentEquals(null, null));
        Assert.False(NativeWorldGroundCacheState.ContentEquals(original, null));
        Assert.False(NativeWorldGroundCacheState.ContentEquals(null, original));
    }

    [Fact]
    public void ForestComparesIndependentParticlesAndNestedMotion()
    {
        NativeWorldForestParticle particle = new(0, 10, 100, -200, 300, 40, 50, 4096, 1, 12, true) { Motion = new(60, 70, 80, 90) };
        NativeWorldForestState original = NativeWorldForestState.Create(123, -5) with { Particles = [particle, particle with { Slot = 1 }] };
        NativeWorldForestState copy = Copy(original);
        Assert.NotSame(original.Particles, copy.Particles);
        Assert.NotSame(original.Particles[0].Motion, copy.Particles[0].Motion);
        Assert.True(NativeWorldForestState.ContentEquals(original, copy));
        NativeWorldForestState[] changed =
        [
            copy with { SeedCounter = 124 },
            copy with { RandomCursor = 124 },
            copy with { RandomOffset = 124 },
            copy with { CameraPitch = -111 },
            copy with { CameraCooldown = -4 },
            copy with { Particles = copy.Particles.Reverse().ToArray() },
            copy with { Particles = [particle] },
            copy with { Particles = [particle with { X = 101 }, copy.Particles[1]] },
            copy with { Particles = [particle with { Visible = false }, copy.Particles[1]] },
            copy with { Particles = [particle with { Motion = null }, copy.Particles[1]] },
            copy with { Particles = [particle with { Motion = particle.Motion! with { VelocityZ = 91 } }, copy.Particles[1]] }
        ];
        foreach (NativeWorldForestState candidate in changed)
        {
            Assert.False(NativeWorldForestState.ContentEquals(original, candidate));
            Assert.False(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(original), JsonSerializer.SerializeToElement(candidate)));
        }
        Assert.True(NativeWorldForestState.ContentEquals(null, null));
        Assert.False(NativeWorldForestState.ContentEquals(original, null));
        Assert.False(NativeWorldForestState.ContentEquals(null, original));
    }

    private static T Copy<T>(T value)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    }
}
