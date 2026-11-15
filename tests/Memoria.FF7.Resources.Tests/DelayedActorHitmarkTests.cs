using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class DelayedActorHitmarkTests
{
    [Fact]
    public void D7DecodesTwoUnsignedBytesAndRetainsTheFollowingInstructionBoundary()
    {
        IReadOnlyList<BattleActionInstruction> instructions = BattleActionInstructions.ReadUntilIdle(new byte[] { 0xD7, 255, 128, 0xEE }, 1);
        Assert.Equal(2, instructions.Count);
        Assert.Equal(new byte[] { 255, 128 }, instructions[0].Operands.ToArray());
        Assert.Equal(3, instructions[1].Offset);
        Assert.Throws<InvalidDataException>(() => BattleActionInstructions.ReadUntilIdle(new byte[] { 0xD7, 1 }, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(255)]
    public void DelayedHitmarkUsesItsExecutingActorAtDueTimeAndFreezesTheSharedSpriteAnchor(int delay)
    {
        Ff7BattleHitmarkDefinition definition = new(44, [new(0, 0, 0, 0, 4096, 0, 2)]);
        Ff7BattleSecondaryState state = Ff7BattleSecondaryEffects.ScheduleActorHitmark(new(), 7, definition, delay);
        int queried = 0;
        int frame = 0;
        Ff7BattleSecondaryState cold = JsonSerializer.Deserialize<Ff7BattleSecondaryState>(JsonSerializer.Serialize(state))!;
        for (; frame < delay + 3; frame++)
        {
            Vector3 currentAnchor = new(100 + frame * 10, 20, 30);
            int before = queried;
            state = Advance(state, actor =>
            {
                Assert.Equal(7, actor);
                queried++;
                return (currentAnchor, 87);
            });
            cold = Advance(cold, _ => (currentAnchor, 87));
            Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(cold));
            if (frame < delay)
            {
                Assert.Equal(before, queried);
                Assert.Empty(state.Hitmarks);
            }
            else if (frame < delay + 2)
            {
                Ff7BattleHitmarkState hitmark = Assert.Single(state.Hitmarks);
                Assert.Equal(7, hitmark.Target);
                Assert.Equal(new Ff7BattleBraverPoint(checked((short)(100 + delay * 10)), 20, 30), hitmark.Anchor);
                Assert.Equal(87, hitmark.CollisionRadius);
                Assert.False(hitmark.Flipped);
                Assert.Equal(frame - delay, hitmark.Frame);
            }
        }
        Assert.Equal(1, queried);
        Assert.False(state.HasCallbacks);
        Assert.Empty(state.Hitmarks);
    }

    [Fact]
    public void DelayedHitmarkRetainsNativePhysicalSlotOrderingAndRequiresItsAnchorOwner()
    {
        Ff7BattleHitmarkDefinition definition = new(44, [new(0, 0, 0, 0, 4096, 0, 1)]);
        Ff7BattleSecondaryState state = Ff7BattleSecondaryEffects.ScheduleActorHitmark(new(), 4, definition, 0);
        state = Ff7BattleSecondaryEffects.ScheduleActorHitmark(state, 5, definition, 0);
        state = Advance(state, actor => (new(actor, 0, 0), 10));
        Assert.Equal(new[] { 4, 5 }, state.Hitmarks.Select(hitmark => hitmark.Target));
        Assert.Throws<NotSupportedException>(() => Ff7BattleSecondaryEffects.Advance(Ff7BattleSecondaryEffects.ScheduleActorHitmark(new(), 4, definition, 0), (_, _, _) => Vector3.Zero, _ => default, () => throw new InvalidOperationException("Hitmarks must not draw RNG.")));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ff7BattleSecondaryEffects.ScheduleActorHitmark(new(), 3, definition, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ff7BattleSecondaryEffects.ScheduleActorHitmark(new(), 4, definition, 256));
    }

    private static Ff7BattleSecondaryState Advance(Ff7BattleSecondaryState state, Func<int, (Vector3 Position, int CollisionRadius)> anchor)
    {
        return Ff7BattleSecondaryEffects.Advance(state, (_, _, _) => throw new InvalidOperationException("A hitmark requires its shared frozen joint owner."), _ => throw new InvalidOperationException("Hitmarks must not sample actor rotation."), () => throw new InvalidOperationException("Hitmarks must not draw RNG."), anchor);
    }
}
