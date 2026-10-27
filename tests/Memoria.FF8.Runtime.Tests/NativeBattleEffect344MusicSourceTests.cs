using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect344MusicSourceTests
{
    [Fact]
    public void Static_source_survives_restore_and_worker_publishes_actual_completion_byte()
    {
        const uint source = 0xDEE388;
        byte[] directory = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(directory, 16);
        BinaryPrimitives.WriteInt32LittleEndian(directory.AsSpan(4), 129584);
        BinaryPrimitives.WriteInt32LittleEndian(directory.AsSpan(8), 140112);
        byte[] music = new byte[10528];
        "AKAO"u8.CopyTo(music);
        music[4] = 91;
        List<(uint Address, int Length)> reads = [];
        NativeBattleMusicProcessState staged = new NativeBattleMusicProcessState(new(1, null, null, null, 0, null), 0, null).InitializeCommands501C60().StageLoad501A20(source, Read);
        Assert.Equal(new NativeBattleMusicLoadPointers(0xDEE398, 0xE0DDB8, 10528), staged.LoadSource);
        Assert.Equal(new[] { (source + 4, 12) }, reads);
        NativeBattleMusicLoadStep stopped = staged.AdvanceLoad501A70(NativeGlobalSoundProcess.FromBufferlessColdProcess(), null, 0, 0x24DFD98);
        NativeBattleMusicCommandStep drained = stopped.State.DrainCommands501B60(stopped.Sound);
        staged = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleMusicProcessState>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(drained.State)))!);
        NativeBattleMusicLoadStep copied = staged.AdvanceLoad501A70(drained.Sound, null, 1, 0x24DFD98, readSource: Read);
        Assert.Equal(new NativeMusicBufferIdentity(91, NativeMusicBufferProducer.Battle501A70), copied.State.Buffers.ReadBuffer(0));
        Assert.Equal(new[] { (source + 4, 12), (0xE0DDB8u, 10528) }, reads);
        List<(uint Address, byte Value)> writes = [];
        NativeBattleMusicLoadStep completed = copied.State.AdvanceLoad501A70(copied.Sound, null, 2, 0x24DFD98, (address, value) => writes.Add((address, value)), (_, _) => throw new InvalidOperationException());
        Assert.True(completed.Completed);
        Assert.Equal(new[] { (0x24DFD98u, (byte)1) }, writes);

        byte[] Read(uint address, int length)
        {
            reads.Add((address, length));
            if (address == source + 4 && length == 12)
            {
                return directory;
            }
            if (address == 0xE0DDB8 && length == music.Length)
            {
                return music;
            }
            throw new NotSupportedException("The loader must read the selected addressed source.");
        }
    }

    [Fact]
    public void Pointer_validation_rejects_null_reversed_and_overflowed_sources()
    {
        Assert.Throws<InvalidDataException>(() => new NativeBattleMusicLoadPointers(0, 8, 8).Validate());
        Assert.Throws<InvalidDataException>(() => new NativeBattleMusicLoadPointers(0xDEE398, 0xDEE397, 8).Validate());
        Assert.Throws<InvalidDataException>(() => new NativeBattleMusicLoadPointers(0xDEE398, 0xE0DDB8, -1).Validate());
        Assert.Throws<InvalidDataException>(() => new NativeBattleMusicLoadPointers(0xDEE398, uint.MaxValue - 3, 8).Validate());
    }
}
