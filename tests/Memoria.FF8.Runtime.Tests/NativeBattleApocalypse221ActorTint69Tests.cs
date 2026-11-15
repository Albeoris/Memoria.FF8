using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleApocalypse221ActorTint69Tests
{
    [Theory]
    [InlineData(0, -1, 0x1234, 0x11223344u)]
    [InlineData(0, 0, 0x4D, 0x11223344u)]
    [InlineData(0, 128, 0x1A34, 0x02000080u)]
    [InlineData(3, -1, 0x1234, 0x11223344u)]
    [InlineData(3, 0, 0x4D, 0x11223344u)]
    [InlineData(-1, 128, 0x1A34, 0x02000080u)]
    public void Side_selection_and_restore_hide_tint_modes_preserve_original_actor_flags(sbyte side, short red, ushort expectedFlags, uint expectedColor)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        const uint header = NativeBattleScriptedEffectMemory.Address;
        frame = frame with { Core = frame.Core.Write(header + 0x48, new[] { unchecked((byte)side) }) };
        int sourceSlot = side < 0 ? 0 : side;
        int targetSlot = sourceSlot < 3 ? 3 : 0;
        BattleModelOwner source = OriginalOwner(sourceSlot);
        BattleModelOwner target = OriginalOwner(targetSlot);
        BattleActorModels models = OriginalModels([(sourceSlot, new(0)), (targetSlot, new(0x1234))]).Models;
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorContext.FromGraphics(models).WithCurrentActors([source]);
        frame = frame with { Core = frame.Core with { PresentationActors = context } };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, unchecked((ushort)red));
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 0);
        NativeBattleGraphicsModel actor = new(0x45) { Color40 = 0x11223344, Color44 = 0x55667788 };
        int writes = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleApocalypse221ActorTint69.Execute(frame, (current, selectedSide, combined, color) =>
        {
            Assert.Equal(target.Side, selectedSide);
            writes++;
            actor = NativeBattleApocalypse221ActorTint69.ApplyTint(actor, combined, color, () => NativeBattleCinematicActorAccess.ReadCachedFlags0(current, target));
            return current;
        });
        Assert.Equal(1, writes);
        Assert.Equal(expectedFlags, actor.Flags0);
        Assert.Equal(expectedColor, actor.Color40);
        Assert.Equal(0x55667788u, actor.Color44);
        Assert.Same(frame, result);
    }
}
