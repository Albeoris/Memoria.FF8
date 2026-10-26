using Memoria.FF8.Assets.Localization;
using System.Text;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Reads current Unicode presentation values without changing the battle's original native content owners.</summary>
internal sealed class LocalizationBattlePresentation(ResourceService resources)
{
    internal string SystemText(string id, string original) => resources.Localize(Memoria.FF8.Resources.Localization.SystemTextCatalog.Key(id), original);

    internal string GlobalMessage(int ordinal, string original) => Kernel("MiscTexts", "Text", ordinal, original);

    public string Table(string file, int ordinal, string original) => resources.Localize(LocalizationSourceExporter.IndexedKey(resources.Source.Language, file, ordinal), original);

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

    public string CommandName(BattleAbility ability, BattleCombatant? actor, string original)
    {
        int command = ability switch { BattleAbility.Attack => 1, BattleAbility.Magic => 2, BattleAbility.GuardianForce => 3, BattleAbility.Item => 4, BattleAbility.Draw => 6, BattleAbility.LimitBreak => actor?.NativeLimit?.Command ?? -1, _ => -1 };
        return command >= 0 ? Kernel("BattleCommands", "Name", command, original) : original;
    }

    public string Message(BattleEvent message) => message.NativeTextIdentity is { } identity ? Table(identity.File, identity.Ordinal, message.Message) : message.Message;

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

    public IReadOnlyList<NativeScanWindowText> Scan(BattleSession session, NativeBattleScanTextFrame frame, IReadOnlyList<NativeScanWindowText> original)
    {
        NativeBattleOriginalEffectState? effect = session.NativeGraphics?.OriginalEffect;
        if (effect?.Binding.ScanTextResources is null)
        {
            return original;
        }
        NativeBattleModulePoolTask? viewer = effect.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => task.Callback == 0x84F860);
        if (viewer is null)
        {
            return original;
        }
        int actorIndex = viewer.ReadByte(0x2D);
        string? actorId = session.NativeGraphics!.ActorIds.ElementAtOrDefault(actorIndex);
        BattleCombatant? actor = session.Combatants.FirstOrDefault(candidate => candidate.Id == actorId);
        if (actor is null)
        {
            return original;
        }
        string descriptionKey = LocalizationSourceExporter.IndexedKey(resources.Source.Language, "scan_monsters", actor.ModelId);
        string name = CombatantName(actor);
        return original.Select(window =>
        {
            string? replacement = window.Index == 0 && resources.Localization.Values.TryGetValue(descriptionKey, out var value) ? value.Text : window.Index == 2 && name != actor.Name ? name : null;
            if (replacement is null)
            {
                return window;
            }
            NativeBattleScanByteOwner row = frame.Windows.Row(window.Index);
            return ScanWindow(window, row, replacement);
        }).ToArray();
    }

    internal static NativeScanWindowText ScanWindow(NativeScanWindowText window, NativeBattleScanByteOwner row, string replacement)
    {
        DialogueLine[] lines = DialogueTextFormatter.Parse(replacement).Pages.SelectMany(page => page.Lines).ToArray();
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
