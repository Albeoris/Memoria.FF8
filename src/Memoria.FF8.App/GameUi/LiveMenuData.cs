using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.MngrpBin;
using Albeoris.Games.FF8.MngrpBin.Abstractions;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

/// <summary>A read-only projection of canonical session owners. It never creates native state or grants menu mutations.</summary>
public static class LiveMenuData
{
    public static MenuData Build(GameState state, IReadOnlyDictionary<int, string>? names = null, LiveMenuCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        MenuData result = new()
        {
            Location = state.Scene == SceneKind.WorldMap ? "World map" : state.FieldName,
            SourceDescription = "Live session: native inventory, party and junction records",
            UnavailableReason = "The current engine does not support this native menu action."
        };
        if (state.Economy is { } economy)
        {
            economy.Validate();
            result.Currency = checked((int)economy.Gil);
            foreach (NativeItemStock item in economy.Items.Where(item => item.ItemId != 0 && item.Count != 0))
            {
                result.Items.Add(new MenuItem { Id = $"item-{item.ItemId}", Name = catalog?.ItemName(item.ItemId) ?? $"Item {item.ItemId}", Quantity = item.Count, Description = catalog?.ItemDescription(item.ItemId) ?? "Item effect unavailable; using, discarding and refining are unsupported." });
            }
        }
        if (state.SeeD is { SalaryUiEnabled: true } seeD)
        {
            seeD.Validate();
            result.SeeDRank = seeD.RankPoints / 100;
        }
        if (state.CharacterVitals is { } vitals)
        {
            vitals.Validate();
            state.WorldControls?.Validate();
            IEnumerable<int> rows = vitals.CombatParty.Where(row => row != 255).Select(row => (int)row).Concat(Enumerable.Range(0, vitals.Records.Length).Where(row => !vitals.CombatParty.Contains((byte)row)));
            foreach (int row in rows)
            {
                NativeCharacterVital native = vitals.Records[row];
                bool inParty = vitals.CombatParty.Contains((byte)row);
                bool member = native.CharacterId < 8 ? (native.MemberFlags & 1) != 0 : state.WorldControls is { } controls && (controls.EventFlags104 & (1u << (native.CharacterId - 8))) != 0;
                if (!inParty && !member)
                {
                    continue;
                }
                MenuCharacter character = new()
                {
                    Id = $"character-{row}",
                    Name = CharacterName(state, native.CharacterId, names, catalog),
                    InParty = inParty,
                    Level = NativeStatDefinitions.Level(native.Experience, vitals.Definitions.Characters[native.CharacterId]),
                    CurrentHp = native.HitPoints.Minimum == native.HitPoints.Maximum ? native.HitPoints.Minimum : null,
                    HpDisplay = native.HitPoints.Minimum == native.HitPoints.Maximum ? native.HitPoints.Minimum.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"{native.HitPoints.Minimum}..{native.HitPoints.Maximum}",
                    MaximumHp = vitals.Definitions.CharacterMaximum(native),
                    Experience = native.Experience <= int.MaxValue ? (int)native.Experience : null,
                    Weapon = catalog?.WeaponName(native.CharacterId >= 8 ? vitals.DreamWeapons[native.CharacterId - 8] : native.Weapon) ?? $"Weapon {(native.CharacterId >= 8 ? vitals.DreamWeapons[native.CharacterId - 8] : native.Weapon)}"
                };
                if (native.Status != 0)
                {
                    character.Conditions.Add($"Native status 0x{native.Status:X4}");
                }
                foreach (int id in native.Junction.GuardianForceAssignments)
                {
                    character.GuardianForceIds.Add($"gf-{id}");
                }
                for (int slot = 0; slot < 4; slot++)
                {
                    byte command = unchecked((byte)(native.Junction.Commands >> (8 * slot)));
                    byte ability = unchecked((byte)(native.Junction.Abilities >> (8 * slot)));
                    if (command != 0)
                    {
                        character.Commands.Add(catalog?.CommandName(command) ?? $"Command {command}");
                    }
                    if (ability != 0)
                    {
                        character.Abilities.Add(catalog?.AbilityName(ability) ?? $"Ability {ability}");
                    }
                }
                // Non-HP combat formulas are outside the proved native contract; empty stats render as unavailable.
                for (int slot = 0; slot < native.Junction.MagicJunctions.Length; slot++)
                {
                    byte spell = native.Junction.MagicJunctions[slot];
                    if (spell != 0)
                    {
                        character.Junctions[JunctionSlotName(slot)] = catalog?.MagicName(spell) ?? $"Magic {spell}";
                    }
                }
                foreach (NativeMagicStock stock in native.MagicInventory.Where(stock => stock.SpellId != 0 && stock.Count != 0))
                {
                    result.Magic.Add(new MenuMagic { Id = $"magic-{row}-{stock.SpellId}", CharacterId = character.Id, Name = catalog?.MagicName(stock.SpellId) ?? $"Magic {stock.SpellId}", Quantity = stock.Count, Description = catalog?.MagicDescription(stock.SpellId) ?? "Magic effect unavailable; casting, exchanging and junctioning are unsupported." });
                }
                result.Characters.Add(character);
            }
            for (int id = 0; id < vitals.GuardianForces.Length; id++)
            {
                NativeGuardianForceVital native = vitals.GuardianForces[id];
                if ((native.Flags & 1) == 0)
                {
                    continue;
                }
                MenuGuardianForce guardianForce = new()
                {
                    Id = $"gf-{id}",
                    Name = catalog?.GuardianForceName(id) ?? $"GF {id}",
                    Level = NativeStatDefinitions.Level(native.Experience, vitals.Definitions.GuardianForces[id]),
                    CurrentHp = native.HitPoints,
                    MaximumHp = vitals.Definitions.GuardianForceMaximum(id, native),
                    Experience = native.Experience <= int.MaxValue ? (int)native.Experience : null
                };
                for (int bit = 0; bit < 64; bit++)
                {
                    if ((native.LearnedAbilities & (1UL << bit)) != 0)
                    {
                        guardianForce.Abilities.Add(catalog?.AbilityName(64 + bit) ?? $"Learned ability {64 + bit}");
                    }
                }
                result.GuardianForces.Add(guardianForce);
            }
        }
        if (state.CardInventory is { } cards)
        {
            cards.Validate();
            for (int id = 0; id < cards.Cards.Length; id++)
            {
                bool common = id < NativeCardInventory.CommonCardCount;
                int count = common ? cards.Cards[id] & 0x7F : cards.Cards[id] == 0xF0 ? 1 : 0;
                int rare = id - NativeCardInventory.CommonCardCount;
                bool obtained = common ? (cards.Cards[id] & 0x80) != 0 : (cards.ObtainedRareCards[rare / 8] & (1 << (rare & 7))) != 0;
                if (count != 0 || obtained)
                {
                    result.Cards.Add(new MenuCard { Id = $"card-{id}", Name = catalog?.CardName(id) ?? $"Card {id}", Quantity = count, Description = (count > 0 ? "Owned card. " : "Previously obtained; not currently owned. ") + (catalog?.CardDescription(id) ?? "Card rules, ranks and modification are unavailable.") });
                }
            }
        }
        if (state.CharacterVitals is null)
        {
            result.SourceDescription = "Live session: character state unavailable";
        }
        return result;
    }

    private static string JunctionSlotName(int slot)
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

    private static string CharacterName(GameState state, byte identity, IReadOnlyDictionary<int, string>? names, LiveMenuCatalog? catalog)
    {
        // EventEngine.CharacterNames owns naming subjects0/1; their combat identities are0/4.
        int? subject = identity switch
        {
            0 => 0,
            4 => 1,
            _ => null
        };
        string? named = subject.HasValue ? Nonempty(names?.GetValueOrDefault(subject.Value)) ?? SavedName(state, subject.Value) : null;
        return named ?? Nonempty(catalog?.CharacterName(identity)) ?? (identity switch
        {
            0 => "Squall",
            4 => "Rinoa",
            _ => $"Character {identity}"
        });
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
public sealed class LiveMenuCatalog
{
    private readonly KernelData kernel;
    private readonly string[] guardianForceNames;
    private readonly string?[] abilityNames;
    private readonly string[] cardNames;
    private readonly string[] cardDescriptions;

    private LiveMenuCatalog(KernelData kernel, string[] guardianForceNames, string[] cardNames, string[] cardDescriptions)
    {
        this.kernel = kernel;
        this.guardianForceNames = guardianForceNames;
        this.cardNames = cardNames;
        this.cardDescriptions = cardDescriptions;
        abilityNames = [.. kernel.JunctionAbilities.Select(ability => ability.Name), .. kernel.CommandAbilities.Select(ability => ability.Name), .. kernel.CharacterStatAbilities.Select(ability => ability.Name), .. kernel.CharacterAbilities.Select(ability => ability.Name), .. kernel.PartyAbilities.Select(ability => ability.Name), .. kernel.GuardianAbilities.Select(ability => ability.Name), .. kernel.MenuAbilities.Select(ability => ability.Name)];
    }

    public static LiveMenuCatalog ReadInstalled(GameAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        using IFlArchive main = FlArchive.OpenForRead(Path.Combine(assets.GameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files);
        byte[] Read(string name)
        {
            IFlArchiveEntry entry = main.Entries.Single(entry => string.Equals(Path.GetFileName(entry.RelativePath), name, StringComparison.OrdinalIgnoreCase));
            using Stream stream = entry.OpenForRead();
            using MemoryStream buffer = new();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
        byte[] initial = Read("init.out");
        if (initial.Length != 2812)
        {
            throw new InvalidDataException("Menu default GF names require the complete original New Game template.");
        }
        System.Text.Encoding encoding = FF8Encoding.CreateEuropean();
        string[] guardianForceNames = Enumerable.Range(0, 16).Select(id =>
        {
            int start = id * 68;
            int end = Array.IndexOf(initial, (byte)0, start, 12);
            return encoding.GetString(initial, start, end < 0 ? 12 : end - start);
        }).ToArray();
        KernelData kernel = KernelData.ReadFromArray(Read("kernel.bin"), encoding);
        using IFlArchive menu = FlArchive.OpenForRead(Path.Combine(assets.GameDirectory, "Data", "lang-en", "menu.fs"), FlArchiveRepresentation.Files);
        byte[] ReadMenu(string name)
        {
            IFlArchiveEntry entry = menu.Entries.Single(entry => string.Equals(Path.GetFileName(entry.RelativePath), name, StringComparison.OrdinalIgnoreCase));
            using Stream stream = entry.OpenForRead();
            using MemoryStream buffer = new();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
        MngrpArchive textArchive = MngrpArchive.Read(ReadMenu("mngrp.bin"), ReadMenu("mngrphd.bin"), encoding);
        MngrpTextRecordSection cardText = textArchive.GetSection<MngrpTextRecordSection>(192);
        if (cardText.Records.Count != NativeCardInventory.CardCount || cardText.Records.Where((record, id) => record.Payload[3] != id).Any())
        {
            throw new InvalidDataException("Original card text catalogue does not preserve its 110 card identities.");
        }
        string[] cardDescriptions = cardText.Records.Select(record => record.Text.Value.Replace("{Line}", " ", StringComparison.Ordinal)).ToArray();
        string[] cardNames = cardDescriptions.Select(description =>
        {
            int end = description.IndexOf(':');
            if (end < 0)
            {
                throw new InvalidDataException("Original card description has no card name heading.");
            }
            string name = description[..end];
            return name.EndsWith(" Card", StringComparison.Ordinal) ? name[..^5] : name;
        }).ToArray();
        return new LiveMenuCatalog(kernel, guardianForceNames, cardNames, cardDescriptions);
    }

    public string? CharacterName(int id) => (uint)id < kernel.Characters.Count ? kernel.Characters[id].Name : null;
    public string? GuardianForceName(int id) => (uint)id < guardianForceNames.Length ? guardianForceNames[id] : null;
    public string? CardName(int id) => (uint)id < cardNames.Length ? cardNames[id] : null;
    public string? CardDescription(int id) => (uint)id < cardDescriptions.Length ? cardDescriptions[id] : null;
    public string? MagicName(int id) => (uint)id < kernel.MagicSpells.Count ? kernel.MagicSpells[id].Name : null;
    public string? MagicDescription(int id) => (uint)id < kernel.MagicSpells.Count ? kernel.MagicSpells[id].Description : null;
    public string? WeaponName(int id) => (uint)id < kernel.Weapons.Count ? kernel.Weapons[id].Name : null;
    public string? CommandName(int id) => (uint)id < kernel.BattleCommands.Count ? kernel.BattleCommands[id].Name : null;
    public string? ItemName(int id) => (uint)id < kernel.BattleItems.Count ? kernel.BattleItems[id].Name : id >= kernel.BattleItems.Count && id - kernel.BattleItems.Count < kernel.FieldItems.Count ? kernel.FieldItems[id - kernel.BattleItems.Count].Name : null;
    public string? ItemDescription(int id) => (uint)id < kernel.BattleItems.Count ? kernel.BattleItems[id].Description : id >= kernel.BattleItems.Count && id - kernel.BattleItems.Count < kernel.FieldItems.Count ? kernel.FieldItems[id - kernel.BattleItems.Count].Description : null;
    public string? AbilityName(int id)
    {
        return (uint)id < abilityNames.Length ? abilityNames[id] : null;
    }
}
