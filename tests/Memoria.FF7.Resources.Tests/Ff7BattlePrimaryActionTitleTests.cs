using System.Text.Json;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7BattlePrimaryActionTitleTests
{
    [Fact]
    public void NativeTitlePublishesThirtySixFramesAndRetiresOnTheFollowingCallback()
    {
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleActionTitle(new(), 0x20, 7, 128);
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), () => throw new InvalidOperationException());
        Assert.Null(state.ActionTitle);
        for (int frame = 0; frame < 36; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            Assert.Equal(new Ff7BattleActionTitle(0x20, 7), state.ActionTitle);
            Assert.True(state.HasCallbacks);
            if (frame == 18)
            {
                state = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
            }
        }
        state = Ff7BattlePrimaryEffects.Advance(state, context);
        Assert.Null(state.ActionTitle);
        Assert.False(state.HasCallbacks);
    }
}
