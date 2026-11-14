using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private int battleMagicCastCount = 1;
    private readonly List<ushort> battleMagicTargets = [];
    private readonly List<bool> battleMagicTargetGroups = [];

    private bool CanConfirmGuestMagicSelection(Ff7BattleCommandOption option)
    {
        return option.Command == Ff7BattleCommand.Magic && battleMagicCastCount > 1 && battleMagicTargets.Count > 0;
    }

    private void BeginGuestMagicSelection(Ff7BattleCommandOption option)
    {
        battleMagicTargets.Clear();
        battleMagicTargetGroups.Clear();
        battleMagicCastCount = option.Command == Ff7BattleCommand.Magic && Session?.Battle is { } battle && battleInputOwner is int owner ? battle.GuestMagicCastCounts(owner, option.Argument).LastOrDefault(1) : 1;
    }

    private bool CancelGuestMagicSelection()
    {
        if (battleMagicTargets.Count == 0)
        {
            return false;
        }
        battleMagicTargets.RemoveAt(battleMagicTargets.Count - 1);
        battleMagicTargetGroups.RemoveAt(battleMagicTargetGroups.Count - 1);
        return true;
    }

    private void SynchronizeGuestMagicSelection(Ff7BattleSession battle, int owner, Ff7BattleCommandOption option)
    {
        if (option.Command != Ff7BattleCommand.Magic)
        {
            return;
        }
        IReadOnlyList<int> counts = battle.GuestMagicCastCounts(owner, option.Argument);
        if (!counts.Contains(battleMagicCastCount))
        {
            battleMagicCastCount = counts.LastOrDefault(1);
            battleMagicTargets.Clear();
            battleMagicTargetGroups.Clear();
        }
    }

    private bool CollectGuestMagicTarget(Ff7BattleSession battle, int owner, Ff7BattleCommandOption option, ushort targets, bool group, out bool submitted)
    {
        submitted = false;
        if (option.Command != Ff7BattleCommand.Magic || battleMagicCastCount <= 1)
        {
            return false;
        }
        battleMagicTargets.Add(targets);
        battleMagicTargetGroups.Add(group);
        if (battleMagicTargets.Count == battleMagicCastCount)
        {
            submitted = battle.SubmitMagicSequence(owner, option.Argument, battleMagicTargets, battleMagicTargetGroups);
            if (!submitted)
            {
                battleMagicTargets.RemoveAt(battleMagicTargets.Count - 1);
                battleMagicTargetGroups.RemoveAt(battleMagicTargetGroups.Count - 1);
            }
        }
        return true;
    }
}
