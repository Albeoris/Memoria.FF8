using System.Buffers.Binary;
using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    internal static bool TryApplyAutoPlayFieldRecovery(Ff7Session session)
    {
        if (!session.CanUsePlayerMenu)
        {
            return false;
        }
        Ff7CharacterState[] targets = session.Progress.Party.Where(id => id is >= 0 and <= 8 && session.GuestFieldVitals?.StoryCharacters.Contains(id) != true).Select(id => session.Progress.Characters.Single(character => character.Id == id)).ToArray();
        AutoPlayBattleRecoveryTarget[] vitals = targets.Select(character => new AutoPlayBattleRecoveryTarget((uint)character.HitPoints, BinaryPrimitives.ReadUInt16LittleEndian(character.NativeRecord.AsSpan(56)), character.HitPoints == 0)).ToArray();
        if (vitals.All(target => !target.KnockedOut && (target.MaximumHitPoints == 0 || (ulong)target.HitPoints * 2 > target.MaximumHitPoints)))
        {
            return false;
        }
        List<(int? Item, int Caster, int Spell, int Target)> commands = [];
        List<AutoPlayBattleRecoveryOption> options = [];
        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
        {
            Ff7CharacterState target = targets[targetIndex];
            foreach (Ff7CharacterState caster in targets.Where(character => character.HitPoints > 0))
            {
                foreach (Ff7BattleSpell spell in session.ReadFieldMagic(caster.Id))
                {
                    if (session.CanCastFieldMagic(caster.Id, spell, target.Id))
                    {
                        options.Add(new(commands.Count, targetIndex, target.HitPoints == 0));
                        commands.Add((null, caster.Id, spell.AttackId, target.Id));
                    }
                }
            }
            foreach (Ff7InventorySlot item in session.Progress.Inventory)
            {
                if (item.ItemId is 0 or 1 or 2 or 5 or 7 && session.CanUseFieldItem(item.ItemId, target.Id))
                {
                    options.Add(new(commands.Count, targetIndex, target.HitPoints == 0));
                    commands.Add((item.ItemId, -1, -1, target.Id));
                }
            }
        }
        if (AutoPlayBattlePolicy.ChooseRecovery(vitals, options) is not { } selected)
        {
            return false;
        }
        var command = commands[selected.CommandIndex];
        bool applied = command.Item is int itemId ? session.TryUseFieldItem(itemId, command.Target) : session.TryCastFieldMagic(command.Caster, command.Spell, command.Target);
        if (!applied)
        {
            throw new InvalidOperationException("The native field menu rejected the selected automatic recovery command.");
        }
        return true;
    }
}
