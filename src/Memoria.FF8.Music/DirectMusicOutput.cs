using System.Runtime.InteropServices;

namespace Memoria.FF8;

/// <summary>Windows' installed DirectMusic synthesizer plays the original SGT and FF8.dls assets.</summary>
internal sealed class DirectMusicOutput : IDisposable
{
    private nint loader;
    private nint performance;
    private nint collection;
    private nint segment;
    private nint segmentState;
    private bool paused;
    private bool uninitialize;

    public DirectMusicOutput(nint window, string directory)
    {
        int initialized = CoInitializeEx(0, 2);
        WindowsAudioCom.Check(initialized);
        uninitialize = true;
        try
        {
            loader = WindowsAudioCom.Create(new Guid("d2ac2892-b39b-11d1-8704-00600893b1bd"), new Guid("19e7c08c-0a44-4e6a-a116-595a7cd5de8c"));
            performance = WindowsAudioCom.Create(new Guid("d2ac2881-b39b-11d1-8704-00600893b1bd"), new Guid("679c4137-c62e-4147-b2b4-9d569acb254c"));
            WindowsAudioCom.Check(WindowsAudioCom.Method<InitAudio>(performance, 44)(performance, 0, 0, window, 1, 64, 0, 0));
            Guid allTypes = new("d2ac2893-b39b-11d1-8704-00600893b1bd");
            WindowsAudioCom.Check(WindowsAudioCom.Method<SearchDirectory>(loader, 5)(loader, ref allTypes, directory, 0));
            collection = Load(new Guid("480ff4b0-28b2-11d1-bef7-00c04fbf8fef"), new Guid("d2ac287c-b39b-11d1-8704-00600893b1bd"), Path.Combine(directory, "FF8.dls"));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private nint Load(Guid classId, Guid interfaceId, string path)
    {
        WindowsAudioCom.Check(WindowsAudioCom.Method<LoadFile>(loader, 14)(loader, ref classId, ref interfaceId, path, out nint result));
        return result;
    }

    public void Play(string path, int volume, int startPoint = 0)
    {
        Stop();
        segment = Load(new Guid("d2ac2882-b39b-11d1-8704-00600893b1bd"), new Guid("c6784488-41a3-418f-aa15-b35093ba42d4"), path);
        WindowsAudioCom.Check(WindowsAudioCom.Method<ObjectArgument>(segment, 29)(segment, performance));
        WindowsAudioCom.Check(WindowsAudioCom.Method<IntegerArgument>(segment, 21)(segment, startPoint));
        Volume(volume);
        WindowsAudioCom.Check(WindowsAudioCom.Method<PlaySegment>(performance, 4)(performance, segment, 2048, 0, out segmentState));
    }

    public bool IsPlaying => segment != 0 && WindowsAudioCom.Method<Playing>(performance, 14)(performance, segment, 0) == 0;

    public void Pause()
    {
        if (segment == 0 || paused)
        {
            return;
        }
        if (segmentState == 0)
        {
            throw new InvalidOperationException("DirectMusic pause requires the current segment state.");
        }
        //46F300 retains the musical cursor on the segment before releasing its current state.
        WindowsAudioCom.Check(WindowsAudioCom.Method<ReadMusicTime>(segmentState, 5)(segmentState, out int startTime));
        WindowsAudioCom.Check(WindowsAudioCom.Method<ReadMusicTime>(segmentState, 7)(segmentState, out int startPoint));
        WindowsAudioCom.Check(WindowsAudioCom.Method<StopSegment>(performance, 5)(performance, 0, 0, 0, 0));
        WindowsAudioCom.Check(WindowsAudioCom.Method<GetTime>(performance, 15)(performance, 0, out int currentTime));
        WindowsAudioCom.Check(WindowsAudioCom.Method<IntegerArgument>(segment, 21)(segment, unchecked(currentTime - startTime + startPoint)));
        WindowsAudioCom.Release(ref segmentState);
        paused = true;
    }

    public void Resume()
    {
        if (segment == 0 || !paused)
        {
            return;
        }
        //46F4F0 starts the retained segment at46F300's saved musical cursor.
        WindowsAudioCom.Check(WindowsAudioCom.Method<PlaySegment>(performance, 4)(performance, segment, 0x4000, 0, out segmentState));
        paused = false;
    }

    public void Volume(int hundredthDecibels)
    {
        Guid masterVolume = new("d2ac28b1-b39b-11d1-8704-00600893b1bd");
        WindowsAudioCom.Check(WindowsAudioCom.Method<GlobalParameter>(performance, 34)(performance, ref masterVolume, ref hundredthDecibels, 4));
    }

    public void Stop()
    {
        if (segment != 0)
        {
            WindowsAudioCom.Check(WindowsAudioCom.Method<StopSegment>(performance, 5)(performance, segment, 0, 0, 0));
            WindowsAudioCom.Check(WindowsAudioCom.Method<ObjectArgument>(segment, 30)(segment, performance));
            WindowsAudioCom.Release(ref segment);
        }
        WindowsAudioCom.Release(ref segmentState);
        paused = false;
    }

    public void Dispose()
    {
        Stop();
        WindowsAudioCom.Release(ref collection);
        if (performance != 0)
        {
            WindowsAudioCom.Check(WindowsAudioCom.Method<NoArguments>(performance, 38)(performance));
            WindowsAudioCom.Release(ref performance);
        }
        WindowsAudioCom.Release(ref loader);
        if (uninitialize)
        {
            CoUninitialize();
            uninitialize = false;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int InitAudio(nint self, nint music, nint sound, nint window, uint pathType, uint channels, uint flags, nint parameters);
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)] private delegate int SearchDirectory(nint self, ref Guid type, string path, int clear);
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)] private delegate int LoadFile(nint self, ref Guid classId, ref Guid interfaceId, string path, out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ObjectArgument(nint self, nint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int IntegerArgument(nint self, int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int PlaySegment(nint self, nint segment, uint flags, long start, out nint state);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int StopSegment(nint self, nint segment, nint state, int time, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Playing(nint self, nint segment, nint state);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ReadMusicTime(nint self, out int time);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetTime(nint self, nint referenceTime, out int musicTime);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GlobalParameter(nint self, ref Guid type, ref int value, uint bytes);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int NoArguments(nint self);

    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
}
