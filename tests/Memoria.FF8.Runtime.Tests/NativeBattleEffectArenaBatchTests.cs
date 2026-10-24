using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectArenaBatchTests
{
    [Fact]
    public void Ordered_overlapping_stores_match_scalar_memory_without_mutating_the_input()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(10, [1, 2, 3, 4]).MarkUnknown(11, 1);
        string original = JsonSerializer.Serialize(source);
        using NativeBattleEffectArenaBatch batch = new(source);
        byte[] bytes = [8, 9];
        batch.Write(11, bytes);
        bytes[0] = 0;
        byte[] alias = batch.Read(10, 4);
        batch.Write(12, alias);
        NativeBattleEffectArena expected = source.Write(11, [8, 9]).Write(12, [1, 8, 9, 4]);
        NativeBattleEffectArena published = batch.Publish();
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(published));
        Assert.Null(published.Transaction);
        Assert.Equal(original, JsonSerializer.Serialize(source));
        Assert.Throws<NotSupportedException>(() => source.Read(11, 1));
    }

    [Fact]
    public void Read_only_batches_publish_the_original_owner_without_opening_a_transaction()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, [4]);
        using NativeBattleEffectArenaBatch batch = new(source);
        Assert.Equal(new byte[] { 4 }, batch.Read(0, 1));
        Assert.Same(source, batch.Publish());
    }

    [Fact]
    public void Each_publication_is_immutable_while_later_local_stores_remain_visible()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear();
        using NativeBattleEffectArenaBatch batch = new(source);
        batch.Write(20, [1, 2]);
        NativeBattleEffectArena first = batch.Publish();
        batch.Write(21, [9]);
        Assert.Equal(new byte[] { 1, 9 }, batch.Read(20, 2));
        Assert.Equal(new byte[] { 1, 2 }, first.Read(20, 2));
        NativeBattleEffectArena second = batch.Publish();
        batch.Dispose();
        Assert.Equal(new byte[] { 1, 9 }, second.Read(20, 2));
        Assert.Equal(new byte[] { 0, 0 }, source.Read(20, 2));
        Assert.Throws<ObjectDisposedException>(() => batch.Read(20, 2));
    }

    [Fact]
    public void External_replacement_discards_unpublished_stores_and_preserves_external_changes()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, [1]);
        NativeBattleEffectArena replacement = source.Write(1, [2]);
        using NativeBattleEffectArenaBatch batch = new(source);
        batch.Write(0, [99]);
        batch.Replace(replacement);
        Assert.Same(replacement, batch.Publish());
        Assert.Equal(new byte[] { 1, 2 }, batch.Read(0, 2));
        batch.Write(2, [3]);
        Assert.Equal(new byte[] { 1, 2, 3 }, batch.Publish().Read(0, 3));
        Assert.Equal(new byte[] { 1, 0, 0 }, source.Read(0, 3));
    }

    [Fact]
    public void Borrowed_transaction_views_remain_owned_by_the_outer_callback()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, [1]);
        using NativeBattleEffectArenaTransaction outer = new(source);
        using (NativeBattleEffectArenaBatch batch = new(outer.View))
        {
            batch.Write(1, [2]);
            Assert.Same(outer.View, batch.Publish());
            outer.View.Write(2, [3]);
            Assert.Equal(new byte[] { 1, 2, 3 }, batch.Read(0, 3));
        }
        outer.View.Write(3, [4]);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, outer.Freeze().Read(0, 4));
        Assert.Equal(new byte[] { 1, 0, 0, 0 }, source.Read(0, 4));
    }

    [Fact]
    public void Failed_local_callback_discards_stores_since_its_last_publication()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, [1]);
        NativeBattleEffectArena? visible = null;
        Assert.Throws<NotSupportedException>((Action)(() =>
        {
            using NativeBattleEffectArenaBatch batch = new(source);
            batch.Write(1, [2]);
            visible = batch.Publish();
            batch.Write(1, [99]);
            batch.Write(2, [3]);
            throw new NotSupportedException("The next local callback has no supported operation.");
        }));
        Assert.Equal(new byte[] { 1, 2, 0 }, visible!.Read(0, 3));
        Assert.Equal(new byte[] { 1, 0, 0 }, source.Read(0, 3));
    }
}
