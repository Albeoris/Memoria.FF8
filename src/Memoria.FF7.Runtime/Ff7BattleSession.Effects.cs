using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    private Ff7BattleActionState StartEffect(Ff7BattleActionState action)
    {
        Ff7BattleQueuedCommand command = action.Command;
        if (command.CommandId == 0x20)
        {
            byte effect = Data.Attacks.Single(attack => attack.Slot == command.Argument && attack.Id == command.AttackId).Data.AttackEffect;
            return effect switch
            {
                0x02 when !action.SilkStarted => StartPrimarySilk(action),
                0x13 when action.Rockets is null => StartPrimaryRocket(action),
                0x15 when action.Smog is null => StartPrimarySmog(action),
                0x40 when action.SmokeShot is null => StartPrimarySmokeShot(action),
                0x4B when action.SearchScope is null => StartSearchScope(action),
                0x5A when action.Destroyed is null => StartPrimaryDestroyed(action),
                _ => action
            };
        }
        ushort? spell = command.CommandId is (byte)Ff7BattleCommand.Magic or (byte)Ff7BattleCommand.Limit ? command.Argument : null;
        return spell switch
        {
            0x00 when action.Cure is null => StartPrimaryCure(action, Ff7BattleCureKind.Cure),
            0x1B when action.Fire is null => StartPrimaryFire(action),
            0x1E when action.Ice is null => StartPrimaryIce(action),
            0x21 when action.Bolt is null => StartPrimaryBolt(action),
            0x80 when action.Braver is null => StartPrimaryBraver(action),
            0x87 when action.BigShot is null => StartPrimaryBigShot(action),
            _ => action
        };
    }

    private Ff7BattleActionState StartItemEffect(Ff7BattleActionState action)
    {
        if (action.Command.Argument == 20 && action.Grenade is null)
        {
            return action with { Grenade = Ff7BattleGrenadeEffect.Start(action.Command.TargetMask) };
        }
        Ff7BattleCureKind? kind = action.Command.Argument switch
        {
            0 => Ff7BattleCureKind.Cure,
            3 => Ff7BattleCureKind.Ether,
            7 => Ff7BattleCureKind.PhoenixDown,
            8 => Ff7BattleCureKind.Antidote,
            _ => null
        };
        return kind is Ff7BattleCureKind item && action.Cure is null ? StartPrimaryCure(action, item) : action;
    }

    private Ff7BattleActionState AdvanceEffects(Ff7BattleActionState action)
    {
        Ff7BattleActionState beforeEffects = action;
        ushort impacts = 0;
        if (action.Grenade is { Completed: false } grenade)
        {
            Ff7BattleGrenadeUpdate update = Ff7BattleGrenadeEffect.Advance(grenade, ImpactAnchor, CollisionRadius);
            action = action with { Grenade = update.State };
            impacts |= update.ImpactTargets;
        }
        EffectCallbackSounds(beforeEffects, action);
        return impacts == 0 ? action : PublishTargetReactions(action, Enumerable.Range(0, Ff7NativeAtb.SlotCount).Where(slot => (impacts & (1 << slot)) != 0));
    }

    private Vector3 ImpactAnchor(int slot) => JointPoint(slot, 0);

    private Ff7BattleActionState StartSearchScope(Ff7BattleActionState action)
    {
        Ff7BattleQueuedCommand command = action.Command;
        System.Numerics.Vector3 source = JointPoint(command.ActorSlot, 9);
        System.Numerics.Vector3 center = MeanImpactAnchor(command.TargetMask);
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.ScheduleSearchScope(EnsurePrimaryEffects(), command.TargetMask, source, center);
        state = state with { PrimaryEffects = primary, StageFade = primary.StageFade };
        return action with { SearchScope = Ff7BattleSearchScopeEffect.Start(command.TargetMask, source, center, state.SearchScopeScaleCounter) with { Primary = null }, PrimaryCallbacksRunning = true };
    }

    private Vector3 MeanImpactAnchor(ushort mask)
    {
        int x = 0;
        int y = 0;
        int z = 0;
        int count = 0;
        foreach (Ff7BattleActorState actor in state.Actors.Where(actor => (mask & (1 << actor.Actor.Slot)) != 0))
        {
            Vector3 point = ImpactAnchor(actor.Actor.Slot);
            x += (short)point.X;
            y += (short)point.Y;
            z += (short)point.Z;
            count++;
        }
        if (count == 0)
        {
            throw new InvalidDataException("FFVII effect anchor requires a selected physical target.");
        }
        return new((short)(x / count), (short)(y / count), (short)(z / count));
    }
    private Vector3 JointPoint(int slot, int selector) => BonePosition(slot, animations!.JointSelector(ActorModel(slot), selector));
    private int CollisionRadius(int slot) => animations!.CollisionRadius(ActorModel(slot));

    private Vector3 LimitWeaponPoint(Ff7BattleActionState action, Vector3 point)
    {
        int slot = action.Command.ActorSlot;
        (int clip, int frame) = VisualPose(slot);
        Ff7BattlePlacement placement = VisualPlacement(slot);
        if (action.Motion is { } motion)
        {
            placement = placement with { Position = motion.Position.ToVector() };
        }
        return animations!.LimitWeaponPosition(ActorModel(slot), clip, frame, placement, action.Command.Argument, point);
    }


}
