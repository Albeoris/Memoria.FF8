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

/// <summary>A local object/train contact exclusion for normal Squall world type 0, not a terrain or script certificate.</summary>
public sealed record WorldFootContactProof(int RuleVersion, ushort StoryWord, int PlayerX, int PlayerRawZ, double CameraEyeX, NativeSavedWorldOrigin SavedWorldOrigin);

public sealed record WorldFootContactCertificateResult(WorldFootContactProof? Certificate, WorldFootContactRefusal? Refusal, string? Reason)
{
    public bool Certified => Certificate is not null;
}

/// <summary>Pure native contact exclusion within the audited Garden-road region. The caller must establish normal Squall type 0.</summary>
public static class WorldFootContactCertificate
{
    public const int RuleVersion = 1;
    public const int CenterX = 24576;
    public const int CenterRawZ = -29406;
    public const int PlayerRadius = 2048;
    public const int CameraEyeXRadius = 16384;

    public static WorldFootContactCertificateResult Evaluate(NativeSavedWorldState? savedWorld, byte event256, byte event257, byte event264, int playerX, int playerRawZ, double cameraEyeX, bool allowTravel = false)
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
        if (storyWord >= 34)
        {
            return Refuse(WorldFootContactRefusal.UnsupportedStory, "The native setup clause must have story WORD in [0,34).");
        }
        if ((event264 & 7) != 0)
        {
            return Refuse(WorldFootContactRefusal.ActiveObjectPairs, "Native byte264 enables object pairs outside this contact exclusion.");
        }
        if (savedWorld.Slot4.ShapeId != 0 || savedWorld.Slot5.ShapeId != 0)
        {
            return Refuse(WorldFootContactRefusal.UnsupportedSavedVehicle, "This contact exclusion requires both native saved vehicle shape IDs to be zero.");
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

        // Clause 0 creates the excluded player; type 94 is skipped. Routes 0/1 fail
        // native camera-block proximity here regardless of their unrepresented phase.
        var proof = new WorldFootContactProof(allowTravel ? 2 : RuleVersion, storyWord, playerX, playerRawZ, cameraEyeX, savedWorld.Origin);
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

    private static WorldFootContactCertificateResult Refuse(WorldFootContactRefusal refusal, string reason)
    {
        return new WorldFootContactCertificateResult(null, refusal, reason);
    }
}
