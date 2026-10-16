using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeForestCameraResult(short Pitch, NativeWorldProcess Process);

/// <summary>Process-owned main-loop DWORD1A78C14 and camera cooldown DWORD204DAE0, outside the saved-game block.</summary>
public sealed record NativeWorldProcess([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] uint MainLoopCounter, [property: JsonRequired] long MainLoopCallbacks, [property: JsonRequired] int CameraCooldown, [property: JsonRequired] int CarCameraFollowSpeed)
{
    public const string CurrentRule = "ff8-en-4022b0-557a90-process-v1";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldVerticalBiasState? VerticalBias204DADC { get; init; }

    [JsonIgnore]
    public bool IsColdProcess => MainLoopCounter == 0 && MainLoopCallbacks == 0 && CameraCooldown == 0 && CarCameraFollowSpeed == 0;

    public static NativeWorldProcess FromColdProcess()
    {
        // Both DWORDs are in the preserved executable's zero-filled PE.data virtual tail.
        return new NativeWorldProcess(1, CurrentRule, 0, 0, 0, 0) { VerticalBias204DADC = NativeWorldVerticalBiasState.Exact(0) };
    }

    public void Validate()
    {
        VerticalBias204DADC?.Validate();
        if (Version != 1 || Rule != CurrentRule || MainLoopCallbacks < 0 || MainLoopCounter != unchecked((uint)MainLoopCallbacks) || CarCameraFollowSpeed is < 0 or > 128)
        {
            throw new InvalidDataException("Native world process state lacks its exact cold-process callback history.");
        }
    }

    public NativeWorldProcess AdvanceMainLoopCallbacks(long callbacks)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(callbacks);
        return this with { MainLoopCounter = unchecked(MainLoopCounter + (uint)callbacks), MainLoopCallbacks = checked(MainLoopCallbacks + callbacks) };
    }

    public NativeForestCameraResult StepNormalFootCamera(short pitch, byte groundType, bool collision = false, bool held8 = false, bool held11 = false, bool held13 = false)
    {
        return StepOrdinaryCamera(pitch, groundType, 0, collision, held8, held11, held13);
    }

    public NativeForestCameraResult StepOrdinaryCamera(short pitch, byte groundType, int entityType, bool collision = false, bool held8 = false, bool held11 = false, bool held13 = false)
    {
        Validate();
        if (entityType is not (>= 0 and < 10 or 128 or 49 or >= 32 and <= 40 or 132 or 48))
        {
            throw new NotSupportedException("Ordinary terrain camera requires its native foot or ground-vehicle entity branch.");
        }
        int cooldown = CameraCooldown;
        bool forestPitch = groundType <= 5 && entityType is >= 0 and < 10 or 128 or 49;
        if (forestPitch || collision)
        {
            // Native557A90 writes the low WORD before its signed clamp.
            pitch = unchecked((short)(pitch - 6));
            pitch = (short)Math.Clamp((int)pitch, -512, 0);
            if (!forestPitch && collision)
            {
                cooldown = 60;
            }
        }
        else if (cooldown <= 0 && pitch < -112)
        {
            // Native permits a final +4 step to overshoot the -112 baseline.
            pitch = unchecked((short)(pitch + 4));
            cooldown = unchecked(cooldown - 1);
        }
        if (held8)
        {
            cooldown = unchecked(cooldown - 2);
        }
        if (held11)
        {
            cooldown = unchecked(cooldown - 2);
        }
        if (held13)
        {
            cooldown = unchecked(cooldown - 10);
        }
        return new NativeForestCameraResult(pitch, this with { CameraCooldown = cooldown });
    }
}
