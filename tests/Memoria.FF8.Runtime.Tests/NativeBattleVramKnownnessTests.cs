using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleVramKnownnessTests
{
    [Fact]
    public void Opaque_write_preserves_bits_and_mask_across_pages_without_numeric_reads()
    {
        NativeBattleVramOwner owner = new([]);
        using NativeBattleVramOwner.Mutation mutation = owner.BeginMutation(owner.Initial);
        mutation.WriteWithKnownness(510, [1, 2, 3, 4], [true, false, false, true]);
        NativeBattleVramVersion version = mutation.Commit();
        byte[] bytes = new byte[4];
        bool[] known = new bool[4];
        version.ReadWithKnownness(510, bytes, known);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes);
        Assert.Equal(new[] { true, false, false, true }, known);
        Assert.Throws<NotSupportedException>(() => version.ReadInto(510, new byte[4]));
        Assert.Throws<NotSupportedException>(() => version.ReadWord(510));
        Assert.Throws<NotSupportedException>(() => version.ReadWord(512));
        Assert.Equal((ushort)0, version.ReadWord(508));
        NativeBattleEffectUnknownRegion range = Assert.Single(version.CaptureUnknownRegions());
        Assert.Equal(511, range.Offset);
        Assert.Equal(2, range.Length);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, Assert.Single(version.CaptureRegions()).Data);
        Assert.Equal((ushort)0, owner.Initial.ReadWord(510));
    }

    [Fact]
    public void Known_overwrite_clears_unknown_lanes_and_keeps_older_version_immutable()
    {
        NativeBattleVramOwner owner = new([new(0, [7, 8])]);
        NativeBattleVramVersion opaque;
        using (NativeBattleVramOwner.Mutation mutation = owner.BeginMutation(owner.Initial))
        {
            mutation.WriteWithKnownness(0, [7, 8], [false, true]);
            opaque = mutation.Commit();
        }
        using NativeBattleVramOwner.Mutation overwrite = owner.BeginMutation(opaque);
        overwrite.Write(0, [7, 8]);
        NativeBattleVramVersion known = overwrite.Commit();

        Assert.NotSame(opaque, known);
        Assert.Equal((ushort)0x0807, known.ReadWord(0));
        Assert.Empty(known.CaptureUnknownRegions());
        Assert.Throws<NotSupportedException>(() => opaque.ReadWord(0));
        Assert.Equal((ushort)0x0807, owner.Initial.ReadWord(0));
    }

    [Fact]
    public void Outer_rollback_restores_raw_bytes_knownness_and_publication()
    {
        NativeBattleVramOwner owner = new([new(0, [1, 2])]);
        owner.Arena.Working.BeginCheckpoint();
        using (NativeBattleVramOwner.Mutation mutation = owner.BeginMutation(owner.Initial))
        {
            mutation.WriteWithKnownness(0, [3, 4], [false, true]);
            mutation.Commit();
        }
        Assert.Throws<NotSupportedException>(() => owner.Current.ReadWord(0));
        owner.Arena.Working.RollbackCheckpoint();

        Assert.Same(owner.Initial, owner.Current);
        Assert.Equal((ushort)0x0201, owner.Arena.ReadUInt16(0));
        using NativeBattleVramOwner.Mutation next = owner.BeginMutation(owner.Initial);
        next.Write(0, [1, 2]);
        Assert.Same(owner.Initial, next.Commit());
    }

    [Fact]
    public void Saved_unknown_ranges_round_trip_and_independent_restore_can_overwrite_them()
    {
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess() with
        {
            VramRegions = [new(0, [5, 6, 7, 8])],
            VramUnknownRegions = [new(1, 2)]
        };
        NativeBattleEffectTextureState snapshot = NativeBattleStorageSnapshots.Capture(state);
        string json = JsonSerializer.Serialize(snapshot);
        NativeBattleEffectTextureState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTextureState>(json)!);
        byte[] bytes = new byte[4];
        bool[] known = new bool[4];
        restored.ReadVramWithKnownness(0, bytes, known);

        Assert.Equal(new byte[] { 5, 6, 7, 8 }, bytes);
        Assert.Equal(new[] { true, false, false, true }, known);
        Assert.Equal(json, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        Assert.Throws<NotSupportedException>(() => restored.ReadVramWord(0, 0));
        NativeBattleEffectTextureState replaced = restored.PublishVramSpan(1, [6, 7]);
        Assert.Equal((ushort)0x0605, replaced.ReadVramWord(0, 0));
        Assert.Throws<NotSupportedException>(() => snapshot.ReadVramWord(0, 0));
    }

    [Fact]
    public void Older_snapshot_without_unknown_ranges_restores_all_bytes_as_known()
    {
        NativeBattleEffectTextureState snapshot = NativeBattleStorageSnapshots.Capture(NativeBattleEffectTextureState.FromColdProcess().PublishVramSpan(0, [9, 10]));
        string json = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain("VramUnknownRegions", json);
        NativeBattleEffectTextureState restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectTextureState>(json)!);

        Assert.Equal((ushort)0x0A09, restored.ReadVramWord(0, 0));
        Assert.Equal((ushort)0, restored.ReadVramWord(1, 0));
    }
}
