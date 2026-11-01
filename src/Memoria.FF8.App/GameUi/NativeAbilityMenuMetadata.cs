using Memoria.FF8.Ui;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

public static class NativeAbilityMenuMetadata
{
    internal static bool SupportsSummon(int guardianForce, bool enabled) => enabled && BattleGuardianFormProfiles.Find(guardianForce) is not null;

    internal static string CommandName(int ability, string name, bool supportsSummon, Func<string, string, string>? localize = null)
    {
        if (ability != 21 || !supportsSummon)
        {
            return name;
        }
        string summon = localize?.Invoke("battle.ui.choicelabels.summon", "Summon") ?? "Summon";
        return name + " / " + summon;
    }

    public static UiImageSource? Icon(int ability)
    {
        // Native4A62E0 uses the same seven category boundaries emitted by Native4ACB70.
        return ability switch
        {
            >= 0 and < 20 => new UiImageSource(UiImageKind.Icon, 216),
            >= 20 and < 39 => new UiImageSource(UiImageKind.Icon, 217),
            >= 39 and < 58 => new UiImageSource(UiImageKind.Icon, 218),
            >= 58 and < 78 => new UiImageSource(UiImageKind.Icon, 219),
            >= 78 and < 83 => new UiImageSource(UiImageKind.Icon, 220),
            >= 83 and < 92 => new UiImageSource(UiImageKind.Icon, 221),
            >= 92 and < 116 => new UiImageSource(UiImageKind.Icon, 222),
            _ => null
        };
    }
}
