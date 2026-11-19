using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect298PresentationPacketTests
{
    [Fact]
    public void Installed_callbacks_publish_fresh_packet_receipts_without_changing_retained_previous_receipts()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(298));
        NativeBattleGraphicsState original = source.Presentation!.Graphics;
        BattleSession fork = source.ForkForContinuation();
        ImmutableArray<NativeBattlePrimitiveReference> held = [];
        NativeBattlePrimitiveReference[] heldValues = [];
        HashSet<string> banks = [];
        int publishedCallbacks = 0;
        bool entered = false;
        bool completed = false;
        for (int callback = 0; callback < 2400 && !completed; callback++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            NativeBattleOriginalEffectState? current = fork.Presentation!.Graphics.OriginalEffect;
            if (current?.RequestedEffect != 298)
            {
                continue;
            }
            entered = true;
            ImmutableArray<NativeBattlePrimitiveReference> packets = current.PresentationPackets;
            Assert.False(packets.IsDefault);
            if (!packets.IsEmpty)
            {
                if (!held.IsEmpty)
                {
                    Assert.Equal(heldValues, held.ToArray());
                    Assert.DoesNotContain(packets, packet => held.Any(previous => ReferenceEquals(previous, packet)));
                }
                foreach (NativeBattlePrimitiveReference packet in packets)
                {
                    Assert.True(packet.ByteOffset >= 0);
                    banks.Add(packet.Bank);
                }
                held = packets;
                heldValues = packets.ToArray();
                publishedCallbacks++;
            }
            completed = current.ReturnedDescriptor == 0;
        }
        Assert.True(entered && completed, "The actual original component must complete its callback sequence.");
        Assert.True(publishedCallbacks >= 2, "The original component must publish independently owned receipts across callbacks.");
        Assert.Contains("20DFAB8", banks);
        Assert.Contains(banks, bank => bank.StartsWith("off_B6D0", StringComparison.Ordinal));
        Assert.Equal(heldValues, held.ToArray());
        Assert.Same(original, source.Presentation!.Graphics);
    }
}
