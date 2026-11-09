using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeCameraShakeTests
{
    public static void Run()
    {
        byte[] random = new byte[256];
        random[128] = 128;
        random[129] = 255;
        random[0] = 64;
        random[127] = 64;
        Ff7NativeMovement interpolation = Interpolation();
        Ff7CameraShakeAxis axis = Ff7NativeCameraShake.Configure(new(), true, 16, 2);
        axis = Ff7NativeCameraShake.Advance(axis, random, interpolation);
        Equal(new Ff7CameraShakeAxis(true, 1, 1, 0, 16, 0, 8, 2, 0), axis);
        axis = Ff7NativeCameraShake.Advance(axis, random, interpolation);
        Equal((sbyte)4, axis.Offset);
        Ff7CameraShakeAxis disabled = Ff7NativeCameraShake.Configure(axis, false, 0, 0);
        Equal((sbyte)4, disabled.Offset);
        foreach (sbyte expected in new sbyte[] { 8, 4, 4, 4, 0, 0 })
        {
            disabled = Ff7NativeCameraShake.Advance(disabled, random, interpolation);
            Equal(expected, disabled.Offset);
        }
        Equal((byte)2, disabled.RandomIndex);
        Equal((byte)0, disabled.Phase);
        axis = Ff7NativeCameraShake.Advance(axis, random, interpolation);
        Equal((sbyte)8, axis.Offset);
        axis = Ff7NativeCameraShake.Advance(axis, random, interpolation);
        Equal((sbyte)4, axis.Offset);
        axis = Ff7NativeCameraShake.Advance(axis, random, interpolation);
        Equal((short)-15, axis.Target);
        Equal((short)8, axis.From);
        Equal((short)0, axis.Step);
        Equal((sbyte)4, axis.Offset);
        Equal((short)4, Ff7NativeCameraShake.Advance(new(true, RandomIndex: 128, Amplitude: 16, Duration: 2), random, interpolation).Target);
        Equal((byte)0, Ff7NativeCameraShake.Advance(new(true, RandomIndex: 255, Amplitude: 16, Duration: 2), random, interpolation).RandomIndex);
        Equal((short)-1, Ff7NativeCameraShake.Advance(new(true, RandomIndex: 1, Amplitude: 255, Duration: 2), random, interpolation).Target);
        Ff7CameraShakeAxis reset = Ff7NativeCameraShake.ResetForField(axis);
        Equal(axis.RandomIndex, reset.RandomIndex);
        Equal(axis.Offset, reset.Offset);
        Equal(axis.From, reset.From);
        Equal(axis.Target, reset.Target);
        Equal(false, reset.Enabled);
        Equal((ushort)0, reset.Duration);
        Equal((sbyte)0, Ff7NativeCameraShake.Advance(reset, random, interpolation).Offset);
        ProductionFieldTransitionCarriesShake();
        InstalledContinuation();
    }

    private static void ProductionFieldTransitionCarriesShake()
    {
        Ff7CameraShakeAxis x = new(true, 1, 231, -3, 255, 11, -21, 2, 3);
        Ff7CameraShakeAxis y = new(false, 1, 17, 4, 5, -2, 3, 6, 4);
        Ff7FieldProcess process = new()
        {
            NativeMusicTrack = 5,
            SavedNativeMusicTrack = 7,
            MusicTicks = 1234,
            MusicVolume = 47,
            MusicLock = 7,
            SoundChannelVolumes = [0, 1, 64, 126, 127],
            PreloadedMap = ushort.MaxValue,
            CameraShakeX = x,
            CameraShakeY = y
        };
        var field = new Ff7FieldResources("shake-transition-fixture", null!, null!, null!, [], [], null!, null!, null!);
        var host = new Ff7FieldHost(field, (_, _) => 1, process);
        Equal(x, host.Process.CameraShakeX);
        Equal(y, host.Process.CameraShakeY);
        Ff7FieldProcess next = host.CreateNextFieldProcess();
        Equal(x with { Enabled = false, Phase = 0, Amplitude = 0, Duration = 0, Step = 0 }, next.CameraShakeX);
        Equal(y with { Enabled = false, Phase = 0, Amplitude = 0, Duration = 0, Step = 0 }, next.CameraShakeY);
        Equal(5, next.NativeMusicTrack);
        Equal(7, next.SavedNativeMusicTrack);
        Equal(1234L, next.MusicTicks);
        Equal(47, next.MusicVolume);
        Equal((byte)7, next.MusicLock);
        Equal((ushort?)ushort.MaxValue, next.PreloadedMap);
        Equal(true, process.SoundChannelVolumes.SequenceEqual(next.SoundChannelVolumes));
        Equal(false, ReferenceEquals(process.SoundChannelVolumes, next.SoundChannelVolumes));
        Equal(x, host.Process.CameraShakeX);
        Equal(y, host.Process.CameraShakeY);
        var restored = new Ff7FieldHost(field, (_, _) => 1, JsonSerializer.Deserialize<Ff7FieldProcess>(JsonSerializer.Serialize(host.Capture()))!);
        Equal(x, restored.Process.CameraShakeX);
        Equal(y, restored.Process.CameraShakeY);
    }

    private static Ff7NativeMovement Interpolation()
    {
        short[] vectors = new short[512];
        vectors[1] = 4096;
        vectors[257] = -4096;
        PcWalkmeshTriangle triangle = new(0, Vector3.Zero, new(0, 100, 0), new(100, 0, 0), -1, -1, -1);
        return new([triangle], vectors, new byte[129]);
    }

    private static void InstalledContinuation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP original-resource camera shake: optional fixture inputs are unset.");
            return;
        }
        CombinedSessionSave envelope;
        using (FileStream input = File.OpenRead(path))
        {
            envelope = CombinedSessionCodec.Read(input);
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        Ff7PcExecutableData native = Ff7PcExecutableData.Load(installation);
        Equal(256, native.NativeCameraShakeRandomTable.Count);
        Equal(true, native.NativeCameraShakeRandomTable.Skip(128).SequenceEqual(native.NativeFieldRandomTable.Take(128)));
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        string vm = JsonSerializer.Serialize(session.Capture().Vm);
        string progress = JsonSerializer.Serialize(session.Progress);
        session.Host.Execute(new(long.MaxValue - 400, 0, 7, 0, new FieldCameraShake(2, 0, 0, 5, 2)));
        Ff7CameraShakeAxis expectedX = Ff7NativeCameraShake.Advance(session.Host.Process.CameraShakeX!, native.NativeCameraShakeRandomTable, session.Host.Movement!);
        Ff7CameraShakeAxis expectedY = Ff7NativeCameraShake.Advance(session.Host.Process.CameraShakeY!, native.NativeCameraShakeRandomTable, session.Host.Movement!);
        session.Host.AdvanceActors();
        Equal(expectedX, session.Host.Process.CameraShakeX);
        Equal(expectedY, session.Host.Process.CameraShakeY);
        using Ff7Session cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(session.Capture()))!);
        for (int tick = 0; tick < 12; tick++)
        {
            if (tick == 3)
            {
                var stop = new FieldHostRequest(long.MaxValue - 399, 0, 7, 0, new FieldCameraShake(0, 0, 0, 0, 0));
                session.Host.Execute(stop);
                cold.Host.Execute(stop);
            }
            session.Host.AdvanceActors();
            cold.Host.AdvanceActors();
            Equal(JsonSerializer.Serialize(session.Host.Capture()), JsonSerializer.Serialize(cold.Host.Capture()));
        }
        Equal(vm, JsonSerializer.Serialize(session.Capture().Vm));
        Equal(progress, JsonSerializer.Serialize(session.Progress));
        Equal((sbyte)0, session.Host.Process.CameraShakeY!.Offset);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"Expected {expected}, got {actual}.");
        }
    }
}
