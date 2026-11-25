using Memoria.FF8.Assets;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.GameUi;

internal static class Ff8TargetScan
{
    internal static NativeBattleScanRect NativeTextArea { get; } = new(0, 0, 320, 224);

    public static NativeBattlePreparedScanActor? VisibleActor(NativeBattlePreparedScan? scan)
    {
        if (scan?.Actor is not { TargetData: not null } actor)
        {
            return null;
        }
        for (int index = 0; index <= 2; index++)
        {
            if (NativeScanWindowTextProjection.IsVisible(scan.Text.Windows, index, NativeTextArea))
            {
                return actor;
            }
        }
        return null;
    }

    public static TargetScanData Create(NativeBattlePreparedScan scan, NativeBattleScanTargetData data, string name, string description, string language, Func<string, string, string> localize)
    {
        var encoding = NativeTextEncoding.Create(language);
        string? Misc(int id)
        {
            if (scan.Resources?.ReadEncodedMiscText(id) is not { } bytes)
            {
                return null;
            }
            int start = bytes is [5, _, ..] ? 2 : 0;
            string label = DialogueTextFormatter.Format(encoding.GetString(bytes.AsSpan()[start..])).Trim();
            return label.Length == 0 ? null : label;
        }

        var elements = data.ElementDefenses.Select((defense, index) => TargetScanScreen.Element(index, defense - 800, localize, Misc(0x65 + index))).ToArray();
        List<TargetScanResistance> statuses = [];
        if (!data.StatusResistances.IsEmpty)
        {
            for (int index = 0; index < TargetScanScreen.Statuses.Length; index++)
            {
                int status = (int)TargetScanScreen.Statuses[index];
                int resistanceIndex = status < 16 ? status : status - 8;
                statuses.Add(TargetScanScreen.Status(index, data.StatusResistances[resistanceIndex] - 100, localize));
            }
            foreach (NativeStatusId status in AdditionalStatuses)
            {
                statuses.Add(TargetScanScreen.Status(status, data.StatusResistances[(int)status - 8] - 100, localize));
            }
            statuses.Add(TargetScanScreen.Eject(data.StatusResistances[24] - 100, localize));
        }
        string title = SystemTextCatalog.Translate(localize, "battle.scan.title", "Scan");
        return new(title, name, description, data.Level, data.Hp, data.MaximumHp, data.Strength, data.Vitality, data.Speed, data.Magic, data.Spirit, data.Evasion, elements, statuses);
    }

    private static readonly NativeStatusId[] AdditionalStatuses = [NativeStatusId.Doom, NativeStatusId.Petrifying, NativeStatusId.Vit0];
}
