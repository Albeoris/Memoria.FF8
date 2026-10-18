using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaProcess([property: JsonRequired] NativeBattleShivaEntryState? Entry, [property: JsonRequired] NativeBattleActionMatrix? WorldView, [property: JsonRequired] NativeBattleGuardianCameraTask? CameraTask, [property: JsonRequired] int VibrationHandle, [property: JsonRequired] byte SoundReady, [property: JsonRequired] NativeBattleShivaGeometryState Geometry, [property: JsonRequired] NativeBattleCactuarRawModelState RawModel)
{
    [JsonRequired]
    public NativeBattleShivaRetainedVector LocalAnchor22BCFC8 { get; init; } = new(0, 0, 0, 0);
    [JsonRequired]
    public NativeBattleShivaRetainedVector WorldAnchor22BCFC0 { get; init; } = new(0, 0, 0, 0);

    public static NativeBattleShivaProcess FromColdProcess() => new(null, null, null, 0, 0, new(new(0, 0, 0, 0), new(0, 0, 0, 0)), NativeBattleCactuarRawModelState.FromColdProcess());

    public void Validate()
    {
        if (Geometry is null || Geometry.Current is null || Geometry.Previous is null || LocalAnchor22BCFC8 is null || WorldAnchor22BCFC0 is null)
        {
            throw new InvalidDataException("Native Shiva requires both retained physical SVECTOR owners.");
        }
        if (RawModel is null || RawModel.Model.IsDefault || RawModel.Model.Length != 0x90 || RawModel.Control.IsDefault || RawModel.Control.Length != 0x34 || RawModel.ProjectedScratch is not null)
        {
            throw new InvalidDataException("Native Shiva requires its original model/control banks and the single shared projected arena owner.");
        }
        Entry?.World.Validate();
        WorldView?.Validate();
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleShivaProcess? Shiva { get; init; }
}
