namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState PrepareNativeActionGeometry(NativeBattleGraphicsState graphics, BattleCombatant actor, BattleAttackCommand command)
    {
        int actorIndex = graphics.ActorIds.IndexOf(actor.Id);
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native50D300 requires its retained camera alias memory.");
        NativeBattleActionGeometryGlobals globals = memory.ActionGeometryGlobals ?? NativeBattleActionGeometryGlobals.Unknown();
        NativeBattlePhysicalTargets targets = NativeBattlePhysicalTargets.FromNativeWord(command.NativeTargetMask);
        if (targets.IsEmpty && command.NativeTargetMask == 0 && command.NativeQueuedCommand is 1 or 12 or 28)
        {
            int index = graphics.ActorIds.IndexOf(command.TargetId);
            if (index >= 0)
            {
                targets = NativeBattlePhysicalTargets.Single(NativeBattleSlots.Decode(index));
            }
        }
        if (targets.IsEmpty)
        {
            throw new NotSupportedException("Native50D300 requires a resolved nonempty physical target mask.");
        }
        NativeBattleGraphicsState prepared = NativeBattleActionGeometryRules.PrepareGeometry50D300(graphics, actorIndex, command.NativeTargetMask, targets.Bits, globals, ReadNativeGraphicsBone);
        NativeBattleActionGeometry context = prepared.ActionContext!;
        NativeBattleCameraAliases aliases = memory.CameraAliases ?? NativeBattleCameraAliases.Unknown();
        NativeEffectMemory = memory with
        {
            CameraAliases = aliases.Observe50D300(checked((byte)actorIndex), targets.Bits),
            ActionGeometryGlobals = globals.Observe50D300(context)
        };
        return prepared;
    }

    private NativeBattleGraphicsState PrepareNativeActionGeometry(NativeBattleGraphicsState graphics, int actorIndex, NativeBattleGraphicsActorSet sourceTargets, NativeBattleGraphicsActorSet targetActors, ushort sourceMaskFlags = 0)
    {
        NativeBattleGraphicsRoster roster = graphics.PresentationRoster ?? throw new InvalidDataException("Hosted50D300 requires an explicit presentation roster.");
        RequireHostedActionModelBank(graphics, roster);
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native50D300 requires its retained camera alias memory.");
        NativeBattleActionGeometryGlobals globals = memory.ActionGeometryGlobals ?? NativeBattleActionGeometryGlobals.Unknown();
        NativeBattleGraphicsState prepared = NativeBattleActionGeometryRules.PrepareGeometry50D300(graphics, actorIndex, sourceTargets, targetActors, globals, ReadNativeGraphicsBone, sourceMaskFlags);
        NativeBattleCameraAliases aliases = memory.CameraAliases ?? NativeBattleCameraAliases.Unknown();
        NativeEffectMemory = memory with
        {
            CameraAliases = aliases.Observe50D300(checked((byte)actorIndex), targetActors, roster),
            ActionGeometryGlobals = globals.Observe50D300(prepared.ActionContext!)
        };
        return prepared;
    }

    private static void RequireHostedActionModelBank(NativeBattleGraphicsState graphics, NativeBattleGraphicsRoster roster)
    {
        if (graphics.Models.IsDefault || graphics.Models.Length != roster.ActorCount || graphics.ActorIds.IsDefault || graphics.ActorIds.Length != roster.ActorCount)
        {
            throw new InvalidDataException("Hosted action services require a model bank and actor identities for their presentation roster.");
        }
        for (int row = 0; row < roster.ActorCount; row++)
        {
            if (graphics.Models[row]?.Index != row || !string.Equals(graphics.ActorIds[row], roster.Rows[row]?.ActorId, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Hosted action model or actor identity differs from its presentation binding at row {row}.");
            }
        }
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
