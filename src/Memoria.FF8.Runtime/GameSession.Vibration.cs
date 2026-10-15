using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeVibrationProcessState? VibrationProcess => State.VibrationProcess;
    public bool CanStoreVibrationProcess => true;

    public void SetVibrationProcess(NativeVibrationProcessState vibration)
    {
        ArgumentNullException.ThrowIfNull(vibration);
        vibration.Validate();
        State = State with { VibrationProcess = vibration };
    }

    private static void ValidateVibrationCheckpoint(GameState state, JsonElement? currentVm)
    {
        state.VibrationProcess?.Validate();
        if (currentVm is not JsonElement vm)
        {
            return;
        }
        if (!vm.TryGetProperty("VibrationProcess", out JsonElement captured))
        {
            throw new InvalidDataException("Saved VM lacks its native vibration process; migrate this save explicitly.");
        }
        NativeVibrationProcessState? vibration = captured.ValueKind == JsonValueKind.Null ? null : captured.Deserialize<NativeVibrationProcessState>(JsonOptions);
        vibration?.Validate();
        if (state.Scene != SceneKind.WorldMap && !JsonElement.DeepEquals(captured, JsonSerializer.SerializeToElement(state.VibrationProcess, JsonOptions)))
        {
            throw new InvalidDataException("Root and VM native vibration owners disagree.");
        }
    }
}
