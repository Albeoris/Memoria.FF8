namespace Memoria.FF7.Events;

public sealed partial class WorldMachine
{
    private int? ActiveFreeModelIndex()
    {
        int index = 0;
        foreach (Entity entity in freeModels)
        {
            if (ReferenceEquals(entity, active))
            {
                return index;
            }
            index++;
        }
        return null;
    }

    private void LinkResidentModels()
    {
        Entity? previous = null;
        foreach (Entity entity in models.Where(entity => !entity.Detached))
        {
            if (previous is not null)
            {
                previous.Next = entity;
            }
            previous = entity;
        }
        if (previous is not null)
        {
            previous.Next = null;
        }
    }

    private Entity AllocateModel(int model)
    {
        if (models.Count == 16)
        {
            throw new InvalidDataException("FFVII world model pool is exhausted.");
        }
        Entity entity = freeModels.TryPop(out Entity? free) ? free : new(model);
        entity.Model = model;
        entity.Pc = 0;
        entity.MoveCounter = 0;
        entity.Priority = 0;
        entity.Disabled = false;
        entity.Detached = false;
        entity.CalledModel = 0;
        entity.Calls.Clear();
        models.Insert(0, entity);
        LinkResidentModels();
        return entity;
    }

    public void RenameResidentModel(int model, int replacement)
    {
        EnsureHealthy();
        if ((uint)replacement > 255 || models.Any(entity => entity.Model == replacement && entity.Model != model))
        {
            throw new InvalidDataException("FFVII world model identity replacement collides with a resident model.");
        }
        models.Single(entity => entity.Model == model).Model = replacement;
    }

    public void EnsurePresentationModel(int model)
    {
        EnsureHealthy();
        if ((uint)model > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(model));
        }
        if (models.Any(entity => entity.Model == model && !entity.Detached))
        {
            return;
        }
        if (models.Count == 16 || models.Any(entity => entity.Model == model))
        {
            throw new InvalidDataException("FFVII world presentation model cannot allocate its native model slot.");
        }
        Entity previousActive = active;
        Entity created = AllocateModel(model);
        active = created;
        try
        {
            Send(WorldOperation.CreateModel, 0, [model]);
        }
        finally
        {
            active = previousActive;
        }
    }
}
