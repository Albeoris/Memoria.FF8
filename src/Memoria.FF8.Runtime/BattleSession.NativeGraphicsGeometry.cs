using System.Numerics;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState PrepareNativeActionGeometry(NativeBattleGraphicsState graphics, BattleCombatant actor, BattleAttackCommand command)
    {
        int actorIndex = graphics.ActorIds.IndexOf(actor.Id);
        NativeBattleActionGeometry context = new(checked((byte)actorIndex), command.NativeTargetMask) { CustomMatrix = graphics.ActionContext?.CustomMatrix };
        ushort? physical = null;
        if (command.NativeTargetMask != 0 && (command.NativeTargetMask & 0xA000) == 0)
        {
            physical = (ushort)(command.NativeTargetMask & 127);
        }
        else if (command.NativeTargetMask == 0 && command.NativeQueuedCommand is 1 or 12 or 28)
        {
            int index = graphics.ActorIds.IndexOf(command.TargetId);
            if (index >= 0)
            {
                physical = checked((ushort)(1 << index));
            }
        }
        if (physical is not ushort mask || mask == 0)
        {
            return PrepareNativeActionMatrices(graphics with { ActionContext = context });
        }
        int recipientIndex = BitOperations.Log2(mask);
        context = context with { PhysicalTargetMask = mask, TargetIndex = checked((byte)recipientIndex), ContactDistance = graphics.ActionContext?.ContactDistance, TargetYaw = graphics.ActionContext?.TargetYaw };
        bool group = BitOperations.PopCount(mask) >= 2;
        for (int index = 0; index < 7; index++)
        {
            if ((mask & (1 << index)) != 0)
            {
                NativeBattleGraphicsModel selected = graphics.Models[index];
                ushort flags = selected.Flags2 ?? throw new NotSupportedException("Native50D300 target grouping requires its model flags WORD2.");
                graphics = graphics with { Models = graphics.Models.SetItem(index, selected with { Flags2 = group ? (ushort)(flags | 0x10) : (ushort)(flags & ~0x10) }) };
            }
        }
        NativeBattleGraphicsModel model = graphics.Models[actorIndex];
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native50D300 action geometry requires its actor position/scale WORDs.");
        if (group)
        {
            //50D300 preserves dword1D99A98 for a multiple-recipient packet; its group centroid matrices remain separate owners.
            return PrepareNativeActionMatrices(graphics with { Models = graphics.Models.SetItem(actorIndex, model with { Rotation12 = 0, Rotation14 = actorIndex >= 3 ? (short)2048 : (short)0, Rotation16 = 0 }), ActionContext = context });
        }
        NativeBattleGraphicsModel recipient = graphics.Models[recipientIndex];
        NativeBattleModelTransform recipientTransform = recipient.Transform ?? throw new NotSupportedException("Native50D300 action geometry requires its recipient position/scale WORDs.");
        NativeBattleVector first = new(transform.Position28.X, transform.Angle36, transform.Position28.Z);
        NativeBattleVector second = recipient.ModelId4 is 96 or 97 ? ReadNativeGraphicsBone(recipientIndex, 241, 2048) : new(recipientTransform.Position28.X, recipientTransform.Angle36, recipientTransform.Position28.Z);
        second = second with { Y = recipientTransform.Angle36 };
        short scale = NativeBattleActionGeometryRules.DistanceScale(first, second);
        short? yaw = actorIndex == recipientIndex ? context.TargetYaw : unchecked((short)NativeWorldScriptedPathMath.NativeAngle(second.X - first.X, second.Z - first.Z));
        short actorYaw = actorIndex == recipientIndex ? actorIndex >= 3 ? (short)2048 : (short)0 : unchecked((short)NativeWorldScriptedPathMath.NativeAngle(first.X - second.X, first.Z - second.Z));
        context = context with { DistanceScale = scale, UnadjustedDistance = actorIndex == recipientIndex ? 1000 : 5000 * scale >> 12, ContactDistance = actorIndex == recipientIndex ? 1000 : NativeBattleActionGeometryRules.ContactDistance(scale, transform.Scale38, recipientTransform.Scale38), TargetYaw = yaw };
        return PrepareNativeActionMatrices(graphics with { Models = graphics.Models.SetItem(actorIndex, model with { Rotation14 = actorYaw }), ActionContext = context });
    }

    private int ReadNativeGraphicsActionVariable(NativeBattleGraphicsState graphics, byte variable)
    {
        if (variable == 25)
        {
            return NativeBattleActionGraphics.ReadCameraFlag50A6E0(nativeActionPackets);
        }
        NativeBattleActionGeometry context = graphics.ActionContext ?? throw new NotSupportedException("Native model expression requires its50D300 action geometry owner.");
        return variable switch
        {
            16 => context.UnadjustedDistance ?? throw new NotSupportedException("Native action distance requires its resolved physical target packet."),
            17 => context.ContactDistance ?? throw new NotSupportedException("Native contact distance requires its resolved physical target packet."),
            24 => context.TargetIndex ?? throw new NotSupportedException("Native current target requires its resolved physical graphics identity."),
            26 => (short)((context.TargetYaw ?? throw new NotSupportedException("Native current target requires its50D300 rotation.")) & 4095),
            _ => throw new NotSupportedException($"Native model variable{variable:X2} requires its original graphics service owner.")
        };
    }
}
