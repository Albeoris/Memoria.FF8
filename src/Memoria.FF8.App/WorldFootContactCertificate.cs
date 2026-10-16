using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

public enum WorldFootContactRefusal
{
    UnknownSavedWorld,
    InvalidSavedWorld,
    UnsupportedStory,
    ActiveObjectPairs,
    UnsupportedSavedVehicle,
    PlayerXOutsideRegion,
    PlayerZOutsideRegion,
    InvalidCameraEye,
    CameraEyeOutsideRegion
}

/// <summary>A local object/train exclusion for original Squall foot types0/1, not a terrain or script certificate.</summary>
public sealed record WorldFootContactProof(int RuleVersion, ushort StoryWord, int PlayerX, int PlayerRawZ, double CameraEyeX, NativeSavedWorldOrigin SavedWorldOrigin);

public sealed record WorldFootContactCertificateResult(WorldFootContactProof? Certificate, WorldFootContactRefusal? Refusal, string? Reason)
{
    public bool Certified => Certificate is not null;
}

/// <summary>Pure native contact exclusion for the supported original normal and uniform Squall allocation clauses.</summary>
public static class WorldFootContactCertificate
{
    public const int RuleVersion = 1;
    public const int CenterX = 24576;
    public const int CenterRawZ = -29406;
    public const int PlayerRadius = 2048;
    public const int CameraEyeXRadius = 16384;

    public static WorldFootContactCertificateResult Evaluate(NativeSavedWorldState? savedWorld, byte event256, byte event257, byte event264, int playerX, int playerRawZ, double cameraEyeX, bool allowTravel = false, byte entityType = 0)
    {
        if (savedWorld is null)
        {
            return Refuse(WorldFootContactRefusal.UnknownSavedWorld, "Native saved-world vehicle provenance is unknown.");
        }
        try
        {
            savedWorld.Validate();
        }
        catch (InvalidDataException exception)
        {
            return Refuse(WorldFootContactRefusal.InvalidSavedWorld, exception.Message);
        }

        ushort storyWord = (ushort)(event256 | event257 << 8);
        bool postGraduation = entityType == 0 && storyWord is >= 205 and < 290;
        if (entityType == 0 ? storyWord is >= 34 and < 205 or >= 290 : entityType != 1 || storyWord is < 34 or >= 205)
        {
            return Refuse(WorldFootContactRefusal.UnsupportedStory, "The native setup requires type0/story [0,34) or [205,290), or type1/story [34,205).");
        }
        if ((event264 & 7) != 0)
        {
            return Refuse(WorldFootContactRefusal.ActiveObjectPairs, "Native byte264 enables object pairs outside this contact exclusion.");
        }
        bool supportedVehicles = entityType == 1 ? (savedWorld.Slot4.ShapeId is 0 or 73 or 255) && (savedWorld.Slot5.ShapeId is 0 or 78) : savedWorld.Slot4.ShapeId == 0 && savedWorld.Slot5.ShapeId == 0 || postGraduation && savedWorld.Slot4.ShapeId == 255 && savedWorld.Slot5.ShapeId == 0;
        if (!supportedVehicles)
        {
            return Refuse(WorldFootContactRefusal.UnsupportedSavedVehicle, "Saved vehicle shapes lie outside the proved original foot allocation clause.");
        }
        if (allowTravel && (playerX is < -131072 or >= 131072 || playerRawZ is < -98304 or >= 98304))
        {
            return Refuse(WorldFootContactRefusal.PlayerXOutsideRegion, "Native travel coordinates must already be wrapped into the original X/Z world spans.");
        }
        if (!allowTravel && Math.Abs((long)playerX - CenterX) > PlayerRadius)
        {
            return Refuse(WorldFootContactRefusal.PlayerXOutsideRegion, "Player X is outside the proved Garden-road region.");
        }
        if (!allowTravel && Math.Abs((long)playerRawZ - CenterRawZ) > PlayerRadius)
        {
            return Refuse(WorldFootContactRefusal.PlayerZOutsideRegion, "Player raw Z is outside the proved Garden-road region.");
        }
        if (!double.IsFinite(cameraEyeX))
        {
            return Refuse(WorldFootContactRefusal.InvalidCameraEye, "The actual camera eye X must be finite.");
        }
        if (allowTravel ? !ExcludesOriginalTrainRoutes(cameraEyeX) : Math.Abs(cameraEyeX - CenterX) > CameraEyeXRadius)
        {
            return Refuse(WorldFootContactRefusal.CameraEyeOutsideRegion, "The actual camera eye X is outside the native train-block exclusion envelope.");
        }
        if (entityType == 1 && savedWorld.Slot4.ShapeId == 73 && !ExcludesOriginalStaticVehicle(cameraEyeX, savedWorld.Slot4.Pose.X))
        {
            return Refuse(WorldFootContactRefusal.UnsupportedSavedVehicle, "The active saved car may be presented or contacted inside the native camera block envelope.");
        }

        // Clauses0/1/2 create controlled shapes0/4/0;548CD0 skips that selected record
        // and94. Mode0/203FD60=0 inspects slot4 only; boat shape78 is not a car,
        // so the native saved-vehicle append branch never adds it from slot5.
        var proof = new WorldFootContactProof(postGraduation ? 4 : entityType == 1 ? 3 : allowTravel ? 2 : RuleVersion, storyWord, playerX, playerRawZ, cameraEyeX, savedWorld.Origin);
        return new WorldFootContactCertificateResult(proof, null, null);
    }

    internal static bool ExcludesOriginalTrainRoutes(double cameraEyeX)
    {
        if (!double.IsFinite(cameraEyeX) || cameraEyeX is < -131072 or >= 131072)
        {
            return false;
        }
        // 5470E0/53DD10 compare wrapped 2048-unit X cells against the camera.
        // Include adjacent camera cells to cover continuous/fixed matrix rounding.
        int cameraCell = (int)Math.Floor((cameraEyeX + 131072) / 2048);
        (int Minimum, int Maximum)[] routes = [(-28130, -17477), (-61703, -21000)];
        foreach ((int minimum, int maximum) in routes)
        {
            int first = (minimum + 131072) / 2048;
            int last = (maximum + 131072) / 2048;
            for (int carCell = first; carCell <= last; carCell++)
            {
                for (int roundingCell = -1; roundingCell <= 1; roundingCell++)
                {
                    int difference = carCell - ((cameraCell + roundingCell + 128) % 128);
                    if (difference > 64)
                    {
                        difference -= 128;
                    }
                    else if (difference < -64)
                    {
                        difference += 128;
                    }
                    if (Math.Abs(difference) <= 5)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    internal static bool ExcludesOriginalTrainRoutes(NativeWorldPathProjectionCertificate projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        // Original rail.obj routes0/1;545B20 interpolation stays inside these
        // endpoint bounds.
        (int MinimumX, int MaximumX, int MinimumZ, int MaximumZ)[] routes = [(-28130, -17477, 6490, 43516), (-61703, -21000, 5871, 32258)];
        foreach ((int minimumX, int maximumX, int minimumZ, int maximumZ) in routes)
        {
            int firstX = (minimumX + 131072) / 2048;
            int lastX = (maximumX + 131072) / 2048;
            int firstZ = (98304 - maximumZ) / 2048;
            int lastZ = (98304 - minimumZ) / 2048;
            for (int x = firstX; x <= lastX; x++)
            {
                for (int z = firstZ; z <= lastZ; z++)
                {
                    // One representative per native53DC70 cell, including both
                    // interval ends. IsNear owns544490 rounding and128/96 wrap;
                    // later signed-WORD collision aliases cannot bypass this gate.
                    NativeWorldPathPosition representative = new(x * 2048 - 131072, 0, 98304 - z * 2048);
                    try
                    {
                        if (projection.IsNear(representative))
                        {
                            return false;
                        }
                    }
                    catch (NotSupportedException)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    private static bool ExcludesOriginalStaticVehicle(double cameraEyeX, int vehicleX)
    {
        if (vehicleX is < -131072 or >= 131072)
        {
            return false;
        }
        int cameraCell = (int)Math.Floor((cameraEyeX + 131072) / 2048);
        int vehicleCell = (vehicleX + 131072) / 2048;
        for (int roundingCell = -1; roundingCell <= 1; roundingCell++)
        {
            int difference = vehicleCell - ((cameraCell + roundingCell + 128) % 128);
            if (difference > 64)
            {
                difference -= 128;
            }
            else if (difference < -64)
            {
                difference += 128;
            }
            if (Math.Abs(difference) <= 5)
            {
                return false;
            }
        }
        return true;
    }

    private static WorldFootContactCertificateResult Refuse(WorldFootContactRefusal refusal, string reason)
    {
        return new WorldFootContactCertificateResult(null, refusal, reason);
    }
}
