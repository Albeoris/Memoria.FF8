using System.Text.Json;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7BattlePrimaryHellBubblesTests
{
    [Fact]
    public void NativeBubblesRetainCallbackOrderAndRandomAcrossColdRestore()
    {
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleHellBubbles(new(), 4, 1);
        int random = 0;
        int coldRandom = 0;
        Ff7BattlePrimaryState? cold = null;
        int stream = 0;
        int burst = 0;
        int cloud = 0;
        int ribbon = 0;
        int peakCallbacks = 0;
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new InvalidOperationException(), _ => default, _ => 0, () => ++random, JointPoint: (slot, selector) => (slot, selector) switch
        {
            (4, 8) => new(0, -650, 0),
            (0, 0) => new(900, -800, 1800),
            _ => throw new InvalidOperationException()
        });
        for (int callback = 0; callback < 85; callback++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            Assert.Equal(callback == 60 ? 1 : 0, state.ImpactTargets);
            peakCallbacks = Math.Max(peakCallbacks, state.Slots.Count(slot => slot is not null));
            if (callback == 2)
            {
                Assert.Equal(new Ff7BattleBraverPoint(45, -651, 90), state.Draws.OfType<Ff7BattlePrimaryBubbleSpriteDraw>().First().Position);
            }
            if (callback == 21)
            {
                Assert.Equal(new Ff7BattleBraverPoint(361, -1338, 1263), Assert.Single(state.Draws.OfType<Ff7BattlePrimaryBubbleSpriteDraw>(), draw => draw.Burst).Position);
            }
            stream += state.Draws.OfType<Ff7BattlePrimaryBubbleSpriteDraw>().Count(draw => !draw.Burst);
            burst += state.Draws.OfType<Ff7BattlePrimaryBubbleSpriteDraw>().Count(draw => draw.Burst);
            cloud += state.Draws.OfType<Ff7BattlePrimaryBubbleCloudDraw>().Count();
            ribbon += state.Draws.OfType<Ff7BattlePrimaryBubbleRibbonDraw>().Count();
            if (cold is not null)
            {
                cold = Ff7BattlePrimaryEffects.Advance(cold, context with { NextCrt = () => ++coldRandom });
                Assert.Equal(random, coldRandom);
                Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(cold));
            }
            if (callback == 31)
            {
                cold = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
                coldRandom = random;
            }
        }
        Assert.Equal((2304, 228, 45, 40, 249), (stream, burst, cloud, ribbon, random));
        Assert.Equal(16, peakCallbacks);
        Assert.False(state.HasCallbacks);
        Assert.Equal(0, state.StageTransparency);
    }
}
