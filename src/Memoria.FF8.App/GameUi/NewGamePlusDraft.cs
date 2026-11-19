using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

/// <summary>Detached configuration: no operation writes to its source, an installed package, or a live session.</summary>
internal sealed class NewGamePlusDraft
{
    private readonly GameState source;
    private GameState native;
    private NativeCharacterVitals guardianForces;
    private NativeCharacterVitals magic;
    private NativeEconomy items;
    private NativeCardInventory cards;
    private readonly Dictionary<byte, NewGamePlusCharacter> characters = [];
    internal UiPreferences Settings { get; set; }
    internal string SourceRevision { get; set; } = "";
    internal GameplayPreferences GameplaySettings { get; private set; }
    internal NewGamePlusPreset CharacterPreset { get; private set; } = NewGamePlusPreset.Stored;
    internal NewGamePlusPreset GuardianForcePreset { get; private set; } = NewGamePlusPreset.Stored;
    internal NewGamePlusPreset MagicPreset { get; private set; } = NewGamePlusPreset.Stored;
    internal NewGamePlusPreset ItemPreset { get; private set; } = NewGamePlusPreset.Stored;
    internal NewGamePlusPreset CardPreset { get; private set; } = NewGamePlusPreset.Stored;
    internal int ExperienceMultiplierTenths { get; set; } = 10;
    internal int ApMultiplierTenths { get; set; } = 10;
    internal int GlobalLevel { get; private set; }
    internal bool AllowWorldMapPartyChanges { get; private set; }
    internal IReadOnlyList<NewGamePlusCharacter> Characters => characters.Values.OrderBy(character => character.CharacterId).ToArray();
    internal NativeCharacterVitals GuardianForces => guardianForces;
    internal NativeCharacterVitals Magic => magic;
    internal NativeEconomy Items => items;
    internal NativeCardInventory Cards => cards;
    internal bool Dirty { get; private set; }
    internal MagicStockMove[] MagicOverflow { get; private set; } = [];
    internal MagicStockMove[] MagicConversionMoves { get; private set; } = [];

    internal NewGamePlusDraft(GameState source, GameState native, UiPreferences settings)
    {
        this.source = Detach(source);
        this.native = Detach(native);
        Settings = settings with { };
        GameplaySettings = native.GameplayPreferences;
        guardianForces = this.source.CharacterVitals!;
        guardianForces = guardianForces.ApplyGuardianForceTemplates(guardianForces.ResolveGuardianForceTemplates());
        magic = this.source.CharacterVitals!.WithGuestBattleRecords().DeepCopy();
        items = this.source.Economy!.DeepCopy();
        cards = ImportCards(this.source.CardInventory!);
        SeedCharacters(NewGamePlusPreset.Stored);
    }

    private static GameState Detach(GameState state)
    {
        if (state.CharacterVitals is null || state.Economy is null || state.CardInventory is null)
        {
            throw new InvalidDataException("New Game+ requires characters, items and cards from a readable completed game and pristine new game.");
        }
        return state with { CharacterVitals = state.CharacterVitals.DeepCopy(), Economy = state.Economy.DeepCopy(), CardInventory = state.CardInventory.DeepCopy() };
    }

    internal void SeedCharacters(NewGamePlusPreset preset)
    {
        if (preset is not (NewGamePlusPreset.Stored or NewGamePlusPreset.Native))
        {
            throw new ArgumentOutOfRangeException(nameof(preset));
        }
        NativeCharacterVitals target = native.CharacterVitals!.WithGuestBattleRecords();
        NativeCharacterVitals input = preset == NewGamePlusPreset.Stored ? source.CharacterVitals! : target;
        characters.Clear();
        for (byte identity = 0; identity < 11; identity++)
        {
            NativeCharacterVital? record = input.Records.FirstOrDefault(record => record.CharacterId == identity);
            NativeCharacterVital fallback = target.Records.Single(record => record.CharacterId == identity);
            int level = NativeStatDefinitions.Level((record ?? fallback).Experience, target.Definitions.Characters[identity]);
            characters.Add(identity, new(identity, level, (fallback.MemberFlags & 1) != 0, record is null));
        }
        GlobalLevel = 0;
        AllowWorldMapPartyChanges = false;
        CharacterPreset = preset;
    }

    internal void RevalidateNative(GameState cold)
    {
        native = Detach(cold);
        if (CharacterPreset == NewGamePlusPreset.Native)
        {
            SeedCharacters(NewGamePlusPreset.Native);
        }
        else
        {
            NativeCharacterVitals target = native.CharacterVitals!.WithGuestBattleRecords();
            foreach (NewGamePlusCharacter character in Characters.Where(character => character.NativeFallback))
            {
                NativeCharacterVital record = target.Records.Single(record => record.CharacterId == character.CharacterId);
                characters[character.CharacterId] = character with { IndividualLevel = NativeStatDefinitions.Level(record.Experience, target.Definitions.Characters[record.CharacterId]) };
            }
        }
        if (GuardianForcePreset == NewGamePlusPreset.Native)
        {
            guardianForces = native.CharacterVitals!.DeepCopy();
            guardianForces = guardianForces.ApplyGuardianForceTemplates(guardianForces.ResolveGuardianForceTemplates());
        }
        ConvertMagic(cold.GameplayPreferences);
    }

    internal void SetGlobalLevel(int value)
    {
        GlobalLevel = Math.Clamp(value, 0, 100);
        CharacterPreset = NewGamePlusPreset.Custom;
        Dirty = true;
    }

    internal void SetLevel(byte identity, int value)
    {
        if (GlobalLevel != 0)
        {
            return;
        }
        characters[identity] = characters[identity] with { IndividualLevel = Math.Clamp(value, 1, 100), NativeFallback = false };
        CharacterPreset = NewGamePlusPreset.Custom;
        Dirty = true;
    }

    internal void ToggleAvailability(byte identity)
    {
        characters[identity] = characters[identity] with { AvailableFromStart = !characters[identity].AvailableFromStart };
        CharacterPreset = NewGamePlusPreset.Custom;
        Dirty = true;
    }

    internal void ToggleWorldPartyChanges()
    {
        AllowWorldMapPartyChanges = !AllowWorldMapPartyChanges;
        CharacterPreset = NewGamePlusPreset.Custom;
        Dirty = true;
    }

    internal void Reset(string category, NewGamePlusPreset preset)
    {
        switch (category)
        {
            case "characters":
                SeedCharacters(preset);
                break;
            case "gf":
                guardianForces = (preset == NewGamePlusPreset.Stored ? source : native).CharacterVitals!.DeepCopy();
                guardianForces = guardianForces.ApplyGuardianForceTemplates(guardianForces.ResolveGuardianForceTemplates());
                GuardianForcePreset = preset;
                break;
            case "magic":
                magic = source.CharacterVitals!.WithGuestBattleRecords().DeepCopy();
                if (preset == NewGamePlusPreset.Empty)
                {
                    magic = ClearMagic(magic);
                }
                MagicPreset = preset;
                ConvertMagic(GameplaySettings);
                break;
            case "items":
                items = source.Economy!.DeepCopy();
                if (preset == NewGamePlusPreset.Empty)
                {
                    items = items with { Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray() };
                }
                ItemPreset = preset;
                break;
            case "cards":
                cards = preset == NewGamePlusPreset.Empty ? NativeCardInventory.CreateNewGame() : ImportCards(source.CardInventory!);
                CardPreset = preset;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(category));
        }
        Dirty = true;
    }

    internal void SetItem(int identity, int quantity)
    {
        quantity = Math.Clamp(quantity, 0, 100);
        NativeItemStock[] stock = items.Items.ToArray();
        int slot = Array.FindIndex(stock, item => item.ItemId == identity);
        if (slot < 0)
        {
            slot = Array.FindIndex(stock, item => item.ItemId == 0 || item.Count == 0);
        }
        if (slot < 0)
        {
            throw new InvalidOperationException("The item collection is full. Remove an item first.");
        }
        stock[slot] = quantity == 0 ? new(0, 0) : new(checked((byte)identity), checked((byte)quantity));
        items = items with { Items = stock };
        ItemPreset = NewGamePlusPreset.Custom;
        Dirty = true;
    }

    internal void SetCard(int identity, int quantity)
    {
        quantity = Math.Clamp(quantity, 0, identity < NativeCardInventory.CommonCardCount ? 100 : 1);
        NativeCardInventory next = cards.DeepCopy();
        if (identity < NativeCardInventory.CommonCardCount)
        {
            next.Cards[identity] = (byte)(quantity == 0 ? 0 : 0x80 | quantity);
        }
        else if (quantity != 0)
        {
            next = next.Acquire(identity, out _);
        }
        else
        {
            NativeCardInventory empty = NativeCardInventory.CreateNewGame();
            next.Cards[identity] = empty.Cards[identity];
            int rare = identity - NativeCardInventory.CommonCardCount;
            next.ObtainedRareCards[rare / 8] &= (byte)(0xFF ^ (1 << (rare & 7)));
        }
        cards = next;
        CardPreset = NewGamePlusPreset.Custom;
        Dirty = true;
    }

    internal void SetMagic(int row, int spell, int quantity)
    {
        int current = magic.GetMagicCount(row, spell);
        magic = magic.ChangeMagicStock(row, spell, Math.Clamp(quantity, 0, 100) - current, MagicOperationReason.Transfer, out MagicStockMutation mutation);
        if (mutation.Rejected != 0)
        {
            throw new InvalidOperationException("This magic bank has no space for that spell. Remove another spell first.");
        }
        MagicPreset = NewGamePlusPreset.Custom;
        Dirty = true;
    }

    internal void SetRetainedMagic(byte ownerIdentity, int spell, int quantity)
    {
        int row = Array.FindIndex(magic.Records, record => record.CharacterId == ownerIdentity);
        if (!magic.MagicSystem.SharedMagicStock || row <= 0 || spell is < 1 or > 56 || quantity is < 0 or > 100)
        {
            throw new InvalidOperationException("Select a retained personal stock from the shared magic collection.");
        }
        NativeCharacterVitals next = magic.DeepCopy();
        int slot = Array.FindIndex(next.Records[row].MagicInventory, stock => stock.SpellId == spell && stock.Count > 0);
        if (slot < 0)
        {
            throw new InvalidOperationException("This character has no retained stock of that spell.");
        }
        next.Records[row].MagicInventory[slot] = quantity == 0 ? new(0, 0) : new(checked((byte)spell), checked((byte)quantity));
        next.ValidateMagicSystemConsistency();
        magic = next;
        RefreshMagicOverflow();
        MagicPreset = NewGamePlusPreset.Custom;
        Dirty = true;
    }

    internal void RemoveMagicEverywhere(int spell)
    {
        if (spell is < 1 or > 56)
        {
            throw new ArgumentOutOfRangeException(nameof(spell));
        }
        NativeCharacterVitals next = magic.DeepCopy();
        foreach (NativeCharacterVital record in next.Records)
        {
            for (int slot = 0; slot < record.MagicInventory.Length; slot++)
            {
                if (record.MagicInventory[slot].SpellId == spell)
                {
                    record.MagicInventory[slot] = new(0, 0);
                }
            }
        }
        next = next with { MagicSystem = next.MagicSystem with { RecordZeroExtension = next.MagicSystem.RecordZeroExtension.Where(stock => stock.SpellId != spell).ToArray() } };
        next = MagicJunctionPolicy.Reconcile(next, out _);
        next.ValidateMagicSystemConsistency();
        magic = next;
        RefreshMagicOverflow();
        MagicPreset = NewGamePlusPreset.Custom;
        Dirty = true;
    }

    private void RefreshMagicOverflow()
    {
        MagicOverflow = magic.MagicSystem.SharedMagicStock ? magic.Records.SelectMany((record, row) => row == 0 ? [] : record.MagicInventory.Where(stock => stock.SpellId > 0 && stock.Count > 0).Select(stock => new MagicStockMove(row, stock.SpellId, stock.Count))).ToArray() : [];
    }

    internal void ConvertMagic(GameplayPreferences preferences)
    {
        GameplaySettings = preferences;
        MagicModePreview preview = MagicModeCoordinator.Preview(magic, preferences);
        if (magic.MagicSystem.SharedMagicStock != preferences.SharedMagicStock)
        {
            MagicConversionMoves = preview.Moves;
        }
        magic = preview.Vitals;
        RefreshMagicOverflow();
    }

    internal void ToggleGuardianForce(int identity)
    {
        guardianForces = guardianForces.DeepCopy();
        NativeGuardianForceVital gf = guardianForces.GuardianForces[identity];
        guardianForces.GuardianForces[identity] = gf with { Flags = (byte)(gf.Flags ^ 1) };
        GuardianForcePreset = NewGamePlusPreset.Custom;
        Dirty = true;
    }

    internal void ToggleAbility(int identity, int ability)
    {
        guardianForces = guardianForces.DeepCopy();
        NativeGuardianForceVital gf = guardianForces.GuardianForces[identity];
        GuardianForceProgression progress = gf.Progression ?? new(false, []);
        GuardianForceAbilityProgress old = progress.Abilities.FirstOrDefault(entry => entry.Ability == ability) ?? new(ability, false);
        GuardianForceAbilityProgress replacement = old with { Learned = !old.Learned, EarnedPoints = 0, Forgotten = false, Active = true };
        guardianForces.GuardianForces[identity] = gf with { Progression = progress with { Initialized = true, Abilities = progress.Abilities.Where(entry => entry.Ability != ability).Append(replacement).ToImmutableArray() } };
        guardianForces = guardianForces.ApplyGuardianForceTemplates(guardianForces.ResolveGuardianForceTemplates());
        GuardianForcePreset = NewGamePlusPreset.Custom;
        Dirty = true;
    }

    internal void AddAbility(int identity, int ability)
    {
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates = guardianForces.ResolveGuardianForceTemplates();
        if (templates[identity].Any(entry => entry.Ability == ability))
        {
            throw new InvalidOperationException("This Guardian Force already has that ability.");
        }
        guardianForces = guardianForces.ApplyGuardianForceTemplates(templates.SetItem(identity, templates[identity].Add(new(ability, false, guardianForces.MenuDefinitions.AbilityPoints[ability]))));
        GuardianForcePreset = NewGamePlusPreset.Custom;
        Dirty = true;
    }

    internal void RemoveAbility(int identity, int ability)
    {
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates = guardianForces.ResolveGuardianForceTemplates();
        guardianForces = guardianForces.ApplyGuardianForceTemplates(templates.SetItem(identity, templates[identity].Where(entry => entry.Ability != ability).ToImmutableArray()));
        NativeGuardianForceVital gf = guardianForces.GuardianForces[identity];
        if (gf.Progression is { } progress)
        {
            guardianForces.GuardianForces[identity] = gf with { Learning = gf.Learning == ability ? (byte)0 : gf.Learning, Progression = progress with { Abilities = progress.Abilities.Where(entry => entry.Ability != ability).ToImmutableArray() } };
        }
        GuardianForcePreset = NewGamePlusPreset.Custom;
        Dirty = true;
    }

    private static NativeCharacterVitals ClearMagic(NativeCharacterVitals vitals)
    {
        return vitals with { Records = vitals.Records.Select(record => record with { MagicInventory = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray() }).ToArray(), MagicSystem = vitals.MagicSystem with { RecordZeroExtension = [], Reservations = [] } };
    }

    private static NativeCardInventory ImportCards(NativeCardInventory source)
    {
        NativeCardInventory result = NativeCardInventory.CreateNewGame();
        for (int id = 0; id < NativeCardInventory.CardCount; id++)
        {
            int count = Math.Max(0, source.AvailableCount(id));
            if (id < NativeCardInventory.CommonCardCount)
            {
                result.Cards[id] = count == 0 ? (byte)0 : checked((byte)(0x80 | count));
            }
            else if (count > 0)
            {
                result = result.Acquire(id, out _);
            }
        }
        return result;
    }

    private NativeCharacterVitals FreezeGuardianForces()
    {
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates = guardianForces.ResolveGuardianForceTemplates();
        return NewGamePlusGuardianForceDefinitions.BindEmbedded(guardianForces, templates, GuardianForceExclusions());
    }

    private ImmutableArray<string> GuardianForceExclusions() => (guardianForces.GuardianForceProfile?.AppliedLayers.Select(layer => layer.Document.DocumentId) ?? []).Concat(guardianForces.GuardianForceProfile?.LocalDocuments.Select(document => document.DocumentId) ?? []).Concat(guardianForces.GuardianForceProfile?.ExcludedDocuments ?? []).Distinct(StringComparer.Ordinal).ToImmutableArray();

    internal NewGamePlusDefinition ResolveDefinition()
    {
        NewGamePlusDefinition definition = new()
        {
            SourceCompletionIdentity = source.CompletedGame?.Completion.Identity ?? source.Ff8Completion?.Identity ?? "", SourceContentIdentity = source.CompletedGame?.ContentIdentity ?? source.CharacterVitals!.KernelSha256, SourceRevision = SourceRevision, Settings = GameplaySettings,
            RequiredCapabilities = Characters.Any(character => character.CharacterId >= 6 && character.AvailableFromStart) ? ["extended-roster-v1"] : [],
            GuardianForceExcludedDocuments = GuardianForceExclusions(),
            InitialMagicConversionMoves = MagicConversionMoves.ToImmutableArray(), InitialMagicOverflow = MagicOverflow.ToImmutableArray(),
            Characters = Characters.ToImmutableArray(), GlobalLevel = GlobalLevel, AllowWorldMapPartyChanges = AllowWorldMapPartyChanges,
            ExperienceMultiplierTenths = ExperienceMultiplierTenths, ApMultiplierTenths = ApMultiplierTenths,
            CharacterPreset = CharacterPreset, GuardianForcePreset = GuardianForcePreset, MagicPreset = MagicPreset, ItemPreset = ItemPreset, CardPreset = CardPreset,
            InitialItems = items.Items.ToImmutableArray(), InitialCardQuantities = Enumerable.Range(0, 110).Select(identity => Math.Max(0, cards.AvailableCount(identity))).ToImmutableArray(),
            InitialMagic = magic.Records.Select(record => new NewGamePlusMagicBank(record.CharacterId, record.MagicInventory.ToImmutableArray())).ToImmutableArray(), InitialMagicExtension = magic.MagicSystem.RecordZeroExtension.ToImmutableArray(),
            InitialGuardianForces = GameplaySaveCharacters.Capture(FreezeGuardianForces()).GuardianForces.ToImmutableArray(), InitialGuardianForceTemplates = guardianForces.ResolveGuardianForceTemplates()
        };
        definition.Validate();
        return definition;
    }

    internal GameState ApplyTo(GameState cold)
    {
        NewGamePlusDefinition definition = ResolveDefinition();
        NativeCharacterVitals result = cold.CharacterVitals!.WithGuestBattleRecords().DeepCopy();
        result = result with { GuardianForces = guardianForces.GuardianForces.ToArray(), GuardianForceProfile = FreezeGuardianForces().GuardianForceProfile, MenuDefinitions = result.MenuDefinitions with { SourceTemplates = guardianForces.ResolveGuardianForceTemplates() } };
        result = result.ApplyGuardianForceTemplates(guardianForces.ResolveGuardianForceTemplates());
        result = result with { ExtendedRosterEnabled = definition.RequiresExpandedRoster, NewGamePlusPermanentGuests = (ushort)Characters.Where(character => character.CharacterId >= 6 && character.AvailableFromStart).Aggregate(0, (mask, character) => mask | (1 << character.CharacterId)) };
        for (int row = 0; row < result.Records.Length; row++)
        {
            NativeCharacterVital record = result.Records[row];
            NewGamePlusCharacter choice = characters[record.CharacterId];
            uint low = 0;
            uint high = int.MaxValue;
            while (low < high)
            {
                uint middle = low + (high - low) / 2;
                if (NativeStatDefinitions.Level(middle, result.Definitions.Characters[record.CharacterId]) >= definition.ResolvedLevel(record.CharacterId))
                {
                    high = middle;
                }
                else
                {
                    low = middle + 1;
                }
            }
            NewGamePlusMagicBank? bank = definition.InitialMagic.FirstOrDefault(bank => bank.CharacterId == record.CharacterId);
            result.Records[row] = record with { Experience = low, Status = 0, MemberFlags = (ushort)(choice.AvailableFromStart ? record.MemberFlags | 1 : record.MemberFlags & ~1), MagicInventory = bank?.Stock.ToArray() ?? Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray() };
        }
        result = result with { MagicSystem = result.MagicSystem.DeepCopy() with { SharedMagicStock = magic.MagicSystem.SharedMagicStock, RecordZeroExtension = magic.MagicSystem.RecordZeroExtension.ToArray() } };
        result = MagicModeCoordinator.Preview(result, cold.GameplayPreferences).Vitals;
        for (int row = 0; row < result.Records.Length; row++)
        {
            ushort hp = result.GetMagicMaximumHitPoints(row);
            result.Records[row] = result.Records[row] with { HitPoints = new(hp, hp) };
        }
        for (int id = 0; id < 16; id++)
        {
            NativeGuardianForceVital gf = result.GuardianForces[id];
            ushort hp = result.Definitions.GuardianForceMaximum(id, gf);
            result.GuardianForces[id] = gf with { HitPoints = hp, MaximumHitPoints = hp };
        }
        result.Validate();
        definition = definition with { StartingChoicesApplied = true, InitializedCharacters = result.Records.Select(record => record.CharacterId).ToImmutableArray(), InitializedGuardianForces = Enumerable.Range(0, 16).Select(id => (byte)id).ToImmutableArray() };
        return cold with { CharacterVitals = result, Economy = cold.Economy! with { Items = items.Items.ToArray() }, CardInventory = cards.DeepCopy(), NewGamePlus = definition };
    }
}
