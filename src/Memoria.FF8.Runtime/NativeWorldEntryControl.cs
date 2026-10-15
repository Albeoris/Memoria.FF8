namespace Memoria.FF8.Runtime;

/// <summary>53F310 first-callback flag and53FAC0 pre-input203FD54 decrement, derived from actual local callback age.</summary>
public static class NativeWorldEntryControl
{
    public static int DelayAfterCallback(long completedWorldCallbacks, byte nativeLoaderMode)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(completedWorldCallbacks);
        if (nativeLoaderMode > 4)
        {
            throw new NotSupportedException("Native world entry control requires its executed2036B4C loader mode0..4.");
        }
        //4706B0 field-origin entry writes loader mode0.53F310 sets first=1,
        //and53FAC0 first selects24 (mode4:8), then decrements before input.
        int duration = nativeLoaderMode == 4 ? 8 : 24;
        return duration - (int)Math.Min(completedWorldCallbacks, duration);
    }
}
