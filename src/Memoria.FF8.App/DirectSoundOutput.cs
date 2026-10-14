using System.Runtime.InteropServices;

namespace Memoria.FF8;

internal sealed class DirectSoundOutput : IDisposable
{
    private nint device;

    public DirectSoundOutput(nint window)
    {
        WindowsAudioCom.Check(DirectSoundCreate8(0, out device, 0));
        WindowsAudioCom.Check(WindowsAudioCom.Method<CooperativeLevel>(device, 6)(device, window, 1));
    }

    public Buffer Create(byte[] pcm, int rate, int channels)
    {
        if (pcm.Length == 0 || channels is not (1 or 2) || pcm.Length % (channels * 2) != 0)
        {
            throw new InvalidDataException("DirectSound requires aligned PCM16 mono or stereo samples.");
        }
        byte[] format = new byte[18];
        using (var writer = new BinaryWriter(new MemoryStream(format)))
        {
            writer.Write((ushort)1);
            writer.Write((ushort)channels);
            writer.Write(rate);
            writer.Write(checked(rate * channels * 2));
            writer.Write((ushort)(channels * 2));
            writer.Write((ushort)16);
            writer.Write((ushort)0);
        }
        GCHandle pinned = GCHandle.Alloc(format, GCHandleType.Pinned);
        nint buffer = 0;
        try
        {
            var description = new BufferDescription { Size = (uint)Marshal.SizeOf<BufferDescription>(), Flags = 0x8000 | 0x80 | 0x40 | 0x20, Bytes = (uint)pcm.Length, Format = pinned.AddrOfPinnedObject() };
            WindowsAudioCom.Check(WindowsAudioCom.Method<CreateBuffer>(device, 3)(device, ref description, out buffer, 0));
            WindowsAudioCom.Check(WindowsAudioCom.Method<LockBuffer>(buffer, 11)(buffer, 0, (uint)pcm.Length, out nint first, out uint firstLength, out nint second, out uint secondLength, 0));
            try
            {
                Marshal.Copy(pcm, 0, first, checked((int)firstLength));
                if (secondLength != 0)
                {
                    Marshal.Copy(pcm, checked((int)firstLength), second, checked((int)secondLength));
                }
            }
            finally
            {
                WindowsAudioCom.Check(WindowsAudioCom.Method<UnlockBuffer>(buffer, 19)(buffer, first, firstLength, second, secondLength));
            }
            return new Buffer(buffer);
        }
        catch
        {
            WindowsAudioCom.Release(ref buffer);
            throw;
        }
        finally
        {
            pinned.Free();
        }
    }

    public void Dispose() => WindowsAudioCom.Release(ref device);

    internal sealed class Buffer(nint instance) : IDisposable
    {
        private nint handle = instance;
        public void Play(bool loop) => WindowsAudioCom.Check(WindowsAudioCom.Method<PlayBuffer>(handle, 12)(handle, 0, 0, loop ? 1u : 0u));
        public void Stop() => WindowsAudioCom.Check(WindowsAudioCom.Method<NoArguments>(handle, 18)(handle));
        public void Volume(int hundredthDecibels) => WindowsAudioCom.Check(WindowsAudioCom.Method<SetValue>(handle, 15)(handle, hundredthDecibels));
        public void Pan(int hundredthDecibels) => WindowsAudioCom.Check(WindowsAudioCom.Method<SetValue>(handle, 16)(handle, hundredthDecibels));
        public void Dispose()
        {
            if (handle != 0)
            {
                try
                {
                    Stop();
                }
                finally
                {
                    WindowsAudioCom.Release(ref handle);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BufferDescription
    {
        public uint Size;
        public uint Flags;
        public uint Bytes;
        public uint Reserved;
        public nint Format;
        public Guid Algorithm;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CooperativeLevel(nint self, nint window, uint level);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateBuffer(nint self, ref BufferDescription description, out nint buffer, nint outer);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int LockBuffer(nint self, uint offset, uint bytes, out nint first, out uint firstLength, out nint second, out uint secondLength, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int UnlockBuffer(nint self, nint first, uint firstLength, nint second, uint secondLength);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int PlayBuffer(nint self, uint reserved, uint priority, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetValue(nint self, int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int NoArguments(nint self);

    [DllImport("dsound.dll")]
    private static extern int DirectSoundCreate8(nint guid, out nint device, nint outer);
}
