using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7WorldRuntime
{
    private readonly List<Ff7WorldEntity> freeEntities = [];

    private Ff7WorldEntity ResolveHostEntity(WorldHostRequest request)
    {
        return ResolveHostEntity(request.ActiveModel, request.FreeModelIndex);
    }

    private Ff7WorldEntity ResolveHostEntity(int model, int? freeIndex)
    {
        if (freeIndex is not int index)
        {
            return RequireEntity(model);
        }
        if ((uint)index >= freeEntities.Count || freeEntities[index].Model != model)
        {
            throw new InvalidDataException("FFVII world host has no matching native free-pool object.");
        }
        return freeEntities[index];
    }

    private void PublishHostEntity(WorldHostRequest request, Ff7WorldEntity entity)
    {
        if (request.FreeModelIndex is int index)
        {
            freeEntities[index] = entity;
        }
        else
        {
            UpdateEntity(entity);
        }
    }

    private void RecordFreedWorldEntity(int model)
    {
        Ff7WorldEntity removed = RequireEntity(model);
        if (model == controlledModel)
        {
            removed = removed with { TerrainCache = input.TerrainCache?.ToArray() };
        }
        freeEntities.Insert(0, removed);
        entities.Remove(model);
        foreach (Ff7WorldEntity entity in entities.Values.Where(entity => entity.ContactModel == model).ToArray())
        {
            UpdateEntity(entity with { ContactModel = null });
        }
    }

    private static void ValidateFreeWorldEntities(Ff7WorldProcess state)
    {
        WorldEntitySnapshot[] free = state.Vm.FreeModels ?? [];
        if (state.FreeEntities is null)
        {
            if (free.Any(model => model.ProgramCounter != 0))
            {
                throw new NotSupportedException("FFVII world free-pool scripts require their recorded native object state.");
            }
            return;
        }
        if (state.FreeEntities.Length != free.Length || state.FreeEntities.Where((entity, index) => entity is null || entity.Model != free[index].Model).Any())
        {
            throw new InvalidDataException("FFVII world free-pool object and script owners differ.");
        }
    }
}
