namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleGraphicsModel
{
    public BattleModelOwner? LinkedModel { get; init; }

    public BattleModelOwner? ReadLinkedModel()
    {
        if (LinkedModel is { } owner)
        {
            return HasLinkedModelRing140 == true ? owner : throw new InvalidDataException("Native model DWORD140 contradicts its linked model owner.");
        }
        return HasLinkedModelRing140 == false ? null : throw new NotSupportedException("Native model DWORD140 pointer has not been established.");
    }

    public NativeBattleGraphicsModel WriteLinkedModel(BattleModelOwner? owner)
    {
        return this with { LinkedModel = owner, HasLinkedModelRing140 = owner is not null };
    }
}

public static class NativeBattleModelRings
{
    public static uint PreservedStatusMask50A000(ushort sourceFlags2) => (sourceFlags2 & 0x40) != 0 ? 0x831E0u : 0x21E35F0u;

    public static NativeBattleGraphicsModel BindWorldMatrix502AB0(BattleActorModels models, BattleModelOwner owner, NativeBattleGraphicsModel model)
    {
        if (model.ReadLinkedModel() is null)
        {
            return model;
        }
        BattleModelOwner ringOwner = NativeBattleScanModelSelection.Minimum501FF0(models.Set(owner, model), owner);
        if (ringOwner == owner)
        {
            return model;
        }
        NativeBattleActionMatrix matrix = models.Get(ringOwner).GeometryWorldMatrix ?? throw new NotSupportedException("Native502AB0 requires its ring owner's retained model matrix.");
        return model with { GeometryWorldMatrix = matrix };
    }

    public static BattleActorModels Bind90(BattleActorModels models, BattleModelOwner source, byte selector, NativeBattleModelBoundary boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        NativeBattleGraphicsModel sourceModel = models.Get(source);
        if (selector >= 16)
        {
            BattleModelEntry<BattleEnemyModelOwner>? target = models.Enemies.Entries.FirstOrDefault(entry => entry.State.ModelId4 == selector);
            return target is null ? models : models.Set(source, sourceModel.WriteLinkedModel(target.Owner));
        }
        if (selector >= 3)
        {
            return models.Set(source, sourceModel.WriteLinkedModel(boundary.DecodeLiteral(selector, models)));
        }
        List<BattleModelOwner> ring = [];
        BattleModelOwner current = source;
        do
        {
            if (ring.Contains(current))
            {
                throw new InvalidDataException("Native90 unlink chain does not return to its source.");
            }
            ring.Add(current);
            current = models.Get(current).ReadLinkedModel() ?? throw new InvalidDataException("Native90 unlink dereferences a NULL link before closing its ring.");
        }
        while (current != source);
        foreach (BattleModelOwner owner in ring)
        {
            models = models.Set(owner, models.Get(owner).WriteLinkedModel(null));
        }
        return models;
    }

    public static IEnumerable<BattleModelOwner> Walk(BattleActorModels models, BattleModelOwner source)
    {
        ArgumentNullException.ThrowIfNull(source);
        HashSet<BattleModelOwner> visited = [];
        BattleModelOwner? current = source;
        while (current is not null)
        {
            if (!visited.Add(current))
            {
                if (current == source)
                {
                    yield break;
                }
                throw new InvalidDataException("Native model ring enters a cycle that does not return to its source.");
            }
            NativeBattleGraphicsModel model = models.Get(current);
            yield return current;
            current = model.ReadLinkedModel();
        }
    }
}
