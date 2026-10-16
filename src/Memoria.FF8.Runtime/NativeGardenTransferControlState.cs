using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeGardenReboardCompletion(NativeGardenTransferControlState Controls, short Pitch, int Speed, int SpeedParameter, short OffsetY, short OffsetZ);

/// <summary>Post-loader54A7F0 gates, retained until an executed world transition changes their owner.</summary>
public sealed record NativeGardenTransferControlState([property: JsonRequired] int Version, [property: JsonRequired] string Rule, [property: JsonRequired] int TransitionPhase, [property: JsonRequired] int ActiveWorldScript, [property: JsonRequired] bool ViewToggleBlocked)
{
    public const string CurrentRule = "ff8-en-garden-load-control-542b90-53f310-559240-v1";

    public static NativeGardenTransferControlState CreateAfterOrdinaryLoad(NativeGardenWorldAllocation allocation)
    {
        allocation.Validate();
        // 542B90 resets phase;544860's ordinary packet selects mode0,
        // so54CC60 leaves its pointer clear.53F310 either retains script=-1
        // or drains543340 until its completion writes script=-1 again.
        return new NativeGardenTransferControlState(1, CurrentRule, 0, -1, false);
    }

    public void Validate()
    {
        if (Version != 1 || Rule != CurrentRule || TransitionPhase is not (0 or int.MinValue) || ActiveWorldScript != -1 || ViewToggleBlocked)
        {
            throw new NotSupportedException("Garden transfer controls reached an unrepresented world script, camera path or transition phase.");
        }
    }

    public NativeGardenTransferInput PrepareInput(uint buttons, int entity, long completedWorldCallbacks, NativeGardenTransferState? transfer, NativeWorldEntryMemory memory)
    {
        Validate();
        memory.Validate();
        transfer?.Validate();
        byte mode = transfer is { Counter: < 31 } ? (byte)8 : (byte)0;
        // 559240 clears activity every callback.53FAC0 decrements its
        // entry hold before54A7F0.544860 copied event264 into2036BD6.
        return new NativeGardenTransferInput(buttons, 0, entity, mode, TransitionPhase, false, ActiveWorldScript, NativeWorldEntryControl.DelayAfterCallback(completedWorldCallbacks, 0), memory.ReplacementFlags264, ViewToggleBlocked);
    }

    public NativeGardenTransferControlState BeginLowering()
    {
        Validate();
        return this with { TransitionPhase = int.MinValue };
    }

    public NativeGardenTransferControlState AfterLowering()
    {
        Validate();
        // First54B460 case8 replaces its negative first-execution marker
        // with0; its counter keeps subsequent callbacks distinct.
        return this with { TransitionPhase = 0 };
    }

    public NativeGardenReboardCompletion CompleteReboarding(int actualProjectionDistance, int actualFarClip, short currentPitch, short currentOffsetY, short currentOffsetZ, NativeGardenCameraDefaults defaults)
    {
        Validate();
        defaults.Validate();
        // 54B460 applies the accepted boarding packet, then558D70 maps
        // its negative marker to183. Certify all active bits completing
        // in that same callback; a longer transition needs its live owner.
        if (Math.Abs((long)actualProjectionDistance - 1024) > 8 || Math.Abs((long)actualFarClip - 5216) > 128 || Math.Abs((int)unchecked((short)(currentPitch - defaults.Pitch))) > 16 || Math.Abs((int)unchecked((short)(currentOffsetY - defaults.OffsetY))) > 512 || Math.Abs((int)unchecked((short)(currentOffsetZ - defaults.OffsetZ))) > 512)
        {
            throw new NotSupportedException("Garden reboarding reached a multi-callback558D70 camera transition requiring its actual projection/camera phase owner.");
        }
        return new NativeGardenReboardCompletion(this with { TransitionPhase = 0 }, defaults.Pitch, 0, 32, defaults.OffsetY, defaults.OffsetZ);
    }
}
