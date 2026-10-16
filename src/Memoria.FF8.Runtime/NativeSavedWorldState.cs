using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeSavedWorldOriginKind
{
    NativeNewGame
}

/// <summary>The original twelve-byte saved pose, with raw Z and unmodified native integer widths.</summary>
public sealed record NativeSavedWorldPose([property: JsonRequired] int X, [property: JsonRequired] int RawZ, [property: JsonRequired] short Height, [property: JsonRequired] ushort Yaw);

/// <summary>The stored shape ID is converted by native 546F90 before world-type comparisons.</summary>
public sealed record NativeSavedWorldVehicleSlot([property: JsonRequired] byte ShapeId, [property: JsonRequired] NativeSavedWorldPose Pose);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NativeSavedWorldOrigin([property: JsonRequired] int ProofVersion, [property: JsonRequired] NativeSavedWorldOriginKind Kind, [property: JsonRequired] string InitializationRule);

/// <summary>Known native549E80 foot-slot writes, with explicit unknown auxiliary bytes rather than invented saved history.</summary>
public sealed record NativeSavedWorldFootReturn
{
    public const int CurrentProofVersion = 1;
    public const string SaveRule = "ff8-en-foot-world-return-549e80-mode0-type0-v1";

    [JsonRequired]
    public required int ProofVersion { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required long SourceWorldFrame { get; init; }
    [JsonRequired]
    public required ushort LastFieldId { get; init; }
    [JsonRequired]
    public required ushort StoryWord { get; init; }
    [JsonRequired]
    public required int WorldMode { get; init; }
    /// <summary>Saved-world +0/+4 DWORD X/raw Z, +8 signed WORD height, +10 WORD body yaw.</summary>
    [JsonRequired]
    public required NativeSavedWorldPose Pose0 { get; init; }
    /// <summary>Saved-world +96 WORD.</summary>
    [JsonRequired]
    public required ushort CameraYaw96 { get; init; }
    /// <summary>Saved-world +105 BYTE; native world entity type, independent of terrain ground type.</summary>
    [JsonRequired]
    public required byte EntityType105 { get; init; }
    [JsonRequired]
    public required byte Channel106 { get; init; }
    [JsonRequired]
    public required byte Channel107 { get; init; }
    /// <summary>Saved-world +108 BYTE from 20409E4; null is unproved, not zero.</summary>
    [JsonRequired]
    public required byte? AnimationVariant108 { get; init; }
    /// <summary>Saved-world +110 BYTE map-display mode from C75CF8, with native 3 normalized to 0.</summary>
    [JsonRequired]
    public required byte? MapDisplayMode110 { get; init; }
    /// <summary>Save-header +4 WORD: sign-extended original WMSET zero-based section18 byte, indexed by native553910.</summary>
    [JsonRequired]
    public required short MapLocationId { get; init; }

    public static NativeSavedWorldFootReturn Create(WorldState world, ushort lastFieldId, ushort storyWord, short mapLocationId, byte? animationVariant108 = null, byte? mapDisplayMode110 = null, byte entityType105 = 0)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.NativeHeight is < short.MinValue or > short.MaxValue || world.BodyYaw is < 0 or >= 4096 || world.CameraYaw is < 0 or >= 4096)
        {
            throw new InvalidDataException("The current world pose cannot be represented by the supported native foot-save widths.");
        }
        var receipt = new NativeSavedWorldFootReturn
        {
            ProofVersion = CurrentProofVersion,
            Rule = SaveRule,
            SourceWorldFrame = world.Frame,
            LastFieldId = lastFieldId,
            StoryWord = storyWord,
            WorldMode = 0,
            Pose0 = new NativeSavedWorldPose(world.X, world.Z, (short)world.NativeHeight, (ushort)world.BodyYaw),
            CameraYaw96 = (ushort)world.CameraYaw,
            EntityType105 = entityType105,
            Channel106 = 255,
            Channel107 = 255,
            AnimationVariant108 = animationVariant108,
            MapDisplayMode110 = mapDisplayMode110 == 3 ? (byte)0 : mapDisplayMode110,
            MapLocationId = mapLocationId
        };
        receipt.Validate();
        return receipt;
    }

    public static NativeSavedWorldFootReturn RecordFootDeparture(WorldState world, ushort lastFieldId, ushort storyWord, short mapLocationId)
    {
        ArgumentNullException.ThrowIfNull(world);
        world.NativeFootRuntime?.Validate();
        return Create(world, lastFieldId, storyWord, mapLocationId, world.NativeFootRuntime?.AnimationVariant, world.NativeFootRuntime?.MapDisplayMode, world.NativeFootRuntime?.EntityType ?? throw new NotSupportedException("Native foot departure requires its executed world-entry entity type."));
    }

    public void Validate()
    {
        if (ProofVersion != CurrentProofVersion || Rule != SaveRule || SourceWorldFrame < 0 || WorldMode != 0 || EntityType105 > 1 || Channel106 != 255 || Channel107 != 255)
        {
            throw new InvalidDataException("Unsupported or invalid native foot-return provenance.");
        }
        if (Pose0 is null || Pose0.X is < -131072 or >= 131072 || Pose0.RawZ is < -98304 or >= 98304 || Pose0.Yaw >= 4096 || CameraYaw96 >= 4096 || MapLocationId is < sbyte.MinValue or > sbyte.MaxValue || MapDisplayMode110 == 3)
        {
            throw new InvalidDataException("Invalid native foot-return pose or auxiliary metadata.");
        }
    }

    public void RequireCompleteFootReload()
    {
        Validate();
        if (AnimationVariant108 is null || MapDisplayMode110 is null)
        {
            throw new NotSupportedException("Native saved foot reload additionally reads unproved saved-world bytes +108/+110.");
        }
    }
}

/// <summary>Persistent native vehicle slots and optional naturally recorded foot return; absence means unknown provenance.</summary>
public sealed partial record NativeSavedWorldState
{
    public const int CurrentSchemaVersion = 1;
    public const int CurrentProofVersion = 1;
    public const string NewGameInitializationRule = "ff8-en-newgame-4e43fc-56d9f0-56da10-v1";

    public required int SchemaVersion { get; init; }
    public required NativeSavedWorldOrigin Origin { get; init; }
    /// <summary>Saved-world pose +48 and shape-ID byte +98.</summary>
    public required NativeSavedWorldVehicleSlot Slot4 { get; init; }
    /// <summary>Saved-world pose +60 and shape-ID byte +99.</summary>
    public required NativeSavedWorldVehicleSlot Slot5 { get; init; }
    /// <summary>Native pose3/+36; a missing older value remains unknown until the authentic fresh Garden loader overwrites it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldPose? GardenPose3 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldGardenReturn? GardenReturn { get; init; }
    /// <summary>Absent older checkpoints have no inferred slot0 pose, camera or last return frame.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldFootReturn? FootReturn { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldCarReturn? CarReturn { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldScriptedReturn? ScriptedReturn { get; init; }
    /// <summary>Saved-world +110 initialized by native New Game whole-save zeroing.</summary>
    [JsonRequired]
    public byte? InitialMapDisplayMode110 { get; init; }
    /// <summary>Saved-world DWORD112, cleared by native New Game and preserved by field CLEAR.</summary>
    [JsonRequired]
    public uint? FuelDistance112 { get; init; }
    /// <summary>Saved-world DWORD116/120, cleared by native New Game and consumed by546100/FF27.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldEventFlags? WorldEventFlags116 { get; init; }
    /// <summary>Saved-world BYTE124, the original section36 workflow channel0; absent older values remain unknown.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? WorldEventVariable124 { get; init; }
    /// <summary>Saved-world BYTE125, the original section36 workflow channel1; absent older values remain unknown.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? WorldEventVariable125 { get; init; }

    public byte ReadMapDisplayModeForFreshFootLoader()
    {
        Validate();
        if (RagnarokReturn is { } ragnarok)
        {
            return ragnarok.MapDisplayMode110;
        }
        if (GardenReturn is NativeSavedWorldGardenReturn garden)
        {
            return garden.MapDisplayMode110;
        }
        if (ScriptedReturn is NativeSavedWorldScriptedReturn boat)
        {
            return boat.MapDisplayMode110;
        }
        if (CarReturn is NativeSavedWorldCarReturn car)
        {
            return car.MapDisplayMode110;
        }
        if (FootReturn is NativeSavedWorldFootReturn receipt)
        {
            receipt.RequireCompleteFootReload();
            return receipt.MapDisplayMode110!.Value;
        }
        return InitialMapDisplayMode110!.Value;
    }

    public NativeSavedWorldState WithFootReturn(NativeSavedWorldFootReturn receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        Validate();
        receipt.Validate();
        return this with { FootReturn = receipt, CarReturn = null, ScriptedReturn = null, GardenReturn = null, RagnarokReturn = null };
    }

    public NativeSavedWorldState WithGardenReturn(NativeSavedWorldGardenReturn receipt)
    {
        Validate();
        receipt.Validate();
        return this with { GardenPose3 = receipt.Pose3, GardenReturn = receipt, FootReturn = null, CarReturn = null, ScriptedReturn = null, RagnarokReturn = null };
    }

    public NativeSavedWorldState WithGardenParking(NativeGardenWorldRuntimeState garden)
    {
        Validate();
        garden.Validate();
        NativeGardenWorldObject body = garden.Objects[garden.Allocation.Garden64Index];
        NativeSavedWorldPose pose = new(body.Position.X, unchecked(-body.Position.Z), unchecked((short)body.Position.Y), unchecked((ushort)body.RotationY));
        if (pose.Yaw >= 4096)
        {
            throw new InvalidDataException("Parked Garden yaw is outside the normalized native object angle.");
        }
        return this with { GardenPose3 = pose, GardenReturn = null };
    }

    public NativeSavedWorldState WithCarReturn(NativeSavedWorldCarReturn receipt)
    {
        Validate();
        receipt.Validate();
        if (Slot4.ShapeId != (receipt.EntityType105 == 32 ? 74 : 73))
        {
            throw new InvalidDataException("The native car save requires its active shape73 vehicle slot.");
        }
        return this with { Slot4 = new NativeSavedWorldVehicleSlot(255, receipt.Pose), FootReturn = null, CarReturn = receipt, ScriptedReturn = null, GardenReturn = null, RagnarokReturn = null };
    }

    public NativeSavedWorldState WithScriptedReturn(NativeSavedWorldScriptedReturn receipt)
    {
        Validate();
        receipt.Validate();
        bool retainedCar = CarReturn is NativeSavedWorldCarReturn car && car.SourceWorldFrame == receipt.Load.SourceWorldFrame && car.Pose.Yaw == receipt.Load.RetainedBodyYaw && car.MapDisplayMode110 == receipt.Load.MapDisplayMode;
        bool retainedFoot = FootReturn is NativeSavedWorldFootReturn foot && foot.SourceWorldFrame == receipt.Load.SourceWorldFrame && foot.Pose0.Yaw == receipt.Load.RetainedBodyYaw && foot.MapDisplayMode110 == receipt.Load.MapDisplayMode;
        bool retainedScripted = ScriptedReturn is NativeSavedWorldScriptedReturn prior && prior.SourceWorldFrame + 16 == receipt.Load.SourceWorldFrame && prior.Load.RetainedBodyYaw == receipt.Load.RetainedBodyYaw && prior.MapDisplayMode110 == receipt.Load.MapDisplayMode;
        if (Slot4 != receipt.Load.Slot4 || Slot5 != receipt.Load.Slot5 || !retainedCar && !retainedFoot && !retainedScripted)
        {
            throw new InvalidDataException("Native scripted save differs from its preceding return and retained original object/vehicle records.");
        }
        return this with { FootReturn = null, CarReturn = null, ScriptedReturn = receipt, GardenReturn = null, RagnarokReturn = null };
    }

    public NativeSavedWorldState WithOrdinaryCarEntry(byte entityType = 33)
    {
        Validate();
        if (Slot4.ShapeId is not 0 and not 255 || Slot5.ShapeId != 0)
        {
            throw new NotSupportedException("Native ordinary car entry would replace an unrepresented saved vehicle.");
        }
        byte shape = entityType switch { 32 => 74, 33 => 73, _ => throw new NotSupportedException("Native ordinary car entry requires its supported actual entity type.") };
        return this with { Slot4 = Slot4 with { ShapeId = shape }, CarReturn = null, ScriptedReturn = null, GardenReturn = null, RagnarokReturn = null };
    }

    public static NativeSavedWorldState CreateNewGame()
    {
        return CreateInitial(new NativeSavedWorldOrigin(CurrentProofVersion, NativeSavedWorldOriginKind.NativeNewGame, NewGameInitializationRule));
    }

    public void Validate()
    {
        ValidateRagnarokReturn();
        if (SchemaVersion != CurrentSchemaVersion || Origin is null || Origin.ProofVersion != CurrentProofVersion || !Enum.IsDefined(Origin.Kind) || Origin.InitializationRule != NewGameInitializationRule)
        {
            throw new InvalidDataException("Unsupported or invalid native saved-world provenance.");
        }
        if (Slot4?.Pose is null || Slot5?.Pose is null)
        {
            throw new InvalidDataException("Native saved-world vehicle slots require their original poses.");
        }
        FootReturn?.Validate();
        CarReturn?.Validate();
        ScriptedReturn?.Validate();
        GardenReturn?.Validate();
        WorldEventFlags116?.Validate();
        if (GardenPose3 is not null && (GardenPose3.X is < -131072 or >= 131072 || GardenPose3.RawZ is < -98304 or >= 98304 || GardenPose3.Yaw >= 4096) || GardenReturn is not null && (GardenPose3 != GardenReturn.Pose3 || FootReturn is not null || CarReturn is not null || ScriptedReturn is not null))
        {
            throw new InvalidDataException("Native Garden parking/return differs from its distinct pose3 or current return owner.");
        }
        if (ScriptedReturn is not null && (FootReturn is not null || CarReturn is not null || Slot4 != ScriptedReturn.Load.Slot4 || Slot5 != ScriptedReturn.Load.Slot5))
        {
            throw new InvalidDataException("Native scripted return disagrees with its retained saved vehicle slots.");
        }
        if (CarReturn is not null && (FootReturn is not null || Slot4.ShapeId != 255 || Slot4.Pose != CarReturn.Pose))
        {
            throw new InvalidDataException("Native car return disagrees with its retired vehicle slot.");
        }
        if (InitialMapDisplayMode110 != 0 || FuelDistance112 is null)
        {
            throw new InvalidDataException("Native initial saved-world map-display authority differs from the New Game zeroing proof.");
        }
    }

    private static NativeSavedWorldState CreateInitial(NativeSavedWorldOrigin origin)
    {
        // Native 56D9F0 clears both slots; init.out ends before this saved-world block.
        return new NativeSavedWorldState
        {
            SchemaVersion = CurrentSchemaVersion,
            Origin = origin,
            Slot4 = new NativeSavedWorldVehicleSlot(0, new NativeSavedWorldPose(0, 0, 0, 0)),
            Slot5 = new NativeSavedWorldVehicleSlot(0, new NativeSavedWorldPose(0, 0, 0, 0)),
            GardenPose3 = new NativeSavedWorldPose(0, 0, 0, 0),
            RagnarokPose2 = new NativeSavedWorldPose(0, 0, 0, 0),
            InitialMapDisplayMode110 = 0,
            FuelDistance112 = 0,
            WorldEventFlags116 = NativeWorldEventFlags.CreateNewGame(),
            WorldEventVariable124 = 0,
            WorldEventVariable125 = 0
        };
    }
}
