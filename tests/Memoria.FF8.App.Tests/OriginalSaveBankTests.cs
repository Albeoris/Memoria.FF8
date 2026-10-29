using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OriginalSaveBankTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-original-banks-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Bank_boundaries_use_native_filenames_and_never_rename_existing_Memoria_slots()
    {
        PlayerSaveStore store = CreateStore();
        Assert.Equal("slot-60.json", Path.GetFileName(store.GetPath(60)));
        Assert.Equal("slot-100.json", Path.GetFileName(store.GetPath(100)));
        Assert.Equal("slot1_save01.ff8", Path.GetFileName(store.GetPath(101)));
        Assert.Equal("slot1_save30.ff8", Path.GetFileName(store.GetPath(130)));
        Assert.Equal("slot2_save01.ff8", Path.GetFileName(store.GetPath(131)));
        Assert.Equal("slot2_save30.ff8", Path.GetFileName(store.GetPath(160)));
        Assert.Equal(new[] { 0, 0, 1, 1, 2, 2 }, new[] { 1, 100, 101, 130, 131, 160 }.Select(PlayerSaveStore.GetBank));
        Assert.Equal(new[] { 1, 101, 131 }, Enumerable.Range(0, 3).Select(PlayerSaveStore.GetFirstSlot));
        Assert.Equal(new[] { 100, 30, 30 }, Enumerable.Range(0, 3).Select(PlayerSaveStore.GetBankSlotCount));
        Assert.Equal(30, store.ReadEntries(1, true).Count);
        Assert.Equal(30, store.ReadEntries(2, false).Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => store.ReadEntries(3, false));
        Assert.True(store.ContainsPath(store.GetPath(101)));
    }

    [Fact]
    public void Native_rows_read_small_preview_without_importing_game_state()
    {
        int imports = 0;
        PlayerSaveStore store = CreateStore(_ =>
        {
            imports++;
            throw new InvalidOperationException("Listing must not initialize the game.");
        }, saved => saved.EventState!.Value.GetProperty("Scene").GetProperty("PlaceId").GetInt32() == 21 ? "Saved location" : null);
        NativePcSaveData original = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(Defaults()));
        BinaryPrimitives.WriteUInt16LittleEndian(original.Block.AsSpan(388), 21);
        BinaryPrimitives.WriteUInt32LittleEndian(original.Block.AsSpan(400), 90061);
        original.Block[405] = 2;
        original.Block[406] = 255;
        original.Block[407] = 0;
        File.WriteAllBytes(store.GetPath(101), NativePcSaveCodec.Encode(original));
        PlayerSaveEntry row = store.ReadEntries(1, false)[0];
        Assert.True(row.CanLoad);
        Assert.Equal("Saved location", row.Details);
        Assert.Equal("25:01:01", row.PlayTimeText);
        Assert.Equal(new[] { 2, 0 }, row.PartyPortraits);
        Assert.Equal(0, imports);
        File.WriteAllText(store.GetPath(102), "corrupt");
        Assert.False(store.ReadEntries(1, false)[1].CanLoad);
    }

    [Fact]
    public void Transfers_convert_both_directions_preserve_sources_and_copy_native_banks_exactly()
    {
        PlayerSaveStore store = CreateStore();
        GameState state = Defaults() with { X = 42, Playtime = new NativePlaytimeState(1234, 0, 0) };
        store.Save(60, new GameSession(state));
        byte[] memoria = File.ReadAllBytes(store.GetPath(60));
        int validations = 0;
        store.Transfer(60, 101, candidate =>
        {
            validations++;
            Assert.Equal(1234u, candidate.Playtime!.Seconds);
            Assert.False(File.Exists(store.GetPath(101)));
        });
        byte[] original = File.ReadAllBytes(store.GetPath(101));
        Assert.Equal(memoria, File.ReadAllBytes(store.GetPath(60)));
        store.Transfer(101, 100);
        Assert.Equal(1234u, store.Load(100).Playtime!.Seconds);
        store.Transfer(101, 160);
        Assert.Equal(original, File.ReadAllBytes(store.GetPath(101)));
        Assert.Equal(original, File.ReadAllBytes(store.GetPath(160)));
        Assert.Equal(1, validations);
        Assert.Throws<InvalidOperationException>(() => store.Transfer(101, 101));
    }

    [Fact]
    public void Rejected_conversion_preserves_destination_and_removes_temporary_output()
    {
        PlayerSaveStore store = CreateStore();
        store.Save(101, new GameSession(Defaults()));
        byte[] before = File.ReadAllBytes(store.GetPath(101));
        store.Save(1, new GameSession(Defaults() with { Playtime = new NativePlaytimeState(90, 0, 0) }));
        Assert.Throws<NotSupportedException>(() => store.Transfer(1, 101, _ => throw new NotSupportedException("Unsupported continuation")));
        Assert.Equal(before, File.ReadAllBytes(store.GetPath(101)));
        Assert.False(File.Exists(store.GetPath(101) + ".tmp"));
        Assert.Throws<NotSupportedException>(() => store.Transfer(1, 102, _ => throw new NotSupportedException("Unsupported continuation")));
        Assert.False(File.Exists(store.GetPath(102)));
        Assert.False(File.Exists(store.GetPath(102) + ".tmp"));
    }

    [Fact]
    public void Native_targets_cannot_replace_configured_checkpoint()
    {
        PlayerSaveStore store = CreateStore();
        string checkpoint = store.GetPath(101);
        File.WriteAllText(checkpoint, "checkpoint");
        var conflicting = new PlayerSaveStore(checkpoint, Path.Combine(directory, "memoria"), originalDirectory: store.OriginalDirectory, loadOriginal: LoadOriginal);
        Assert.Throws<InvalidOperationException>(() => conflicting.GetPath(101));
        Assert.Throws<InvalidOperationException>(() => conflicting.Save(101, new GameSession(Defaults())));
        Assert.False(conflicting.ReadEntries(1, false)[0].CanLoad);
        Assert.Equal("checkpoint", File.ReadAllText(checkpoint));
    }

    [Fact]
    public void Discovery_returns_all_accounts_for_explicit_choice_and_ignores_unrelated_folders()
    {
        string root = Path.Combine(directory, "documents", "Square Enix", "FINAL FANTASY VIII Steam");
        string first = Directory.CreateDirectory(Path.Combine(root, "user_111")).FullName;
        string second = Directory.CreateDirectory(Path.Combine(root, "user_222")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "unrelated"));
        Assert.Equal(new[] { first, second }, OriginalSaveLocation.FindCandidates(Path.Combine(directory, "documents")));
        Assert.Empty(OriginalSaveLocation.FindCandidates(Path.Combine(directory, "missing")));
    }

    [Fact]
    public void ReviewedGuardianForceExportPreservesActiveStateAndCannotBypassOtherBlockers()
    {
        PlayerSaveStore store = CreateStore();
        GameState state = Defaults();
        NativeCharacterVitals source = state.CharacterVitals!;
        var templates = source.ResolveGuardianForceTemplates().SetItem(0, Enumerable.Range(1, 50).Where(ability => !ChocoboRewardCatalog.Entries.Any(reward => reward.TaughtAbilityId == ability)).Select(ability => new GuardianForceAbilityTemplate(ability, true, 10)).ToImmutableArray());
        GameState expanded = state with { CharacterVitals = source.ApplyGuardianForceTemplates(templates) };
        GameSession session = new(expanded);
        // Unique reward abilities would independently require a Memoria slot after acquisition reconciliation.
        Assert.False(session.State.ChocoboRewards.HasState);
        Assert.False(PlayerSaveStore.RequiresMemoriaFormat(session.State, allowGuardianForceProjection: true));
        store.Save(101, new GameSession(state));
        byte[] previous = File.ReadAllBytes(store.GetPath(101));
        Assert.True(PlayerSaveStore.RequiresMemoriaFormat(expanded));
        Assert.False(PlayerSaveStore.RequiresMemoriaFormat(expanded, allowGuardianForceProjection: true));
        Assert.Throws<NotSupportedException>(() => store.Save(101, session, targetSource: source));
        Assert.Equal(previous, File.ReadAllBytes(store.GetPath(101)));
        store.Save(101, session, targetSource: source, allowGuardianForceLoss: true);
        Assert.True(store.Load(101).CharacterVitals!.GuardianForces[0].LearnedBank.Has(22));
        Assert.False(store.Load(101).CharacterVitals!.GuardianForces[0].LearnedBank.Has(23));
        Assert.True(session.State.CharacterVitals!.GuardianForces[0].LearnedBank.Has(50));
        store.Save(1, session);
        byte[] fullSave = File.ReadAllBytes(store.GetPath(1));
        store.Transfer(1, 102, targetSource: source, allowGuardianForceLoss: true, reviewedSource: expanded);
        Assert.Equal(fullSave, File.ReadAllBytes(store.GetPath(1)));
        Assert.True(store.Load(102).CharacterVitals!.GuardianForces[0].LearnedBank.Has(22));
        Assert.False(store.Load(102).CharacterVitals!.GuardianForces[0].LearnedBank.Has(23));
        byte[] reviewed = File.ReadAllBytes(store.GetPath(101));
        GameState unsupported = expanded with { GameplayPreferences = new() { GuardianFormsEnabled = true } };
        Assert.True(PlayerSaveStore.RequiresMemoriaFormat(unsupported, allowGuardianForceProjection: true));
        Assert.Throws<NotSupportedException>(() => store.Save(101, new GameSession(unsupported), targetSource: source, allowGuardianForceLoss: true));
        Assert.Equal(reviewed, File.ReadAllBytes(store.GetPath(101)));
        Assert.False(File.Exists(store.GetPath(101) + ".tmp"));
    }

    private PlayerSaveStore CreateStore(Func<string, GameState>? loader = null, Func<GameState, string?>? location = null)
    {
        string original = Directory.CreateDirectory(Path.Combine(directory, "original")).FullName;
        return new PlayerSaveStore(Path.Combine(directory, "checkpoint.json"), Path.Combine(directory, "memoria"), location, original, loader ?? LoadOriginal);
    }

    private static GameState LoadOriginal(string path)
    {
        return NativePcSaveConverter.Import(File.ReadAllBytes(path), Defaults(), id => $"field{id}", "en");
    }

    private static GameState Defaults()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        string hash = NativeInputConfiguration.OriginalInitHash;
        NativeStatCurve curve = new(100, 0, 100, 255, 200);
        NativeStatModifier modifier = new(0, 0, 0);
        NativeCharacterVitals vitals = new()
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = hash,
            KernelSha256 = new string('0', 64),
            Records = Enumerable.Range(0, 8).Select(id => new NativeCharacterVital((byte)id, new NativeHpWord(100, 100), 0, new NativeCharacterJunction(0, 0, 0, new byte[20]), 0, 0, 0, new byte[6], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), 1, 0)).ToArray(),
            CombatParty = [0, 255, 255],
            DreamEnabled = false,
            GuardianForces = Enumerable.Range(0, 16).Select(_ => new NativeGuardianForceVital(0, 1, 100, new NativeGuardianForceLearnedBank(0, 0), null, ImmutableArray.CreateRange(new byte[21]), 0, 0)).ToArray(),
            Definitions = new NativeStatDefinitions(Enumerable.Repeat(curve, 11).ToImmutableArray(), ImmutableArray.CreateRange(new byte[57]), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray()),
            MenuDefinitions = new NativeGuardianForceMenuDefinitions(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), ImmutableArray.CreateRange(new byte[116]), ImmutableArray.CreateRange(new byte[19]), ImmutableArray.CreateRange(new byte[5])),
            DreamWeapons = new byte[3],
            JunctionPartyBackup = new byte[6]
        };
        NativeEconomy economy = new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = hash, Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), ItemOrder = Enumerable.Range(0, 32).Select(rank => (byte)rank).ToArray(), Gil = 5000, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 };
        return session.State with { CharacterVitals = vitals, Economy = economy, FieldArrival = new FieldTransition(74, 10, 20, 0, 0) };
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
