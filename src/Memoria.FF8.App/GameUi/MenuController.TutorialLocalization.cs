using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Func<string, int, string, string>? nativeTutorialLocalization;
    private Func<string, int, string, BoundLocalizationText>? nativeTutorialBinding;
    internal event Action<LocalizationTextBinding, string, string, string>? TutorialDialoguePresented;

    internal void ConfigureNativeTutorialLocalization(Func<string, int, string, string> localize, Func<string, int, string, BoundLocalizationText>? bind = null)
    {
        nativeTutorialLocalization = localize;
        nativeTutorialBinding = bind;
        _view = null;
    }

    internal string LocalizeNativeTutorial(string file, int ordinal, string original) => nativeTutorialLocalization?.Invoke(file, ordinal, original) ?? original;

    internal BoundLocalizationText? BindNativeTutorial(string file, int ordinal, string original) => nativeTutorialBinding?.Invoke(file, ordinal, original);

    internal void ObserveTutorialDialogue(LocalizationTextBinding binding, string occurrenceId, string rendered) => TutorialDialoguePresented?.Invoke(binding, "Tutorial/messages", occurrenceId, rendered);

    private string NativeMenuText(int slot, int? table, int ordinal, string original) => LocalizeNativeTutorial($"mngrp_slot{slot:000}_table" + (table.HasValue ? $"{table:000}" : ""), ordinal, original);

    private string NativeRecordText(int slot, int ordinal, string original) => LocalizeNativeTutorial($"mngrp_slot{slot:000}_records", ordinal, original);
}
