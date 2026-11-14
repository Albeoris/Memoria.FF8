namespace Memoria.FF7.Events;

public sealed partial class WorldMachine
{
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
        Entity created = new(model);
        models.Insert(0, created);
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
