using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeGlobalRandomTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Graphicsless_stage_camera_selection_belongs_to_legacy_nonexpanded_sessions(bool expanded)
    {
        NativeGlobalRandomState before = new(3);
        NativeGlobalRandomState selected = before.Draw(out ushort roll);
        BattleSession battle = new(new BattleDefinition
        {
            UsesExpandedRoster = expanded,
            NativeGlobalRandom = before,
            NativeStageCameraBank = new([0, 0]),
            NativeStageCameraSelectors = [6, 7],
            NativeStageCameraControl = [2],
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            Combatants = [new() { Id = "player", Name = "Player", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);

        Assert.Null(battle.Presentation?.Graphics);
        Assert.Equal((byte)0, battle.NativeEntryAdvantage);
        NativeBattleCameraState camera = Assert.IsType<NativeBattleCameraState>(battle.Presentation?.Camera);
        if (expanded)
        {
            Assert.Equal(new NativeGlobalRandomChange(before, before, 0), battle.NativeGlobalRandomChange);
            Assert.Null(camera.ControlCursor);
            Assert.Equal(0, camera.Variables[0]);
        }
        else
        {
            Assert.Equal(new NativeGlobalRandomChange(before, selected, 1), battle.NativeGlobalRandomChange);
            Assert.Equal(0, camera.ControlCursor);
            Assert.Equal(6 + (roll & 1), camera.Variables[0]);
        }
    }

    [Fact]
    public void SavedGeneratorPreservesDwordOverflowAndFastAdvanceMatchesRepeatedCalls()
    {
        NativeGlobalRandomState start = new(uint.MaxValue);
        NativeGlobalRandomState repeated = start;
        for (int index = 0; index < 1000; index++)
        {
            uint next = unchecked(repeated.State * 69069u + 1);
            repeated = repeated.Draw(out ushort value);
            Assert.Equal(next, repeated.State);
            Assert.Equal((ushort)(next >> 17), value);
        }
        Assert.Equal(repeated, start.Advance(1000));
        Assert.Equal(start, start.Advance(0));
        new NativeGlobalRandomChange(start, repeated, 1000).Validate();
        Assert.Throws<InvalidDataException>(() => new NativeGlobalRandomChange(start, repeated, 999).Validate());
    }

    [Fact]
    public void BattleSnapshotRequiresItsExactSavedGeneratorAndCallReceipt()
    {
        BattleSession battle = new(new BattleDefinition
        {
            NativeGlobalRandom = new(123),
            Combatants = [new() { Id = "player", Name = "Player", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        BattleSnapshot saved = battle.Capture();
        Assert.Equal(123u, saved.NativeGlobalRandom!.State);
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(BattleSession.Restore(saved).Capture()));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { NativeGlobalRandom = null }));
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(saved with { NativeGlobalRandom = new(124) }));
        NativeGlobalRandomState advanced = saved.NativeGlobalRandom.Advance(3);
        BattleSession restored = BattleSession.Restore(saved with { NativeGlobalRandom = advanced, NativeGlobalRandomCalls = 3 });
        Assert.Equal(new NativeGlobalRandomChange(new(123), advanced, 3), restored.NativeGlobalRandomChange);
    }
}
