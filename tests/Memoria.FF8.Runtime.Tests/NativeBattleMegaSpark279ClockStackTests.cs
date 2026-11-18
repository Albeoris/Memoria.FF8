using System.Collections.Immutable;
using System.Reflection;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMegaSpark279ClockStackTests
{
    [Theory]
    [InlineData(0x4842B0u, 1)]
    [InlineData(0x483470u, 2)]
    [InlineData(0x482F80u, 3)]
    [InlineData(0x4842B0u, 4)]
    [InlineData(0x4842B0u, 5)]
    public void Qualified_shallow_clock_path_preserves_only_existing_current_frame_knowledge(uint callback, int path)
    {
        NativeBattleStackBytes known = Vitals();
        Assert.Same(known, NativeBattleMegaSpark279ClockStack.Observe(known, callback, "selected clock", 4, (NativeBattleMegaSpark279ClockPath)path));
        Assert.Equal((ushort)0, known.ReadUInt16(-0xA6));
        NativeBattleStackBytes unknown = NativeBattleStackBytes.Unknown("different-current-frame", -0xAC, 8);
        Assert.Same(unknown, NativeBattleMegaSpark279ClockStack.Observe(unknown, callback, "selected clock", 4, (NativeBattleMegaSpark279ClockPath)path));
        Assert.Throws<NotSupportedException>(() => unknown.ReadUInt16(-0xA6));
    }

    [Fact]
    public void Unknown_path_foreign_callback_or_unproved_outer_phase_invalidates_the_word()
    {
        NativeBattleStackBytes known = Vitals();
        AssertUnknown(NativeBattleMegaSpark279ClockStack.Observe(known, 0x4842B0, "productive clock", 4, NativeBattleMegaSpark279ClockPath.Unknown));
        AssertUnknown(NativeBattleMegaSpark279ClockStack.Observe(known, 0x484720, "registered callback", 4, NativeBattleMegaSpark279ClockPath.AtbWithoutHelper));
        AssertUnknown(NativeBattleMegaSpark279ClockStack.Observe(known, 0x482F80, "wrong path", 4, NativeBattleMegaSpark279ClockPath.AtbWithoutHelper));
        AssertUnknown(NativeBattleMegaSpark279ClockStack.Observe(known, 0x4842B0, "tampered path", 4, (NativeBattleMegaSpark279ClockPath)99));
        foreach (int? phase in new int?[] { null, 0, 1, 2, 3 })
        {
            AssertUnknown(NativeBattleMegaSpark279ClockStack.Observe(known, 0x4842B0, "other outer frame", phase, NativeBattleMegaSpark279ClockPath.AtbWithoutHelper));
        }
    }

    [Fact]
    public void Overlapping_footprint_invalidates_exact_bytes_and_adjacent_ranges_preserve()
    {
        NativeBattleStackBytes known = Vitals();
        NativeBattleStackBytes overlap = NativeBattleMegaSpark279ClockStack.ObserveWriteRange(known, -0xA8, -0xA4, 0x4842B0, "overlapping native footprint");
        Assert.Throws<NotSupportedException>(() => overlap.ReadUInt16(-0xA6));
        Assert.Equal((ushort)0, overlap.ReadUInt16(-0xAA));
        Assert.Same(known, NativeBattleMegaSpark279ClockStack.ObserveWriteRange(known, -0xA4, 0, 0x4842B0, "upper adjacent footprint"));
        Assert.Same(known, NativeBattleMegaSpark279ClockStack.ObserveWriteRange(known, -0xB0, -0xAC, 0x4842B0, "lower adjacent footprint"));
        Assert.Throws<InvalidDataException>(() => NativeBattleMegaSpark279ClockStack.ObserveWriteRange(known, 0, 0, 0x4842B0, "invalid footprint"));
        Assert.Throws<InvalidDataException>(() => NativeBattleMegaSpark279ClockStack.Observe(known with { FirstOffset = -0xA8 }, 0x4842B0, "foreign interval", 4, NativeBattleMegaSpark279ClockPath.AtbWithoutHelper));
        Assert.Throws<InvalidDataException>(() => NativeBattleMegaSpark279ClockStack.ObserveWriteRange(known with { Known = [] }, -0x40, 0, 0x4842B0, "corrupt knowledge"));
    }

    [Fact]
    public void Gauge_registration_distinguishes_proved_menu_and_script_from_forced_commands()
    {
        NativeBattleAtbState gauge = new(4000, 1);
        Assert.Equal(NativeBattleMegaSpark279ClockPath.AtbWithoutHelper, NativeBattleMegaSpark279ClockStack.AtbParticipantPath(gauge, 30, 1, 1, 0, 0));
        Assert.Equal(1, gauge.Current);
        NativeBattleAtbState almostFull = new(4000, 3999);
        Assert.Equal(NativeBattleMegaSpark279ClockPath.AtbMenu, NativeBattleMegaSpark279ClockStack.AtbParticipantPath(almostFull, 30, 1, 1, 0, 0));
        Assert.Equal(NativeBattleMegaSpark279ClockPath.Unknown, NativeBattleMegaSpark279ClockStack.AtbParticipantPath(almostFull, 30, 1, 1, 0x20, 0));
        Assert.Equal(NativeBattleMegaSpark279ClockPath.AtbDeferredScript, NativeBattleMegaSpark279ClockStack.AtbParticipantPath(almostFull, 30, 1, 0x11, 0, 0));
        Assert.Equal(NativeBattleMegaSpark279ClockPath.AtbDeferredScript, NativeBattleMegaSpark279ClockStack.AtbParticipantPath(almostFull, 30, 1, 0x11, 0x20, 0));
        Assert.Equal(NativeBattleMegaSpark279ClockPath.AtbDeferredScript, NativeBattleMegaSpark279ClockStack.AtbParticipantPath(almostFull, 30, 1, 0x11, 0, 0x2004000));
        Assert.Equal(NativeBattleMegaSpark279ClockPath.Unknown, NativeBattleMegaSpark279ClockStack.AtbParticipantPath(almostFull, 30, 1, 1, 0, 0x2004000));
        Assert.Equal(NativeBattleMegaSpark279ClockPath.AtbWithoutHelper, NativeBattleMegaSpark279ClockStack.AtbParticipantPath(new(4000, 4000), 30, 1, 9, 0, 0));
        Assert.Throws<InvalidDataException>(() => NativeBattleMegaSpark279ClockStack.AtbParticipantPath(new(4000, 4001), 30, 1, 1, 0, 0));
    }

    [Fact]
    public void Combined_participants_keep_the_deepest_proved_footprint_and_reject_foreign_paths()
    {
        Assert.Equal(NativeBattleMegaSpark279ClockPath.AtbMenu, NativeBattleMegaSpark279ClockStack.CombineAtbPaths(NativeBattleMegaSpark279ClockPath.AtbMenu, NativeBattleMegaSpark279ClockPath.AtbDeferredScript));
        Assert.Equal(NativeBattleMegaSpark279ClockPath.AtbMenu, NativeBattleMegaSpark279ClockStack.CombineAtbPaths(NativeBattleMegaSpark279ClockPath.AtbDeferredScript, NativeBattleMegaSpark279ClockPath.AtbMenu));
        Assert.Equal(NativeBattleMegaSpark279ClockPath.AtbDeferredScript, NativeBattleMegaSpark279ClockStack.CombineAtbPaths(NativeBattleMegaSpark279ClockPath.AtbWithoutHelper, NativeBattleMegaSpark279ClockPath.AtbDeferredScript));
        Assert.Equal(NativeBattleMegaSpark279ClockPath.Unknown, NativeBattleMegaSpark279ClockStack.CombineAtbPaths(NativeBattleMegaSpark279ClockPath.AtbMenu, NativeBattleMegaSpark279ClockPath.Unknown));
        Assert.Equal(NativeBattleMegaSpark279ClockPath.Unknown, NativeBattleMegaSpark279ClockStack.CombineAtbPaths(NativeBattleMegaSpark279ClockPath.AtbMenu, NativeBattleMegaSpark279ClockPath.SupportCooldown));
    }

    [Theory]
    [InlineData(0, false, false, true)]
    [InlineData(1, false, false, false)]
    [InlineData(0, true, false, false)]
    [InlineData(0, false, true, false)]
    public void Guardian_and_escape_paths_require_separate_footprint_proof(int guardians, bool held, bool presented, bool expected)
    {
        Assert.Equal(expected, NativeBattleMegaSpark279ClockStack.AtbContextHasNoHelper(guardians, held, presented));
    }

    [Fact]
    public void Status_guard_rejects_live_expired_missing_or_wrong_width_timer_banks()
    {
        short[] timers = Enumerable.Repeat((short)-1111, 14).ToArray();
        Assert.True(NativeBattleMegaSpark279ClockStack.StatusHasNoTimer(0, timers));
        foreach (short value in new short[] { -1110, 0, 1, 200 })
        {
            timers[3] = value;
            Assert.False(NativeBattleMegaSpark279ClockStack.StatusHasNoTimer(0, timers));
        }
        Assert.False(NativeBattleMegaSpark279ClockStack.StatusHasNoTimer(0, null));
        Assert.False(NativeBattleMegaSpark279ClockStack.StatusHasNoTimer(0, []));
        Assert.True(NativeBattleMegaSpark279ClockStack.StatusHasNoTimer(1, null));
    }

    [Fact]
    public void Support_leaf_requires_a_real_initialized_positive_cooldown()
    {
        Assert.False(NativeBattleMegaSpark279ClockStack.SupportHasCooldown(null));
        Assert.False(NativeBattleMegaSpark279ClockStack.SupportHasCooldown(new(false, 1, 0)));
        Assert.False(NativeBattleMegaSpark279ClockStack.SupportHasCooldown(new(true, 0, 0)));
        Assert.True(NativeBattleMegaSpark279ClockStack.SupportHasCooldown(new(true, 1, 0)));
    }

    [Fact]
    public void Qualified_real_clock_keeps_other_stack_interval_invalidation_and_absent_support_makes_no_call()
    {
        BattleSession logical = new(new() { Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        var (roster, aliases) = logical.ReadBattlePresentationPacketBindings();
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, _ => new(0)) with { EntryLifecycle = 4 };
        BattlePresentationProcess process = NativeBattleHostedTestFixture.SeedProcess(logical, graphics);
        typeof(BattleSession).GetField("expandedPresentationProcess", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(logical, process);
        NativeBattleStackBytes known = Vitals();
        NativeBattleStackBytes other = NativeBattlePreRootStackObserver.ObserveUiWindowUpdate(NativeBattlePreRootStackObserver.BeginHostFrame(1), 0x4B0500, NativeBattleUiStackContext.NormalPreCore47CF60, "UI writer");
        NativeBattleHostedTestFixture.SetField(process, "nativeMegaSpark279Stack", known);
        NativeBattleHostedTestFixture.SetField(process, "nativePreRootStack", other);
        typeof(BattleSession).GetMethod("AdvanceNativeSupportCallbacks", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logical, []);
        Assert.Same(known, Read("nativeMegaSpark279Stack"));
        Assert.Same(other, Read("nativePreRootStack"));
        process.ObserveLogicalMegaSpark279ClockCall(0x4842B0, "proved ATB prefix", NativeBattleMegaSpark279ClockPath.AtbWithoutHelper);
        Assert.Same(known, Read("nativeMegaSpark279Stack"));
        Assert.Throws<NotSupportedException>(() => Read("nativePreRootStack").ReadUInt32(-0x8C));
        NativeBattleHostedTestFixture.SetField(process, "nativePreRootStack", other);
        process.ObserveLogicalMegaSpark279ClockCall(0x4842B0, "proved script branch", NativeBattleMegaSpark279ClockPath.AtbDeferredScript);
        Assert.Same(known, Read("nativeMegaSpark279Stack"));
        Assert.Throws<NotSupportedException>(() => Read("nativePreRootStack").ReadUInt32(-0x8C));
        NativeBattleHostedTestFixture.SetField(process, "nativePreRootStack", other);
        process.ObserveLogicalMegaSpark279ClockCall(0x4842B0, "proved Menu closure", NativeBattleMegaSpark279ClockPath.AtbMenu);
        Assert.Same(known, Read("nativeMegaSpark279Stack"));
        Assert.Throws<NotSupportedException>(() => Read("nativePreRootStack").ReadUInt32(-0x8C));

        NativeBattleStackBytes Read(string field)
        {
            return Assert.IsType<NativeBattleStackBytes>(typeof(BattlePresentationProcess).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(process));
        }
    }

    private static NativeBattleStackBytes Vitals()
    {
        NativeBattlePartyPresentedVital row = new(100, 1000, null, null) { DisplayedHitPointsQ12 = 100 << 12 };
        NativeBattlePartyPresentation party = new(ImmutableArray.Create<NativeBattlePartyPresentedVital?>(row, null, null));
        return NativeBattleMegaSpark279Stack.ObserveVitals(NativeBattleStackBytes.Unknown("current-frame", -0xAC, 8), party, new[] { false, false, false });
    }

    private static void AssertUnknown(NativeBattleStackBytes stack)
    {
        Assert.Throws<NotSupportedException>(() => stack.ReadUInt16(-0xA6));
    }
}
