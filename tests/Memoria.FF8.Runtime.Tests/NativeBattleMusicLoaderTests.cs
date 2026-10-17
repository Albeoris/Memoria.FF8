using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMusicLoaderTests
{
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    public void Shared_auxiliary_worker_stops_copies_the_initialized_destination_and_completes_on_its_third_callback(byte bank)
    {
        const uint completionAddress = 0x1D9700E;
        NativeBattleMusicProcessState process = CreateStaged(bank, out NativeBattleFileStaging staging);
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        List<(uint Address, byte Value)> writes = [];
        NativeBattleAuxiliaryState auxiliary = NativeBattleAuxiliaryState.Clear().AppendCameraBlend(8).AppendMusicLoad501A20(completionAddress, (address, value) => writes.Add((address, value)));
        Assert.Equal(new[] { (completionAddress, (byte)0) }, writes);
        Assert.Equal(0, auxiliary.Head);
        Assert.Equal(1, auxiliary.Tail);
        NativeBattleAuxiliaryInputs inputs = new()
        {
            AdvanceMusicLoad501A70 = (phase, address) =>
            {
                NativeBattleMusicLoadStep step = process.AdvanceLoad501A70(sound, staging, phase, address, (pointer, value) => writes.Add((pointer, value)));
                process = step.State;
                sound = step.Sound;
                return (step.Phase, step.Completed);
            }
        };
        auxiliary = auxiliary.Advance(inputs).State;
        Assert.Equal((byte)1, process.Commands!.Command);
        Assert.Equal(new[] { (completionAddress, (byte)0) }, writes);
        NativeBattleMusicCommandStep stopped = process.DrainCommands501B60(sound);
        process = stopped.State;
        sound = stopped.Sound;
        auxiliary = auxiliary.Advance(inputs).State;
        Assert.Equal((byte)2, process.Commands!.Command);
        Assert.Equal((byte)(bank ^ 1), process.ReturnBank);
        Assert.Equal(bank == 0 ? 0x1CE075Cu : 0x1CDC750u, process.Commands.TargetBufferAddress);
        Assert.Equal(new NativeMusicBufferIdentity(2, NativeMusicBufferProducer.Battle501A70), process.Buffers.ReadBuffer(bank));
        Assert.False(sound.MusicPlayback!.BattleFirstLoadB8B7D4);
        Assert.Equal(new[] { (completionAddress, (byte)0) }, writes);
        NativeBattleMusicCommandStep played = process.DrainCommands501B60(sound);
        process = played.State;
        sound = played.Sound;
        Assert.Equal(1, played.Receipt!.Playback!.MusicId);
        auxiliary = auxiliary.Advance(inputs).State;
        Assert.Equal(new[] { (completionAddress, (byte)0), (completionAddress, (byte)1) }, writes);
        Assert.Equal(0, auxiliary.Head);
        Assert.Equal(0, auxiliary.Tail);
        Assert.Equal(0, auxiliary.Slots[1].Flags);
        Assert.Equal(NativeBattleAuxiliaryKind.CameraBlend509930, auxiliary.Slots[0].Kind);
    }

    [Fact]
    public void Independent_saved_globals_continue_without_a_journal_or_replaying_stop()
    {
        NativeBattleMusicProcessState process = CreateStaged(0, out NativeBattleFileStaging staging);
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        NativeBattleMusicLoadStep first = process.AdvanceLoad501A70(sound, staging, 0, 0x1234);
        NativeBattleMusicCommandStep drained = first.State.DrainCommands501B60(first.Sound);
        process = JsonSerializer.Deserialize<NativeBattleMusicProcessState>(JsonSerializer.Serialize(drained.State))!;
        sound = JsonSerializer.Deserialize<NativeGlobalSoundProcess>(JsonSerializer.Serialize(drained.Sound))!;
        string before = JsonSerializer.Serialize(process);
        NativeBattleMusicLoadStep next = process.AdvanceLoad501A70(sound, staging, 1, 0x1234);
        NativeBattleFileStaging copiedStaging = JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(staging))!;
        NativeBattleMusicLoadStep independent = JsonSerializer.Deserialize<NativeBattleMusicProcessState>(before)!.AdvanceLoad501A70(JsonSerializer.Deserialize<NativeGlobalSoundProcess>(JsonSerializer.Serialize(sound))!, copiedStaging, 1, 0x1234);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(independent));
        Assert.Equal(before, JsonSerializer.Serialize(process));
        Assert.Equal((byte)2, next.State.Commands!.Command);
        Assert.Equal((byte)2, next.Phase);
    }

    [Fact]
    public void Subsequent_process_load_does_not_toggle_again_and_missing_old_first_load_state_stays_unknown()
    {
        NativeBattleMusicProcessState process = CreateStaged(0, out NativeBattleFileStaging staging);
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        sound = sound with { MusicPlayback = sound.MusicPlayback! with { BattleFirstLoadB8B7D4 = false } };
        NativeBattleMusicLoadStep step = process.AdvanceLoad501A70(sound, staging, 1, 0x1234);
        Assert.Equal((byte)0, step.State.ReturnBank);
        Assert.Throws<NotSupportedException>(() => process.AdvanceLoad501A70(sound with { MusicPlayback = sound.MusicPlayback! with { BattleFirstLoadB8B7D4 = null } }, staging, 1, 0x1234));
    }

    [Fact]
    public void Unreached_operands_are_not_required_and_unknown_phase_has_no_completion_write()
    {
        NativeBattleMusicProcessState process = Create(0);
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.FromBufferlessColdProcess() with { MusicPlayback = NativeMusicPlaybackState.Unknown() };
        NativeBattleMusicLoadStep first = process.AdvanceLoad501A70(sound, null, 0, 0x1234);
        Assert.Equal((byte)1, first.State.Commands!.Command);
        Assert.Equal((byte)1, first.Phase);
        Assert.Throws<NotSupportedException>(() => process.AdvanceLoad501A70(sound, null, 2, 0x1234));
        List<byte> writes = [];
        NativeBattleMusicLoadStep completed = process.AdvanceLoad501A70(sound, null, 2, 0x1234, (_, value) => writes.Add(value));
        Assert.True(completed.Completed);
        Assert.Equal(new byte[] { 1 }, writes);
        Assert.Same(process, process.AdvanceLoad501A70(sound, null, 255, 0x1234, (_, _) => Assert.Fail("Unknown phase does not write completion.")).State);
    }

    [Fact]
    public void Source_directory_bounds_and_unavailable_shared_pool_refuse_without_a_completion_write()
    {
        byte[] invalid = Directory().ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(invalid.AsSpan(12), invalid.Length + 1);
        Assert.Throws<InvalidDataException>(() => NativeBattleMusicLoadSource.Read501A20(invalid.ToImmutableArray()));
        NativeBattleAuxiliaryState auxiliary = NativeBattleAuxiliaryState.Clear();
        for (int index = 0; index < 16; index++)
        {
            auxiliary = auxiliary.AppendCameraBlend(8);
        }
        Assert.Throws<InvalidOperationException>(() => auxiliary.AppendMusicLoad501A20(0x1234, (_, _) => Assert.Fail("No caller write follows failed506C10 allocation.")));
    }

    private static NativeBattleMusicProcessState Create(byte bank) => new(new(1, null, null, null, 0, null), bank, null);

    private static NativeBattleMusicProcessState CreateStaged(byte bank, out NativeBattleFileStaging staging)
    {
        staging = NativeBattleFileStaging.Unknown().Write(NativeBattleFileStaging.Address, Directory().AsSpan());
        return Create(bank).InitializeCommands501C60().StageLoad501A20(staging, NativeBattleFileStaging.Address);
    }

    private static ImmutableArray<byte> Directory()
    {
        byte[] bytes = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 24);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), bytes.Length);
        "AKAO"u8.CopyTo(bytes.AsSpan(16));
        bytes[20] = 12;
        "AKAO"u8.CopyTo(bytes.AsSpan(24));
        bytes[28] = 2;
        return bytes.ToImmutableArray();
    }
}
