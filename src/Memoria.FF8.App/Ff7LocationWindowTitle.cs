using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal static class Ff7LocationWindowTitle
{
    internal static string Format(Ff7Session? session, int speedMultiplier)
    {
        return FormatState(session?.Field.Name, session?.Host.Process, session?.World?.Process, speedMultiplier);
    }

    internal static string FormatSnapshot(Ff7SessionSave? saved, int speedMultiplier)
    {
        return FormatState(saved?.FieldName, saved?.Process, saved?.World, speedMultiplier);
    }

    private static string FormatState(string? fieldName, Ff7FieldProcess? field, Ff7WorldProcess? world, int speedMultiplier)
    {
        string location = "Title";
        if (world is { } state)
        {
            Ff7WorldEntity? actor = state.Entities.SingleOrDefault(entity => entity.Model == state.ControlledModel);
            location = FormattableString.Invariant($"World {state.WorldId}{(actor is null ? "" : $" XYZ({actor.X},{actor.Y},{actor.Z}) T{actor.Terrain}")} F{state.Tick}");
        }
        else if (field is not null)
        {
            Ff7Actor? actor = field.Actors.SingleOrDefault(item => item.PlayerControlled);
            location = FormattableString.Invariant($"Field {fieldName}{(actor is null ? "" : $" XYZ({actor.X4096 >> 12},{actor.Y4096 >> 12},{actor.Z4096 >> 12}) T{actor.Triangle}")} F{field.Tick}");
        }
        return $"Memoria FFVII | {location}{(speedMultiplier == 1 ? "" : $" | x{speedMultiplier}")}";
    }
}

internal sealed partial class Ff7Scene
{
    internal string LocationWindowTitle => error is null ? Ff7LocationWindowTitle.Format(Session, sharedClock.SpeedMultiplier) : Ff7LocationWindowTitle.FormatSnapshot(reportCheckpoint?.Ff7, sharedClock.SpeedMultiplier);
}
