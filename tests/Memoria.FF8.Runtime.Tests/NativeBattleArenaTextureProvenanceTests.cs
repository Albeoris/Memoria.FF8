using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleArenaTextureProvenanceTests
{
    [Fact]
    public void Borrowed_views_share_canonical_metadata_with_relative_bounds_and_domain_identity()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        Load(source, 32, 1, "first.tim", NativeBattleTextureDomain.Magic);
        Load(source, 112, 2, "outside.tim", NativeBattleTextureDomain.Battle);
        NativeBattleEffectArena borrowed = NativeBattleEffectArena.BorrowWorking(source.Working, 16, 80);
        Assert.Equal(16, Assert.Single(borrowed.TextureFiles!.Value).Offset);
        Assert.Same(borrowed, Load(borrowed, 16, 3, "replacement.tim", NativeBattleTextureDomain.Battle));
        Assert.Equal(2, source.TextureFiles!.Value.Length);
        NativeBattleTextureFileSource remembered = Assert.Single(source.TextureFiles!.Value.Where(file => file.Offset == 32));
        Assert.Equal(NativeBattleTextureDomain.Battle, remembered.Identity.Domain);
        Assert.Equal("replacement.tim", remembered.Identity.Name);
        Assert.NotNull(source.FindTextureFile(32, Payload(3)));
        Assert.NotNull(borrowed.FindContainingTextureFile(18, 2));
        Assert.Equal("outside.tim", Assert.Single(source.TextureFiles!.Value.Where(file => file.Offset == 112)).Identity.Name);
        NativeBattleEffectArena truncated = NativeBattleEffectArena.BorrowWorking(source.Working, 34, 4);
        Assert.Empty(truncated.TextureFiles!.Value);
    }

    [Fact]
    public void Public_transaction_and_forwarding_writer_keep_metadata_private_until_same_owner_publication()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        Load(source, 32, 1, "original.tim");
        using NativeBattleEffectArenaTransaction transaction = new(source);
        NativeBattleEffectArena borrowed = NativeBattleEffectArena.BorrowWorking(source.Working, 16, 64);
        Load(borrowed, 16, 2, "pending.tim");
        Assert.Equal("original.tim", Assert.Single(source.TextureFiles!.Value).Identity.Name);
        Assert.Equal("pending.tim", Assert.Single(transaction.View.TextureFiles!.Value).Identity.Name);
        Assert.Equal("pending.tim", Assert.Single(borrowed.TextureFiles!.Value).Identity.Name);
        using (NativeBattleEffectArenaWriter writer = new(borrowed))
        {
            writer.SetTextureFiles([new(16, 8, new("forwarded.tim", NativeBattleTextureDomain.Magic), Assert.Single(borrowed.TextureFiles!.Value).Sha256)]);
            Assert.Same(borrowed, writer.Freeze());
        }
        Assert.Equal("original.tim", Assert.Single(source.TextureFiles!.Value).Identity.Name);
        Assert.Same(source, transaction.Freeze());
        Assert.Equal("forwarded.tim", Assert.Single(source.TextureFiles!.Value).Identity.Name);
        Assert.NotNull(source.FindTextureFile(32, Payload(2)));
    }

    [Fact]
    public void Overlay_checkpoints_and_nested_private_children_publish_and_discard_exact_provenance_prefixes()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        Load(source, 32, 1, "original.tim");
        using NativeBattleEffectArenaTransaction transaction = new(source);
        transaction.BeginCheckpoint();
        Load(transaction.View, 32, 2, "discarded.tim");
        transaction.RollbackCheckpoint();
        Assert.Equal("original.tim", Assert.Single(transaction.View.TextureFiles!.Value).Identity.Name);
        transaction.BeginCheckpoint();
        using (NativeBattleEffectArenaTransaction child = NativeBattleEffectArenaTransaction.CreatePrivateChild(transaction.View))
        {
            Load(child.View, 32, 3, "child.tim");
            Assert.Equal("original.tim", Assert.Single(transaction.View.TextureFiles!.Value).Identity.Name);
            child.Freeze();
        }
        Assert.Equal("child.tim", Assert.Single(transaction.View.TextureFiles!.Value).Identity.Name);
        transaction.CommitCheckpoint();
        transaction.Freeze();
        using (NativeBattleEffectArenaTransaction child = NativeBattleEffectArenaTransaction.CreatePrivateChild(transaction.View))
        {
            Load(child.View, 32, 4, "unpublished.tim");
        }
        Assert.Equal("child.tim", Assert.Single(source.TextureFiles!.Value).Identity.Name);
        Assert.NotNull(source.FindTextureFile(32, Payload(3)));
    }

    [Fact]
    public void Physical_parent_rollback_restores_published_bytes_and_file_metadata_together()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        Load(source, 32, 1, "original.tim");
        source.Working.BeginCheckpoint();
        using (NativeBattleEffectArenaTransaction transaction = new(source))
        {
            Load(transaction.View, 32, 2, "nested.tim");
            transaction.Freeze();
        }
        source.Working.BeginNestedCheckpoint();
        Load(source, 32, 3, "later.tim");
        source.Working.RollbackNestedCheckpoint();
        Assert.Equal("nested.tim", Assert.Single(source.TextureFiles!.Value).Identity.Name);
        Assert.NotNull(source.FindTextureFile(32, Payload(2)));
        source.Working.RollbackCheckpoint();
        Assert.Equal("original.tim", Assert.Single(source.TextureFiles!.Value).Identity.Name);
        Assert.NotNull(source.FindTextureFile(32, Payload(1)));
    }

    [Fact]
    public void Failed_group_publication_restores_prior_bank_metadata_and_bytes()
    {
        NativeBattleEffectArena first = NativeBattleEffectArena.Clear();
        NativeBattleEffectArena second = NativeBattleEffectArena.Clear();
        Load(first, 32, 1, "first.tim");
        Load(second, 32, 2, "second.tim");
        using NativeBattleEffectArenaTransaction firstWrites = new(first);
        using NativeBattleEffectArenaTransaction secondWrites = new(second);
        Load(firstWrites.View, 32, 3, "new-first.tim");
        Load(secondWrites.View, 32, 4, "new-second.tim");
        using (NativeBattleTelemetry.BeginScope(new FailSecondBank()))
        {
            Assert.Throws<InvalidOperationException>(() => NativeBattleEffectArenaTransaction.CommitGroup([firstWrites, secondWrites]));
        }
        Assert.Equal("first.tim", Assert.Single(first.TextureFiles!.Value).Identity.Name);
        Assert.Equal("second.tim", Assert.Single(second.TextureFiles!.Value).Identity.Name);
        Assert.NotNull(first.FindTextureFile(32, Payload(1)));
        Assert.NotNull(second.FindTextureFile(32, Payload(2)));
        Assert.False(first.Working.HasCheckpoint);
        Assert.False(second.Working.HasCheckpoint);
        NativeBattleEffectArenaTransaction.CommitGroup([firstWrites, secondWrites]);
        Assert.NotNull(first.FindTextureFile(32, Payload(3)));
        Assert.NotNull(second.FindTextureFile(32, Payload(4)));
    }

    [Fact]
    public void Group_rollback_diagnostic_failure_still_restores_every_owner_and_preserves_primary_failure()
    {
        NativeBattleEffectArena first = NativeBattleEffectArena.Clear();
        NativeBattleEffectArena second = NativeBattleEffectArena.Clear();
        Load(first, 32, 1, "first.tim");
        Load(second, 32, 2, "second.tim");
        using NativeBattleEffectArenaTransaction firstWrites = new(first);
        using NativeBattleEffectArenaTransaction secondWrites = new(second);
        Load(firstWrites.View, 32, 3, "new-first.tim");
        Load(secondWrites.View, 32, 4, "new-second.tim");
        DoubleFault traffic = new();
        using (NativeBattleTelemetry.BeginScope(traffic))
        {
            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => NativeBattleEffectArenaTransaction.CommitGroup([firstWrites, secondWrites]));
            Assert.Same(traffic.Primary, failure);
            Exception[] rollback = Assert.IsType<Exception[]>(failure.Data["NativeBattleEffectArenaTransaction.RollbackFailures"]);
            Assert.Same(traffic.Rollback, Assert.Single(rollback));
        }
        Assert.False(first.Working.HasCheckpoint);
        Assert.False(second.Working.HasCheckpoint);
        Assert.Equal("first.tim", Assert.Single(first.TextureFiles!.Value).Identity.Name);
        Assert.Equal("second.tim", Assert.Single(second.TextureFiles!.Value).Identity.Name);
        Assert.NotNull(first.FindTextureFile(32, Payload(1)));
        Assert.NotNull(second.FindTextureFile(32, Payload(2)));
        NativeBattleEffectArenaTransaction.CommitGroup([firstWrites, secondWrites]);
        Assert.NotNull(first.FindTextureFile(32, Payload(3)));
        Assert.NotNull(second.FindTextureFile(32, Payload(4)));
    }

    [Fact]
    public void Detached_json_preserves_identity_and_hash_while_independent_restores_do_not_alias()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        Load(source, 32, 1, "retained.tim", NativeBattleTextureDomain.Battle);
        NativeBattleEffectArena frozen = NativeBattleStorageSnapshots.Capture(source);
        string json = JsonSerializer.Serialize(frozen);
        Assert.Contains("TextureFiles", json);
        NativeBattleEffectArena decoded = JsonSerializer.Deserialize<NativeBattleEffectArena>(json)!;
        NativeBattleEffectArena first = NativeBattleStorageSnapshots.Restore<NativeBattleEffectArena>(decoded);
        NativeBattleEffectArena second = NativeBattleStorageSnapshots.Restore<NativeBattleEffectArena>(decoded);
        Assert.Equal(frozen.TextureFiles!.Value.ToArray(), decoded.TextureFiles!.Value.ToArray());
        Assert.Throws<InvalidOperationException>(() => frozen.RememberTextureFile(32, Payload(2), new("invalid.tim", NativeBattleTextureDomain.Magic)));
        Load(first, 32, 2, "changed.tim");
        Assert.NotNull(second.FindTextureFile(32, Payload(1)));
        Assert.Equal("retained.tim", Assert.Single(frozen.TextureFiles!.Value).Identity.Name);
        second.MarkUnknown(32, 1);
        Assert.Null(second.FindContainingTextureFile(32, 1));
    }

    [Fact]
    public void Metadata_only_publication_is_pending_and_does_not_escape_disposal()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        source.Write(32, Payload(1));
        using (NativeBattleEffectArenaTransaction transaction = new(source))
        {
            transaction.View.RememberTextureFile(32, Payload(1), new("private.tim", NativeBattleTextureDomain.Magic));
            Assert.Throws<InvalidOperationException>(() => NativeBattleStorageSnapshots.Capture(source));
            Assert.Null(source.TextureFiles);
        }
        Assert.Null(source.TextureFiles);
        using NativeBattleEffectArenaTransaction published = new(source);
        published.View.RememberTextureFile(32, Payload(1), new("accepted.tim", NativeBattleTextureDomain.Magic));
        Assert.Same(source, published.Freeze());
        Assert.NotNull(source.FindTextureFile(32, Payload(1)));
    }

    [Fact]
    public void Existing_descriptors_do_not_add_allocations_to_warmed_scalar_stores()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        Load(source, 32, 1, "unchanged.tim");
        for (int index = 0; index < 1000; index++)
        {
            source.WriteUInt32(0, (uint)index);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1000; index++)
        {
            source.WriteUInt32(0, (uint)index);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal("unchanged.tim", Assert.Single(source.TextureFiles!.Value).Identity.Name);
    }

    private static byte[] Payload(byte value) => Enumerable.Repeat(value, 8).ToArray();
    private static NativeBattleEffectArena Load(NativeBattleEffectArena arena, int offset, byte value, string name, NativeBattleTextureDomain domain = NativeBattleTextureDomain.Magic)
    {
        byte[] bytes = Payload(value);
        return arena.Write(offset, bytes).RememberTextureFile(offset, bytes, new(name, domain));
    }

    private sealed class DoubleFault : INativeBattleTelemetrySink
    {
        private int records;
        internal InvalidOperationException Primary { get; } = new("Second bank application failed.");
        internal InvalidOperationException Rollback { get; } = new("Second bank rollback diagnostic failed.");
        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId != 1005 || kind != NativeBattleTrafficKind.CpuCopyPayload)
            {
                return;
            }
            records++;
            if (records == 3)
            {
                throw Primary;
            }
            if (records == 4)
            {
                throw Rollback;
            }
        }
    }

    private sealed class FailSecondBank : INativeBattleTelemetrySink
    {
        private int records;
        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId == 1005 && kind == NativeBattleTrafficKind.CpuCopyPayload && ++records == 3)
            {
                throw new InvalidOperationException("Second bank store failed.");
            }
        }
    }
}
