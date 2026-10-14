using System.Runtime.InteropServices;

namespace Memoria.FF8;

internal static class WindowsAudioCom
{
    public static T Method<T>(nint instance, int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));
    public static void Check(int result) => Marshal.ThrowExceptionForHR(result);

    public static nint Create(Guid classId, Guid interfaceId)
    {
        Check(CoCreateInstance(ref classId, 0, 1, ref interfaceId, out nint instance));
        return instance;
    }

    public static void Release(ref nint instance)
    {
        if (instance != 0)
        {
            Marshal.Release(instance);
            instance = 0;
        }
    }

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(ref Guid classId, nint outer, uint context, ref Guid interfaceId, out nint instance);
}
