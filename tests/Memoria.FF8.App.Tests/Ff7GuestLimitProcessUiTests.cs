using System.Reflection;
using System.Runtime.CompilerServices;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7GuestLimitProcessUiTests
{
    [Fact]
    public void Duel_panel_shows_native_recipes_and_forwards_raw_button_state()
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Duel", 1, 4, nativeProcess: true);
        fixture.OpenNativeSelection();
        fixture.AdvanceToNativeInput();
        CrossoverGuestHostActions provider = fixture.ProcessProvider;
        Ff7Scene scene = Scene(fixture);
        KeyboardInput input = Get<KeyboardInput>(scene, "sharedInput");
        Set(input, "<HeldFieldButtons>k__BackingField", (ushort)0x20);
        Assert.True(Read(scene));
        Assert.Equal((ushort)0x20, Get<ushort>(provider, "processButtons"));
        BattleUiTests.TestCanvas canvas = Draw(scene);
        foreach (BattleNativeDuelChoice choice in provider.DuelProcessChoices.Where(choice => choice.Learned))
        {
            Assert.Contains(choice.Name, canvas.Texts);
            Assert.Contains(string.Join(" ", choice.Buttons.Select(input.NativeButtonBindingHint)), canvas.Texts);
        }
    }

    [Fact]
    public void Shot_panel_forwards_fire_and_finish_to_native_process()
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Shot", 2, 4, nativeProcess: true);
        fixture.OpenNativeSelection(0x0E00);
        fixture.AdvanceToNativeInput();
        CrossoverGuestHostActions provider = fixture.ProcessProvider;
        Ff7Scene scene = Scene(fixture);
        KeyboardInput input = Get<KeyboardInput>(scene, "sharedInput");
        Set(input, "<GunbladeTriggerRequested>k__BackingField", true);
        Assert.True(Read(scene));
        Assert.Equal(BattleNativeShotPhase.Ready, provider.ShotProcess!.Phase);
        AdvanceUntil(BattleNativeShotPhase.AwaitingEffect);
        Assert.Equal(BattleNativeShotPhase.AwaitingEffect, provider.ShotProcess!.Phase);
        Assert.Contains("2 ammo", Draw(scene).Texts);
        Set(input, "<GunbladeTriggerRequested>k__BackingField", false);
        Set(scene, "semanticInput", new InputFrame(0, 0, false, true, false, false));
        Assert.True(Read(scene));
        AdvanceUntil(BattleNativeShotPhase.Ending);
        Assert.Equal(BattleNativeShotPhase.Ending, provider.ShotProcess!.Phase);

        void AdvanceUntil(BattleNativeShotPhase phase)
        {
            for (int tick = 0; tick < 60 && provider.ShotProcess?.Phase != phase; tick++)
            {
                fixture.ProcessHost.Advance(waitForCommand: true);
            }
        }
    }

    [Fact]
    public void Slots_Do_over_changes_source_roll_and_Cast_accepts_current_roll()
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Slot", 5, 4, nativeProcess: true);
        fixture.OpenNativeSelection();
        CrossoverGuestHostActions provider = fixture.ProcessProvider;
        Ff7Scene scene = Scene(fixture);
        CrossoverNativeRenzokukenSelection before = Get<CrossoverNativeRenzokukenSelection>(provider, "limitSelection");
        Set(scene, "semanticInput", new InputFrame(0, 0, true, false, false, false));
        Assert.True(Read(scene));
        Assert.NotEqual(before.Random.Indexes, Get<CrossoverNativeRenzokukenSelection>(provider, "limitSelection").Random.Indexes);
        fixture.SelectSlotsSpell("Fire");
        Set(scene, "mixedSlotsChoice", 1);
        Assert.True(Read(scene));
        Assert.False(provider.SlotsChoosing);
        Assert.True(provider.ProcessActive);
    }

    [Fact]
    public void Unsupported_Slots_cast_can_be_dismissed_then_rerolled_or_cancelled()
    {
        using CrossoverCompositeActionTests.Fixture fixture = new("Slot", 5, 4, nativeProcess: true);
        fixture.OpenNativeSelection();
        fixture.SelectSlotsSpell("Rapture");
        CrossoverGuestHostActions provider = fixture.ProcessProvider;
        Ff7Scene scene = Scene(fixture);
        Set(scene, "mixedSlotsChoice", 1);
        Set(scene, "semanticInput", new InputFrame(0, 0, true, false, false, false));
        Assert.True(Read(scene));
        Assert.True(provider.SlotsChoosing);
        Assert.NotNull(provider.ProcessFailure);
        Assert.True(Read(scene));
        Assert.NotNull(Get<object?>(scene, "unavailableBattleCommand"));
        Assert.True((bool)typeof(Ff7Scene).GetMethod("ReadUnavailableBattleCommand", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, [])!);
        Assert.Null(Get<object?>(scene, "unavailableBattleCommand"));
        Set(scene, "semanticInput", new InputFrame(0, 0, false, true, false, false));
        Assert.True(Read(scene));
        Assert.False(provider.ProcessActive);
        Assert.Equal(0, fixture.ProcessHost.ReadyPlayer);
    }

    private static Ff7Scene Scene(CrossoverCompositeActionTests.Fixture fixture)
    {
        CrossoverMixedBattle mixed = (CrossoverMixedBattle)RuntimeHelpers.GetUninitializedObject(typeof(CrossoverMixedBattle));
        Set(mixed, "<Guests>k__BackingField", new[] { fixture.ProcessProvider });
        Set(mixed, "<Host>k__BackingField", fixture.ProcessHost);
        Ff7Scene scene = (Ff7Scene)RuntimeHelpers.GetUninitializedObject(typeof(Ff7Scene));
        Set(scene, "mixedCombat", mixed);
        Set(scene, "sharedInput", new KeyboardInput());
        Set(scene, "envelope", new CombinedSessionSave());
        return scene;
    }

    private static bool Read(Ff7Scene scene) => (bool)typeof(Ff7Scene).GetMethod("ReadGuestLimitProcess", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, [])!;

    private static BattleUiTests.TestCanvas Draw(Ff7Scene scene)
    {
        BattleUiTests.TestCanvas canvas = new();
        Assert.True((bool)typeof(Ff7Scene).GetMethod("DrawGuestLimitProcess", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, [canvas, new UiTheme(), new BattleHudLayout(new(0, 0, 960, 672)), 1f])!);
        return canvas;
    }

    private static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;

    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);
}
