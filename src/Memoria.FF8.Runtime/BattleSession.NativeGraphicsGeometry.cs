namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState PrepareNativeActionGeometry(NativeBattleGraphicsState graphics, BattleCombatant actor, BattleAttackCommand command)
    {
        BattleTargetRequest request = command.TargetRequest;
        BattleCombatant[] recipients = request.Selection is NativeBattleTargetRequestAdapter.DeferredOperands original
            ? NativeBattleTargetRequestAdapter.ReadAuthoredTargets(original, ReadOriginalTargetOccupant).Select(reference => FindTargetActor(reference)!).ToArray()
            : ResolveBattleTargets(request, sampleRandom: false);
        if (recipients.Length == 0)
        {
            throw new NotSupportedException("Native50D300 requires a resolved nonempty target selection.");
        }
        BattleModelOwner source = CreatePresentationModelOwner(actor);
        BattleModelTargets targets = BattleModelTargets.From(recipients.Select(CreatePresentationModelOwner));
        return PrepareNativeActionGeometry(graphics, source, targets, targets, NativeBattleTargetRequestAdapter.HasNonActorSelectors(request));
    }

    private NativeBattleGraphicsState PrepareNativeActionGeometry(NativeBattleGraphicsState graphics, BattleModelOwner source, BattleModelTargets sourceTargets, BattleModelTargets targetActors, bool sourceHasNonActorSelectors = false)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native50D300 requires its retained camera alias memory.");
        NativeBattleActionGeometryGlobals globals = memory.ActionGeometryGlobals ?? NativeBattleActionGeometryGlobals.Unknown();
        NativeBattleGraphicsState prepared = NativeBattleActionGeometryRules.PrepareGeometry50D300(graphics, source, sourceTargets, targetActors, globals, ReadNativeGraphicsBone, sourceHasNonActorSelectors);
        NativeBattleCameraAliases aliases = memory.CameraAliases ?? NativeBattleCameraAliases.Unknown();
        NativeEffectMemory = memory with
        {
            CameraAliases = aliases.Observe50D300(source, targetActors, graphics.ActorModels, graphics.NativeBindings.CaptureAliases(graphics.ActorModels)),
            ActionGeometryGlobals = globals.Observe50D300(prepared.ActionContext!)
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
            26 => (short)((context.TargetYaw ?? throw new NotSupportedException("Native current target requires its50D300 rotation.")) & 4095),
            _ => throw new NotSupportedException($"Native model variable{variable:X2} requires its original graphics service owner.")
        };
    }

}
