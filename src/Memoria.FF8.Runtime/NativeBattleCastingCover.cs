namespace Memoria.FF8.Runtime;

/// <summary>50AFC0's cover recipient and50A9A0's phase4/6 position WORD stores.</summary>
public static class NativeBattleCastingCover
{
    public static BattleModelOwner Recipient(BattleActorModels models, BattleModelTargets targets)
    {
        return models.Party.Entries.Select(entry => entry.Owner).LastOrDefault(targets.Contains) ??
            throw new NotSupportedException("Native50AFC0 cover requires a selected party recipient.");
    }

    public static BattleActorModels Enter(BattleActorModels models, NativeBattleModelBoundary boundary, NativeBattleActionGraphicsState action)
    {
        BattleModelOwner source = Source(models, boundary, action);
        BattleModelOwner recipient = Recipient(models, action.TargetActors);
        NativeBattleModelTransform sourceTransform = Transform(models, source);
        NativeBattleGraphicsModel target = models.Get(recipient);
        NativeBattleModelTransform targetTransform = Transform(models, recipient);
        models = models.Set(recipient, target with { Transform = targetTransform with { Position28 = targetTransform.Position28 with { X = sourceTransform.Initial20.X, Z = sourceTransform.Initial20.Z } } });
        NativeBattleGraphicsModel cover = models.Get(source);
        NativeBattleModelTransform coverTransform = Transform(models, source);
        return models.Set(source, cover with { Transform = coverTransform with { Position28 = coverTransform.Position28 with { Z = unchecked((short)(sourceTransform.Initial20.Z + 800)) } } });
    }

    public static BattleActorModels Complete(BattleActorModels models, NativeBattleModelBoundary boundary, NativeBattleActionGraphicsState action)
    {
        BattleModelOwner source = Source(models, boundary, action);
        BattleModelOwner recipient = Recipient(models, action.TargetActors);
        NativeBattleGraphicsModel target = models.Get(recipient);
        NativeBattleModelTransform targetTransform = Transform(models, recipient);
        ushort flags = target.ResourceFlags44 ?? throw new NotSupportedException("Native50A9A0 cover completion requires the recipient's resource WORD44.");
        models = models.Set(recipient, target with { ResourceFlags44 = (ushort)(flags | 0x200), Transform = targetTransform with { Position28 = targetTransform.Position28 with { X = targetTransform.Initial20.X, Z = targetTransform.Initial20.Z } } });
        NativeBattleGraphicsModel cover = models.Get(source);
        NativeBattleModelTransform coverTransform = Transform(models, source);
        return models.Set(source, cover with { Transform = coverTransform with { Position28 = coverTransform.Position28 with { Z = coverTransform.Initial20.Z } } });
    }

    private static BattleModelOwner Source(BattleActorModels models, NativeBattleModelBoundary boundary, NativeBattleActionGraphicsState action)
    {
        return !action.CoverActors.IsEmpty && action.CoverSourceBinding is { } source
            ? source.Resolve(models, boundary)
            : throw new InvalidDataException("Native50A9A0 cover requires the actual50BF90 source and nonempty cover selection.");
    }

    private static NativeBattleModelTransform Transform(BattleActorModels models, BattleModelOwner index) => models.Get(index).Transform ?? throw new NotSupportedException("Native50A9A0 cover requires the original initial/current position WORDs.");
}
