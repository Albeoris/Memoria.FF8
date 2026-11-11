using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7BattlePrimaryBeamGunTests
{
    [Fact]
    public void NativeBeamGunPreservesMuzzleShiftShotOrderAndDrawCountsAcrossRestore()
    {
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleBeamGun(new(), 4, 1);
        Ff7BattlePrimaryState? cold = null;
        int random = 0;
        int coldRandom = 0;
        int meshes = 0;
        int flashes = 0;
        int bursts = 0;
        int peak = 0;
        Vector3 target = new(900, -800, 1800);
        Ff7BattlePrimaryContext context = new((_, _, _) => target, _ => default, _ => 350, () => ++random, JointPoint: (slot, selector) => (slot, selector) switch
        {
            (4, 6) => new(0, -650, 0),
            (0, 0) => target,
            _ => throw new InvalidOperationException()
        }, JointSelector: (_, _) => 0);
        for (int callback = 0; callback < 40; callback++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            Assert.Equal(callback == 15 ? 1 : 0, state.ImpactTargets);
            Ff7BattlePrimaryBeamGunMeshDraw[] shots = state.Draws.OfType<Ff7BattlePrimaryBeamGunMeshDraw>().ToArray();
            Ff7BattlePrimaryBeamGunSpriteDraw[] sprites = state.Draws.OfType<Ff7BattlePrimaryBeamGunSpriteDraw>().ToArray();
            meshes += shots.Length;
            flashes += sprites.Count(draw => draw.FlashBasis is not null);
            bursts += sprites.Count(draw => draw.FlashBasis is null);
            peak = Math.Max(peak, state.Slots.Count(slot => slot is not null));
            if (callback == 0)
            {
                Assert.Equal(new Ff7BattleBraverPoint(222, -685, 446), Assert.Single(sprites).Position);
                Assert.Equal(new Ff7BattleBraverPoint[] { new(314, -682, 346), new(147, -572, 439), new(140, -792, 426) }, shots.Select(shot => shot.Position));
            }
            if (callback == 9)
            {
                Ff7BattlePrimaryBeamGunSpriteDraw burst = Assert.Single(sprites);
                Assert.Equal(new Ff7BattleBraverPoint(531, -1168, 1433), burst.Position);
                Assert.Equal(350, burst.Radius);
            }
            if (cold is not null)
            {
                cold = Ff7BattlePrimaryEffects.Advance(cold, context with { NextCrt = () => ++coldRandom });
                Assert.Equal(random, coldRandom);
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(cold));
            }
            if (callback == 12)
            {
                cold = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
                coldRandom = random;
            }
        }
        Assert.Equal((315, 4, 152, 102, 25), (meshes, flashes, bursts, random, peak));
        Assert.False(state.HasCallbacks);
    }
}
