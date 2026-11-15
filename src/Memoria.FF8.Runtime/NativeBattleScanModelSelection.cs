namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanModelSelectionResult(BattleModelOwner Primary, byte Category, BattleModelTargets Targets);

public static class NativeBattleScanModelSelection
{
    public static NativeBattleScanModelSelectionResult Select84D320(BattleActorModels models, BattleModelOwner source, Func<BattleModelOwner, byte> readResourceSequence)
    {
        BattleModelOwner primary = Minimum501FF0(models, source);
        byte category = Category84D410(models, source, readResourceSequence);
        IEnumerable<BattleModelOwner> owners = category == 0x54
            ? models.Entries.Where(entry => ReadCategory(entry.State) is 0x54 or 0x60 or 0x61).Select(entry => entry.Owner)
            : NativeBattleModelRings.Walk(models, source).Where(owner => (models.Get(owner).Flags0 & 2) != 0);
        BattleModelTargets selected = BattleModelTargets.From(owners);
        return new(primary, category, BattleModelTargets.From(models.Owners.Where(selected.Contains)));
    }

    public static BattleModelOwner Minimum501FF0(BattleActorModels models, BattleModelOwner source)
    {
        HashSet<BattleModelOwner> ring = [source];
        BattleModelOwner? next = models.Get(source).ReadLinkedModel();
        while (next != source)
        {
            if (next is null)
            {
                return source;
            }
            if (!ring.Add(next))
            {
                throw new InvalidDataException("Native minimum model ring does not return to its actual source.");
            }
            next = models.Get(next).ReadLinkedModel();
        }
        return models.Owners.First(ring.Contains);
    }

    public static byte Category84D410(BattleActorModels models, BattleModelOwner source, Func<BattleModelOwner, byte> readResourceSequence)
    {
        NativeBattleGraphicsModel model = models.Get(source);
        byte category = ReadCategory(model);
        return category switch
        {
            0x24 or 0x25 => (model.SequenceState ?? throw new NotSupportedException("Native Scan category requires its current mesh-mask owner.")).MeshMask124 == uint.MaxValue ? (byte)0xA0 : category,
            0x2B => readResourceSequence(source) != 1 ? (byte)0xA1 : category,
            0x77 => readResourceSequence(source) != 1 ? (byte)0xA2 : category,
            0x3B => (model.Transform ?? throw new NotSupportedException("Native Scan category requires its actual model scale.")).Scale154 switch { 0x1800 => 0xA3, 0x2000 => 0xA4, _ => category },
            0x4B => readResourceSequence(source) != 1 ? (byte)0xA5 : category,
            _ => category
        };
    }

    private static byte ReadCategory(NativeBattleGraphicsModel model) => model.ModelId4 ?? throw new NotSupportedException("Native Scan requires its actual model category.");
}
