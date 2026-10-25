using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Memoria.FF8.Runtime;

/// <summary>Steamworks SDK 1.65 flat API and manual dispatch ABI; all image loading uses an explicit path.</summary>
internal sealed class SteamAchievementNativeApi : ISteamAchievementApi
{
    private const string ImageHash = "e6d9bafb9a41e42fba7b21553db49f8719027af3f0deb23ff86cfc44d60e776d";
    private static int activeInstance;
    private readonly string path;
    private readonly Dictionary<string, Delegate> exports = new(StringComparer.Ordinal);
    private IntPtr library;
    private bool ownsProcess;
    private bool initialized;
    private bool disposed;
    private IntPtr user;
    private IntPtr stats;
    private IntPtr utils;
    private int pipe;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InitializeFlat(IntPtr error);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void VoidExport();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int IntExport();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr InterfaceExport();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint GetAppId(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate ulong GetUserId(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool BooleanMethod(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void PipeMethod(int pipe);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool NextCallback(int pipe, out CallbackMessage message);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool AchievementRead(IntPtr self, [MarshalAs(UnmanagedType.LPStr)] string key, [MarshalAs(UnmanagedType.I1)] out bool value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool StatRead(IntPtr self, [MarshalAs(UnmanagedType.LPStr)] string key, out int value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool AchievementWrite(IntPtr self, [MarshalAs(UnmanagedType.LPStr)] string key);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool StatWrite(IntPtr self, [MarshalAs(UnmanagedType.LPStr)] string key, int value);

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct CallbackMessage
    {
        public int User;
        public int Callback;
        public IntPtr Data;
        public int Bytes;
    }

    public SteamAchievementNativeApi(string nativePath)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess)
        {
            throw new BadImageFormatException("The bundled Steam achievement SDK requires Windows x64.");
        }
        path = Path.GetFullPath(nativePath);
        using (FileStream stream = File.OpenRead(path))
        {
            if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(ImageHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The bundled Steam achievement SDK image does not match its verified descriptor.");
            }
            stream.Position = 0;
            using PEReader image = new(stream);
            if (image.PEHeaders.CoffHeader.Machine != Machine.Amd64)
            {
                throw new BadImageFormatException("The bundled Steam achievement SDK is not an AMD64 image.");
            }
        }
    }

    public bool Initialize(string product, out string? diagnostic)
    {
        if (Interlocked.CompareExchange(ref activeInstance, 1, 0) != 0)
        {
            throw new InvalidOperationException("A Steam SDK lifetime already owns this process.");
        }
        ownsProcess = true;
        Environment.SetEnvironmentVariable("SteamAppId", product);
        Environment.SetEnvironmentVariable("SteamGameId", product);
        library = NativeLibrary.Load(path);
        IntPtr error = Marshal.AllocHGlobal(1024);
        try
        {
            Marshal.Copy(new byte[1024], 0, error, 1024);
            int result = Export<InitializeFlat>("SteamAPI_InitFlat")(error);
            if (result != 0)
            {
                diagnostic = "Steam initialization failed (" + result + "): " + Marshal.PtrToStringUTF8(error);
                return false;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(error);
        }
        initialized = true;
        Export<VoidExport>("SteamAPI_ManualDispatch_Init")();
        pipe = Export<IntExport>("SteamAPI_GetHSteamPipe")();
        user = Export<InterfaceExport>("SteamAPI_SteamUser_v023")();
        stats = Export<InterfaceExport>("SteamAPI_SteamUserStats_v013")();
        utils = Export<InterfaceExport>("SteamAPI_SteamUtils_v011")();
        if (pipe == 0 || user == IntPtr.Zero || stats == IntPtr.Zero || utils == IntPtr.Zero)
        {
            diagnostic = "Steam could not provide the verified SDK interfaces.";
            return false;
        }
        diagnostic = null;
        return true;
    }

    public uint AppId => Export<GetAppId>("SteamAPI_ISteamUtils_GetAppID")(utils);
    public ulong UserId => Export<GetUserId>("SteamAPI_ISteamUser_GetSteamID")(user);
    public bool LoggedOn => Export<BooleanMethod>("SteamAPI_ISteamUser_BLoggedOn")(user);

    public IReadOnlyList<SteamAchievementCallback> Pump()
    {
        List<SteamAchievementCallback> callbacks = [];
        Export<PipeMethod>("SteamAPI_ManualDispatch_RunFrame")(pipe);
        NextCallback next = Export<NextCallback>("SteamAPI_ManualDispatch_GetNextCallback");
        PipeMethod free = Export<PipeMethod>("SteamAPI_ManualDispatch_FreeLastCallback");
        for (int count = 0; count < 256 && next(pipe, out CallbackMessage message); count++)
        {
            try
            {
                if (message.Data == IntPtr.Zero)
                {
                    continue;
                }
                // SDK 1.65 uses pack8: received game0/result8/user16; stored game0/result8.
                if (message.Callback == 1101 && message.Bytes == 24)
                {
                    callbacks.Add(new(SteamAchievementCallbackKind.StatsReceived, unchecked((ulong)Marshal.ReadInt64(message.Data)), Marshal.ReadInt32(message.Data, 8), unchecked((ulong)Marshal.ReadInt64(message.Data, 16))));
                }
                else if (message.Callback == 1102 && message.Bytes == 16)
                {
                    callbacks.Add(new(SteamAchievementCallbackKind.StatsStored, unchecked((ulong)Marshal.ReadInt64(message.Data)), Marshal.ReadInt32(message.Data, 8)));
                }
                // Achievement progress callback 1103 cannot acknowledge persistence.
            }
            finally
            {
                free(pipe);
            }
        }
        return callbacks;
    }

    public bool GetAchievement(string key, out bool unlocked) => Export<AchievementRead>("SteamAPI_ISteamUserStats_GetAchievement")(stats, key, out unlocked);
    public bool GetStat(string key, out int value) => Export<StatRead>("SteamAPI_ISteamUserStats_GetStatInt32")(stats, key, out value);
    public bool SetAchievement(string key) => Export<AchievementWrite>("SteamAPI_ISteamUserStats_SetAchievement")(stats, key);
    public bool SetStat(string key, int value) => Export<StatWrite>("SteamAPI_ISteamUserStats_SetStatInt32")(stats, key, value);
    public bool StoreStats() => Export<BooleanMethod>("SteamAPI_ISteamUserStats_StoreStats")(stats);
    private T Export<T>(string name) where T : Delegate
    {
        if (!exports.TryGetValue(name, out Delegate? function))
        {
            function = Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));
            exports.Add(name, function);
        }
        return (T)function;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        try
        {
            if (initialized)
            {
                initialized = false;
                Export<VoidExport>("SteamAPI_Shutdown")();
            }
        }
        finally
        {
            if (library != IntPtr.Zero)
            {
                NativeLibrary.Free(library);
                library = IntPtr.Zero;
            }
            if (ownsProcess)
            {
                ownsProcess = false;
                Interlocked.Exchange(ref activeInstance, 0);
            }
        }
    }
}
