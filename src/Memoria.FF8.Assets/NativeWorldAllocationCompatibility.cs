namespace Memoria.FF8.Assets;

/// <summary>Normal foot battle reload compatibility for authenticated settings revisions with identical world control tables.</summary>
public static class NativeWorldAllocationCompatibility
{
    private const string FirstSettings = "41E4391576C7497EBF13EF0D30C3E21925C2C4E28CB3CD61F6CC82A4A27BCE09";
    private const string SecondSettings = "114171520663E17D2258DD05AC49650A89E254D5D111E64AEBE18CF1847EDF66";

    public static bool CanReloadFootBattle(string recordedSettings, string selectedSettings)
    {
        ArgumentNullException.ThrowIfNull(recordedSettings);
        ArgumentNullException.ThrowIfNull(selectedSettings);
        return recordedSettings == selectedSettings || recordedSettings == FirstSettings && selectedSettings == SecondSettings || recordedSettings == SecondSettings && selectedSettings == FirstSettings;
    }
}
