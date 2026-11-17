using static Memoria.FF8.App.Tests.NativeBattleOriginalModelAssertions;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleSpecializedSelfContactSourceTests
{
    [Theory]
    [InlineData(64, 5, 353, 16)]
    [InlineData(69, 4, 350, 12)]
    public void Original_C8_self_sources_preserve_cached_statuses_and_cold_next_callback(ushort effect, byte actorIndex, ushort action, byte sequence)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for the original self-target component sources.");
        string output = Path.Combine(Path.GetTempPath(), "specialized-self-contact-" + Guid.NewGuid().ToString("N"));
        try
        {
            BattleSnapshot source = Assert.IsType<BattleSnapshot>(NativeBattleSpecializedComponentSources.Prepare(effect, directory!, output));
            NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(source.ReadFixturePresentation().NativeGraphics);
            NativeBattleActionPacketsState packets = Assert.IsType<NativeBattleActionPacketsState>(source.ReadFixturePresentation().NativeActionPackets!);
            int descriptor = Assert.Single(graphics.Descriptors.Queue, index => graphics.Descriptors.Packets[index].Command == 104);
            int headerIndex = checked((int)((graphics.Descriptors.Packets[descriptor].Argument - 0x1D280C4u) / 20));
            NativeBattleActionHeaderSlot header = packets.ReadHeader(new BattleActionHeaderReference(headerIndex));
            Assert.Equal(actorIndex, ReadOriginalModelOperand(packets.ModelAliases, header.Source));
            Assert.Equal((byte)8, header.Command);
            Assert.Equal(sequence, header.Sequence);
            Assert.Equal(action, header.Action);
            Assert.Equal(effect, header.Effect);
            Assert.Equal((byte)1, header.ReadContactCount());
            NativeBattleContactPacket contact = packets.ReadContact(header.FirstContact);
            BattleCombatantSnapshot self = Assert.Single(source.Combatants, actor => actor.NativeParticipant!.OriginalSlot?.Offset == actorIndex);
            NativeBattleStatusSnapshot status = Assert.IsType<NativeBattleStatusSnapshot>(self.NativeStatus);
            Assert.True(self.NativeParticipant!.Active);
            Assert.False(self.NativeParticipant.Suspended);
            Assert.False(self.NativeParticipant.AllocationPendingStatistics);
            Assert.Equal(actorIndex, ReadOriginalModelOperand(packets.ModelAliases, contact.Primary.Recipient));
            Assert.Equal((ushort)0, contact.Primary.Amount6);
            Assert.Equal((byte)0, contact.Primary.Sequence);
            Assert.Equal(status.Presentation.Saved, contact.Primary.SavedStatuses4);
            Assert.Equal(status.Presentation.Battle, contact.Primary.BattleStatuses8);
            Assert.Null(contact.Secondary);
            Assert.True(NativeBattleActionGraphics.Normalize(packets, new BattleActionHeaderReference(headerIndex)).TargetActors.Resolve(packets.ModelAliases).IsSingle(packets.ModelAliases.DecodeLiteral(checked((byte)actorIndex))));
            if (effect == 64)
            {
                Assert.NotEqual(status.Battle, status.Presentation.Battle);
            }
            string frozen = JsonSerializer.Serialize(source);
            BattleSession direct = BattleSession.Restore(source);
            BattleSession cold = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(frozen)!);
            Assert.Equal(frozen, JsonSerializer.Serialize(cold.Capture()));
            using GameAssets game = new(directory!);
            direct.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
            cold.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
            using IDisposable scope = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
            direct.Tick();
            cold.Tick();
            BattleSnapshot next = direct.Capture();
            Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(cold.Capture()));
            Assert.Equal(source.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), next.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
            Assert.Equal(frozen, JsonSerializer.Serialize(source));
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }
}
