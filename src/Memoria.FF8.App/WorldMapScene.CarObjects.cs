using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private NativeNormalFootObjectProcess CreateCarObjectProcess(NativeTrainWorldAllocation allocation, NativeWorldFootCameraState camera)
    {
        NativeTrainRailBank? primary = (allocation.EventFlags103 & 1) == 0 ? NativeTrainRailBank.Create(assets.ReadScriptedPath(0), 1) : null;
        NativeTrainRailBank? alternate = (allocation.EventFlags103 & 1) == 0 ? NativeTrainRailBank.Create(assets.ReadScriptedPath(1), 1) : null;
        var process = new NativeNormalFootObjectProcess(1, allocation, 0, primary, alternate, camera, primary is null ? null : uint.MaxValue, alternate is null ? null : uint.MaxValue);
        process.ValidateCar();
        return process;
    }

    private void ValidateCarObjects(WorldState world, NativeCarWorldRuntimeState car, bool validateProximity = false)
    {
        NativeNormalFootObjectProcess process = car.ObjectProcess ?? throw new NotSupportedException("Native car rail allocation requires its executed ordinary object process; migrate the historical snapshot explicitly.");
        process.ValidateCar();
        ValidateOrdinaryRailPresentation(world, process.Callback, process.Primary, process.Alternate, process.Camera, process.PrimaryDistanceSquared, process.AlternateDistanceSquared, process.CarDistancePosition, validateProximity);
    }

    private static NativeTrainRailBank[] CarRailBanks(NativeCarWorldRuntimeState car)
    {
        return car.ObjectProcess is { } process ? new[] { process.Primary, process.Alternate }.OfType<NativeTrainRailBank>().ToArray() : [];
    }

    private NativeCarWorldRuntimeState StepCarObjects(WorldState cameraState, NativeCarWorldRuntimeState car, int selectedEntity)
    {
        NativeWorldFootCameraState camera = cameraState.NativeFootRuntime is not null ? NativeWorldFootCameraState.Create(cameraState) : car.ProjectionCamera ?? throw new InvalidDataException("Native car rails require the current pre-terrain camera.");
        byte scriptMode = cameraState.EventUi?.ScriptMode ?? 0;
        if (car.GardenObjects?.BackgroundRails is { } gardenRails)
        {
            return car with { GardenObjects = car.GardenObjects with { BackgroundRails = StepBackgroundRails(gardenRails, camera, selectedEntity, scriptMode) } };
        }
        if (car.RagnarokObjects?.BackgroundRails is { } ragnarokRails)
        {
            return car with { RagnarokObjects = car.RagnarokObjects with { BackgroundRails = StepBackgroundRails(ragnarokRails, camera, selectedEntity, scriptMode) } };
        }
        if (car.ObjectProcess is not { } process)
        {
            return car;
        }
        process.ValidateCar();
        long callback = checked(process.Callback + 1);
        NativeTrainRailBank? primary = process.Primary is null ? null : AdvanceOrdinaryRailBank(process.Primary, process.PrimaryDistanceSquared!.Value, callback, camera, selectedEntity, scriptMode);
        NativeTrainRailBank? alternate = process.Alternate is null ? null : AdvanceOrdinaryRailBank(process.Alternate, process.AlternateDistanceSquared!.Value, callback, camera, selectedEntity, scriptMode);
        return car with { ObjectProcess = process with { Callback = callback, Primary = primary, Alternate = alternate, Camera = camera } };
    }

    private static NativeCarWorldRuntimeState CompleteCarObjectCallback(NativeCarWorldRuntimeState car, int x, int rawZ, int height)
    {
        if (car.GardenObjects?.BackgroundRails is { } gardenRails)
        {
            return car with { GardenObjects = car.GardenObjects with { BackgroundRails = CompleteBackgroundRails(gardenRails, x, rawZ, height) } };
        }
        if (car.RagnarokObjects?.BackgroundRails is { } ragnarokRails)
        {
            return car with { RagnarokObjects = car.RagnarokObjects with { BackgroundRails = CompleteBackgroundRails(ragnarokRails, x, rawZ, height) } };
        }
        if (car.ObjectProcess is not { } process)
        {
            return car;
        }
        NativeWorldPathProjectionCertificate context = NativeWorldPathProjectionCertificate.CreateFoot(process.Camera);
        NativeWorldPathWords player = context.RelativeWords(new(x, height, -rawZ));
        return car with { ObjectProcess = process with { CarDistancePosition = new(x, height, -rawZ), PrimaryDistanceSquared = process.Primary is null ? null : NormalTrainDistance(process.Primary, player, context), AlternateDistanceSquared = process.Alternate is null ? null : NormalTrainDistance(process.Alternate, player, context) } };
    }

    private IReadOnlyList<NativeWorldStaticModelDraw> ReadCarRailModels(NativeCarWorldRuntimeState car)
    {
        if (car.ObjectProcess is not { } process)
        {
            return [];
        }
        return ReadOrdinaryRailModels(process.Primary, process.Alternate, process.Camera);
    }
}
