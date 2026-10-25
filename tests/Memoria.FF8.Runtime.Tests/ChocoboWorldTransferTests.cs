using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class ChocoboWorldTransferTests
{
    [Fact]
    public void Commit_rejects_changed_source_and_changed_game_owners_without_mutating_either()
    {
        GameSession session = new(State());
        ChocoboImportPreview preview = session.PreviewChocoboWorldImport(Import(1, 2), Table());
        Assert.True(preview.CanImport);
        string original = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidOperationException>(() => session.CommitChocoboWorldImport(preview, new string('B', 64)));
        Assert.Equal(original, JsonSerializer.Serialize(session.State));
        session.SetGameplayPreferences(session.State.GameplayPreferences with { ChocoboWorldMode = ChocoboWorldMode.Memoria });
        string changed = JsonSerializer.Serialize(session.State);
        Assert.Throws<InvalidOperationException>(() => session.CommitChocoboWorldImport(preview, preview.Import.ContentSha256));
        Assert.Equal(changed, JsonSerializer.Serialize(session.State));
    }

    [Fact]
    public void Committed_transfer_survives_compact_save_load_and_cannot_be_replayed()
    {
        GameSession session = new(State());
        NativeChocoboWorldImport import = Import(1, 2);
        ChocoboImportPreview preview = session.PreviewChocoboWorldImport(import, Table());
        session.CommitChocoboWorldImport(preview, import.ContentSha256);
        Assert.Throws<InvalidOperationException>(() => session.CommitChocoboWorldImport(preview, import.ContentSha256));
        string path = Path.GetTempFileName();
        try
        {
            session.Save(path, compact: true);
            GameSession restored = new(GameSession.Load(path));
            Assert.Null(restored.PreviewChocoboWorldImport(import, Table()).Candidate);
            Assert.Equal(2, restored.State.Economy!.Items.Single(item => item.ItemId == 33).Count);
            Assert.Equal(session.State.MenuRandom, restored.State.MenuRandom);
            Assert.Equal(JsonSerializer.Serialize(session.State.ChocoboRewards), JsonSerializer.Serialize(restored.State.ChocoboRewards));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Unchanged_import_is_empty_and_later_revision_grants_only_counter_delta()
    {
        GameState original = State();
        NativeChocoboWorldImport first = Import(1, 5);
        ChocoboImportPreview preview = ChocoboWorldTransfers.Preview(original, first, Table());
        GameState imported = Assert.IsType<GameState>(preview.Candidate);
        Assert.Equal(5, imported.Economy!.Items.Single(item => item.ItemId == 33).Count);
        Assert.Equal(original.MenuRandom!.Advance(5), imported.MenuRandom);
        ChocoboImportPreview repeated = ChocoboWorldTransfers.Preview(imported, first, Table());
        Assert.Null(repeated.Candidate);
        Assert.Empty(repeated.Items);
        ChocoboImportPreview newer = ChocoboWorldTransfers.Preview(imported, Import(2, 7), Table());
        GameState updated = Assert.IsType<GameState>(newer.Candidate);
        Assert.Equal(7, updated.Economy!.Items.Single(item => item.ItemId == 33).Count);
        Assert.Equal(original.MenuRandom.Advance(7), updated.MenuRandom);
        Assert.Equal(2, Assert.Single(newer.Items).Count);
        Assert.Empty(original.ChocoboRewards.NativeImportSources);
    }

    [Fact]
    public void Wrong_mode_and_foreign_world_require_explicit_resolution()
    {
        GameState state = State();
        Assert.Null(ChocoboWorldTransfers.Preview(state with { GameplayPreferences = new() { ChocoboWorldMode = ChocoboWorldMode.Memoria } }, Import(1, 1), Table()).Candidate);
        NativeChocoboWorldImport foreign = Import(1, 1) with { WorldId = 999 };
        ChocoboImportPreview blocked = ChocoboWorldTransfers.Preview(state, foreign, Table());
        Assert.Null(blocked.Candidate);
        Assert.True(blocked.RequiresAssociationReview);
        Assert.NotNull(ChocoboWorldTransfers.Preview(state, foreign, Table(), reviewedAssociation: true).Candidate);
    }

    [Fact]
    public void Capacity_failure_does_not_advance_rng_ledger_or_progression()
    {
        GameState source = State();
        source = source with { Economy = source.Economy!.AddItem(33, 99, out _) };
        string before = JsonSerializer.Serialize(source);
        ChocoboImportPreview preview = ChocoboWorldTransfers.Preview(source, Import(1, 2), Table());
        Assert.Null(preview.Candidate);
        Assert.Contains("capacity", preview.Message);
        Assert.Equal(before, JsonSerializer.Serialize(source));
        Assert.Empty(source.ChocoboRewards.NativeImportSources);
        Assert.Equal(99, source.Economy!.Items.Single(item => item.ItemId == 33).Count);
    }

    [Theory]
    [InlineData(0u, 5)]
    [InlineData(2u, 4)]
    [InlineData(1u, 6)]
    public void Older_revision_reset_counts_and_same_revision_mutations_fail_closed(uint revision, int count)
    {
        GameState imported = ChocoboWorldTransfers.Preview(State(), Import(1, 5), Table()).Candidate!;
        Assert.Null(ChocoboWorldTransfers.Preview(imported, Import(revision, count), Table()).Candidate);
        Assert.Equal(5, imported.Economy!.Items.Single(item => item.ItemId == 33).Count);
    }

    [Fact]
    public void Progression_is_monotonic_unknown_bytes_retained_and_home_state_normalized()
    {
        GameState source = State();
        byte[] raw = source.PocketStation!.Raw.ToArray();
        raw[1] = 80;
        raw[3] = 0x80;
        raw[45] = 3;
        raw[0] |= 4;
        source = source with { PocketStation = source.PocketStation with { Raw = raw.ToImmutableArray() } };
        NativeChocoboWorldImport import = Import(1, 0);
        byte[] incoming = import.RawState.ToArray();
        incoming[2] = 0;
        incoming[55] = 0xAB;
        import = import with { CurrentHp = 0, RawState = incoming.ToImmutableArray() };
        GameState result = ChocoboWorldTransfers.Preview(source, import, Table()).Candidate!;
        Assert.Equal(80, result.PocketStation!.Level);
        Assert.Equal(3, result.PocketStation.AttackVariant);
        Assert.Equal(0x80, result.PocketStation.Raw[3]);
        Assert.Equal(1, result.PocketStation.Raw[2]);
        Assert.Equal(0xAB, result.PocketStation.Raw[55]);
        Assert.Equal(0, result.PocketStation.Flags & 2);
        Assert.NotEqual(0, result.PocketStation.Flags & 4);
        Assert.NotEqual(0, result.PocketStation.Raw[13] & 128);
    }

    [Fact]
    public void Unique_rolls_record_once_without_rerolling_or_refunding_rng()
    {
        GameState source = State();
        GameState result = ChocoboWorldTransfers.Preview(source, Import(1, 2), Table(100)).Candidate!;
        Assert.Equal(1, result.Economy!.Items.Single(item => item.ItemId == 100).Count);
        Assert.Contains("ribbon", result.ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.Equal(source.MenuRandom!.Advance(2), result.MenuRandom);
    }

    [Fact]
    public void Reviewed_new_native_world_uses_a_distinct_checkpoint_for_repeatable_loot()
    {
        GameState first = ChocoboWorldTransfers.Preview(State(), Import(4, 5), Table()).Candidate!;
        NativeChocoboWorldImport nextCycle = WithWorld(Import(1, 2), 456);
        Assert.True(ChocoboWorldTransfers.Preview(first, nextCycle, Table()).RequiresAssociationReview);
        GameState next = ChocoboWorldTransfers.Preview(first, nextCycle, Table(), reviewedAssociation: true).Candidate!;
        Assert.Equal(7, next.Economy!.Items.Single(item => item.ItemId == 33).Count);
        Assert.Equal(2, next.ChocoboRewards.NativeImportSources.Count);
        Assert.Null(ChocoboWorldTransfers.Preview(next, nextCycle, Table()).Candidate);
    }

    [Fact]
    public void File_content_changes_outside_native_state_do_not_replay_items()
    {
        NativeChocoboWorldImport import = Import(1, 2);
        GameState first = ChocoboWorldTransfers.Preview(State(), import, Table()).Candidate!;
        byte[] extra = new byte[512];
        extra[300] = 99;
        NativeChocoboWorldImport edited = import with { ContentSha256 = new string('B', 64), RawWorld = extra.ToImmutableArray() };
        Assert.Null(ChocoboWorldTransfers.Preview(first, edited, Table()).Candidate);
        Assert.Equal(2, first.Economy!.Items.Single(item => item.ItemId == 33).Count);
    }

    [Fact]
    public void Receipt_retains_selected_and_backup_counters_and_world_zero_can_associate()
    {
        NativeChocoboWorldImport import = WithWorld(Import(9, 1), 0) with { BackupRevision = 8, BackupStructurallyValid = true };
        ChocoboImportPreview preview = ChocoboWorldTransfers.Preview(State(), import, Table());
        Assert.False(preview.RequiresAssociationReview);
        GameState first = Assert.IsType<GameState>(preview.Candidate);
        ChocoboImportReceipt receipt = Assert.Single(first.ChocoboRewards.NativeImportSources).Value;
        Assert.Equal(9u, receipt.Revision);
        Assert.Equal(8u, receipt.BackupRevision);
        Assert.Equal(123u, BinaryPrimitives.ReadUInt32LittleEndian(first.PocketStation!.Raw.AsSpan(0x28, 4)));
        Assert.Null(ChocoboWorldTransfers.Preview(first, import, Table()).Candidate);
    }

    [Fact]
    public void MiniMog_substitution_uses_merged_discovery_flags_and_first_discovery_awards_once()
    {
        NativeChocoboWorldImport import = Import(1, 2);
        GameState notFound = ChocoboWorldTransfers.Preview(State(), import, Table(65)).Candidate!;
        Assert.Equal(2, notFound.Economy!.Items.Single(item => item.ItemId == 110).Count);
        Assert.DoesNotContain("mogs_amulet", notFound.ChocoboRewards.AcquiredUniqueRewardIds);
        byte[] raw = import.RawState.ToArray();
        raw[0] |= 4;
        NativeChocoboWorldImport discovered = import with { EventFlags = 7, RawState = raw.ToImmutableArray() };
        GameState found = ChocoboWorldTransfers.Preview(State(), discovered, Table(65)).Candidate!;
        Assert.Equal(1, found.Economy!.Items.Single(item => item.ItemId == 65).Count);
        Assert.DoesNotContain(found.Economy.Items, item => item.ItemId == 110 && item.Count != 0);
        Assert.Contains("mogs_amulet", found.ChocoboRewards.AcquiredUniqueRewardIds);
        Assert.Equal(State().MenuRandom!.Advance(2), found.MenuRandom);
    }

    private static NativeChocoboWorldImport WithWorld(NativeChocoboWorldImport import, uint world)
    {
        byte[] raw = import.RawState.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x28), world);
        return import with { WorldId = world, RawState = raw.ToImmutableArray() };
    }

    private static NativeChocoboWorldTransferTable Table(byte item = 33) => new(Enumerable.Repeat(item, 512).ToImmutableArray());

    private static NativeChocoboWorldImport Import(uint revision, int count)
    {
        byte[] raw = new byte[64];
        raw[0] = 3;
        raw[1] = 0x20;
        raw[2] = 0x20;
        raw[3] = 0x30;
        raw[13] = 2;
        raw[12] = 0x11;
        raw[0x14] = (byte)(((count / 10) << 4) | (count % 10));
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(8), revision);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x28), 123);
        return new("Fixture", new string('A', 64), 0, revision, 123, 211, 20, 20, 30, 0, 3, ImmutableArray.Create(count, 0, 0, 0), raw.ToImmutableArray(), raw.ToImmutableArray());
    }

    private static GameState State()
    {
        byte[] raw = new byte[64];
        raw[0] = 1;
        raw[1] = 1;
        raw[2] = 0x06;
        raw[3] = 0x06;
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x28), 123);
        return new()
        {
            GameplayPreferences = new() { ChocoboWorldMode = ChocoboWorldMode.NativeImport },
            MenuRandom = NativeMenuRandomState.FromColdProcess(),
            PocketStation = new(NativeInputConfiguration.OriginalInitHash, raw.ToImmutableArray()),
            Economy = new()
            {
                Version = 1,
                Rule = NativeEconomy.InitializationRule,
                InitOutSha256 = NativeInputConfiguration.OriginalInitHash,
                Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(),
                Gil = 0,
                PastGil = 0,
                EventBankGil72 = 0,
                EventBankPastGil76 = 0
            }
        };
    }
}
