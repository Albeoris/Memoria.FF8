using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private int battleMagicCastCount = 1;
    private readonly List<ushort> battleMagicTargets = [];

    private void BeginGuestMagicSelection(Ff7BattleCommandOption option)
    {
        battleMagicTargets.Clear();
        battleMagicCastCount = option.Command == Ff7BattleCommand.Magic && Session?.Battle is { } battle && battleInputOwner is int owner ? battle.GuestMagicCastCounts(owner, option.Argument).LastOrDefault(1) : 1;
    }

    private bool CancelGuestMagicSelection()
    {
        if (battleMagicTargets.Count == 0)
        {
            return false;
        }
        battleMagicTargets.RemoveAt(battleMagicTargets.Count - 1);
        return true;
    }

    private bool ChangeGuestMagicCastCount(Ff7BattleSession battle, int owner, Ff7BattleCommandOption option)
    {
        if (option.Command != Ff7BattleCommand.Magic)
        {
            return false;
        }
        IReadOnlyList<int> counts = battle.GuestMagicCastCounts(owner, option.Argument);
        if (!counts.Contains(battleMagicCastCount))
        {
            battleMagicCastCount = counts.LastOrDefault(1);
            battleMagicTargets.Clear();
        }
        if (counts.Count <= 1)
        {
            return false;
        }
        if (semanticInput.MenuPage != 0)
        {
            int index = counts.ToList().IndexOf(battleMagicCastCount);
            battleMagicCastCount = counts[(index + Math.Sign(semanticInput.MenuPage) + counts.Count) % counts.Count];
            battleMagicTargets.Clear();
        }
        return true;
    }

    private bool CollectGuestMagicTarget(Ff7BattleSession battle, int owner, Ff7BattleCommandOption option, ushort targets, bool group, out bool submitted)
    {
        submitted = false;
        if (option.Command != Ff7BattleCommand.Magic || battleMagicCastCount <= 1)
        {
            return false;
        }
        battleMagicTargets.Add(targets);
        if (battleMagicTargets.Count == battleMagicCastCount)
        {
            submitted = battle.SubmitGuestMagicSequence(owner, option.Argument, battleMagicTargets, group);
            if (!submitted)
            {
                battleMagicTargets.RemoveAt(battleMagicTargets.Count - 1);
            }
        }
        return true;
    }
}
