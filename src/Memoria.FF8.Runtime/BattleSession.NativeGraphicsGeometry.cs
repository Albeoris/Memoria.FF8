namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState PrepareNativeActionGeometry(NativeBattleGraphicsState graphics, BattleCombatant actor, BattleAttackCommand command)
    {
        int actorIndex = graphics.ActorIds.IndexOf(actor.Id);
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native50D300 requires its retained camera alias memory.");
        NativeBattleActionGeometryGlobals globals = memory.ActionGeometryGlobals ?? NativeBattleActionGeometryGlobals.Unknown();
        ushort? physical = null;
        if ((command.NativeTargetMask & 127) != 0)
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
            throw new NotSupportedException("Native50D300 requires a resolved nonempty physical target mask.");
        }
        NativeBattleGraphicsState prepared = NativeBattleActionGeometryRules.PrepareGeometry50D300(graphics, actorIndex, command.NativeTargetMask, mask, globals, ReadNativeGraphicsBone);
        NativeBattleActionGeometry context = prepared.ActionContext!;
        NativeBattleCameraAliases aliases = memory.CameraAliases ?? NativeBattleCameraAliases.Unknown();
        NativeEffectMemory = memory with
        {
            CameraAliases = aliases.Observe50D300(checked((byte)actorIndex), mask),
            ActionGeometryGlobals = globals.Observe50D300(context)
        };
        return prepared;
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
            16 => unchecked((short)(context.UnadjustedDistance ?? throw new NotSupportedException("Native action distance requires its resolved physical target packet."))),
            17 => unchecked((short)(context.ContactDistance ?? throw new NotSupportedException("Native contact distance requires its retained50D300 global; migrate the needed older snapshot explicitly."))),
            24 => context.TargetIndex ?? throw new NotSupportedException("Native current target requires its resolved physical graphics identity."),
            26 => (short)((context.TargetYaw ?? throw new NotSupportedException("Native current target requires its50D300 rotation.")) & 4095),
            _ => throw new NotSupportedException($"Native model variable{variable:X2} requires its original graphics service owner.")
        };
    }
}
