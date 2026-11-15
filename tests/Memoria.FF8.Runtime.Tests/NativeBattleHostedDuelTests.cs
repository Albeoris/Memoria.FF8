using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedDuelTests
{
    [Fact]
    public void Reached_poll_preserves_one_waiting_control_then_requires_the_exact_selection_acknowledgement()
    {
        BattleDefinition definition = new()
        {
            UsesExpandedRoster = true,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "zell", IsPlayer = true, Name = "Zell" },
                new() { Id = "enemy", Name = "Enemy" }
            ]
        };
        BattleSession logical = new(definition, BattleAtbMode.TurnBased);
        BattleSession child = new(definition, BattleAtbMode.TurnBased);
        var (roster, aliases) = logical.ReadBattlePresentationPacketBindings();
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, _ => new(0)) with { EntryReady = true, EntryLifecycle = 4 };
        SetGraphics(child, graphics);
        Field("hostedBattlePresentation").SetValue(child, true);
        FieldInfo entry = Field("hostedEntryDescriptors");
        entry.SetValue(child, Activator.CreateInstance(entry.FieldType));
        BattleModelOwner owner = logical.ReadPresentationModelOwner("zell");
        HostedDuelPoll poll = new(owner, "zell", 0x48E5A0);
        BattleSession.HostedDispatchControls opening = new([new BattleSession.HostedDispatchControl.DuelReadiness(poll), new BattleSession.HostedDispatchControl.DuelSelection(poll)], []);
        BattleSession.PreparedHostedDispatch admitted = child.PrepareHostedDispatch([], controls: opening);
        child.ValidatePreparedHostedDispatch(admitted);
        child.CommitHostedDispatch(admitted);

        BattleNativeDuelState openingWidget = new(0, BattleTargetRequest.None, 20, 20, BattleNativeDuelPhase.Opening) { Selection1D28DEF = 254 };
        NativeBattleModelLifecycleServices frame = Advance(openingWidget);
        NativeBattleDuelReadinessReached readiness = Assert.IsType<NativeBattleDuelReadinessReached>(Assert.Single(frame.ReadOrderedReceipts()));
        Assert.Same(poll, readiness.Frame.Poll);
        Assert.Equal(0x48E5A0u, readiness.Callback);
        NativeBattleDescriptorPacket waiting = ReadPoll();
        Assert.False(child.HostedControlPrefixRetired);

        BattleNativeDuelState ready = openingWidget with { Phase = BattleNativeDuelPhase.Ready };
        Assert.Empty(Advance(ready).ReadOrderedReceipts());
        NativeBattleDescriptorPacket requeued = ReadPoll();
        Assert.NotEqual(waiting.Serial, requeued.Serial);
        Assert.False(child.HostedControlPrefixRetired);

        NativeBattleModelLifecycleServices selectedFrame = Advance(ready with { Selection1D28DEF = 2, Phase = BattleNativeDuelPhase.AwaitingEffect });
        NativeBattleDuelSelectionReached selection = Assert.IsType<NativeBattleDuelSelectionReached>(Assert.Single(selectedFrame.ReadOrderedReceipts()));
        Assert.Same(poll, selection.Frame.Poll);
        Assert.Equal((byte?)2, selection.Frame.Widget.Selection1D28DEF);
        Assert.Same(selection, child.PendingHostedDuelSelection);
        Assert.True(child.HostedControlPrefixRetired);
        Assert.Equal(requeued.Serial, selection.Serial);
        NativeBattleActionPacketsState packets = child.NativeActionPackets;
        BattleSession.HostedDispatchControls continuation = new([new BattleSession.HostedDispatchControl.ResetCounts()], []);
        Assert.Throws<InvalidOperationException>(() => child.PrepareHostedDispatch([], controls: continuation, duelSelection: selection with { Serial = unchecked((byte)(selection.Serial + 1)) }));
        Assert.Same(selection, child.PendingHostedDuelSelection);
        Assert.Same(packets, child.NativeActionPackets);

        BattleSession.PreparedHostedDispatch response = child.PrepareHostedDispatch([], controls: continuation, duelSelection: selection);
        child.ValidatePreparedHostedDispatch(response);
        child.CommitHostedDispatch(response);
        Assert.Null(child.PendingHostedDuelSelection);
        Assert.Throws<InvalidOperationException>(() => child.PrepareHostedDispatch([], controls: continuation, duelSelection: selection));

        NativeBattleModelLifecycleServices Advance(BattleNativeDuelState widget)
        {
            NativeBattleModelLifecycleServices frameServices = new(logical.CaptureModelLifecycleServices(roster).Facts) { Duel = new(poll, widget) };
            Field("hostedFrameServices").SetValue(child, frameServices);
            try
            {
                var result = (NativeBattleGraphicsState)typeof(BattleSession).GetMethod("AdvanceNativeDescriptors", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(child, [child.NativeGraphics!])!;
                SetGraphics(child, result);
                return frameServices;
            }
            finally
            {
                Field("hostedFrameServices").SetValue(child, null);
            }
        }

        NativeBattleDescriptorPacket ReadPoll()
        {
            NativeBattleDescriptorState descriptors = child.NativeGraphics!.Descriptors;
            NativeBattleDescriptorPacket packet = descriptors.Packets[Assert.Single(descriptors.Queue)];
            Assert.Equal((ushort)10, packet.Command);
            Assert.Equal(0x4852B0u, packet.Argument);
            return packet;
        }
    }

    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static void SetGraphics(BattleSession child, NativeBattleGraphicsState graphics) => typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(child, graphics);
}
