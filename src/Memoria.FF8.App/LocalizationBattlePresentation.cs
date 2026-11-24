using Memoria.FF8.Assets.Localization;
using System.Text;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources.Localization;

namespace Memoria.FF8;

/// <summary>Reads current Unicode presentation values without changing the battle's original native content owners.</summary>
internal sealed class LocalizationBattlePresentation(ResourceService resources)
{
    internal string SystemText(string id, string original) => Memoria.FF8.Resources.Localization.SystemTextCatalog.Translate(resources.Localize, id, original);

    internal string GlobalMessage(int ordinal, string original) => Kernel("MiscTexts", "Text", ordinal, original);

    public string Table(string file, int ordinal, string original) => resources.Localize(LocalizationSourceExporter.IndexedKey(resources.Source.Language, file, ordinal), original);

    internal BoundLocalizationText BoundTable(string file, int ordinal, string original) => resources.ResolveText(LocalizationSourceExporter.IndexedKey(resources.Source.Language, file, ordinal), original);

    internal BoundLocalizationText? BoundMessage(BattleEvent message) => message.NativeTextIdentity is { } identity ? BoundTable(identity.File, identity.Ordinal, message.Message) : null;

    public string CombatantName(BattleCombatant actor) => actor.IsPlayer || actor.HasNativeDisplayNameOverride ? actor.Name : Table($"c0m{actor.ModelId:000}_names", 0, actor.Name);

    public string ActionName(BattleActionDefinition action)
    {
        string original = action.DisplayName ?? action.Name;
        if (action.NativeAction is { } native)
        {
            return NativeActionName(native, original);
        }
        return action.Ability switch
        {
            BattleAbility.Magic => Kernel("MagicSpells", "Name", action.Id, original),
            BattleAbility.Item => Kernel("BattleItems", "Name", action.Id, original),
            BattleAbility.GuardianForce => Kernel("Guardians", "AttackName", action.Id, original),
            _ => original
        };
    }

    public string CommandName(int command, string original)
    {
        return command >= 0 ? Kernel("BattleCommands", "Name", command, original) : original;
    }

    public string Message(BattleEvent message)
    {
        if (message.LevelChange is { } change)
        {
            string label = resources.Source.Language == "ru" ? "Уровень" : "Level";
            return change.Format(SystemText("battle.message.level-label", label));
        }
        return message.NativeTextIdentity is { } identity ? Table(identity.File, identity.Ordinal, message.Message) : message.Message;
    }

    private string Kernel(string collection, string role, int ordinal, string original) => ordinal < 0 ? original : Table(LocalizationSourceExporter.KernelTable(collection, role), ordinal, original);

    private string NativeActionName(BattleNativeAction action, string original)
    {
        (string Collection, string Role)? identity = action.KernelSection switch
        {
            1 => ("MagicSpells", "Name"),
            2 => ("Guardians", "AttackName"),
            3 => ("EnemyAttacks", "Name"),
            5 => ("SquallLimits", "Name"),
            7 => ("BattleItems", "Name"),
            9 => ("IndependentGuardianAttacks", "AttackName"),
            10 => ("BattleCommands", "Name"),
            18 => ("NpcLimits", "Name"),
            19 => ("BlueMagics", "Name"),
            21 => ("IrvinLimits", "Name"),
            22 => ("ZellLimits", "Name"),
            25 => ("RinoaAngeloAttacks", "Name"),
            _ => null
        };
        return identity is { } source ? Kernel(source.Collection, source.Role, action.KernelSection == 10 ? action.SupplementalKernelIndex ?? action.KernelIndex : action.KernelIndex, original) : original;
    }

    public IReadOnlyList<NativeScanWindowText> Scan(NativeBattlePreparedScan scan, IReadOnlyList<NativeScanWindowText> original)
    {
        if (scan.Resources is null || scan.Actor is not { } actor)
        {
            return original;
        }
        BoundLocalizationText? description = actor.IsPlayer ? null : ScanDescription(scan, actor.TargetData?.DescriptionIndex ?? actor.ModelId + 16);
        BoundLocalizationText? name = ScanName(actor);
        Dictionary<int, string> names = ScanNames(scan);
        return original.Select(window =>
        {
            BoundLocalizationText? bound = window.Index == 0 ? description : window.Index == 2 ? name : null;
            if (bound is null)
            {
                return window;
            }
            if (bound.Text == bound.Binding.Source)
            {
                LocalizationTextBinding binding = bound.Binding with { Text = window.Document.Text, Arguments = [names] };
                return window with { Binding = binding, Lines = window.Lines.Select(line => line with { Binding = binding }).ToArray() };
            }
            NativeBattleScanByteOwner row = scan.Text.Windows.Row(window.Index);
            NativeScanWindowText replacement = ScanWindow(window, row, bound.Text, names);
            LocalizationTextBinding replacementBinding = bound.Binding with { Text = replacement.Document.Text, Arguments = [names] };
            return replacement with { Binding = replacementBinding, Lines = replacement.Lines.Select(line => line with { Binding = replacementBinding }).ToArray() };
        }).ToArray();
    }

    public (string Name, string Description) TargetScanText(NativeBattlePreparedScan scan, NativeBattlePreparedScanActor actor, int descriptionIndex)
    {
        BoundLocalizationText? boundName = ScanName(actor);
        BoundLocalizationText? description = ScanDescription(scan, descriptionIndex);
        Dictionary<int, string> names = ScanNames(scan);
        return (Format(boundName) ?? actor.Name, Format(description) ?? "");

        string? Format(BoundLocalizationText? text)
        {
            if (text is null)
            {
                return null;
            }
            string formatted = DialogueTextFormatter.Parse(text.Text, names, originalCompatible: true).Text.Trim();
            return LocalizationTextTrace.Attach(formatted, text.Binding with { Text = formatted, Arguments = [names] });
        }
    }

    private BoundLocalizationText? ScanName(NativeBattlePreparedScanActor actor)
    {
        return actor.IsPlayer || actor.HasNativeDisplayNameOverride ? null : BoundTable($"c0m{actor.ModelId:000}_names", 0, actor.Name);
    }

    private BoundLocalizationText? ScanDescription(NativeBattlePreparedScan scan, int descriptionIndex)
    {
        if (scan.Resources is not { } text || (uint)descriptionIndex >= text.DescriptionOffsets.Length)
        {
            return null;
        }
        int offset = text.DescriptionOffsets[descriptionIndex];
        if ((uint)offset >= text.DescriptionBytes.Length)
        {
            throw new InvalidDataException("Scan description leaves its retained source bank.");
        }
        int length = text.DescriptionBytes.AsSpan()[offset..].IndexOf((byte)0);
        if (length < 0)
        {
            throw new InvalidDataException("Scan description has no bounded native terminator.");
        }
        string source = Memoria.FF8.Assets.NativeTextEncoding.Create(resources.Source.Language).GetString(text.DescriptionBytes.AsSpan().Slice(offset, length));
        return BoundTable("scan_monsters", descriptionIndex, source);
    }

    private Dictionary<int, string> ScanNames(NativeBattlePreparedScan scan)
    {
        var encoding = Memoria.FF8.Assets.NativeTextEncoding.Create(resources.Source.Language);
        Dictionary<int, string> names = [];
        foreach (var pair in scan.Names)
        {
            int subject = pair.Key switch { >= 0x30 and <= 0x3A => GameNames.CharacterSubject(pair.Key - 0x30), 0x3B or 0x40 => 2, 0x50 => GameNames.GrieverSubject, _ => -1 };
            if (subject >= 0)
            {
                names[subject] = DialogueTextFormatter.Format(encoding.GetString(pair.Value.AsSpan()));
            }
        }
        return names;
    }

    internal static NativeScanWindowText ScanWindow(NativeScanWindowText window, NativeBattleScanByteOwner row, string replacement, IReadOnlyDictionary<int, string>? names = null)
    {
        DialogueLine[] lines = DialogueTextFormatter.Parse(replacement, names, originalCompatible: true).Pages.SelectMany(page => page.Lines).ToArray();
        bool complete = row.ReadByte(0x28) != 0;
        int completedLines = row.ReadByte(0x22);
        int y = row.ReadByte(0x32) - unchecked((short)row.ReadWord(0x12)) + 2;
        int x = row.ReadWord(0x30) + 2;
        List<NativeScanTextLine> visible = [];
        for (int index = 0; index < lines.Length && (complete || index <= completedLines); index++)
        {
            DialogueLine line = lines[index];
            if (!complete && index == completedLines)
            {
                int remaining = row.ReadByte(0x20);
                List<DialogueRun> runs = [];
                foreach (DialogueRun run in line.Runs)
                {
                    string prefix = string.Concat(run.Text.EnumerateRunes().Take(remaining).Select(rune => rune.ToString()));
                    remaining -= prefix.EnumerateRunes().Count();
                    runs.Add(run with { Text = prefix });
                }
                line = line with { Runs = runs };
            }
            visible.Add(new(line with { SourceLineIndex = index }, x, y + 16 * index) { FitWidth = window.Index == 2 });
        }
        return window with { Lines = visible };
    }
}
