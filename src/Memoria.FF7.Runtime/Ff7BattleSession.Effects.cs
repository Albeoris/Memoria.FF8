using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    private static Ff7BattleActionState StartEffect(Ff7BattleActionState action)
    {
        Ff7BattleQueuedCommand command = action.Command;
        ushort? spell = command.AttackId ?? (command.CommandId == (byte)Ff7BattleCommand.Magic ? command.Argument : null);
        return spell switch
        {
            0x00 when action.Cure is null => action with { Cure = Ff7BattleCureEffect.Start(command.TargetMask) },
            0x1B when action.Fire is null => action with { Fire = Ff7BattleFireEffect.Start(command.TargetMask) },
            0x1E when action.Ice is null => action with { Ice = Ff7BattleIceEffect.Start(command.TargetMask) },
            0x21 when action.Bolt is null => action with { Bolt = Ff7BattleBoltEffect.Start(command.TargetMask) },
            _ => action
        };
    }

    private Ff7BattleActionState AdvanceEffects(Ff7BattleActionState action)
    {
        ushort impacts = 0;
        if (action.Fire is { Completed: false } fire)
        {
            Ff7BattleFireUpdate update = Ff7BattleFireEffect.Advance(fire, slot => animations!.BoneCount(ActorModel(slot)), BonePosition, CollisionRadius, random.NextCrt);
            action = action with { Fire = update.State };
            impacts |= update.ImpactTargets;
        }
        if (action.Ice is { Completed: false } ice)
        {
            Ff7BattleIceUpdate update = Ff7BattleIceEffect.Advance(ice, ImpactAnchor, CollisionRadius);
            action = action with { Ice = update.State };
            impacts |= update.ImpactTargets;
        }
        if (action.Bolt is { Completed: false } bolt)
        {
            Ff7BattleBoltUpdate update = Ff7BattleBoltEffect.Advance(bolt, ImpactAnchor, CollisionRadius, random.NextCrt);
            action = action with { Bolt = update.State };
            impacts |= update.ImpactTargets;
        }
        if (action.Cure is { Completed: false } cure)
        {
            Ff7BattleCureUpdate update = Ff7BattleCureEffect.Advance(cure, slot => animations!.BoneCount(ActorModel(slot)), BonePosition, random.NextCrt);
            action = action with { Cure = update.State };
            impacts |= update.ImpactTargets;
        }
        return impacts == 0 ? action : action with { Reactions = Ff7BattleTargetCallbacks.Schedule(impacts, 0) };
    }

    private Vector3 ImpactAnchor(int slot) => BonePosition(slot, animations!.JointSelector(ActorModel(slot), 0));
    private int CollisionRadius(int slot) => animations!.CollisionRadius(ActorModel(slot));
}
