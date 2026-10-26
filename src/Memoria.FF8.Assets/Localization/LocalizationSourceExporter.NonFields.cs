using System.Buffers.Binary;
using System.Collections;
using System.Globalization;
using System.Reflection;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.MngrpBin;
using Albeoris.Games.FF8.MngrpBin.Abstractions;
using Albeoris.Games.FF8.NamedicBin;
using Albeoris.Games.FF8.Msd;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.Assets.Localization;

public static partial class LocalizationSourceExporter
{
    private sealed partial class Reader
    {
        public void NonFields()
        {
            Attempt("Kernel", "main/kernel.bin", Kernel);
            Attempt("Guardians", "main/init.out", GuardianDefaults);
            Attempt("Menus", "menu/mngrp.bin", Menu);
            foreach ((string resource, string category, string table) in new[] { ("menu/namedic.bin", "Menus", "namedic_menu"), ("menu/areames.dc1", "Locations", "names_locations") })
            {
                Attempt(category, resource, () =>
                {
                    string[] texts = NamedicBin.ReadStrings(Required(resource), nonFieldEncoding);
                    for (int ordinal = 0; ordinal < texts.Length; ordinal++)
                    {
                        Add(category, table, ordinal, texts[ordinal], resource);
                    }
                });
            }
            Attempt("Monsters", "battle", Enemies);
            Attempt("World", "world/wmsetus.obj", World);
            foreach ((string resource, string category, string table) in new[] { ("text/card-names", "Cards", "names_cards"), ("text/card-names-alternate", "Cards", "namesalternate_cards"), ("text/card-descriptions", "Cards", "named_texts_cards"), ("text/draw-points", "Draw points", "drawpoints_world") })
            {
                Attempt(category, resource, () =>
                {
                    if (category == "Cards")
                    {
                        WordBank(resource, category, table);
                    }
                    else
                    {
                        PointerBank(resource, category, table);
                    }
                });
            }
            Attempt("Monsters", "text/enemy-scan", Scan);
            Attempt("Abilities", "menu/pet_exp.msg", () =>
            {
                var moves = NativeAngeloTrainingDefinitionReader.Read(Required("menu/pet_exp.bin"), Required("menu/pet_exp.msg"), nonFieldEncoding);
                for (int ordinal = 0; ordinal < moves.Length; ordinal++)
                {
                    Add("Abilities", "names_angelo_abilities", ordinal, moves[ordinal].Name, "menu/pet_exp.msg");
                    Add("Abilities", "descriptions_angelo_abilities", ordinal, moves[ordinal].Description, "menu/pet_exp.msg");
                }
            });
            gaps.Add(new("System", "authored-interface", "Authored interface messages require explicit semantic identifiers and are not original-game archive text."));
            gaps.Add(new("Minigames", "pocketstation", "Player-owned names and binary transfer data are not localization catalogue resources."));
            gaps.Add(new("Publications", "menu-artwork", "Lettering embedded in textures is outside plain-string source export."));
        }

        private void GuardianDefaults()
        {
            byte[] initial = Required("main/init.out");
            if (initial.Length != 2812)
            {
                throw new InvalidDataException("Default guardian names require the complete original initialization template.");
            }
            for (int ordinal = 0; ordinal < 16; ordinal++)
            {
                ReadOnlySpan<byte> name = initial.AsSpan(ordinal * 68, 12);
                int end = name.IndexOf((byte)0);
                Add("Guardians", "defaults_guardians", ordinal, nonFieldEncoding.GetString(end < 0 ? name : name[..end]), "main/init.out");
            }
        }

        private void Kernel()
        {
            KernelData kernel = KernelData.ReadFromArray(Required("main/kernel.bin"), nonFieldEncoding);
            foreach (PropertyInfo collectionProperty in typeof(KernelData).GetProperties().Where(property => typeof(IList).IsAssignableFrom(property.PropertyType)).OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                IList collection = (IList)collectionProperty.GetValue(kernel)!;
                for (int ordinal = 0; ordinal < collection.Count; ordinal++)
                {
                    object? item = collection[ordinal];
                    if (item is string value)
                    {
                        Add(KernelCategory(collectionProperty.Name), KernelTable(collectionProperty.Name, "text"), ordinal, value, "main/kernel.bin", new Dictionary<string, string?> { ["Kernel Collection"] = collectionProperty.Name, ["Role"] = "Text" });
                    }
                    else if (item is not null)
                    {
                        foreach (PropertyInfo property in item.GetType().GetProperties().Where(property => property.PropertyType == typeof(string) && property.CanRead).OrderBy(property => property.Name, StringComparer.Ordinal))
                        {
                            if (property.GetValue(item) is string text)
                            {
                                Add(KernelCategory(collectionProperty.Name), KernelTable(collectionProperty.Name, property.Name), ordinal, text, "main/kernel.bin", new Dictionary<string, string?> { ["Kernel Collection"] = collectionProperty.Name, ["Role"] = property.Name });
                            }
                        }
                    }
                }
            }
        }

        private static string KernelCategory(string collection) => collection switch
        {
            "MagicSpells" => "Magic",
            "BattleItems" or "FieldItems" => "Items",
            "Weapons" => "Weapons",
            "Characters" => "Characters",
            "Guardians" or "IndependentGuardianAttacks" => "Guardians",
            "BattleCommands" or "EnemyAttacks" or "MiscTexts" => "Battle",
            _ => "Abilities"
        };

        private void Menu()
        {
            byte[] menuContent = Required("menu/mngrp.bin");
            byte[] menuHeader = Required("menu/mngrphd.bin");
            if (menuHeader.Length != 2048)
            {
                throw new InvalidDataException("Menu export requires the complete 256-slot directory.");
            }
            List<IMngrpSection> sections = [];
            for (int slot = 0; slot < 256; slot++)
            {
                uint raw = BinaryPrimitives.ReadUInt32LittleEndian(menuHeader.AsSpan(slot * 8));
                uint size = BinaryPrimitives.ReadUInt32LittleEndian(menuHeader.AsSpan(slot * 8 + 4));
                if ((raw is 0 or uint.MaxValue) && size == 0 || slot is >= 196 and <= 200)
                {
                    continue;
                }
                int index = slot;
                Attempt("Menus", "menu/mngrp.bin/slot" + index, () =>
                {
                    // Independent section parsing keeps a malformed/unsupported text section
                    // from suppressing unrelated tables in the same archive.
                    byte[] selected = new byte[menuHeader.Length];
                    menuHeader.AsSpan(index * 8, 8).CopyTo(selected.AsSpan(index * 8));
                    if (index is >= 188 and <= 192)
                    {
                        menuHeader.AsSpan((index + 8) * 8, 8).CopyTo(selected.AsSpan((index + 8) * 8));
                    }
                    sections.AddRange(MngrpArchive.Read(menuContent, selected, nonFieldEncoding).Sections);
                });
            }
            foreach (IMngrpSection section in sections.OrderBy(section => section.SlotIndex))
            {
                cancellation.ThrowIfCancellationRequested();
                string prefix = "mngrp_slot" + section.SlotIndex.ToString("D3", CultureInfo.InvariantCulture);
                string category = section.SlotIndex switch { 192 => "Cards", 87 => "Publications", >= 88 and <= 90 => "Tutorials", >= 95 and <= 133 => "Information and SeeD exam", _ => "Menus" };
                Dictionary<string, string?> metadata = new() { ["Menu Section"] = section.SlotIndex.ToString(CultureInfo.InvariantCulture), ["Section Layout"] = section.Layout.ToString() };
                switch (section)
                {
                    case MngrpStringTableSection single:
                        if (section.SlotIndex is >= 95 and <= 126)
                        {
                            ExamRecords(section.SlotIndex, prefix + "_table", metadata, menuHeader, menuContent);
                        }
                        else
                        {
                            MenuTable(single.Table, prefix + "_table", category, metadata);
                        }
                        break;
                    case MngrpStringTableGroupSection group:
                        for (int table = 0; table < group.Tables.Count; table++)
                        {
                            if (group.Tables[table] is MngrpStringTable strings)
                            {
                                MenuTable(strings, prefix + "_table" + table.ToString("D3", CultureInfo.InvariantCulture), category, metadata);
                            }
                        }
                        break;
                    case MngrpTextRecordSection records:
                        for (int row = 0; row < records.Records.Count; row++)
                        {
                            Add(category, prefix + "_records", row, records.Records[row].Text.Value, "menu/mngrp.bin", metadata);
                        }
                        break;
                    case MngrpTextBlockSection blocks:
                        for (int block = 0; block < blocks.Blocks.Count; block++)
                        {
                            for (int text = 0; text < blocks.Blocks[block].Texts.Count; text++)
                            {
                                if (section.SlotIndex is >= 128 and <= 133 && text == 1)
                                {
                                    gaps.Add(new(category, "menu/mngrp.bin/" + prefix + "/block" + block + "/text01", "Information bodies require a structured Unicode adapter preserving conditional gates and two-byte link operands; generic text decoding is not a translatable representation."));
                                    continue;
                                }
                                Add(category, prefix + "_block" + block.ToString("D4", CultureInfo.InvariantCulture), text, blocks.Blocks[block].Texts[text].Value, "menu/mngrp.bin", metadata);
                            }
                        }
                        break;
                    case MngrpOpaqueSection:
                        if (section.ReservedSize > 0)
                        {
                            gaps.Add(new(category, "menu/mngrp.bin/" + prefix, "Opaque section retained by its reader; text coverage is not established."));
                        }
                        break;
                }
            }
        }

        private void ExamRecords(int slot, string table, IReadOnlyDictionary<string, string?> metadata, byte[] header, byte[] content)
        {
            uint raw = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(slot * 8));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(slot * 8 + 4));
            if (raw is 0 or uint.MaxValue || (ulong)raw - 1 + size > (ulong)content.Length)
            {
                throw new InvalidDataException("Exam resource is outside the menu archive.");
            }
            byte[] bytes = content.AsSpan(checked((int)raw - 1), checked((int)size)).ToArray();
            int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
            if (2 + count * 2 > bytes.Length)
            {
                throw new InvalidDataException("Exam text directory is truncated.");
            }
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2 + ordinal * 2));
                if (offset < 2 + count * 2 || offset >= bytes.Length)
                {
                    throw new InvalidDataException("Exam text record pointer is invalid.");
                }
                Dictionary<string, string?> values = new(metadata, StringComparer.Ordinal) { ["Native Record Prefix"] = bytes[offset].ToString(CultureInfo.InvariantCulture) };
                if (slot > 95 && bytes[offset] > 1)
                {
                    throw new InvalidDataException("Exam answer prefix is outside its binary contract.");
                }
                Add("Information and SeeD exam", table, ordinal, NullText(bytes, offset + 1, bytes.Length), "menu/mngrp.bin", values);
            }
        }

        private void MenuTable(MngrpStringTable strings, string table, string category, IReadOnlyDictionary<string, string?> metadata)
        {
            for (int ordinal = 0; ordinal < strings.Entries.Count; ordinal++)
            {
                if (strings.Entries[ordinal].Text is MngrpText text)
                {
                    Add(category, table, ordinal, text.Value, "menu/mngrp.bin", metadata);
                }
            }
        }

        private void Enemies()
        {
            using IFlArchive battle = archives.OpenArchive("battle");
            foreach (IFlArchiveEntry entry in battle.Entries.Where(entry => System.Text.RegularExpressions.Regex.IsMatch(Basename(entry.RelativePath), "^c0m[0-9]{3}\\.dat$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).OrderBy(entry => entry.RelativePath, StringComparer.Ordinal))
            {
                string file = Basename(entry.RelativePath).ToLowerInvariant();
                Attempt("Monsters", "battle/" + file, () => Enemy(file, Required("battle/" + file)));
            }
        }

        private void Enemy(string file, byte[] bytes)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            if (count is not (2 or 11))
            {
                throw new NotSupportedException("Enemy text requires a two- or eleven-section directory.");
            }
            string stem = Path.GetFileNameWithoutExtension(file);
            int information = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(count == 11 ? 28 : 4));
            ReadOnlySpan<byte> name = bytes.AsSpan(information, 24);
            int terminator = name.IndexOf((byte)0);
            if (terminator < 0)
            {
                throw new InvalidDataException("Enemy name is not terminated within its fixed buffer.");
            }
            Add("Monsters", stem + "_names", 0, nonFieldEncoding.GetString(name[..terminator]), "battle/" + file);
            int start = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(count == 11 ? 32 : 8));
            int end = count == 11 ? BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(36)) : bytes.Length;
            if (BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(start)) != 3)
            {
                throw new InvalidDataException("Enemy script text header is unsupported.");
            }
            int indices = checked(start + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(start + 8)));
            int text = checked(start + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(start + 12)));
            if (indices < start + 16 || text < indices || text > end || end > bytes.Length || (text - indices) % 2 != 0)
            {
                throw new InvalidDataException("Enemy text index bounds are invalid.");
            }
            for (int ordinal = 0; ordinal < (text - indices) / 2; ordinal++)
            {
                int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(indices + 2 * ordinal));
                if (offset == ushort.MaxValue)
                {
                    continue;
                }
                int position = checked(text + offset);
                string value = NullText(bytes, position, end);
                Add("Battle", stem + "_dialogue_battle", ordinal, value, "battle/" + file, new Dictionary<string, string?> { ["Source Offset"] = position.ToString(CultureInfo.InvariantCulture) });
            }
        }

        private void World()
        {
            byte[] bytes = Required("world/wmsetus.obj");
            int start = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(13 * 4));
            int end = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(14 * 4));
            if (start < 0 || end > bytes.Length || start >= end)
            {
                throw new InvalidDataException("World message section bounds are invalid.");
            }
            uint tableBytes = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(start));
            if (tableBytes < 4 || tableBytes % 4 != 0 || tableBytes > end - start)
            {
                throw new InvalidDataException("World message pointer table is invalid.");
            }
            for (int ordinal = 0; ordinal < tableBytes / 4; ordinal++)
            {
                int offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(start + ordinal * 4)));
                if (offset < tableBytes)
                {
                    throw new InvalidDataException("World message pointer enters its index table.");
                }
                int index = ordinal;
                Attempt("World", "world/wmsetus.obj/" + index, () => Add("World", "messages_world", index, NullText(bytes, checked(start + offset), end), "world/wmsetus.obj"));
            }
        }

        private string NullText(byte[] bytes, int start, int end)
        {
            if (start < 0 || end > bytes.Length || start >= end)
            {
                throw new InvalidDataException("Text pointer is outside its source bank.");
            }
            int terminator = bytes.AsSpan(start, end - start).IndexOf((byte)0);
            if (terminator < 0)
            {
                throw new InvalidDataException("Source text has no bounded terminator.");
            }
            return nonFieldEncoding.GetString(bytes.AsSpan(start, terminator));
        }

        private void WordBank(string resource, string category, string table)
        {
            if (resource == "text/card-names" && source.Descriptor.Edition == Memoria.FF8.Resources.GameDataEdition.Classic && source is IGameNativeMemorySource memory)
            {
                var names = TripleTriadCatalog.Read(memory).Names;
                for (int ordinal = 0; ordinal < names.Length; ordinal++)
                {
                    Add(category, table, ordinal, names[ordinal], resource, new Dictionary<string, string?> { ["Native Source Fingerprint"] = memory.SourceHash });
                }
                return;
            }
            byte[] bytes = Required(resource);
            int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
            int header = checked(2 + count * 2);
            if (header > bytes.Length)
            {
                throw new InvalidDataException("Named WORD text directory is incomplete.");
            }
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                int index = ordinal;
                Attempt(category, resource + "/" + index, () =>
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2 + index * 2));
                    if (offset < header)
                    {
                        throw new InvalidDataException("Named WORD text pointer enters its index table.");
                    }
                    Add(category, table, index, NullText(bytes, offset, bytes.Length), resource);
                });
            }
        }

        private void PointerBank(string resource, string category, string table)
        {
            byte[] bytes = Required(resource);
            int header = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            if (header < 4 || header % 4 != 0 || header > bytes.Length)
            {
                throw new InvalidDataException("Named text pointer table is invalid.");
            }
            int count = header / 4;
            // Draw-point resources include a final end pointer; it is not another message.
            if (resource == "text/draw-points")
            {
                count--;
            }
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(ordinal * 4));
                if (offset < header)
                {
                    throw new InvalidDataException("Named text pointer enters its index table.");
                }
                Add(category, table, ordinal, NullText(bytes, offset, bytes.Length), resource);
            }
        }

        private void Scan()
        {
            byte[]? bytes = Resource("text/enemy-scan");
            if (bytes is null && source.Descriptor.Edition == Memoria.FF8.Resources.GameDataEdition.Classic && source is IGameNativeMemorySource memory)
            {
                var scan = NativeBattleScanTextResourcesReader.Read(memory, Required("main/kernel.bin"));
                byte[] body = scan.DescriptionBytes.ToArray();
                for (int ordinal = 0; ordinal < scan.DescriptionOffsets.Length; ordinal++)
                {
                    Add("Monsters", "scan_monsters", ordinal, NullText(body, scan.DescriptionOffsets[ordinal], body.Length), "text/enemy-scan", new Dictionary<string, string?> { ["Native Source Fingerprint"] = memory.SourceHash });
                }
                return;
            }
            if (bytes is null)
            {
                throw new FileNotFoundException("Missing source resource: text/enemy-scan");
            }
            if (bytes.Length <= 320)
            {
                throw new InvalidDataException("Scan descriptions require a complete 160-entry directory.");
            }
            for (int ordinal = 0; ordinal < 160; ordinal++)
            {
                int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(ordinal * 2));
                Add("Monsters", "scan_monsters", ordinal, NullText(bytes, 320 + offset, bytes.Length), "text/enemy-scan");
            }
        }
    }
}
