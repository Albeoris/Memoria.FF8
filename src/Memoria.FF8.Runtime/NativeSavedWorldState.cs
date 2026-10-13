using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum NativeSavedWorldOriginKind
{
    NativeNewGame,
    VerifiedLegacyNewGame
}

/// <summary>The original twelve-byte saved pose, with raw Z and unmodified native integer widths.</summary>
public sealed record NativeSavedWorldPose([property: JsonRequired] int X, [property: JsonRequired] int RawZ, [property: JsonRequired] short Height, [property: JsonRequired] ushort Yaw);

/// <summary>The stored shape ID is converted by native 546F90 before world-type comparisons.</summary>
public sealed record NativeSavedWorldVehicleSlot([property: JsonRequired] byte ShapeId, [property: JsonRequired] NativeSavedWorldPose Pose);

public sealed record NativeSavedWorldOrigin([property: JsonRequired] int ProofVersion, [property: JsonRequired] NativeSavedWorldOriginKind Kind, [property: JsonRequired] string InitializationRule, string? LegacyCheckpointSha256 = null);

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
    /// <summary>Saved-world +110 BYTE map-display mode from C75CF8, with native 3 normalized to 0; the serialized MovementMode110 name is retained for compatibility.</summary>
    [JsonRequired]
    public required byte? MovementMode110 { get; init; }
    /// <summary>Save-header +4 WORD: sign-extended original WMSET zero-based section18 byte, indexed by native553910.</summary>
    [JsonRequired]
    public required short MapLocationId { get; init; }

    public static NativeSavedWorldFootReturn Create(WorldState world, ushort lastFieldId, ushort storyWord, short mapLocationId, byte? animationVariant108 = null, byte? movementMode110 = null)
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
            EntityType105 = 0,
            Channel106 = 255,
            Channel107 = 255,
            AnimationVariant108 = animationVariant108,
            MovementMode110 = movementMode110 == 3 ? (byte)0 : movementMode110,
            MapLocationId = mapLocationId
        };
        receipt.Validate();
        return receipt;
    }

    public static NativeSavedWorldFootReturn RecordFootDeparture(WorldState world, ushort lastFieldId, ushort storyWord, short mapLocationId)
    {
        ArgumentNullException.ThrowIfNull(world);
        world.NativeFootRuntime?.Validate();
        return Create(world, lastFieldId, storyWord, mapLocationId, world.NativeFootRuntime?.AnimationVariant, world.NativeFootRuntime?.MapDisplayMode);
    }

    public void Validate()
    {
        if (ProofVersion != CurrentProofVersion || Rule != SaveRule || SourceWorldFrame < 0 || WorldMode != 0 || EntityType105 != 0 || Channel106 != 255 || Channel107 != 255)
        {
            throw new InvalidDataException("Unsupported or invalid native foot-return provenance.");
        }
        if (Pose0 is null || Pose0.X is < -131072 or >= 131072 || Pose0.RawZ is < -98304 or >= 98304 || Pose0.Yaw >= 4096 || CameraYaw96 >= 4096 || MapLocationId is < sbyte.MinValue or > sbyte.MaxValue || MovementMode110 == 3)
        {
            throw new InvalidDataException("Invalid native foot-return pose or auxiliary metadata.");
        }
    }

    public void RequireCompleteFootReload()
    {
        Validate();
        if (AnimationVariant108 is null || MovementMode110 is null)
        {
            throw new NotSupportedException("Native saved foot reload additionally reads unproved saved-world bytes +108/+110.");
        }
    }
}

/// <summary>Persistent native vehicle slots and optional naturally recorded foot return; absence means unknown provenance.</summary>
public sealed record NativeSavedWorldState
{
    public const int CurrentSchemaVersion = 1;
    public const int CurrentProofVersion = 1;
    public const string NewGameInitializationRule = "ff8-en-newgame-4e43fc-56d9f0-56da10-v1";
    public const string LegacyCheckpoint265Sha256 = "3b78a5a2af71c9af981d71b713f504778ce06183f0ab6f7a8e3fc5aba1da0a92";
    public const string LegacyCheckpoint300Sha256 = "6c56643212e0151bfcfb01b6471d7b347800786a90f43a8059a199d38506673c";

    public required int SchemaVersion { get; init; }
    public required NativeSavedWorldOrigin Origin { get; init; }
    /// <summary>Saved-world pose +48 and shape-ID byte +98.</summary>
    public required NativeSavedWorldVehicleSlot Slot4 { get; init; }
    /// <summary>Saved-world pose +60 and shape-ID byte +99.</summary>
    public required NativeSavedWorldVehicleSlot Slot5 { get; init; }
    /// <summary>Absent older checkpoints have no inferred slot0 pose, camera or last return frame.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldFootReturn? FootReturn { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldCarReturn? CarReturn { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSavedWorldBoatReturn? BoatReturn { get; init; }
    /// <summary>Native New Game whole-save zeroing proves saved-world +110 initially0; missing historical serialization stays absent until an actual fresh loader consumes its original initialization proof.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? InitialMapDisplayMode110 { get; init; }

    public byte ReadMapDisplayModeForFreshFootLoader()
    {
        Validate();
        if (BoatReturn is NativeSavedWorldBoatReturn boat)
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
            return receipt.MovementMode110!.Value;
        }
        // Both validated origin kinds prove the native whole-save zero/init.out
        // chain before the first world visit. Only an executed fresh loader calls
        // this method; deserialization and existing WorldState restore do not.
        return InitialMapDisplayMode110 ?? 0;
    }

    public NativeSavedWorldState WithFootReturn(NativeSavedWorldFootReturn receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        Validate();
        receipt.Validate();
        return this with { FootReturn = receipt, CarReturn = null, BoatReturn = null };
    }

    public NativeSavedWorldState WithCarReturn(NativeSavedWorldCarReturn receipt)
    {
        Validate();
        receipt.Validate();
        if (Slot4.ShapeId != 73)
        {
            throw new InvalidDataException("The native car save requires its active shape73 vehicle slot.");
        }
        return this with { Slot4 = new NativeSavedWorldVehicleSlot(255, receipt.Pose), FootReturn = null, CarReturn = receipt, BoatReturn = null };
    }

    public NativeSavedWorldState WithBoatReturn(NativeSavedWorldBoatReturn receipt)
    {
        Validate();
        receipt.Validate();
        bool retainedCar = CarReturn is NativeSavedWorldCarReturn car && car.SourceWorldFrame == receipt.Load.CarSourceFrame && car.Pose.Yaw == receipt.Load.RetainedBodyYaw;
        bool retainedBoat = BoatReturn is NativeSavedWorldBoatReturn boat && boat.Load == receipt.Load;
        if (Slot4 != receipt.Load.Slot4 || Slot5 != receipt.Load.Slot5 || !retainedCar && !retainedBoat)
        {
            throw new InvalidDataException("Native boat save differs from its retained original object/vehicle records.");
        }
        return this with { FootReturn = null, CarReturn = null, BoatReturn = receipt };
    }

    public NativeSavedWorldState WithOrdinaryCarEntry()
    {
        Validate();
        if (Slot4.ShapeId is not 0 and not 255 || Slot5.ShapeId != 0)
        {
            throw new NotSupportedException("Native ordinary car entry would replace an unrepresented saved vehicle.");
        }
        return this with { Slot4 = Slot4 with { ShapeId = 73 }, CarReturn = null, BoatReturn = null };
    }

    public static NativeSavedWorldState CreateNewGame()
    {
        return CreateInitial(new NativeSavedWorldOrigin(CurrentProofVersion, NativeSavedWorldOriginKind.NativeNewGame, NewGameInitializationRule));
    }

    public static NativeSavedWorldState CreateVerifiedLegacyNewGame(string sha256)
    {
        if (!IsApprovedLegacyCheckpoint(sha256))
        {
            throw new InvalidDataException("The legacy checkpoint has no approved native New Game provenance.");
        }
        return CreateInitial(new NativeSavedWorldOrigin(CurrentProofVersion, NativeSavedWorldOriginKind.VerifiedLegacyNewGame, NewGameInitializationRule, sha256.ToLowerInvariant()));
    }

    public static bool IsApprovedLegacyCheckpoint(string? sha256)
    {
        return string.Equals(sha256, LegacyCheckpoint265Sha256, StringComparison.OrdinalIgnoreCase) || string.Equals(sha256, LegacyCheckpoint300Sha256, StringComparison.OrdinalIgnoreCase);
    }

    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion || Origin is null || Origin.ProofVersion != CurrentProofVersion || !Enum.IsDefined(Origin.Kind) || Origin.InitializationRule != NewGameInitializationRule)
        {
            throw new InvalidDataException("Unsupported or invalid native saved-world provenance.");
        }
        if (Origin.Kind == NativeSavedWorldOriginKind.NativeNewGame && Origin.LegacyCheckpointSha256 is not null || Origin.Kind == NativeSavedWorldOriginKind.VerifiedLegacyNewGame && !IsApprovedLegacyCheckpoint(Origin.LegacyCheckpointSha256))
        {
            throw new InvalidDataException("Invalid legacy checkpoint identity for native saved-world provenance.");
        }
        if (Slot4?.Pose is null || Slot5?.Pose is null)
        {
            throw new InvalidDataException("Native saved-world vehicle slots require their original poses.");
        }
        FootReturn?.Validate();
        CarReturn?.Validate();
        BoatReturn?.Validate();
        if (BoatReturn is not null && (FootReturn is not null || CarReturn is not null || Slot4 != BoatReturn.Load.Slot4 || Slot5 != BoatReturn.Load.Slot5))
        {
            throw new InvalidDataException("Native boat return disagrees with its retained saved vehicle slots.");
        }
        if (CarReturn is not null && (FootReturn is not null || Slot4.ShapeId != 255 || Slot4.Pose != CarReturn.Pose))
        {
            throw new InvalidDataException("Native car return disagrees with its retired vehicle slot.");
        }
        if (InitialMapDisplayMode110 is not null and not 0)
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
            InitialMapDisplayMode110 = 0
        };
    }
}
