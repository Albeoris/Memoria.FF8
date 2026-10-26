namespace Memoria.FF8.GameUi;

public sealed partial class MenuController
{
    private Func<string, int, string, string>? nativeTutorialLocalization;

    internal void ConfigureNativeTutorialLocalization(Func<string, int, string, string> localize)
    {
        nativeTutorialLocalization = localize;
        _view = null;
    }

    internal string LocalizeNativeTutorial(string file, int ordinal, string original) => nativeTutorialLocalization?.Invoke(file, ordinal, original) ?? original;

    private string NativeMenuText(int slot, int? table, int ordinal, string original) => LocalizeNativeTutorial($"mngrp_slot{slot:000}_table" + (table.HasValue ? $"{table:000}" : ""), ordinal, original);

    private string NativeRecordText(int slot, int ordinal, string original) => LocalizeNativeTutorial($"mngrp_slot{slot:000}_records", ordinal, original);
}
