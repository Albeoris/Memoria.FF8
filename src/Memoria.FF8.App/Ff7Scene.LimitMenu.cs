using System.Globalization;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private int pendingNativeLimitLevel;
    private IReadOnlyList<KernelSection>? nativeMenuLimitKernel;
    private IReadOnlyList<string>? nativeMenuLimitNames;

    private void RegisterNativeLimitMenu(MenuController menu)
    {
        menu.Register(new()
        {
            Id = "ff7-limit-set",
            Title = "Set Limit Level",
            Help = "Choose a level with a learned Limit.",
            Entries = _ => ReadNativeLimitLevels(set: true)
        });
        menu.Register(new()
        {
            Id = "ff7-limit-check",
            Title = "Check Limits",
            Help = "Review learned Limits at each level.",
            Entries = _ => ReadNativeLimitLevels(set: false)
        });
        menu.Register(new()
        {
            Id = "ff7-limit-confirm",
            Title = "Change Limit Level",
            Help = "Setting a Limit level resets the gauge to 0.",
            Entries = _ =>
            [
                new("no", "No", ConfirmNativeLimitLevelHelp, Action: new("menu-back")),
                new("yes", "Yes", ConfirmNativeLimitLevelHelp, Action: new("ff7-limit-apply"))
            ]
        });
    }

    private string ConfirmNativeLimitLevelHelp => $"Set Limit level {pendingNativeLimitLevel} and reset the gauge to 0?";

    private static IReadOnlyList<MenuEntry> ReadNativeLimit(MenuController _, Ff7CharacterState character)
    {
        byte[] record = character.NativeRecord;
        return
        [
            new("set", "Set", $"Current level {record[14]}   Gauge {record[15] * 100 / 255}%. Choose the active level.", "ff7-limit-set"),
            new("check", "Check", "Review learned Limits.", "ff7-limit-check")
        ];
    }

    private IReadOnlyList<MenuEntry> ReadNativeLimitLevels(bool set)
    {
        Ff7CharacterState character = Session!.Progress.Characters.Single(candidate => candidate.Id == selectedNativeMenuCharacter);
        nativeMenuLimitKernel ??= KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<string> names = nativeMenuLimitNames ??= KernelTextArchive.ReadPlainStrings(KernelTextArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel2).Path))[9].Span, 0xB8);

        List<MenuEntry> entries = new(4);
        for (int level = 1; level <= 4; level++)
        {
            bool learned = Ff7NativeBattleLimits.ReadLearnedMask(character, level) != 0;
            IReadOnlyList<byte> limits = Ff7NativeBattleLimits.ReadLearnedIds(character, nativeMenuLimitKernel, level);
            string label = $"Level {level}";
            if (level == character.NativeRecord[14])
            {
                label += "   Current";
            }
            string help = limits.Count == 0 ? "No learned Limits." : string.Join(", ", limits.Select(id => names[id]));
            string value = level.ToString(CultureInfo.InvariantCulture);
            entries.Add(new($"level-{value}", label, help, Action: set ? new("ff7-limit-select-level", Value: value) : null, Enabled: !set || learned));
        }
        return entries;
    }

    private bool ApplyNativeLimitMenuAction(MenuAction action)
    {
        if (action.Kind == "ff7-limit-select-level")
        {
            pendingNativeLimitLevel = int.Parse(action.Value, CultureInfo.InvariantCulture);
            playerMenu!.Push("ff7-limit-confirm");
            return true;
        }
        if (action.Kind == "ff7-limit-apply")
        {
            if (Session!.TrySetMenuLimitLevel(selectedNativeMenuCharacter, pendingNativeLimitLevel))
            {
                playerMenu!.Back();
                playerMenu.Back();
            }
            return true;
        }
        return false;
    }
}
