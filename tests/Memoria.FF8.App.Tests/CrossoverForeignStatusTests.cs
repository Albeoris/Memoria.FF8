using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverForeignStatusTests
{
    [Theory]
    [InlineData(0u, 0u)]
    [InlineData(0x4000u, 2u)]
    [InlineData(0x80u, 0x400000u)]
    [InlineData(0x100u, 0x800u)]
    [InlineData(0x400u, 0x10000000u)]
    [InlineData(0x4580u, 0x10400802u)]
    [InlineData(0x10u, 0u)]
    public void HostStatusFactsUseBothOriginalExecutableMaps(uint hostStatuses, uint expected)
    {
        sbyte[] saved = Enumerable.Repeat((sbyte)-1, 16).ToArray();
        sbyte[] battle = Enumerable.Repeat((sbyte)-1, 32).ToArray();
        saved[2] = 1;
        saved[4] = 22;
        battle[1] = 11;
        battle[3] = 28;
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, saved.ToImmutableArray(), battle.ToImmutableArray());
        Assert.Equal(expected, CrossoverNativeForeignTarget.ProjectHostStatusFlags(hostStatuses, rules));
        saved[2] = 7;
        NativeBattleGraphicsRules changed = rules with { SavedStatusMap = saved.ToImmutableArray() };
        Assert.Equal(0x80u, CrossoverNativeForeignTarget.ProjectHostStatusFlags(0x4000, changed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0u)]
    [InlineData(2u)]
    [InlineData(0x10400802u)]
    public void ForeignActorPreservesKnownAndUnknownProjectedStatusFlags(uint? flags)
    {
        NativeBattleForeignSurface surface = new(new NativeBattleForeignRenderMaterial(new(null), false), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        Ff7BattleActorGeometry geometry = new([], [Vector3.Zero])
        {
            GuestFaces = [(Vector3.Zero, Vector3.UnitX, Vector3.UnitY)],
            GuestSurfaces = [surface],
            Bones = [(Vector3.Zero, Vector3.UnitY)]
        };
        CrossoverNativeForeignTarget target = new(4, new(default, default, 0, 0, 4096), NativeBattleForeignModelGeometry.Identity, new(default, default, 0, new(default, default)), geometry, Matrix4x4.Identity) { NativeStatusFlags = flags };
        Assert.Equal(flags, target.ToActor(OriginalOwner(3)).StatusFlags);
    }
}
