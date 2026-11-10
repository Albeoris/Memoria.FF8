using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum GameplaySaveWorldReturnKind
{
    Foot,
    Car,
    Garden,
    Ragnarok,
    Scripted
}

/// <summary>Committed transport return values, not the loader, camera trajectory or source callback receipt.</summary>
public sealed record GameplaySaveWorldReturn([property: JsonRequired] GameplaySaveWorldReturnKind Kind, [property: JsonRequired] ushort LastFieldId, [property: JsonRequired] ushort StoryWord, [property: JsonRequired] NativeSavedWorldPose Pose, [property: JsonRequired] ushort CameraYaw, [property: JsonRequired] byte EntityType, [property: JsonRequired] byte Channel0, [property: JsonRequired] byte Channel1, [property: JsonRequired] byte? AnimationVariant, [property: JsonRequired] byte? MapDisplayMode, [property: JsonRequired] short MapLocation)
{
    public int? FootMode { get; init; }
    public bool? RetireSelectedVehicle { get; init; }
    public WorldTransition? ScriptedPacket { get; init; }
    public int? ScriptedFieldId { get; init; }
    public GameplaySaveFieldPosition? ScriptedFieldPosition { get; init; }

    public void Validate()
    {
        bool scripted = Kind == GameplaySaveWorldReturnKind.Scripted;
        if (!Enum.IsDefined(Kind) || Pose is null || !scripted && CameraYaw >= 4096 || MapDisplayMode == 3 || Channel0 != 255 || Channel1 != 255 || Kind == GameplaySaveWorldReturnKind.Foot != (FootMode is not null) || Kind == GameplaySaveWorldReturnKind.Car != (RetireSelectedVehicle is not null) || (scripted ? ScriptedPacket is null || ScriptedFieldId is null || ScriptedFieldPosition is null : ScriptedPacket is not null || ScriptedFieldId is not null || ScriptedFieldPosition is not null))
        {
            throw new InvalidDataException("Gameplay world return has inconsistent logical transport values.");
        }
        if (scripted && (!NativeWorldScriptedPathState.SupportsPacket(ScriptedPacket!) || unchecked((byte)ScriptedPacket!.Mode) != EntityType || ScriptedFieldId is < 0 or > ushort.MaxValue || !float.IsFinite(ScriptedFieldPosition!.X) || !float.IsFinite(ScriptedFieldPosition.Y) || ScriptedFieldPosition.Triangle is < 0 or > ushort.MaxValue || AnimationVariant is null || MapDisplayMode is null or > 2 || MapLocation is < sbyte.MinValue or > sbyte.MaxValue))
        {
            throw new InvalidDataException("Dormant scripted return lost its packet, field entry or saved native widths.");
        }
    }
}

public sealed record GameplaySaveWorldData
{
    [JsonRequired]
    public required NativeSavedWorldVehicleSlot Slot4 { get; init; }
    [JsonRequired]
    public required NativeSavedWorldVehicleSlot Slot5 { get; init; }
    [JsonRequired]
    public required NativeSavedWorldPose? GardenPose { get; init; }
    [JsonRequired]
    public required NativeSavedWorldPose? RagnarokPose { get; init; }
    [JsonRequired]
    public required GameplaySaveWorldReturn? Return { get; init; }
    [JsonRequired]
    public required byte? InitialMapDisplayMode { get; init; }
    [JsonRequired]
    public required uint? FuelDistance { get; init; }
    [JsonRequired]
    public required NativeWorldEventFlags? EventFlags { get; init; }
    [JsonRequired]
    public required byte? EventVariable0 { get; init; }
    [JsonRequired]
    public required byte? EventVariable1 { get; init; }
    [JsonRequired]
    public required NativeSavedWorldAngleWords? OriginalAngles { get; init; }
    [JsonRequired]
    public required byte? EntryMode { get; init; }
    [JsonRequired]
    public required NativeWorldTerrainFlags109? TerrainFlags { get; init; }
    [JsonRequired]
    public required NativeSavedWorldUnsupportedEntry? UnresolvedEntry { get; init; }

    internal static GameplaySaveWorldData Capture(NativeSavedWorldState source)
    {
        source.Validate();
        GameplaySaveWorldReturn? savedReturn = null;
        if (source.FootReturn is { } foot)
        {
            savedReturn = new(GameplaySaveWorldReturnKind.Foot, foot.LastFieldId, foot.StoryWord, foot.Pose0, foot.CameraYaw96, foot.EntityType105, foot.Channel106, foot.Channel107, foot.AnimationVariant108, foot.MapDisplayMode110, foot.MapLocationId) { FootMode = foot.WorldMode };
        }
        else if (source.CarReturn is { } car)
        {
            savedReturn = new(GameplaySaveWorldReturnKind.Car, car.LastFieldId, car.StoryWord, car.Pose, car.CameraYaw96, car.EntityType105, car.Channel106, car.Channel107, car.AnimationVariant108, car.MapDisplayMode110, car.MapLocationId) { RetireSelectedVehicle = car.RetireSelectedVehicle };
        }
        else if (source.GardenReturn is { } garden)
        {
            savedReturn = new(GameplaySaveWorldReturnKind.Garden, garden.LastFieldId, garden.StoryWord, garden.Pose3, garden.CameraYaw96, garden.EntityType105, garden.Channel106, garden.Channel107, garden.AnimationVariant108, garden.MapDisplayMode110, garden.MapLocationId);
        }
        else if (source.RagnarokReturn is { } ragnarok)
        {
            savedReturn = new(GameplaySaveWorldReturnKind.Ragnarok, ragnarok.LastFieldId, ragnarok.StoryWord, ragnarok.Pose2, ragnarok.CameraYaw96, 50, 255, 255, ragnarok.AnimationVariant108, ragnarok.MapDisplayMode110, ragnarok.MapLocationId);
        }
        else if (source.ScriptedReturn is { } scripted)
        {
            FieldTransition field = scripted.FieldEntry;
            if (field.DiscJump is not null || field.NativeExtraWord is not null || field.Direction is < 0 or > 255)
            {
                throw new NotSupportedException("Scripted world return requires a semantic adapter for its extended field-entry packet.");
            }
            savedReturn = new(GameplaySaveWorldReturnKind.Scripted, scripted.LastFieldId, scripted.StoryWord, scripted.Pose0, scripted.CameraYaw96, scripted.EntityType105, scripted.Channel106, scripted.Channel107, scripted.AnimationVariant108, scripted.MapDisplayMode110, scripted.MapLocationId) { ScriptedPacket = scripted.Packet, ScriptedFieldId = field.FieldId, ScriptedFieldPosition = new(field.X, field.Y, field.Triangle, (byte)field.Direction) };
        }
        savedReturn ??= source.DormantScriptedReturn;
        return new() { Slot4 = source.Slot4, Slot5 = source.Slot5, GardenPose = source.GardenPose3, RagnarokPose = source.RagnarokPose2, Return = savedReturn, InitialMapDisplayMode = source.InitialMapDisplayMode110, FuelDistance = source.FuelDistance112, EventFlags = source.WorldEventFlags116, EventVariable0 = source.WorldEventVariable124, EventVariable1 = source.WorldEventVariable125, OriginalAngles = source.OriginalAngleWords, EntryMode = source.EntryMode104, TerrainFlags = source.TerrainFlags109, UnresolvedEntry = source.UnsupportedEntry };
    }

    /// <summary>Restores parked vehicles and saved flags into an initialized normal-load owner. The current pose/transport return must be installed separately from the resume descriptor.</summary>
    public NativeSavedWorldState RestoreParkingAndFlags(NativeSavedWorldState initialized)
    {
        ArgumentNullException.ThrowIfNull(initialized);
        initialized.Validate();
        Validate();
        GameplaySaveWorldData saved = GameplaySaveCodec.Detach(this);
        NativeSavedWorldState result = initialized with
        {
            Slot4 = saved.Slot4,
            Slot5 = saved.Slot5,
            GardenPose3 = saved.GardenPose,
            RagnarokPose2 = saved.RagnarokPose,
            FootReturn = null,
            CarReturn = null,
            GardenReturn = null,
            RagnarokReturn = null,
            ScriptedReturn = null,
            DormantScriptedReturn = null,
            InitialMapDisplayMode110 = saved.InitialMapDisplayMode,
            FuelDistance112 = saved.FuelDistance,
            WorldEventFlags116 = saved.EventFlags,
            WorldEventVariable124 = saved.EventVariable0,
            WorldEventVariable125 = saved.EventVariable1,
            OriginalAngleWords = saved.OriginalAngles,
            EntryMode104 = saved.EntryMode,
            TerrainFlags109 = saved.TerrainFlags,
            UnsupportedEntry = saved.UnresolvedEntry
        };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (Slot4?.Pose is null || Slot5?.Pose is null)
        {
            throw new InvalidDataException("Gameplay world state requires both saved vehicle slots.");
        }
        EventFlags?.Validate();
        OriginalAngles?.Validate();
        TerrainFlags?.Validate();
        UnresolvedEntry?.Validate();
        Return?.Validate();
    }
}
