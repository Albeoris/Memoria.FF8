using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

internal static partial class NativeBattleTypedFixture
{
    internal static int OriginalSlot(BattleModelOwner owner)
    {
        return OriginalAliases.TryEncodeExact(owner, out byte slot)
            ? slot
            : throw new InvalidDataException("The test model is not bound to an original fixture slot.");
    }

    internal static (BattleActorModels Models, NativeBattleModelBoundary Boundary) OriginalModels(IEnumerable<(int Slot, NativeBattleGraphicsModel Model)> entries)
    {
        return OriginalModels(entries.Select(entry => (entry.Slot, OriginalOwner(entry.Slot), entry.Model)));
    }

    private static (BattleActorModels Models, NativeBattleModelBoundary Boundary) OriginalModels(IEnumerable<(int Slot, BattleModelOwner Owner, NativeBattleGraphicsModel Model)> entries)
    {
        BattleActorModels models = new([]);
        NativeBattleModelBoundary boundary = NativeBattleModelBoundary.FromRetainedMemory(null);
        HashSet<int> slots = [];
        foreach ((int slot, BattleModelOwner owner, NativeBattleGraphicsModel model) in entries)
        {
            if ((uint)slot >= owners.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(entries), "The fixture slot is outside the original model bank.");
            }
            if (!slots.Add(slot) || models.Contains(owner))
            {
                throw new ArgumentException("The original test fixture already binds this slot or owner.", nameof(entries));
            }
            (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode((byte)slot), owner, models);
            models = models.Set(owner, model);
        }
        return (models, boundary);
    }

    internal static NativeBattleGraphicsState OriginalGraphics(IEnumerable<(int Slot, BattleModelOwner Owner, NativeBattleGraphicsModel Model)> entries, uint flags = 0, long callback = 0)
    {
        (BattleActorModels models, NativeBattleModelBoundary boundary) = OriginalModels(entries);
        return new(models, boundary, flags, callback, OriginalEffects());
    }

    internal static NativeBattleGraphicsModel ReadOriginalModelCell(NativeBattleGraphicsState graphics, int slot)
    {
        return graphics.NativeBindings.ReadOriginalCell(NativeBattleModelSlot.Decode(checked((byte)slot)), graphics.ActorModels);
    }

    internal static ImmutableArray<NativeBattleGraphicsModel> ReadOriginalModelCells(NativeBattleGraphicsState graphics)
    {
        return graphics.NativeBindings.CaptureOriginalCells(graphics.ActorModels);
    }

    internal static string? ReadOriginalModelActorId(NativeBattleGraphicsState graphics, int slot)
    {
        return graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(checked((byte)slot)), graphics.ActorModels) switch
        {
            BattlePartyActorModel party => party.Actor.ActorId,
            BattleEnemyActorModel enemy => enemy.Actor.ActorId,
            BattleForeignPartyModel party => party.Presentation.Key,
            BattleForeignEnemyModel enemy => enemy.Presentation.Key,
            null => null,
            _ => throw new InvalidDataException("The fixture contains an unknown original model owner.")
        };
    }
}
