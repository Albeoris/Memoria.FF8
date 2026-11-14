namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldMapTransfer(int DestinationWorld, int VerticalSpeed);

public sealed partial class Ff7WorldRuntime
{
    private Ff7WorldMapTransfer? mapTransfer;
    private bool submarineMapEntry;

    internal int? ReadyMapTransfer => mapTransfer is not null && mainState is 6 or 7 && Machine.IsUnloading ? mapTransfer.DestinationWorld : null;

    private void BeginSubmarineMapTransfer(int destinationWorld)
    {
        if (mapTransfer is not null || mainState != 1 || controlledModel != SubmarineModel || destinationWorld is not (0 or 2) || destinationWorld == Resources.World)
        {
            return;
        }
        mapTransfer = new(destinationWorld, destinationWorld == 2 ? -20 : 20);
        mainState = destinationWorld == 2 ? 4 : 5;
        movementEnabled = false;
        controlLock = 0;
        fadeRate = fadeAlpha < 255 ? 16 : 0;
    }

    private void AdvanceMapTransfer()
    {
        if (mapTransfer is not { } transfer || mainState is not (4 or 5))
        {
            return;
        }
        Ff7WorldEntity submarine = RequireEntity(SubmarineModel);
        UpdateEntity(submarine with { Y = submarine.Y + transfer.VerticalSpeed });
        mapTransfer = transfer with { VerticalSpeed = transfer.VerticalSpeed + (transfer.VerticalSpeed >> 2) };
        if (fadeRate == 0 && fadeDisplayedAlpha == 255)
        {
            mainState = transfer.DestinationWorld == 2 ? 6 : 7;
            Machine.CompleteUnload();
        }
    }

    internal void PrepareSubmarineMapEntry()
    {
        if (Resources.World is not (0 or 2) || entrySelector != 0 || entryModule != 0)
        {
            throw new InvalidOperationException("FFVII submarine map entry requires the native zero-selector world dispatcher.");
        }
        submarineMapEntry = true;
    }

    private static void ValidateMapTransfer(Ff7WorldProcess state)
    {
        if (state.SubmarineMapEntry && (state.WorldId is not (0 or 2) || state.EntrySelector != 0 || state.EntryModule != 0) || state.MapTransfer is { } transfer &&
            (state.WorldId is not (0 or 2) || transfer.DestinationWorld is not (0 or 2) || transfer.DestinationWorld == state.WorldId || state.ControlledModel != SubmarineModel || state.FieldDeparture is not null || state.MovementEnabled || transfer.DestinationWorld == 2 && (transfer.VerticalSpeed >= 0 || state.MainState is not (4 or 6)) || transfer.DestinationWorld == 0 && (transfer.VerticalSpeed <= 0 || state.MainState is not (5 or 7)) || state.Vm.Unloading != (state.MainState is 6 or 7)))
        {
            throw new InvalidDataException("FFVII submarine map transfer has inconsistent world, fade or model ownership.");
        }
    }
}
