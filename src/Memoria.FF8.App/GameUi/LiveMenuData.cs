using System.Collections.Immutable;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.MngrpBin;
using Albeoris.Games.FF8.MngrpBin.Abstractions;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources;
using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

/// <summary>Projects canonical session owners and supported input kinds without creating or mutating native state.</summary>
public static class LiveMenuData
{
    public static MenuData Build(GameState state, IReadOnlyDictionary<int, string>? names = null, LiveMenuCatalog? catalog = null, IReadOnlySet<int>? visibleCharacterRows = null, int? placeId = null, bool includeJunctionPlanner = true)
    {
        return BuildCore(state, names, catalog, visibleCharacterRows, placeId, includeJunctionPlanner, null);
    }

    internal static MenuData BuildJunctionSnapshot(GameState state, IReadOnlyDictionary<int, string>? names, LiveMenuCatalog? catalog, int? placeId, MenuData? baseline = null, IReadOnlySet<int>? changedRows = null, Func<int, BattleNativePlayerStatRules>? readRules = null)
    {
        return BuildCore(state, names, catalog, Enumerable.Range(0, 8).ToHashSet(), placeId, false, new JunctionReadOptions(baseline, changedRows, readRules));
    }

    private sealed record JunctionReadOptions(MenuData? Baseline, IReadOnlySet<int>? ChangedRows, Func<int, BattleNativePlayerStatRules>? ReadRules);

    private static MenuData BuildCore(GameState state, IReadOnlyDictionary<int, string>? names, LiveMenuCatalog? catalog, IReadOnlySet<int>? visibleCharacterRows, int? placeId, bool includeJunctionPlanner, JunctionReadOptions? junction)
    {
        ArgumentNullException.ThrowIfNull(state);
        string Text(string id, string original) => catalog?.SystemText(id, original) ?? original;
        string Format(string id, string original, params object?[] values) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Format(Text(id, original), original, values);
        IReadOnlyDictionary<int, string> textNames = TextNames(state, names);
        catalog?.SetTextNames(textNames);
        MenuData result = new()
        {
            SharedMagicStock = state.CharacterVitals?.MagicSystem.SharedMagicStock == true,
            PlayTime = state.Playtime is { } playtime ? TimeSpan.FromSeconds(playtime.Seconds) : null,
            Location = catalog?.LocationName(state, placeId) ?? Text("common.unavailable", "Unavailable"),
            SourceDescription = Text("projection.session.source", "Live session: native inventory, party and junction records"),
            UnavailableReason = Text("projection.action.unsupported", "The current engine does not support this native menu action.")
        };
        if (state.LimitProgress?.PetPalsFlags is not null)
        {
            result.SupportedActions.Add("angelo-learn");
        }
        if (junction is null && state.Economy is { } economy)
        {
            economy.Validate();
            result.Currency = checked((int)economy.Gil);
            if (state.Scene is SceneKind.Field or SceneKind.WorldMap)
            {
                result.SupportedActions.Add("item-sort");
            }
            foreach (NativeItemStock item in economy.Items.Where(item => item.ItemId != 0 && item.Count != 0))
            {
                result.Items.Add(new MenuItem { Id = $"item-{item.ItemId}", Icon = catalog?.ItemIcon(item.ItemId), Name = catalog?.ItemName(item.ItemId) ?? Format("fallback.item.name", "Item {0}", item.ItemId), Quantity = item.Count, NativeMagazinePages = state.Scene is SceneKind.Field or SceneKind.WorldMap ? catalog?.MagazinePages(item.ItemId) ?? [] : [], NativeMagazinePageUnavailableReasons = catalog?.MagazineUnavailableReasons(item.ItemId, state) ?? [], Description = catalog?.ItemDescription(item.ItemId) ?? Text("projection.item.effect-unavailable", "Item effect unavailable; using, discarding and refining are unsupported."), NativeFieldUseSupported = state.Scene is SceneKind.Field or SceneKind.WorldMap && catalog?.FieldItem(item.ItemId) is { } definition && (definition.SupportsNameUse && (definition.Type != 13 || state.PocketStation?.ChocoboName is not null || names?.ContainsKey(GameNames.BokoSubject) == true || SavedName(state, GameNames.BokoSubject) is not null) || definition.SupportsBattleUse && state.CharacterVitals?.DreamEnabled == false && !MenuBattleTimerBlocked(state) || definition.SupportsCharacterUse || definition.SupportsGuardianForceUse || definition.SupportsInventoryUse || definition.SupportsRestUse && state.Scene == SceneKind.Field && (state.WorldControls?.Menu209 & 1) != 0 || definition.CanLearnBlueMagic(state.LimitProgress) && state.CharacterVitals?.DreamEnabled == false), NativeFieldBattleUse = catalog?.FieldItem(item.ItemId)?.SupportsBattleUse == true, NativeFieldNameConfirmation = catalog?.FieldItem(item.ItemId)?.Type is 12 or 13, NativeFieldNameSubject = catalog?.FieldItem(item.ItemId)?.Type switch { 12 => 2, 13 => GameNames.BokoSubject, _ => null }, NativeFieldTargetsGuardianForce = catalog?.FieldItem(item.ItemId) is { } targetItem && (targetItem.SupportsGuardianForceUse || targetItem.Type == 11), NativeFieldQuistisOnly = catalog?.FieldItem(item.ItemId)?.SupportsBlueMagicUse == true, NativeFieldForgetsAbility = catalog?.FieldItem(item.ItemId)?.Type == 18, NativeFieldInventoryTarget = catalog?.FieldItem(item.ItemId) is { } inventoryItem && (inventoryItem.SupportsInventoryUse || inventoryItem.SupportsRestUse) });
                result.Items[^1].RecoveryTargetScores = catalog?.ItemRecoveryTargetScores(state, item.ItemId) ?? new(StringComparer.Ordinal);
            }
        }
        if (state.Scene is SceneKind.Field or SceneKind.WorldMap && state.CharacterVitals is not null && result.Items.Any(item => item.NativeFieldUseSupported))
        {
            result.SupportedActions.Add("item-use");
            result.SupportedActions.Add("item-battle");
        }
        if (junction is null && state.SeeD is { SalaryUiEnabled: true } seeD)
        {
            seeD.Validate();
            result.SeeDRank = seeD.RankPoints / 100;
        }
        if (state.CharacterVitals is { } vitals)
        {
            vitals.Validate();
            state.WorldControls?.Validate();
            ProjectionCache projection = new(vitals, catalog, state.WorldControls, junction?.ReadRules);
            Dictionary<string, MenuMagic>? baselineMagic = junction?.Baseline?.Magic.ToDictionary(magic => magic.Id, StringComparer.Ordinal);
            Dictionary<int, (string Name, string Description)> magicMetadata = [];
            Dictionary<int, string> casterNames = [];
            string unboundText = Text("magic.Unbound", MagicUiText.Get("Unbound"));
            string unavailableOwnerText = Text("magic.UnavailableOwner", MagicUiText.Get("UnavailableOwner"));
            string restrictedText = Text("magic.StatusRestricted", MagicUiText.Get("StatusRestricted"));
            string exhaustedText = Text("magic.Exhausted", MagicUiText.Get("Exhausted"));
            string CasterName(int row)
            {
                if (!casterNames.TryGetValue(row, out string? text))
                {
                    text = Format("magic.Caster", MagicUiText.Get("Caster"), CharacterName(state, vitals.Records[row].CharacterId, names, catalog));
                    casterNames.Add(row, text);
                }
                return text;
            }
            if (junction is null && state.Scene is SceneKind.Field or SceneKind.WorldMap && !vitals.DreamEnabled && ((state.WorldControls?.Menu209 ?? 0) & 2) != 0)
            {
                result.SupportedActions.Add("party-switch");
                for (int source = 0; source < 8; source++)
                {
                    for (int target = 0; target < 8; target++)
                    {
                        if (Memoria.FF8.Events.NativePartySwitchInput.CanExchange(vitals, source, target, state.WorldControls!.Menu209))
                        {
                            result.SupportedActions.Add($"party-switch:character-{source}:character-{target}");
                        }
                    }
                }
            }
            result.JunctionExchangeLowersHp = (sourceId, targetId) =>
            {
                MenuAction menu = new("junction-exchange", sourceId, targetId);
                Memoria.FF8.Events.PlayerAction input = NativeJunctionMenuProjection.PlayerAction(menu);
                int target = int.Parse(input.Text!, System.Globalization.CultureInfo.InvariantCulture);
                NativeCharacterVitals exchanged = vitals.ExchangeJunctions(input.Value, target);
                return exchanged.GetMagicMaximumHitPoints(input.Value) < vitals.Records[input.Value].HitPoints.Maximum || exchanged.GetMagicMaximumHitPoints(target) < vitals.Records[target].HitPoints.Maximum;
            };
            IEnumerable<int> rows = vitals.CombatParty.Where(row => row != 255).Select(row => (int)row).Concat(Enumerable.Range(0, vitals.Records.Length).Where(row => !vitals.CombatParty.Contains((byte)row)));
            foreach (int row in rows)
            {
                NativeCharacterVital native = vitals.Records[row];
                bool inParty = vitals.CombatParty.Contains((byte)row);
                bool member = native.CharacterId < 8 ? (native.MemberFlags & 1) != 0 : state.WorldControls is { } controls && (controls.EventFlags104 & (1u << (native.CharacterId - 8))) != 0;
                if (!inParty && !member && visibleCharacterRows?.Contains(row) != true)
                {
                    continue;
                }
                MenuCharacter? cachedCharacter = junction?.Baseline is { } baseline && junction.ChangedRows?.Contains(row) == false ? baseline.Characters.FirstOrDefault(character => character.Id == $"character-{row}") : null;
                if (cachedCharacter is not null && !vitals.MagicSystem.SharedMagicStock)
                {
                    result.Characters.Add(cachedCharacter);
                    result.Magic.AddRange(junction!.Baseline!.Magic.Where(magic => magic.CharacterId == cachedCharacter.Id));
                    continue;
                }
                MenuCharacter character;
                if (cachedCharacter is not null)
                {
                    character = cachedCharacter;
                }
                else
                {
                    character = new()
                    {
                        SavedStatuses = native.Status,
                        AttackMode = vitals.MagicSystem.AttackModes[row].ToString(),
                        NativeFieldItemTargetAllowed = (native.MemberFlags & 1) != 0 && (!vitals.DreamEnabled || inParty),
                        Id = $"character-{row}",
                        NativeId = native.CharacterId,
                        Name = CharacterName(state, native.CharacterId, names, catalog),
                        InParty = inParty,
                        Level = NativeStatDefinitions.Level(native.Experience, vitals.Definitions.Characters[native.CharacterId]),
                        CurrentHp = native.HitPoints.Minimum == native.HitPoints.Maximum ? native.HitPoints.Minimum : null,
                        HpDisplay = native.HitPoints.Minimum == native.HitPoints.Maximum ? native.HitPoints.Minimum.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"{native.HitPoints.Minimum}..{native.HitPoints.Maximum}",
                        MaximumHp = vitals.GetMagicMaximumHitPoints(row),
                        Experience = native.Experience <= int.MaxValue ? (int)native.Experience : null,
                        Weapon = catalog?.WeaponName(vitals.DreamEnabled && native.CharacterId >= 8 ? vitals.DreamWeapons[native.CharacterId - 8] : native.Weapon) ?? Format("fallback.weapon.name", "Weapon {0}", (vitals.DreamEnabled && native.CharacterId >= 8 ? vitals.DreamWeapons[native.CharacterId - 8] : native.Weapon))
                    };
                    if (junction is null)
                    {
                        catalog?.ProjectLimit(character, native.Weapon, state, textNames);
                    }
                    (int Mask, string Key, string Name)[] savedConditions = [(1, "ko", "KO"), (2, "poison", "Poison"), (4, "petrify", "Petrify"), (8, "blind", "Blind"), (16, "silence", "Silence"), (32, "berserk", "Berserk"), (64, "zombie", "Zombie")];
                    foreach (var condition in savedConditions)
                    {
                        if ((native.Status & condition.Mask) != 0)
                        {
                            character.Conditions.Add(Text("battle.status." + condition.Key, condition.Name));
                        }
                    }
                    foreach (int id in native.Junction.GuardianForceAssignments)
                    {
                        character.GuardianForceIds.Add($"gf-{id}");
                    }
                    BattleNativePlayerStatRules? projectedRules = catalog is null ? null : projection.Rules(row);
                    uint passiveFlags = projectedRules?.NativeStatusPassiveFlags ?? 0;
                    int basicCommand = (passiveFlags & 1) != 0 ? 12 : 1;
                    bool supportsSummon = native.Junction.GuardianForceAssignments.Any(id => NativeAbilityMenuMetadata.SupportsSummon(id, state.GameplayPreferences.GuardianFormsEnabled)) && vitals.JunctionLearnedAbilities(row).Contains(21);
                    character.Commands.Add(catalog?.CommandName(basicCommand) ?? Text("fallback.command.attack", "Attack"));
                    for (int slot = 0; slot < 4; slot++)
                    {
                        byte command = unchecked((byte)(native.Junction.Commands >> (8 * slot)));
                        byte ability = unchecked((byte)(native.Junction.Abilities >> (8 * slot)));
                        if (slot < 3)
                        {
                            character.CommandIds.Add(command.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            character.Commands.Add(command == 0 ? "--" : NativeAbilityMenuMetadata.CommandName(command, catalog?.AbilityName(command) ?? Format("fallback.command-ability.name", "Command ability {0}", command), supportsSummon, Text));
                        }
                        character.AbilityIds.Add(ability.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        if (slot < vitals.JunctionAbilitySlots(row))
                        {
                            character.Abilities.Add(ability == 0 ? "--" : catalog?.AbilityName(ability) ?? Format("fallback.ability.name", "Ability {0}", ability));
                        }
                    }
                    character.AbilitySlots = vitals.JunctionAbilitySlots(row);
                    foreach (int ability in vitals.JunctionLearnedAbilities(row).Order())
                    {
                        MenuAbilityChoice choice = new(ability.ToString(System.Globalization.CultureInfo.InvariantCulture), NativeAbilityMenuMetadata.CommandName(ability, catalog?.AbilityName(ability) ?? Format("fallback.ability.name", "Ability {0}", ability), supportsSummon, Text), true) { Icon = NativeAbilityMenuMetadata.Icon(ability), Description = catalog?.AbilityDescription(ability) ?? Text("projection.ability.description-unavailable", "Ability description unavailable.") };
                        if (ability is >= 20 and < 39)
                        {
                            character.AvailableCommands.Add(choice);
                        }
                        if (ability is >= 39 and < 83 && !NativeCharacterVitals.IsGuardianForcePassiveAbility(ability))
                        {
                            character.AvailableAbilities.Add(choice);
                        }
                    }
                    character.NextLevelExperience = ExperienceToNextLevel(native.Experience, vitals.Definitions.Characters[native.CharacterId]);
                    if (catalog is not null)
                    {
                        int characterRow = row;
                        BattleNativePlayerStatRules rules = projectedRules!;
                        IReadOnlyList<NativeMagicStock> inventory = projection.Inventory(row);
                        BattleNativePlayerStatistics projected = rules.Project(inventory, new BattleNativeCharacterGrowth(native.HitPointBonus, native.StatBonuses.ToImmutableArray()));
                        foreach (var stat in Statistics(projected))
                        {
                            character.Stats.Add(stat.Key, stat.Value);
                        }
                        character.JunctionEffects = JunctionEffects(rules, native, inventory, projected);
                        NativeCharacterVitals Preview(string slotName, string spellId)
                        {
                            int slot = Enumerable.Range(0, 19).Single(index => JunctionSlotName(index) == slotName);
                            string prefix = $"magic-{characterRow}-";
                            if (spellId != "0" && !spellId.StartsWith(prefix, StringComparison.Ordinal))
                            {
                                throw new ArgumentException("Junction preview requires the selected character's stock.", nameof(spellId));
                            }
                            int spell = spellId == "0" ? 0 : int.Parse(spellId.AsSpan(prefix.Length), System.Globalization.CultureInfo.InvariantCulture);
                            return vitals.SetJunctionMagic(characterRow, slot, spell, true);
                        }
                        character.JunctionPreview = (slotName, spellId) =>
                        {
                            NativeCharacterVitals preview = Preview(slotName, spellId);
                            return Statistics(catalog.ReadStatRules(preview, characterRow), preview.Records[characterRow], preview.GetMagicInventory(characterRow));
                        };
                        character.JunctionEffectsPreview = (slotName, spellId) =>
                        {
                            NativeCharacterVitals preview = Preview(slotName, spellId);
                            return JunctionEffects(catalog.ReadStatRules(preview, characterRow), preview.Records[characterRow], preview.GetMagicInventory(characterRow));
                        };
                    }
                    for (int slot = 0; slot < 19; slot++)
                    {
                        if (vitals.JunctionMagicSlotAvailable(row, slot))
                        {
                            character.AvailableJunctionSlots.Add(JunctionSlotName(slot));
                        }
                        byte spell = native.Junction.MagicJunctions[slot];
                        character.JunctionIds[JunctionSlotName(slot)] = spell == 0 ? "0" : $"magic-{row}-{spell}";
                        if (spell != 0)
                        {
                            character.Junctions[JunctionSlotName(slot)] = catalog?.MagicName(spell) ?? Format("fallback.magic.name", "Magic {0}", spell);
                        }
                    }
                }
                foreach (NativeMagicStock stock in projection.Inventory(row).Where(stock => stock.SpellId != 0 && stock.Count != 0))
                {
                    // Native4C8990 draws icon192 for a junctioned stock; ordinary rows have no category icon.
                    string magicId = $"magic-{row}-{stock.SpellId}";
                    int? owner = vitals.MagicSystem.SharedMagicStock ? projection.Owner(stock.SpellId) : NativeCharacterVitals.IsMagicJunctioned(native, stock.SpellId) ? row : null;
                    int? caster = catalog is null ? null : projection.Caster(row, stock.SpellId, owner);
                    MagicCastQuote quote = projection.Quote(caster ?? row, stock.SpellId);
                    string availability = catalog is null ? "" : caster is null ? owner is not null && owner != row ? unavailableOwnerText : restrictedText : !quote.Allowed ? exhaustedText : "";
                    MenuMagic? previous = baselineMagic?.GetValueOrDefault(magicId);
                    MenuMagic magic;
                    if (previous is not null)
                    {
                        magic = previous.CopyForJunction();
                    }
                    else
                    {
                        if (!magicMetadata.TryGetValue(stock.SpellId, out var metadata))
                        {
                            metadata = (catalog?.MagicName(stock.SpellId) ?? Format("fallback.magic.name", "Magic {0}", stock.SpellId), catalog?.MagicDescription(stock.SpellId) ?? Text("projection.magic.description-unavailable", "Magic description unavailable."));
                            magicMetadata.Add(stock.SpellId, metadata);
                        }
                        magic = new MenuMagic
                        {
                            SpellId = stock.SpellId,
                            RecoveryTargetScores = junction is null ? catalog?.MagicRecoveryTargetScores(state, stock.SpellId) ?? new(StringComparer.Ordinal) : new(StringComparer.Ordinal),
                            Id = magicId,
                            CharacterId = character.Id,
                            Name = metadata.Name,
                            Quantity = stock.Count,
                            JunctionBaseDescription = metadata.Description,
                            UnlimitedUses = vitals.MagicSystem.UnlimitedMagic
                        };
                    }
                    magic.OwnerCharacterId = owner is int ownerRecord ? $"character-{ownerRecord}" : null;
                    magic.BoundSlot = owner is int bindingRecord ? Enumerable.Range(0, 19).Where(index => vitals.Records[bindingRecord].Junction.MagicJunctions[index] == stock.SpellId).Select(JunctionSlotName).FirstOrDefault() : null;
                    magic.Icon = owner is not null ? new UiImageSource(UiImageKind.Icon, 192) : null;
                    magic.RemainingUses = vitals.MagicSystem.InfiniteMagic && !vitals.MagicSystem.UnlimitedMagic ? quote.Available : null;
                    magic.Capacity = vitals.MagicSystem.InfiniteMagic && !vitals.MagicSystem.UnlimitedMagic ? quote.Capacity : null;
                    magic.Owner = owner is null && vitals.MagicSystem.SharedMagicStock ? unboundText : "";
                    magic.OwnerIcon = owner is int ownerRow ? new UiImageSource(UiImageKind.CharacterPortrait, vitals.Records[ownerRow].CharacterId) : null;
                    magic.Availability = availability;
                    magic.Caster = caster is int casterRow && vitals.MagicSystem.SharedMagicStock ? CasterName(casterRow) : "";
                    magic.JunctionAvailable = owner is null || owner == row;
                    magic.Description = string.Join(" · ", new[] { magic.JunctionBaseDescription, magic.AccessPresentation }.Where(value => value.Length > 0));
                    result.Magic.Add(magic);
                    if (state.Scene is SceneKind.Field or SceneKind.WorldMap && character.CurrentHp is not null && catalog?.IsFieldMagic(stock.SpellId) == true && caster is not null && quote.Allowed)
                    {
                        result.SupportedActions.Add($"magic-use:{magicId}");
                    }
                }
                result.Characters.Add(character);
            }
            bool reuseGuardianForces = junction?.Baseline is { } prior && Enumerable.Range(0, 8).All(row => prior.Characters.First(character => character.Id == $"character-{row}").GuardianForceIds.SequenceEqual(result.Characters.First(character => character.Id == $"character-{row}").GuardianForceIds));
            if (reuseGuardianForces)
            {
                result.GuardianForces.AddRange(junction!.Baseline!.GuardianForces);
            }
            for (int id = 0; !reuseGuardianForces && id < vitals.GuardianForces.Length; id++)
            {
                NativeGuardianForceVital native = vitals.GuardianForces[id];
                if ((native.Flags & 1) == 0)
                {
                    continue;
                }
                int ownerRow = Enumerable.Range(0, vitals.Records.Length).FirstOrDefault(row => (vitals.Records[row].Junction.GuardianForces & (1 << id)) != 0, -1);
                string AbilityName(int ability, string original, string key) => NativeAbilityMenuMetadata.CommandName(ability, catalog?.AbilityName(ability) ?? Format(key, original, ability), NativeAbilityMenuMetadata.SupportsSummon(id, state.GameplayPreferences.GuardianFormsEnabled), Text);
                MenuGuardianForce guardianForce = new()
                {
                    Id = $"gf-{id}",
                    NativeId = id,
                    Owner = ownerRow < 0 ? null : new MenuGuardianForceOwner($"character-{ownerRow}", vitals.Records[ownerRow].CharacterId, CharacterName(state, vitals.Records[ownerRow].CharacterId, names, catalog)),
                    Name = Nonempty(names?.GetValueOrDefault(GameNames.GuardianForceSubject(id))) ?? SavedName(state, GameNames.GuardianForceSubject(id)) ?? catalog?.GuardianForceName(id) ?? GameNames.DefaultName(GameNames.GuardianForceSubject(id)),
                    Level = NativeStatDefinitions.Level(native.Experience, vitals.Definitions.GuardianForces[id]),
                    CurrentHp = native.HitPoints,
                    MaximumHp = vitals.Definitions.GuardianForceMaximum(id, native),
                    Experience = native.Experience <= int.MaxValue ? (int)native.Experience : null,
                    NextLevelExperience = ExperienceToNextLevel(native.Experience, vitals.Definitions.GuardianForces[id]),
                    Learning = native.Learning == 0 ? "--" : AbilityName(native.Learning, "Ability {0}", "fallback.ability.name"),
                    LearningAbilityId = native.Learning == 0 ? null : native.Learning.ToString(System.Globalization.CultureInfo.InvariantCulture)
                };
                foreach (NativeGuardianForceAbility entry in vitals.GuardianForceAbilities(id))
                {
                    if (entry.Learned)
                    {
                        guardianForce.Abilities.Add(AbilityName(entry.Ability, "Learned ability {0}", "fallback.learned-ability.name"));
                    }
                    guardianForce.LearningChoices.Add(new MenuAbilityChoice(entry.Ability.ToString(System.Globalization.CultureInfo.InvariantCulture), AbilityName(entry.Ability, "Ability {0}", "fallback.ability.name"), entry.Learned, entry.Progress, entry.RequiredPoints) { Icon = NativeAbilityMenuMetadata.Icon(entry.Ability), Description = catalog?.AbilityDescription(entry.Ability) ?? Text("projection.ability.description-unavailable", "Ability description unavailable."), IsPassive = NativeCharacterVitals.IsGuardianForcePassiveAbility(entry.Ability), PassiveEnabled = entry.Learned && NativeCharacterVitals.IsGuardianForcePassiveAbility(entry.Ability) && vitals.GuardianForcePassiveAbilityEnabled(id, entry.Ability), PassiveAvailable = entry.Learned && native.HitPoints > 0 });
                    if (entry.Ability == native.Learning && !entry.Learned)
                    {
                        guardianForce.AbilityPoints = entry.Progress;
                    }
                }
                result.GuardianForces.Add(guardianForce);
            }
            if (state.Scene is SceneKind.Field or SceneKind.WorldMap)
            {
                result.SupportedActions.UnionWith(["junction-exchange", "attack-mode", "junction-gf", "junction-gf-reassign", "junction-command", "junction-ability", "junction-magic", "junction-magic-reassign", "junction-off", "junction-auto", "junction-magic-off-all", "gf-learn", "gf-passive-toggle"]);
                if (!vitals.MagicSystem.SharedMagicStock)
                {
                    result.SupportedActions.Add("magic-transfer");
                }
            }
        }
        if (junction is null && state.CardInventory is { } cards)
        {
            cards.Validate();
            result.CardCollectionComplete = cards.CollectionFlags is byte collectionFlags ? (collectionFlags & 1) != 0 : null;
            if (catalog is not null && state.Economy is not null && state.TripleTriadPlay is null && GameSession.CanUseCardMod(state.CharacterVitals))
            {
                result.SupportedActions.Add("card-mod");
            }
            for (int id = 0; id < cards.Cards.Length; id++)
            {
                bool common = id < NativeCardInventory.CommonCardCount;
                int count = common ? cards.Cards[id] & 0x7F : cards.Cards[id] == 0xF0 ? 1 : 0;
                int rare = id - NativeCardInventory.CommonCardCount;
                bool obtained = common ? (cards.Cards[id] & 0x80) != 0 : (cards.ObtainedRareCards[rare / 8] & (1 << (rare & 7))) != 0;
                if (count != 0 || obtained)
                {
                    TripleTriadCard? definition = catalog?.CardDefinition(id);
                    TripleTriadRefinement? recipe = catalog?.CardRefinement(id);
                    result.Cards.Add(new MenuCard
                    {
                        Id = $"card-{id}",
                        NativeId = id,
                        Name = catalog?.CardName(id) ?? Format("fallback.card.name", "Card {0}", id),
                        Quantity = count,
                        Level = id / 11 + 1,
                        Ranks = definition is null ? Text("common.unavailable", "Unavailable") : $"{Rank(definition.Top)} / {Rank(definition.Right)} / {Rank(definition.Bottom)} / {Rank(definition.Left)}",
                        Element = definition?.Element.ToString() ?? Text("common.unavailable", "Unavailable"),
                        Description = (count > 0 ? Text("projection.card.owned", "Owned card. ") : Text("projection.card.previous", "Previously obtained; not currently owned. ")) + (catalog?.CardDescription(id) ?? Text("projection.card.description-unavailable", "Card description unavailable.")),
                        RefinementItemId = recipe is null ? null : $"item-{recipe.ItemId}",
                        RefinementQuantity = recipe?.ItemCount,
                        RefinementRequiredCards = recipe?.RequiredCards,
                        RefinementItemName = recipe is null ? null : catalog?.ItemName(recipe.ItemId)
                    });
                }
            }
        }
        if (state.CharacterVitals is null)
        {
            result.SourceDescription = Text("projection.session.characters-unavailable", "Live session: character state unavailable");
        }
        if (includeJunctionPlanner && state.CharacterVitals is not null && state.Scene is SceneKind.Field or SceneKind.WorldMap)
        {
            result.JunctionPlanner = new MenuJunctionPlanner(state, names, catalog, placeId);
            result.SupportedActions.Add("junction-edit");
        }
        return result;
    }

    private sealed class ProjectionCache(NativeCharacterVitals vitals, LiveMenuCatalog? catalog, NativeWorldControlState? controls, Func<int, BattleNativePlayerStatRules>? readRules)
    {
        private readonly Dictionary<int, BattleNativePlayerStatRules> rules = [];
        private readonly Dictionary<int, IReadOnlyList<NativeMagicStock>> inventories = [];
        private readonly Dictionary<int, int?> owners = [];
        private readonly Dictionary<int, int?> casters = [];
        private readonly Dictionary<(int Row, int Spell), MagicCastQuote> quotes = [];

        public BattleNativePlayerStatRules Rules(int row)
        {
            if (!rules.TryGetValue(row, out BattleNativePlayerStatRules? result))
            {
                result = readRules?.Invoke(row) ?? catalog!.ReadStatRules(vitals, row);
                rules.Add(row, result);
            }
            return result;
        }

        public IReadOnlyList<NativeMagicStock> Inventory(int row)
        {
            int bank = vitals.MagicSystem.SharedMagicStock ? 0 : row;
            if (!inventories.TryGetValue(bank, out IReadOnlyList<NativeMagicStock>? result))
            {
                result = vitals.GetMagicInventory(row);
                inventories.Add(bank, result);
            }
            return result;
        }

        public int? Owner(int spell)
        {
            if (!owners.TryGetValue(spell, out int? result))
            {
                result = MagicJunctionPolicy.GetOwner(vitals, spell);
                owners.Add(spell, result);
            }
            return result;
        }

        public int? Caster(int row, int spell, int? owner)
        {
            // Positive-stock field caster selection depends on its owner, or the same eligible combat party for every unbound shared spell.
            int group = vitals.MagicSystem.SharedMagicStock ? owner ?? -1 : row;
            if (!casters.TryGetValue(group, out int? result))
            {
                result = vitals.ResolveFieldMagicCaster(row, spell, Rules, controls);
                casters.Add(group, result);
            }
            return result;
        }

        public MagicCastQuote Quote(int row, int spell)
        {
            if (!quotes.TryGetValue((row, spell), out MagicCastQuote? result))
            {
                result = MagicCastingService.Quote(vitals, row, spell, MagicCastContext.Field);
                quotes.Add((row, spell), result);
            }
            return result;
        }
    }

    private static string Rank(int value) => value == 10 ? "A" : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static int ExperienceToNextLevel(uint experience, NativeStatCurve curve)
    {
        int level = NativeStatDefinitions.Level(experience, curve);
        return level == 100 ? 0 : checked((int)(10L * level * curve.ExperienceStep + (long)level * level * curve.ExperienceFraction / 256 - experience));
    }

    internal static Dictionary<string, int> Statistics(BattleNativePlayerStatRules rules, NativeCharacterVital character, IReadOnlyList<NativeMagicStock> inventory)
    {
        BattleNativePlayerStatistics stats = rules.Project(inventory, new BattleNativeCharacterGrowth(character.HitPointBonus, character.StatBonuses.ToImmutableArray()));
        return Statistics(stats);
    }

    internal static Dictionary<string, int> Statistics(BattleNativePlayerStatistics stats)
    {
        return new(StringComparer.Ordinal)
        {
            ["HP"] = stats.MaximumHp,
            ["Str"] = stats.Strength,
            ["Vit"] = stats.Vitality,
            ["Mag"] = stats.Magic,
            ["Spr"] = stats.Spirit,
            ["Spd"] = stats.Speed,
            ["Eva"] = stats.Evasion,
            ["Hit"] = stats.Hit,
            ["Luck"] = stats.Luck
        };
    }

    internal static MenuJunctionEffects JunctionEffects(BattleNativePlayerStatRules rules, NativeCharacterVital character, IReadOnlyList<NativeMagicStock> inventory, BattleNativePlayerStatistics? projected = null)
    {
        BattleNativePlayerStatistics stats = projected ?? rules.Project(inventory, new BattleNativeCharacterGrowth(character.HitPointBonus, character.StatBonuses.ToImmutableArray()));
        ImmutableArray<byte> resistances = rules.ProjectStatusResistances(inventory);
        string[] names = ["Fire", "Ice", "Thunder", "Earth", "Poison", "Wind", "Water", "Holy"];
        MenuJunctionPercentage[] elements = names.Select((name, index) => new MenuJunctionPercentage(name, (stats.ElementAttack & (1 << index)) != 0 ? stats.ElementAttackPercent : 0, stats.ElementDefenses[index] - 800)).ToArray();
        (int Index, string Name)[] statuses = [(0, "Death"), (1, "Poison"), (2, "Petrify"), (3, "Darkness"), (4, "Silence"), (5, "Berserk"), (6, "Zombie"), (8, "Sleep"), (10, "Slow"), (11, "Stop"), (17, "Curse"), (22, "Confuse"), (23, "Drain")];
        int spellId = rules.MagicJunctions[10];
        ImmutableArray<byte> spell = rules.MagicRecords[spellId];
        int quantity = spellId == 0 ? 0 : inventory.FirstOrDefault(stock => stock.SpellId == spellId)?.Count ?? 0;
        int attackMask = spell[38] | spell[39] << 8;
        int attackPercent = spell[36] * quantity / 100;
        MenuJunctionPercentage[] statusEffects = statuses.Select((status, index) => new MenuJunctionPercentage(status.Name, (attackMask & (1 << index)) != 0 ? attackPercent : 0, resistances[status.Index] - 100)).ToArray();
        return new MenuJunctionEffects(elements, statusEffects);
    }

    internal static string JunctionSlotName(int slot)
    {
        // Native496440/4967C0/4968A0 consume saved+92..100; 496930/496AF0/4969E0/496BD0 consume+101..110.
        return slot switch
        {
            0 => "HP",
            1 => "Str",
            2 => "Vit",
            3 => "Mag",
            4 => "Spr",
            5 => "Spd",
            6 => "Eva",
            7 => "Hit",
            8 => "Luck",
            9 => "Elem-Atk",
            10 => "ST-Atk",
            >= 11 and <= 14 => $"Elem-Def {slot - 10}",
            >= 15 and <= 18 => $"ST-Def {slot - 14}",
            _ => $"Unknown native slot {slot}"
        };
    }

    private static string? Nonempty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool MenuBattleTimerBlocked(GameState state)
    {
        if (state.Scene == SceneKind.Field && state.EventState is { ValueKind: System.Text.Json.JsonValueKind.Object } snapshot && snapshot.TryGetProperty("PartyMenu", out System.Text.Json.JsonElement menu) && menu.ValueKind == System.Text.Json.JsonValueKind.Object && menu.TryGetProperty("TimerDisplayBeforeMenu", out System.Text.Json.JsonElement display) && display.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
        {
            return display.GetBoolean();
        }
        return state.ExamTimer?.DisplayEnabled != false;
    }

    internal static string CharacterName(GameState state, byte identity, IReadOnlyDictionary<int, string>? names, LiveMenuCatalog? catalog)
    {
        int subject = GameNames.CharacterSubject(identity);
        string? named = Nonempty(names?.GetValueOrDefault(subject)) ?? SavedName(state, subject);
        return named ?? Nonempty(catalog?.CharacterName(identity)) ?? GameNames.DefaultName(subject);
    }

    internal static IReadOnlyDictionary<int, string> TextNames(GameState state, IReadOnlyDictionary<int, string>? names)
    {
        Dictionary<int, string> result = [];
        for (int subject = 0; subject <= GameNames.BokoSubject; subject++)
        {
            if (!GameNames.IsValidSubject(subject))
            {
                continue;
            }
            string? name = Nonempty(names?.GetValueOrDefault(subject)) ?? SavedName(state, subject);
            if (name is not null)
            {
                result.Add(subject, name);
            }
        }
        return result;
    }

    private static string? SavedName(GameState state, int identity)
    {
        System.Text.Json.JsonElement? snapshot = state.EventState ?? state.DepartingFieldState;
        if (snapshot is not { ValueKind: System.Text.Json.JsonValueKind.Object } saved || !saved.TryGetProperty("Scene", out System.Text.Json.JsonElement scene) || scene.ValueKind != System.Text.Json.JsonValueKind.Object || !scene.TryGetProperty("Names", out System.Text.Json.JsonElement names) || names.ValueKind != System.Text.Json.JsonValueKind.Object || !names.TryGetProperty(identity.ToString(System.Globalization.CultureInfo.InvariantCulture), out System.Text.Json.JsonElement name))
        {
            return null;
        }
        if (name.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            throw new InvalidDataException("Saved character name must retain its actual text value.");
        }
        return Nonempty(name.GetString());
    }
}

/// <summary>Original text metadata only. No font, texture or rendered original menu asset is loaded.</summary>
public sealed partial class LiveMenuCatalog
{
    internal string StatRulesFingerprint { get; }
    private readonly KernelData kernel;
    private readonly ITextTermSource terms;
    private readonly BattlePlayerStatReader statistics;
    private readonly string[] guardianForceNames;
    private readonly string?[] abilityNames;
    private readonly string?[] abilityDescriptions;
    private ResourceService? resources;
    private readonly string[] cardDescriptions;
    private readonly byte[] fieldItems;
    private NativeSolomonRingDefinition? solomonRing;
    private readonly TripleTriadCatalog tripleTriad;
    private ImmutableArray<NativeAngeloTrainingDefinition> angeloMoves = [];

    private LiveMenuCatalog(byte[] kernelBytes, KernelData kernel, string[] guardianForceNames, string[] cardDescriptions, byte[] fieldItems, byte[] itemIconKinds, TripleTriadCatalog tripleTriad, ITextTermSource terms)
    {
        StatRulesFingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(kernelBytes));
        this.kernel = kernel;
        this.terms = terms;
        statistics = new BattlePlayerStatReader(kernelBytes);
        this.guardianForceNames = guardianForceNames;
        this.cardDescriptions = cardDescriptions;
        this.fieldItems = fieldItems;
        this.itemIconKinds = itemIconKinds;
        this.tripleTriad = tripleTriad;
        abilityNames = [.. kernel.JunctionAbilities.Select(ability => ability.Name), .. kernel.CommandAbilities.Select(ability => ability.Name), .. kernel.CharacterStatAbilities.Select(ability => ability.Name), .. kernel.CharacterAbilities.Select(ability => ability.Name), .. kernel.PartyAbilities.Select(ability => ability.Name), .. kernel.GuardianAbilities.Select(ability => ability.Name), .. kernel.MenuAbilities.Select(ability => ability.Name)];
        abilityDescriptions = [.. kernel.JunctionAbilities.Select(ability => ability.Description), .. kernel.CommandAbilities.Select(ability => ability.Description), .. kernel.CharacterStatAbilities.Select(ability => ability.Description), .. kernel.CharacterAbilities.Select(ability => ability.Description), .. kernel.PartyAbilities.Select(ability => ability.Description), .. kernel.GuardianAbilities.Select(ability => ability.Description), .. kernel.MenuAbilities.Select(ability => ability.Description)];
    }

    public BattleNativePlayerStatRules ReadStatRules(NativeCharacterVitals vitals, int row) => statistics.ReadRules(vitals, row);

    public static LiveMenuCatalog ReadInstalled(GameAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        byte[] Read(string name) => assets.ReadResource(new ResourceKey("main/" + name));
        byte[] initial = Read("init.out");
        NativeInitialData.Validate(initial);
        System.Text.Encoding encoding = NativeTextEncoding.Create(assets.Resources.Source.Language);
        string[] guardianForceNames = Enumerable.Range(0, 16).Select(id =>
        {
            int start = id * 68;
            int end = Array.IndexOf(initial, (byte)0, start, 12);
            return encoding.GetString(initial, start, end < 0 ? 12 : end - start);
        }).ToArray();
        byte[] kernelBytes = Read("kernel.bin");
        KernelData kernel = KernelData.ReadFromArray(kernelBytes, encoding);
        byte[] ReadMenu(string name) => assets.ReadResource(new ResourceKey("menu/" + name));
        MngrpArchive textArchive = MngrpArchive.Read(ReadMenu("mngrp.bin"), ReadMenu("mngrphd.bin"), encoding);
        string[] cardDescriptions = NativeRefinementGroupReader.ReadCardDescriptions(textArchive);
        TripleTriadCatalog tripleTriad = TripleTriadCatalog.Load(assets.GameDirectory, assets.Resources);
        byte[] fieldItems = ReadMenu("mitem.bin");
        if (fieldItems.Length % 4 != 0)
        {
            throw new InvalidDataException("Original field item metadata requires complete four-byte rows.");
        }
        byte[] itemIconKinds = assets.ReadItemIconKinds();
        LiveMenuCatalog catalog = new(kernelBytes, kernel, guardianForceNames, cardDescriptions, fieldItems, itemIconKinds, tripleTriad, assets.ReadNativeTextTerms());
        catalog.resources = assets.Resources;
        catalog.angeloMoves = NativeAngeloTrainingDefinitionReader.Read(ReadMenu("pet_exp.bin"), ReadMenu("pet_exp.msg"), encoding);
        catalog.ReadLocations(ReadMenu("areames.dc1"), encoding, WorldMapLocations.Read(assets.Resources));
        catalog.solomonRing = assets.ReadNativeSolomonRingDefinition();
        catalog.ReadMagazines(ReadMenu("mmag.bin"), textArchive.GetSection<MngrpStringTableSection>(87).Table, assets.ReadNativeWeaponShopDefinitions(), textArchive.GetSection<MngrpOpaqueSection>(7).Content, ReadMenu("magita.TEX"), itemIconKinds);
        return catalog;
    }

    public NativeFieldItem? FieldItem(int id) => id > 0 && id < fieldItems.Length / 4 ? new NativeFieldItem(fieldItems[id * 4], fieldItems[id * 4 + 1], fieldItems[id * 4 + 2], fieldItems[id * 4 + 3]) { SolomonRingDefinition = fieldItems[id * 4] == 15 ? solomonRing : null } : null;

    public NativeAngeloTrainingDefinition AngeloMove(int index) => (uint)index < angeloMoves.Length ? angeloMoves[index] : throw new ArgumentOutOfRangeException(nameof(index));

    public string? CharacterName(int id) => FormatDescription((uint)id < kernel.Characters.Count ? LocalizedKernel(nameof(kernel.Characters), "Name", id, kernel.Characters[id].Name) : null);
    public string? GuardianForceName(int id) => FormatDescription((uint)id < guardianForceNames.Length ? LocalizedTable("defaults_guardians", id, guardianForceNames[id]) : null);
    public string? CardName(int id) => FormatDescription((uint)id < tripleTriad.Cards.Length ? LocalizedTable("names_cards", id, tripleTriad.Name(id)) : null);
    public string? CardDescription(int id) => (uint)id < cardDescriptions.Length ? FormatDescription(LocalizedTable("mngrp_slot192_records", id, cardDescriptions[id])) : null;
    public TripleTriadCard? CardDefinition(int id) => (uint)id < tripleTriad.Cards.Length ? tripleTriad.Cards[id] : null;
    public TripleTriadRefinement? CardRefinement(int id) => tripleTriad.Refinements.FirstOrDefault(recipe => recipe.CardId == id);
    public string? MagicName(int id) => FormatDescription((uint)id < kernel.MagicSpells.Count ? LocalizedKernel(nameof(kernel.MagicSpells), "Name", id, kernel.MagicSpells[id].Name) : null);
    public bool IsFieldMagic(int id) => (uint)id < kernel.MagicSpells.Count && kernel.MagicSpells[id].AttackType is 3 or 5 or 6;
    public string? MagicDescription(int id) => (uint)id < kernel.MagicSpells.Count ? FormatDescription(LocalizedKernel(nameof(kernel.MagicSpells), "Description", id, kernel.MagicSpells[id].Description)) : null;
    public string? WeaponName(int id) => FormatDescription((uint)id < kernel.Weapons.Count ? LocalizedKernel(nameof(kernel.Weapons), "Name", id, kernel.Weapons[id].Name) : null);
    public string? CommandName(int id) => FormatDescription((uint)id < kernel.BattleCommands.Count ? LocalizedKernel(nameof(kernel.BattleCommands), "Name", id, kernel.BattleCommands[id].Name) : null);
    public string? ItemName(int id) => FormatDescription(EncodedItemName(id));
    internal string? EncodedItemName(int id) => (uint)id < kernel.BattleItems.Count ? LocalizedKernel(nameof(kernel.BattleItems), "Name", id, kernel.BattleItems[id].Name) : id >= kernel.BattleItems.Count && id - kernel.BattleItems.Count < kernel.FieldItems.Count ? LocalizedKernel(nameof(kernel.FieldItems), "Name", id - kernel.BattleItems.Count, kernel.FieldItems[id - kernel.BattleItems.Count].Name) : null;
    public string? ItemDescription(int id) => FormatDescription((uint)id < kernel.BattleItems.Count ? LocalizedKernel(nameof(kernel.BattleItems), "Description", id, kernel.BattleItems[id].Description) : id >= kernel.BattleItems.Count && id - kernel.BattleItems.Count < kernel.FieldItems.Count ? LocalizedKernel(nameof(kernel.FieldItems), "Description", id - kernel.BattleItems.Count, kernel.FieldItems[id - kernel.BattleItems.Count].Description) : null);

    internal static string? FormatText(string? encoded, IReadOnlyDictionary<int, string>? names = null, ITextTermSource? terms = null) => encoded is null ? null : DialogueTextFormatter.Format(encoded, names, terms: terms).Replace('\n', ' ');

    private IReadOnlyDictionary<int, string>? textNames;

    internal void SetTextNames(IReadOnlyDictionary<int, string> names) => textNames = names;

    private string? FormatDescription(string? encoded) => FormatText(encoded, textNames, terms);
    public string? AbilityName(int id)
    {
        return (uint)id < abilityNames.Length && AbilityIdentity(id) is { } identity ? FormatDescription(LocalizedKernel(identity.Collection, "Name", identity.Ordinal, abilityNames[id])) : null;
    }

    public string? AbilityDescription(int id)
    {
        return (uint)id < abilityDescriptions.Length && AbilityIdentity(id) is { } identity ? FormatDescription(LocalizedKernel(identity.Collection, "Description", identity.Ordinal, abilityDescriptions[id])) : null;
    }
}
