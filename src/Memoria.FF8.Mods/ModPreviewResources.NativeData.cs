using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tdw;
using Albeoris.Games.FF8.Imaging.Tex;
using Albeoris.Games.FF8.Jsm;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.MngrpBin;
using Albeoris.Games.FF8.MngrpBin.Abstractions;
using Albeoris.Games.FF8.NamedicBin;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods;

public static partial class ModPreviewResources
{
    private static ModPreviewEntry? DescribeNativeDataPath(ModFile file)
    {
        if (file.Format != ModFormat.Junction || JunctionEncodedResources.Address(file.RelativePath) is not { } key)
        {
            return null;
        }
        if (JunctionEncodedResources.FieldGroup(key) is string field)
        {
            string? name = Path.GetExtension(key.Path) switch
            {
                ".jsm" => "Event script",
                ".rat" or ".mrt" => "Random encounter table",
                _ => null
            };
            return name is null ? null : new(file.RelativePath, "Fields", field, name);
        }
        if (JunctionEncodedResources.IsStaticFileResource(key))
        {
            return new(file.RelativePath, "Textures", "Font metrics", Path.GetFileName(key.Path));
        }
        return NativeDataResourceFiles.Classify(key) switch
        {
            NativeDataResourceKind.Kernel => new(file.RelativePath, "Gameplay", "Battle and abilities", "Kernel mechanics and text"),
            NativeDataResourceKind.InitialData => new(file.RelativePath, "Gameplay", "New Game", "Starting characters and inventory"),
            NativeDataResourceKind.BattleScenes => new(file.RelativePath, "Gameplay", "Encounters", "Battle formations"),
            NativeDataResourceKind.Enemy => new(file.RelativePath, "Models", "Battle enemies", Path.GetFileName(key.Path)),
            NativeDataResourceKind.MenuTable when NativeMenuPreviewName(key) is { } name => new(file.RelativePath, "Gameplay", "Menu tables", name),
            NativeDataResourceKind.NameDictionary => new(file.RelativePath, "Text", "Native text", "Names and terms"),
            NativeDataResourceKind.AreaNames => new(file.RelativePath, "Text", "Native text", "Area names"),
            NativeDataResourceKind.MenuGroupData or NativeDataResourceKind.MenuGroupIndex => new(file.RelativePath, "Text", "Native text", "Menu strings and help"),
            NativeDataResourceKind.MenuSpriteIndex => new(file.RelativePath, "Textures", "Native artwork", key.Path == "menu/icon.sp1" ? "Menu icon sprites" : "Menu portrait sprites"),
            NativeDataResourceKind.CompressedImage or NativeDataResourceKind.Texture => new(file.RelativePath, "Textures", "Native artwork", Path.GetFileNameWithoutExtension(key.Path)),
            NativeDataResourceKind.WorldSettings => new(file.RelativePath, "World map", "World resources", $"World settings · {Path.GetFileName(key.Path)}"),
            NativeDataResourceKind.WorldData when key.Path == "world/rail.obj" => new(file.RelativePath, "World map", "World resources", "Rail routes"),
            NativeDataResourceKind.WorldData when key.Path.StartsWith("world/music", StringComparison.Ordinal) => new(file.RelativePath, "World map", "World resources", $"Music sequence {key.Path[11]}"),
            _ => null
        };
    }

    private static string? NativeMenuPreviewName(ResourceKey key) => key.Path switch
    {
        "menu/shop.bin" => "Shop catalogs",
        "menu/price.bin" => "Shop prices",
        "menu/mwepon.bin" or "menu/mwepon.msg" => "Weapon recipes and messages",
        "menu/mitem.bin" => "Item use rules",
        "menu/mthomas.bin" => "Solomon Ring ingredients",
        "menu/pet_exp.bin" or "menu/pet_exp.msg" => "Angelo training",
        "menu/cyocobo.bin" => "Chocobo World item transfer",
        "menu/mtmag.bin" or "menu/mmag.bin" => "Tutorial pages",
        _ when NativeDataResourceFiles.IsStandaloneRefinementTable(key) => $"Refinement table {key.Path[8]}",
        _ => null
    };

    private static ModPreviewComparison? CompareNativeData(ModCatalogSnapshot catalog, ModFile file, ModPreviewResourcesProfile profile, CancellationToken cancellationToken)
    {
        if (DescribeNativeDataPath(file) is null || JunctionEncodedResources.Address(file.RelativePath) is not { } key)
        {
            return null;
        }
        string? field = JunctionEncodedResources.FieldGroup(key);
        string? group = NativeDataResourceFiles.Group(key);
        ModCatalogSnapshot subset = new(catalog.Generation, catalog.Packages, catalog.Files.Where(candidate => candidate == file
            || candidate.Format == ModFormat.Junction && JunctionEncodedResources.Address(candidate.RelativePath) is { } member
            && (group is not null && NativeDataResourceFiles.Group(member) == group || field is not null && JunctionEncodedResources.FieldGroup(member) == field)));
        if (!JunctionEncodedResources.TryPrepare(subset, out IReadOnlyList<EncodedResource> encoded, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw PreparationError(diagnostics);
        }
        using IResourceSource originalSource = OpenSource(profile);
        using IResourceSource modifiedSource = OpenSource(profile);
        ResourceService original = new(originalSource, profile.Language);
        ResourceService modified = new(modifiedSource, profile.Language);
        modified.Prepare(null, encodedResources: encoded).Commit();
        if (JunctionEncodedResources.IsStaticFileResource(key))
        {
            return CompareNativeFontTables(original.Read(key), modified.Read(key));
        }
        NativeDataResourceKind? kind = NativeDataResourceFiles.Classify(key);
        if (kind == NativeDataResourceKind.Enemy)
        {
            return CompareNativeEnemy(original, modified, key, profile);
        }
        if (kind == NativeDataResourceKind.BattleScenes)
        {
            return CompareNativeFormations(original, modified, key, cancellationToken);
        }
        if (kind == NativeDataResourceKind.Kernel)
        {
            return CompareNativeKernel(original, modified);
        }
        if (kind is NativeDataResourceKind.Texture or NativeDataResourceKind.CompressedImage)
        {
            var before = ReadNativeImage(key, original.Read(key), kind.Value);
            var after = ReadNativeImage(key, modified.Read(key), kind.Value);
            return new(new($"{before.Width} × {before.Height}", before), new($"{after.Width} × {after.Height}", after));
        }
        if (kind == NativeDataResourceKind.MenuSpriteIndex)
        {
            return CompareNativeMenuSprites(original, modified, key, profile.GameDirectory, cancellationToken);
        }
        if (field is not null)
        {
            return CompareNativeRows(ReadFieldRows(original), ReadFieldRows(modified));
        }
        return CompareNativeRows(ReadRows(original), ReadRows(modified));

        IEnumerable<(string Key, string Value)> ReadRows(ResourceService resources)
        {
            return kind switch
            {
                NativeDataResourceKind.InitialData => ReadStartingGame(resources, profile.GameDirectory),
                NativeDataResourceKind.MenuTable => ReadNativeMenuRows(resources, key, profile.GameDirectory),
                NativeDataResourceKind.NameDictionary or NativeDataResourceKind.AreaNames => NamedicBin.ReadStrings(resources.Read(key), NativeTextEncoding.Create(resources.Source.Language)).Select((value, index) => ($"Entry {index}", value)),
                NativeDataResourceKind.MenuGroupData or NativeDataResourceKind.MenuGroupIndex => ReadNativeMenuText(resources),
                NativeDataResourceKind.WorldSettings => ReadWorldSettings(resources, key, profile.GameDirectory, cancellationToken),
                NativeDataResourceKind.WorldData when key.Path == "world/rail.obj" => ReadWorldRail(resources.Read(key), cancellationToken),
                NativeDataResourceKind.WorldData => ReadWorldMusic(resources, key),
                _ => throw new InvalidOperationException($"No comparison is registered for {key.Path}.")
            };
        }

        IEnumerable<(string Key, string Value)> ReadFieldRows(ResourceService resources)
        {
            if (Path.GetExtension(key.Path) == ".jsm")
            {
                return JsmDocument.ReadOperations(resources.Read(key)).Select(operation => ($"Operation {operation.Index}", operation.HasParameter ? $"{operation.Opcode} {operation.Parameter}" : operation.Opcode.ToString()));
            }
            using GameAssets assets = new(profile.GameDirectory, resources: resources);
            int id = assets.FindCatalogFieldId(field!);
            NativeFieldEncounterDefinition? encounters = id >= 0 ? assets.LoadField(id).RandomEncounters : null;
            return encounters is null ? [] : [("Encounter rate", encounters.Rate.ToString(CultureInfo.CurrentCulture)), .. encounters.Formations.Select((formation, slot) => ($"Encounter slot {slot}", $"Formation {formation}"))];
        }
    }

    private static ModPreviewComparison CompareNativeFontTables(byte[] original, byte[] modified)
    {
        TdwFile before = TdwFile.Read(original);
        TdwFile after = TdwFile.Read(modified);
        ModPreviewComparison values = CompareNativeRows(DescribeWidths(before), DescribeWidths(after));
        return new(values.Before with { Image = before.Images.FirstOrDefault()?.Decode(0) }, values.After with { Image = after.Images.FirstOrDefault()?.Decode(0) });

        static IEnumerable<(string Key, string Value)> DescribeWidths(TdwFile font)
        {
            return font.Widths.Select((width, index) => ($"Glyph {index}", $"{width}"));
        }
    }

    private static IEnumerable<(string Key, string Value)> ReadWorldSettings(ResourceService resources, ResourceKey key, string gameDirectory, CancellationToken cancellationToken)
    {
        WorldMapLocations locations = new(resources.Read(key));
        for (int row = 0; row < WorldAssets.Rows; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int z = (row - WorldAssets.Rows / 2) * WorldAssets.SegmentUnits;
            for (int column = 0; column < WorldAssets.Columns; column++)
            {
                int x = (column - WorldAssets.Columns / 2) * WorldAssets.SegmentUnits;
                yield return ($"Location cell {column}, {row}", $"Location ID {locations.GetPlaceId(x, z)}");
            }
        }
        if (key != NativeResourceNames.WorldSettings(resources.ResourceLanguage))
        {
            yield break;
        }
        using WorldAssets world = new(gameDirectory, resources);
        foreach (var (spawn, index) in world.Spawns.Select((spawn, index) => (spawn, index)))
        {
            yield return ($"Spawn {index}", $"X {spawn.X}, Z {spawn.Z} · Height {spawn.NativeHeight}");
        }
        foreach (var (sky, index) in world.Skies.Select((sky, index) => (sky, index)))
        {
            yield return ($"Sky {index}", $"Position {sky.Position} · Radius {sky.Radius} · Stars {(sky.NativeStarsEnabled ? "visible" : "hidden")}\n{sky.Top} · {sky.Center} · {sky.Bottom}");
        }
        foreach (WorldFieldEntry entry in world.FieldEntries.Entries)
        {
            yield return ($"Field entrance {entry.Index}", $"Field {entry.FieldId} · Triangle {entry.Triangle} · Position {entry.X}, {entry.Y} · Direction {entry.Direction * 360f / 256:g}°");
        }
    }

    private static IEnumerable<(string Key, string Value)> ReadWorldRail(byte[] bytes, CancellationToken cancellationToken)
    {
        NativeWorldDataReader.ValidateRail(bytes);
        for (int track = 0; track < 14; track++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int start = track * 2048;
            string label = $"Rail route {track}";
            int points = bytes[start];
            int stops = bytes[start + 1];
            yield return (label, $"{points} points · {stops} stops");
            for (int index = 0; index < stops; index++)
            {
                yield return ($"{label} stop {index}", $"Forward point {bytes[start + 8 + index]} · Reverse point {bytes[start + 4 + index]}");
            }
            for (int index = 0; index < points; index++)
            {
                int offset = start + 12 + index * 16;
                int x = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
                int y = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 4));
                int z = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 8));
                yield return ($"{label} point {index}", $"X {x}, Y {y}, Z {z}");
            }
        }
    }

    private static IEnumerable<(string Key, string Value)> ReadWorldMusic(ResourceService resources, ResourceKey key)
    {
        NativeWorldMusicResource music = NativeWorldDataReader.ReadMusicFile(resources.Read(key), key.Path[11] - '0');
        yield return ("Track", music.MusicId.ToString(CultureInfo.CurrentCulture));
        yield return ("AKAO sequence", $"{music.Length:N0} bytes · {music.Sha256[..12]}");
    }

    private static PixelImage ReadNativeImage(ResourceKey key, byte[] bytes, NativeDataResourceKind kind)
    {
        if (kind == NativeDataResourceKind.Texture)
        {
            return TexFile.Read(bytes).Decode(0);
        }
        return key.Path == "main/ff8.lzs" ? NativeStartupArtwork.Decode(bytes) : NativeLzsArtwork.Decode(bytes);
    }

    private static ModPreviewComparison CompareNativeMenuSprites(ResourceService original, ResourceService modified, ResourceKey key, string gameDirectory, CancellationToken cancellationToken)
    {
        NativeMenuArtwork before = NativeMenuArtwork.Load(gameDirectory, original);
        NativeMenuArtwork after = NativeMenuArtwork.Load(gameDirectory, modified);
        bool icons = key.Path == "menu/icon.sp1";
        int count = icons ? Math.Max(before.IconCount, after.IconCount) : NativeMenuArtwork.CharacterCount + NativeMenuArtwork.GuardianForceCount + 3;
        List<(int Id, PixelImage? Before, PixelImage? After)> changes = [];
        for (int id = 0; id < count; id++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PixelImage? a = Read(before, id);
            PixelImage? b = Read(after, id);
            if (a is not null && b is not null && a.Width == b.Width && a.Height == b.Height && a.Pixels.AsSpan().SequenceEqual(b.Pixels))
            {
                continue;
            }
            changes.Add((id, a, b));
        }
        if (changes.Count == 0)
        {
            return new(new("The compared sprites are unchanged."), new("The compared sprites are unchanged."));
        }
        const int maxSprites = 24;
        var shown = changes.Take(maxSprites).ToArray();
        string details = $"{changes.Count} changed sprites\n{string.Join(", ", shown.Select(change => Label(change.Id)))}";
        return new(new(details, Grid(shown.Select(change => change.Before))), new(details, Grid(shown.Select(change => change.After))));

        PixelImage? Read(NativeMenuArtwork artwork, int id)
        {
            if (icons)
            {
                return id >= artwork.IconCount ? null : id is >= 128 and < 140 ? artwork.GetControllerButtonIcon(id) : artwork.GetIcon(id);
            }
            if (id < NativeMenuArtwork.CharacterCount)
            {
                return artwork.GetCharacterPortrait(id);
            }
            int guardian = id - NativeMenuArtwork.CharacterCount;
            if (guardian < NativeMenuArtwork.GuardianForceCount)
            {
                return artwork.GetGuardianForcePortrait(guardian);
            }
            return (guardian - NativeMenuArtwork.GuardianForceCount) switch
            {
                0 => artwork.AngeloPortrait,
                1 => artwork.ChocoboPortrait,
                _ => artwork.GrieverPortrait
            };
        }

        int Label(int id) => icons || id < NativeMenuArtwork.CharacterCount ? id
            : id < NativeMenuArtwork.CharacterCount + NativeMenuArtwork.GuardianForceCount ? 32 + id - NativeMenuArtwork.CharacterCount
            : new[] { 15, 14, 12 }[id - NativeMenuArtwork.CharacterCount - NativeMenuArtwork.GuardianForceCount];

        static PixelImage Grid(IEnumerable<PixelImage?> source)
        {
            const int columns = 6;
            const int cell = 64;
            PixelImage?[] sprites = source.ToArray();
            PixelImage result = new(Math.Min(sprites.Length, columns) * cell, (sprites.Length + columns - 1) / columns * cell);
            for (int index = 0; index < sprites.Length; index++)
            {
                if (sprites[index] is not { } sprite)
                {
                    continue;
                }
                float scale = Math.Min(1, (cell - 4f) / Math.Max(sprite.Width, sprite.Height));
                int width = Math.Max(1, (int)Math.Round(sprite.Width * scale));
                int height = Math.Max(1, (int)Math.Round(sprite.Height * scale));
                int left = index % columns * cell + (cell - width) / 2;
                int top = index / columns * cell + (cell - height) / 2;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        result[left + x, top + y] = sprite[x * sprite.Width / width, y * sprite.Height / height];
                    }
                }
            }
            return result;
        }
    }

    private static IEnumerable<(string Key, string Value)> ReadNativeMenuText(ResourceService resources)
    {
        MngrpArchive archive = MngrpArchive.Read(resources.Read(new("menu/mngrp.bin")), resources.Read(new("menu/mngrphd.bin")), NativeTextEncoding.Create(resources.Source.Language));
        foreach (IMngrpSection section in archive.Sections)
        {
            string label = $"Menu section {section.SlotIndex}";
            switch (section)
            {
                case MngrpStringTableSection table:
                    foreach (var row in ReadTable(label, table.Table))
                    {
                        yield return row;
                    }
                    break;
                case MngrpStringTableGroupSection group:
                    for (int slot = 0; slot < group.Tables.Count; slot++)
                    {
                        if (group.Tables[slot] is not { } nestedTable)
                        {
                            continue;
                        }
                        foreach (var row in ReadTable($"{label}, table {slot}", nestedTable))
                        {
                            yield return row;
                        }
                    }
                    break;
                case MngrpTextRecordSection records:
                    for (int index = 0; index < records.Records.Count; index++)
                    {
                        yield return ($"{label}, entry {index}", records.Records[index].Text.Value);
                    }
                    break;
                case MngrpTextBlockSection blocks:
                    for (int index = 0; index < blocks.Blocks.Count; index++)
                    {
                        yield return ($"{label}, block {index}", string.Join("\n", blocks.Blocks[index].Texts.Select(text => text.Value)));
                    }
                    break;
            }
        }

        static IEnumerable<(string Key, string Value)> ReadTable(string name, MngrpStringTable table)
        {
            for (int index = 0; index < table.Entries.Count; index++)
            {
                if (table[index] is { } text)
                {
                    yield return ($"{name}, entry {index}", text);
                }
            }
        }
    }

    private static ModPreviewComparison CompareNativeKernel(ResourceService original, ResourceService modified)
    {
        JsonElement before = JsonSerializer.SerializeToElement(ReadKernel(original));
        JsonElement after = JsonSerializer.SerializeToElement(ReadKernel(modified));
        return CompareNativeRows(ReadKernelRows(before), ReadKernelRows(after));

        static IEnumerable<(string Key, string Value)> ReadKernelRows(JsonElement root)
        {
            foreach (JsonProperty table in root.EnumerateObject())
            {
                if (table.Value.ValueKind != JsonValueKind.Array)
                {
                    yield return (table.Name, table.Value.ToString());
                    continue;
                }
                int index = 0;
                foreach (JsonElement record in table.Value.EnumerateArray())
                {
                    string selector = $"{table.Name} {index++}";
                    if (record.ValueKind != JsonValueKind.Object)
                    {
                        yield return (selector, record.ToString());
                        continue;
                    }
                    foreach (JsonProperty property in record.EnumerateObject())
                    {
                        yield return ($"{selector} · {property.Name}", property.Value.ToString());
                    }
                }
            }
        }
    }

    private static ModPreviewComparison CompareNativeFormations(ResourceService original, ResourceService modified, ResourceKey key, CancellationToken cancellationToken)
    {
        byte[] before = original.Read(key);
        byte[] after = modified.Read(key);
        if (before.Length != 1024 * 128 || after.Length != before.Length)
        {
            throw new InvalidDataException("Battle formation preview requires 1024 complete native formation records.");
        }
        return CompareNativeRows(ReadFormations(before), ReadFormations(after));

        IEnumerable<(string Key, string Value)> ReadFormations(byte[] bytes)
        {
            for (int id = 0; id < 1024; id++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int offset = id * 128;
                if (before.AsSpan(offset, 128).SequenceEqual(after.AsSpan(offset, 128)))
                {
                    continue;
                }
                BattleEncounter formation = BattleFormationReader.Read(id, bytes.AsSpan(offset, 128));
                yield return ($"Formation {id}", $"Stage {formation.StageId} · Cameras {formation.PrimaryCamera}/{formation.AlternativeCamera} · Flags {formation.Flags}");
                foreach (BattleEnemyPlacement slot in formation.Slots.Where(slot => slot.IsEnemyModel && slot.NativeModelId != 255))
                {
                    string slotState = !slot.Enabled ? "inactive" : slot.InitiallySuspended ? "suspended" : "active";
                    string visibility = slot.Hidden ? ", hidden" : "";
                    string targetable = slot.Untargetable ? ", untargetable" : "";
                    yield return ($"Formation {id}, slot {slot.Slot}", $"Model {slot.ModelId} · Lv {slot.Level} · {slotState}{visibility}{targetable} · {slot.Position}");
                }
            }
        }
    }

    private static ModPreviewComparison CompareNativeEnemy(ResourceService original, ResourceService modified, ResourceKey key, ModPreviewResourcesProfile profile)
    {
        using BattleAssets before = new(profile.GameDirectory, original);
        using BattleAssets after = new(profile.GameDirectory, modified);
        int id = int.Parse(Path.GetFileNameWithoutExtension(key.Path).AsSpan(3), CultureInfo.InvariantCulture);
        ModPreviewComparison comparison = CompareNativeRows(Rows(before), Rows(after));
        if (BinaryPrimitives.ReadInt32LittleEndian(original.Read(key)) != 11 || BinaryPrimitives.ReadInt32LittleEndian(modified.Read(key)) != 11)
        {
            return comparison;
        }
        return new(comparison.Before with { Model = ModelExchange.FromBattle(before.LoadEnemy(id)) }, comparison.After with { Model = ModelExchange.FromBattle(after.LoadEnemy(id)) });

        IEnumerable<(string Key, string Value)> Rows(BattleAssets assets)
        {
            yield return ("Name", assets.ReadEnemyName(id));
            yield return ("AP", assets.ReadEnemyAbilityPoints(id).ToString(CultureInfo.CurrentCulture));
            NativeBattleEnemyTemplate template = assets.ReadEnemyTemplate(id, 1);
            foreach (int level in new[] { 1, 20, 30, 50, 100 }.Where(level => level < template.Levels.Length))
            {
                NativeBattleEnemyLevel stats = template.Levels[level];
                yield return ($"Lv {level} stats", $"HP {stats.MaximumHp:N0}, STR {stats.Strength}, VIT {stats.Vitality}, MAG {stats.Magic}, SPR {stats.Spirit}, SPD {stats.Speed}");
                yield return ($"Lv {level} Draw", string.Join(", ", stats.DrawSpells.Select(spell => spell.ToString()).Concat(stats.DrawGuardianForces.Select(gf => $"GF {gf}"))));
            }
            foreach (var (script, index) in assets.ReadEnemyAi(id, 1).Scripts.Select((script, index) => (script, index)))
            {
                yield return ($"AI script {index}", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(script.AsSpan()))[..12]);
            }
        }
    }

    private static IEnumerable<(string Key, string Value)> ReadStartingGame(ResourceService resources, string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory, resources: resources);
        NativeCharacterVitals characters = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        KernelData kernel = ReadKernel(resources);
        string Character(int id) => kernel.Characters.ElementAtOrDefault(id)?.Name ?? $"Character {id}";
        yield return ("Gil", economy.Gil.ToString("N0"));
        yield return ("Past gil", economy.PastGil.ToString("N0"));
        yield return ("Battle party", string.Join(", ", characters.CombatParty.Where(row => row < characters.Records.Length).Select(row => Character(characters.Records[row].CharacterId))));
        foreach (NativeCharacterVital character in characters.Records)
        {
            string label = $"{Character(character.CharacterId)} [{character.CharacterId}]";
            yield return (label, $"Lv {characters.CharacterLevel(character)}, HP {character.HitPoints.Minimum}, weapon {character.Weapon}");
            yield return ($"{label} magic", string.Join(", ", character.MagicInventory.Where(stock => stock.Count > 0).Select(stock => $"{MagicName(kernel, stock.SpellId)} ×{stock.Count}")));
        }
        foreach (IGrouping<byte, NativeItemStock> items in economy.Items.Where(stock => stock.Count > 0).GroupBy(stock => stock.ItemId))
        {
            yield return ($"Inventory · {ItemName(kernel, items.Key)} [{items.Key}]", $"×{items.Sum(item => item.Count)}");
        }
    }

    private static IEnumerable<(string Key, string Value)> ReadNativeMenuRows(ResourceService resources, ResourceKey key, string gameDirectory)
    {
        KernelData kernel = ReadKernel(resources);
        switch (key.Path)
        {
            case "menu/shop.bin":
            case "menu/price.bin":
                using (GameAssets assets = new(gameDirectory, resources: resources))
                {
                    NativeShopDefinitions shops = assets.ReadNativeShopDefinitions();
                    if (key.Path == "menu/price.bin")
                    {
                        return shops.Prices.Select((price, id) => ($"Price · {ItemName(kernel, id)} [{id}]", $"{price.Base:N0} base gil · sell multiplier {price.SellMultiplier}")).ToArray();
                    }
                    return shops.Catalogs.SelectMany((catalog, index) => catalog.Select((slot, id) => ($"Shop {index}, slot {id + 1}", $"{ItemName(kernel, slot.ItemId)} · availability {slot.Availability}"))).ToArray();
                }
            case "menu/mwepon.bin":
            case "menu/mwepon.msg":
                using (GameAssets assets = new(gameDirectory, resources: resources))
                {
                    NativeWeaponShopDefinitions weapons = assets.ReadNativeWeaponShopDefinitions();
                    if (key.Path == "menu/mwepon.msg")
                    {
                        return weapons.Messages.Chunk(2).Select((row, id) => ($"Weapon shop message {id}", $"Message {BinaryPrimitives.ReadUInt16LittleEndian(row)}")).ToArray();
                    }
                    return weapons.Recipes.Select((recipe, id) => ($"Weapon {id}", $"{recipe.Name} · base cost {recipe.CostBase} · {string.Join(", ", recipe.Materials.Where(item => item.Count > 0).Select(item => $"{ItemName(kernel, item.ItemId)} ×{item.Count}"))}")).ToArray();
                }
            case "menu/pet_exp.bin":
            case "menu/pet_exp.msg":
                return NativeAngeloTrainingDefinitionReader.Read(resources.Read(new("menu/pet_exp.bin")), resources.Read(new("menu/pet_exp.msg")), NativeTextEncoding.Create(resources.Source.Language)).Select((training, id) => ($"Angelo trick {id}", $"{training.Name} · {training.TrainingTotal} training points\n{training.Description}")).ToArray();
            case "menu/mtmag.bin":
            case "menu/mmag.bin":
                using (GameAssets assets = new(gameDirectory, resources: resources))
                {
                    NativeTutorialBrowserCatalog tutorials = assets.ReadNativeTutorialBrowserCatalog();
                    return tutorials.Categories.SelectMany(category => category.Pages.Select(page => ($"{category.Title} [{category.Id}], page {page.Id}", $"Content {page.ContentResource}\n{string.Join("\n", page.Placements.Select(text => $"{text.Value} · ({text.X}, {text.Y})"))}"))).ToArray();
                }
        }
        byte[] bytes = resources.Read(key);
        if (NativeDataResourceFiles.IsStandaloneRefinementTable(key))
        {
            int kind = key.Path[8] - '0';
            NativeMenuDataReader.ValidateRefinementTable(bytes, kind);
            return bytes.Chunk(8).Select((row, id) =>
            {
                string input = kind == 2 ? MagicName(kernel, row[5]) : kind == 4 ? $"Card {row[5]}" : ItemName(kernel, row[5]);
                string output = kind is 0 or 2 ? MagicName(kernel, row[7]) : ItemName(kernel, row[7]);
                return ($"Recipe {id}", $"{input} ×{row[6]} → {output} ×{BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(2))}");
            }).ToArray();
        }
        if (key.Path == "menu/mthomas.bin")
        {
            NativeMenuDataReader.ValidateSolomonRingIngredients(bytes);
            return bytes.Chunk(4).Take(3).Select((row, id) => ($"Ingredient {id + 1}", $"{ItemName(kernel, row[0])} ×{row[1]}")).ToArray();
        }
        if (key.Path == "menu/cyocobo.bin")
        {
            NativeChocoboWorldTransferTable table = new([.. bytes]);
            table.Validate();
            return table.Entries.Select((item, id) => ($"Transfer slot {id}", ItemName(kernel, item))).ToArray();
        }
        _ = NativeMenuDataReader.ReadSolomonRingItemId(bytes);
        return bytes.Chunk(4).Select((row, id) =>
        {
            NativeFieldItem item = new(row[0], row[1], row[2], row[3]);
            string[] targets = new[] { (item.SupportsCharacterUse, "character"), (item.SupportsGuardianForceUse, "GF"), (item.SupportsRestUse, "rest"), (item.SupportsNameUse, "rename"), (item.SupportsBlueMagicUse, "Blue Magic"), (item.SupportsBattleUse, "battle"), (item.SupportsInventoryUse, "inventory") }.Where(target => target.Item1).Select(target => target.Item2).ToArray();
            return ($"{ItemName(kernel, id)} [{id}]", $"Type {item.Type} · {(targets.Length == 0 ? "No use action" : string.Join(", ", targets))} · Use parameters {row[1]}, {row[2]}, {row[3]}");
        }).ToArray();
    }

    private static string ItemName(KernelData kernel, int id) => (id < kernel.BattleItems.Count ? kernel.BattleItems[id].Name : kernel.FieldItems.ElementAtOrDefault(id - kernel.BattleItems.Count)?.Name) ?? $"Item {id}";
    private static string MagicName(KernelData kernel, int id) => (id > 0 ? kernel.MagicSpells.ElementAtOrDefault(id - 1)?.Name : null) ?? $"Magic {id}";

    private static ModPreviewComparison CompareNativeRows(IEnumerable<(string Key, string Value)> original, IEnumerable<(string Key, string Value)> modified)
    {
        const int maximumRows = 40;
        var before = original.ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
        var after = modified.ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
        string[] changes = before.Keys.Union(after.Keys, StringComparer.Ordinal).Where(key => before.GetValueOrDefault(key) != after.GetValueOrDefault(key)).ToArray();
        string Format(Dictionary<string, string> rows)
        {
            if (changes.Length == 0)
            {
                return "The compared values are unchanged.";
            }
            IEnumerable<string> lines = changes.Take(maximumRows).Select(key => $"{key}\n{rows.GetValueOrDefault(key) ?? "—"}");
            if (changes.Length > maximumRows)
            {
                lines = lines.Append($"Showing {maximumRows} of {changes.Length} changed rows.");
            }
            return string.Join("\n\n", lines);
        }
        return new(new(Format(before)), new(Format(after)));
    }
}
