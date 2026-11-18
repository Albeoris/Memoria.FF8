using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLogicalPacketInsertionJournalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PacketInsertionsReplayCapturedReferencesAndMetadataAndRollBackAtTheirCheckpoint(bool retainAddressedPayload)
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        using (NativeBattleLogicalOrdering.Builder prefix = new(ordering))
        {
            prefix.Prepend(5, Reference(0), Words, 0, []);
            prefix.ToImmutable();
        }
        using NativeBattleLogicalOrderingTransaction transaction = new(ordering);
        string original = Receipt(transaction.View);
        transaction.BeginCheckpoint();
        Append(transaction.Writer, retainAddressedPayload);
        string inserted = Receipt(transaction.View);
        uint header = transaction.Writer.LastHeaderLink;
        NativeBattleLogicalOrdering.Delta delta = transaction.SuspendCheckpointDelta();
        Assert.Equal(original, Receipt(transaction.View));
        Assert.Equal(2, delta.Edits.Length);
        Assert.All(delta.Edits, edit => Assert.Null(edit.Callback));
        Assert.Equal(new[] { Reference(16), Reference(32) }, delta.Edits.Select(edit => edit.ModelPacket.Reference));

        transaction.BeginCheckpoint();
        transaction.ResumeDelta(delta);
        Assert.Equal(inserted, Receipt(transaction.View));
        Assert.Equal(header, transaction.Writer.LastHeaderLink);
        Assert.Equal(3, transaction.View.ProxyCursor);
        NativeBattleLogicalPacket normal = Assert.Single(transaction.View.Packets, packet => packet.Reference == Reference(16));
        Assert.Equal("texture", normal.TextureSourceOwner);
        Assert.Equal("actor", normal.PresentationActorId);
        Assert.Equal(new NativeBattleModelTextureOwner("actor"), normal.ModelTextureOwner);
        Assert.Equal(NativeBattleScreenCoverage.Full, normal.ScreenCoverage);
        Assert.Equal(new[] { new BattleSceneScreenPoint(4, 5) }, normal.ProjectedCoordinates!.Value);
        NativeBattleLogicalPacket addressed = Assert.Single(transaction.View.Packets, packet => packet.Reference == Reference(32));
        Assert.Equal(!retainAddressedPayload, addressed.RequiresPhysicalRead);
        Assert.Equal(retainAddressedPayload ? 0x1C48840u : (uint?)null, addressed.AddressedNextAddress);
        Assert.Equal(NativeBattleScreenCoverage.Full, addressed.ScreenCoverage);
        Assert.Equal(new NativeBattleModelTextureOwner("actor"), addressed.ModelTextureOwner);
        Assert.Throws<InvalidOperationException>(() => transaction.ResumeDelta(delta));
        Assert.Equal(inserted, Receipt(transaction.View));
        transaction.RollbackCheckpoint();
        Assert.Equal(original, Receipt(transaction.View));
        transaction.BeginCheckpoint();
        Append(transaction.Writer, retainAddressedPayload);
        transaction.CommitCheckpoint();
        Assert.Equal(inserted, Receipt(transaction.Freeze()));
    }

    [Fact]
    public void TypedPacketSuffixSurvivesBuilderDisposalAndRejectsAChangedOrderingPrefix()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleLogicalOrdering.Delta delta;
        string inserted;
        using (NativeBattleLogicalOrdering.Builder writer = new(ordering))
        {
            Append(writer, true);
            inserted = Receipt(ordering);
            delta = writer.SuspendDelta();
        }
        Assert.Equal(0, ordering.ProxyCursor);
        using (NativeBattleLogicalOrdering.Builder continuation = new(ordering))
        {
            continuation.ResumeDelta(delta);
            Assert.Equal(inserted, Receipt(ordering));
            continuation.ToImmutable();
        }
        NativeBattleLogicalOrdering.Delta stale;
        using (NativeBattleLogicalOrdering.Builder writer = new(ordering))
        {
            writer.Prepend(6, Reference(48), Words, 0, []);
            stale = writer.SuspendDelta();
        }
        using (NativeBattleLogicalOrdering.Builder changed = new(ordering))
        {
            changed.Prepend(7, Reference(64), Words, 0, []);
            changed.ToImmutable();
        }
        using NativeBattleLogicalOrdering.Builder rejected = new(ordering);
        string before = Receipt(ordering);
        Assert.Throws<InvalidOperationException>(() => rejected.ResumeDelta(stale));
        Assert.Equal(before, Receipt(ordering));
    }

    [Fact]
    public void PrivateAutomaticScopeRollsBackPublishedPacketSuffixAndFailedInsertionKeepsItsPrefix()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        string original = Receipt(ordering);
        NativeBattleLogicalOrdering.AutomaticScope operation = NativeBattleLogicalOrdering.AutomaticScope.Begin(ordering);
        try
        {
            using NativeBattleLogicalOrdering.Builder writer = new(ordering);
            Append(writer, true);
            string inserted = Receipt(ordering);
            Assert.Throws<InvalidDataException>(() => writer.PrependAddressed(0x1C48828, Reference(48), [0x02000000, 0xE6000000], 0, []));
            Assert.Equal(inserted, Receipt(ordering));
            writer.ToImmutable();
        }
        finally
        {
            operation.Complete(rollback: true);
        }
        Assert.Equal(original, Receipt(ordering));
    }

    private static void Append(NativeBattleLogicalOrdering.Builder writer, bool retainAddressedPayload)
    {
        ImmutableArray<BattleSceneScreenPoint> coordinates = [new(4, 5)];
        NativeBattleModelTextureOwner texture = new("actor");
        writer.Prepend(5, Reference(16), Words, 0x1234, [1, 2, 3, 4], 77, coordinates, "texture", presentationActorId: "actor", screenCoverage: NativeBattleScreenCoverage.Full, sceneCapture: NativeBattleSceneCapture.Current, modelTextureOwner: texture);
        writer.PrependAddressed(0x1C48840, Reference(32), Words, 0x2345, [4, 3, 2, 1], 88, screenCoverage: NativeBattleScreenCoverage.Full, sceneCapture: NativeBattleSceneCapture.Current, projectedCoordinates: coordinates, modelTextureOwner: texture, retainAuthoredPayload: retainAddressedPayload);
    }

    private static NativeBattlePrimitiveReference Reference(int offset) => new("20DFAB8", 0, offset);
    private static readonly ImmutableArray<uint> Words = [0x01000000, 0xE6000000];
    private static string Receipt(NativeBattleLogicalOrdering value) => JsonSerializer.Serialize(new { Table = value.Table.ToArray(), Proxies = value.Proxies.ToArray(), Packets = value.Packets.ToArray(), value.ProxyCursor, value.TableBuffer, OtherTable = value.OtherTable?.ToArray() });
}
