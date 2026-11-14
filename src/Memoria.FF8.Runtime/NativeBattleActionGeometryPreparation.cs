namespace Memoria.FF8.Runtime;

public static partial class NativeBattleActionGeometryRules
{
    public static NativeBattleGraphicsState PrepareGeometry50D300(NativeBattleGraphicsState graphics, BattleModelOwner source, BattleModelTargets sourceTargets, BattleModelTargets targetActors, NativeBattleActionGeometryGlobals globals, Func<NativeBattleGraphicsState, BattleModelOwner, byte, short, NativeBattleVector> readBone, bool sourceHasNonActorSelectors = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sourceTargets);
        ArgumentNullException.ThrowIfNull(targetActors);
        BattleActorModels models = graphics.ActorModels;
        if (!models.Contains(source) || sourceTargets.Enumerate().Any(owner => !models.Contains(owner)) ||
            targetActors.IsEmpty || targetActors.Enumerate().Any(owner => !models.Contains(owner)))
        {
            throw new InvalidDataException("Native50D300 requires bound source and target owners and a nonempty target set.");
        }

        BattleModelTargets orderedTargets = BattleModelTargets.From(models.Owners.Where(targetActors.Contains));
        BattleModelOwner recipientOwner = orderedTargets.Enumerate().Last();
        NativeBattleActionGeometry context = new(source, sourceTargets, sourceHasNonActorSelectors)
        {
            TargetActors = orderedTargets,
            Target = recipientOwner,
            ContactDistance = globals.ContactDistance,
            TargetYaw = globals.TargetYaw
        };
        bool group = orderedTargets.Count >= 2;
        foreach (BattleModelOwner owner in orderedTargets.Enumerate())
        {
            NativeBattleGraphicsModel selected = graphics.ActorModels.Get(owner);
            ushort flags = selected.Flags2 ?? throw new NotSupportedException("Native50D300 target grouping requires its model flags WORD2.");
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(owner, selected with { Flags2 = group ? (ushort)(flags | 0x10) : (ushort)(flags & ~0x10) }) };
        }
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(source);
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native50D300 action geometry requires its actor position/scale WORDs.");
        short neutralYaw = source.Side == BattleSide.Allies ? (short)0 : (short)2048;
        if (group)
        {
            return PrepareMatrices50D300(graphics with { ActorModels = graphics.ActorModels.Set(source, model with { Rotation12 = 0, Rotation14 = neutralYaw, Rotation16 = 0 }), ActionContext = context }, readBone);
        }
        NativeBattleGraphicsModel recipient = graphics.ActorModels.Get(recipientOwner);
        NativeBattleModelTransform recipientTransform = recipient.Transform ?? throw new NotSupportedException("Native50D300 action geometry requires its recipient position/scale WORDs.");
        NativeBattleVector first = new(transform.Position28.X, transform.Angle36, transform.Position28.Z);
        NativeBattleVector second = recipient.ModelId4 is 96 or 97 ? readBone(graphics, recipientOwner, 241, 2048) : new(recipientTransform.Position28.X, recipientTransform.Angle36, recipientTransform.Position28.Z);
        second = second with { Y = recipientTransform.Angle36 };
        short scale = DistanceScale(first, second);
        bool sameOwner = source == recipientOwner;
        short? yaw = sameOwner ? context.TargetYaw : unchecked((short)NativeWorldScriptedPathMath.NativeAngle(second.X - first.X, second.Z - first.Z));
        short actorYaw = sameOwner ? neutralYaw : unchecked((short)NativeWorldScriptedPathMath.NativeAngle(first.X - second.X, first.Z - second.Z));
        bool self = context.IsSelfTarget;
        context = context with
        {
            DistanceScale = scale,
            UnadjustedDistance = self ? 1000 : 5000 * scale >> 12,
            ContactDistance = self ? 1000 : ContactDistance(scale, transform.Scale38, recipientTransform.Scale38),
            TargetYaw = yaw
        };
        model = model with
        {
            Rotation12 = sameOwner ? (short)0 : model.Rotation12,
            Rotation14 = actorYaw,
            Rotation16 = sameOwner ? (short)0 : model.Rotation16
        };
        return PrepareMatrices50D300(graphics with { ActorModels = graphics.ActorModels.Set(source, model), ActionContext = context }, readBone);
    }
}
