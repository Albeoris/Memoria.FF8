using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect79MusicSourceTests
{
    private const uint Source = 0x20DFAB8;

    [Fact]
    public void Directory_reads_only_the_three_reached_offsets_from_current_COMMON()
    {
        NativeBattleEffectArena arena = SourceArena(77);
        List<(uint Address, int Length)> reads = [];
        NativeBattleMusicProcessState staged = Process().StageLoad501A20(Source, Read);
        Assert.Equal(new[] { (Source + 4, 12) }, reads);
        Assert.Equal(new NativeBattleMusicLoadPointers(Source + 16, Source + 24, 8), staged.LoadSource);
        Assert.Throws<NotSupportedException>(() => arena.Read(0, 4));
        Assert.Throws<NotSupportedException>(() => arena.Read(16, 8));

        byte[] Read(uint address, int length)
        {
            reads.Add((address, length));
            return arena.Read(checked((int)(address - Source)), length);
        }
    }

    [Fact]
    public void Worker_reads_current_source_at_copy_phase_after_restore_without_staging_mirror()
    {
        NativeBattleEffectArena arena = SourceArena(2);
        NativeBattleMusicProcessState process = Process().StageLoad501A20(Source, Read);
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        NativeBattleMusicLoadStep first = process.AdvanceLoad501A70(sound, null, 0, 0x25DF340, readSource: (_, _) => throw new InvalidOperationException("Phase0 does not read music."));
        NativeBattleMusicCommandStep stopped = first.State.DrainCommands501B60(first.Sound);
        process = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleMusicProcessState>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(stopped.State)))!);
        sound = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeGlobalSoundProcess>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(stopped.Sound)))!);
        arena = arena.Write(28, [77]);
        arena = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(arena)))!);
        NativeBattleMusicLoadStep copied = process.AdvanceLoad501A70(sound, null, 1, 0x25DF340, readSource: Read);
        Assert.Equal(new NativeMusicBufferIdentity(77, NativeMusicBufferProducer.Battle501A70), copied.State.Buffers.ReadBuffer(0));
        Assert.Equal((byte)1, copied.State.ReturnBank);
        Assert.Equal(0x1CE075Cu, copied.State.Commands!.TargetBufferAddress);
        Assert.Equal((byte)2, copied.State.Commands.Command);
        List<(uint Address, byte Value)> completions = [];
        NativeBattleMusicLoadStep completed = copied.State.AdvanceLoad501A70(copied.Sound, null, 2, 0x25DF340, (address, value) => completions.Add((address, value)), (_, _) => throw new InvalidOperationException("Phase2 does not read music."));
        Assert.True(completed.Completed);
        Assert.Equal(new[] { (0x25DF340u, (byte)1) }, completions);

        byte[] Read(uint address, int length) => arena.Read(checked((int)(address - Source)), length);
    }

    [Fact]
    public void Unobserved_current_offsets_and_copy_bytes_are_not_recreated()
    {
        NativeBattleEffectArena unknown = NativeBattleEffectArena.Clear().MarkUnknown(0, 32);
        Assert.Throws<NotSupportedException>(() => Process().StageLoad501A20(Source, (address, length) => unknown.Read(checked((int)(address - Source)), length)));
        NativeBattleEffectArena source = SourceArena(77);
        NativeBattleMusicProcessState staged = Process().StageLoad501A20(Source, (address, length) => source.Read(checked((int)(address - Source)), length));
        source = source.MarkUnknown(24, 8);
        Assert.Throws<NotSupportedException>(() => staged.AdvanceLoad501A70(NativeGlobalSoundProcess.FromBufferlessColdProcess(), null, 1, 0x25DF340, readSource: (address, length) => source.Read(checked((int)(address - Source)), length)));
    }

    private static NativeBattleMusicProcessState Process() => new NativeBattleMusicProcessState(new(1, null, null, null, 0, null), 0, null).InitializeCommands501C60();

    private static NativeBattleEffectArena SourceArena(byte identity)
    {
        byte[] offsets = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(offsets, 16);
        BinaryPrimitives.WriteInt32LittleEndian(offsets.AsSpan(4), 24);
        BinaryPrimitives.WriteInt32LittleEndian(offsets.AsSpan(8), 32);
        byte[] music = new byte[8];
        "AKAO"u8.CopyTo(music);
        music[4] = identity;
        return NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(4, offsets).Write(24, music);
    }
}
