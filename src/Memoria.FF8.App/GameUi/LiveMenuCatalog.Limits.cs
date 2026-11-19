using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public sealed partial class LiveMenuCatalog
{
    public void ProjectLimit(MenuCharacter character, int weaponId, GameState state, IReadOnlyDictionary<int, string>? names = null)
    {
        if (character.NativeId is int identity && state.BattleLoadouts?.Get(identity)?.LimitFamilies is { } families)
        {
            character.LimitMoves.Clear();
            character.LimitSettings.Clear();
            character.LimitToggles.Clear();
            character.LimitAvailability.Clear();
            character.NativeLimitCommand = null;
            List<string> labels = [];
            foreach (BattleLimitFamily family in families)
            {
                MenuCharacter source = new() { NativeId = BattleLimitFamilyRules.SourceCharacter(family) };
                ProjectNativeLimit(source, weaponId, state, names, family);
                labels.Add(source.LimitBreak);
                foreach (MenuLimitMove move in source.LimitMoves)
                {
                    if (families.Length > 1)
                    {
                        move.Id = family + "/" + move.Id;
                        move.Name = source.LimitBreak + " · " + move.Name;
                    }
                    character.LimitMoves.Add(move);
                }
                foreach ((string key, string value) in source.LimitSettings)
                {
                    character.LimitSettings[families.Length > 1 ? source.LimitBreak + " · " + key : key] = value;
                }
                if (families.Length == 1)
                {
                    character.NativeLimitCommand = source.NativeLimitCommand;
                    foreach ((string key, bool value) in source.LimitToggles)
                    {
                        character.LimitToggles[key] = value;
                    }
                    foreach ((string key, bool value) in source.LimitAvailability)
                    {
                        character.LimitAvailability[key] = value;
                    }
                }
            }
            character.LimitBreak = labels.Count == 0 ? "None" : string.Join(" / ", labels);
            return;
        }
        ProjectNativeLimit(character, weaponId, state, names);
    }

    private void ProjectNativeLimit(MenuCharacter character, int weaponId, GameState state, IReadOnlyDictionary<int, string>? names, BattleLimitFamily? family = null)
    {
        if (character.NativeId is not int id || (uint)id >= kernel.Characters.Count)
        {
            return;
        }
        if (names is not null)
        {
            SetTextNames(names);
        }
        byte command = kernel.Characters[id].LimitId;
        character.NativeLimitCommand = command;
        character.LimitBreak = CommandName(command) ?? "Unavailable";
        if (family is BattleLimitFamily.Combine or BattleLimitFamily.AngelWing)
        {
            int choice = family == BattleLimitFamily.Combine ? 0 : 1;
            character.LimitBreak = LocalizedKernel(nameof(kernel.RinoaLimits), "Name", choice, kernel.RinoaLimits[choice].Name) ?? family.ToString()!;
            if (family == BattleLimitFamily.AngelWing)
            {
                character.NativeLimitCommand = null;
            }
        }
        NativeLimitProgress? progress = state.LimitProgress;
        if (command == 11)
        {
            for (int index = 0; index < kernel.ZellLimits.Count; index++)
            {
                var move = kernel.ZellLimits[index];
                character.LimitMoves.Add(LimitMove(index, LocalizedKernel(nameof(kernel.ZellLimits), "Name", index, move.Name), LocalizedKernel(nameof(kernel.ZellLimits), "Description", index, move.Description), progress is null ? null : (progress.DuelFlags & (1 << index)) != 0));
            }
        }
        else if (command == 15)
        {
            for (int index = 0; index < kernel.BlueMagics.Count; index++)
            {
                var move = kernel.BlueMagics[index];
                character.LimitMoves.Add(LimitMove(index, LocalizedKernel(nameof(kernel.BlueMagics), "Name", index, move.Name), LocalizedKernel(nameof(kernel.BlueMagics), "Description", index, move.Description), progress is null ? null : (progress.BlueMagicFlags & (1 << index)) != 0));
            }
        }
        else if (command == 19 && family != BattleLimitFamily.AngelWing)
        {
            character.LimitSettings["Angelo name"] = FormatText("{Char Angelo2}", names, terms)!;
            for (int index = 0; index < 8; index++)
            {
                var definition = AngeloMove(index);
                MenuLimitMove move = LimitMove(index, LocalizedTable("names_angelo_abilities", index, definition.Name), LocalizedTable("descriptions_angelo_abilities", index, definition.Description), progress is null ? null : (progress.AngeloFlags & (1 << index)) != 0);
                if (progress is not null)
                {
                    move.TrainingProgress = AngeloTrainingProgress(definition.TrainingTotal, progress.TrainingCounters[index], move.Learned == true);
                }
                move.TrainingAvailable = progress?.PetPalsFlags is byte available ? (available & (1 << index)) != 0 : null;
                move.IsLearning = progress is not null && progress.TrainingSelection == index && move.Learned == false;
                character.LimitMoves.Add(move);
            }
            if (family != BattleLimitFamily.Combine && state.SavedBattleFlags is { } flags && (flags.EffectiveKnownMask & 0x20) != 0)
            {
                character.LimitSettings["Angel Wing"] = (flags.Value & 0x20) != 0 ? "Available" : "Unavailable";
                character.LimitAvailability["Angel Wing"] = (flags.Value & 0x20) != 0;
            }
        }
        else if (family == BattleLimitFamily.AngelWing)
        {
            character.LimitSettings["Angel Wing"] = state.SavedBattleFlags is { } flags && (flags.EffectiveKnownMask & 0x20) != 0 && (flags.Value & 0x20) != 0 ? "Available" : "Unavailable";
        }
        else if (command == 16)
        {
            for (int index = 0; index < 6; index++)
            {
                var move = kernel.MagicSpells[51 + index];
                character.LimitMoves.Add(LimitMove(index, LocalizedKernel(nameof(kernel.MagicSpells), "Name", 51 + index, move.Name), LocalizedKernel(nameof(kernel.MagicSpells), "Description", 51 + index, move.Description), progress?.SpecialMagicFlags is not byte flags ? null : (flags & (1 << index)) != 0));
            }
        }
        else if (command == 14)
        {
            for (int index = 0; index < kernel.IrvinLimits.Count; index++)
            {
                var move = kernel.IrvinLimits[index];
                int? stock = state.Economy is null ? null : state.Economy.Items.Where(item => item.ItemId == index + 101).Sum(item => item.Count);
                MenuLimitMove choice = LimitMove(index, LocalizedKernel(nameof(kernel.IrvinLimits), "Name", index, move.Name), LocalizedKernel(nameof(kernel.IrvinLimits), "Description", index, move.Description), state.ShotAvailability is null ? null : (state.ShotAvailability.Flags770 & (1 << index)) != 0 || stock > 0);
                choice.Stock = stock;
                character.LimitMoves.Add(choice);
            }
        }
        if (command != 5)
        {
            return;
        }
        if (progress?.Options978 is ushort options)
        {
            character.LimitSettings["Auto"] = (options & 1) != 0 ? "On" : "Off";
            character.LimitToggles["Auto"] = (options & 1) != 0;
            character.LimitSettings["Indicator"] = (options & 0x8000) == 0 ? "On" : "Off";
            character.LimitToggles["Indicator"] = (options & 0x8000) == 0;
        }
        if ((uint)weaponId >= kernel.Weapons.Count)
        {
            throw new InvalidDataException("Renzokuken information requires the character's equipped weapon record.");
        }
        byte finishers = kernel.Weapons[weaponId].CharacterId == 0 ? kernel.Weapons[weaponId].RenzokukenFinishers : (byte)0;
        for (int index = 0; index < kernel.SquallLimits.Count; index++)
        {
            var move = kernel.SquallLimits[index];
            character.LimitMoves.Add(new MenuLimitMove
            {
                Id = $"finisher-{index}",
                Name = FormatDescription(LocalizedKernel(nameof(kernel.SquallLimits), "Name", index, move.Name)) ?? throw new InvalidDataException("Renzokuken finisher has no original name."),
                Description = FormatDescription(LocalizedKernel(nameof(kernel.SquallLimits), "Description", index, move.Description)) ?? "",
                Learned = (finishers & (1 << index)) != 0
            });
        }
    }

    internal static int AngeloTrainingProgress(byte total, byte remaining, bool learned)
    {
        if (learned)
        {
            return 64;
        }
        if (total == 0)
        {
            throw new InvalidDataException("An unfinished Angelo ability requires a nonzero training total.");
        }
        return Math.Clamp((total - remaining) * 64 / total, 0, remaining == 0 ? 64 : 63);
    }

    private MenuLimitMove LimitMove(int index, string? name, string? description, bool? learned)
    {
        return new MenuLimitMove
        {
            Id = $"limit-{index}",
            Name = FormatDescription(name) ?? throw new InvalidDataException("Limit move has no original name."),
            Description = FormatDescription(description) ?? "",
            Learned = learned
        };
    }
}
