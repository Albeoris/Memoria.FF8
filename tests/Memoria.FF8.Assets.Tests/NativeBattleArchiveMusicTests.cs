using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleArchiveMusicTests
{
    [Fact]
    public void Original503040_prefix_preserves_descriptor_before_worker_order_and_the_actual_completion_pointer()
    {
        using BattleAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleVictoryMusicStep request = NativeBattleVictoryMusicLoader.Advance503040(0, null, 0, null, null);
        NativeBattleArchiveFile file = assets.ReadNativeBattleFile(request.FileRequest!.FileId);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(request.FileRequest.DestinationAddress, file.Bytes.AsSpan());
        NativeBattleMusicProcessState process = new NativeBattleMusicProcessState(new(1, null, null, null, null, null), 0, null).InitializeCommands501C60();
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.FromBufferlessColdProcess();
        NativeBattleVictoryMusicStep loaded = NativeBattleVictoryMusicLoader.Advance503040(request.Phase, file.Bytes.Length, 0, null, process);
        NativeBattleMusicCommandStep faded = loaded.Music!.DrainCommands501B60(sound);
        Assert.Equal((byte)4, faded.Receipt!.Command);
        Assert.Equal(0, faded.Receipt.EffectiveDuration);
        NativeBattleVictoryMusicStep staged = NativeBattleVictoryMusicLoader.Advance503040(loaded.Phase, null, 0, staging, faded.State);
        Assert.True(staged.StartWorker);
        Assert.Equal(0x199B050u, staged.Music!.LoadSource!.SoundBankAddress);
        Assert.Equal(0x19B9D10u, staged.Music.LoadSource.MusicAddress);
        process = staged.Music;
        sound = faded.Sound;
        const int caller = 3;
        uint pointer = NativeBattleDescriptorState.CompletionAddress503040(caller);
        NativeBattleDescriptorState descriptors = NativeBattleDescriptorState.Clear();
        NativeBattleAuxiliaryState auxiliary = NativeBattleAuxiliaryState.Clear().AppendMusicLoad501A20(pointer, (address, value) => descriptors = descriptors.WriteCompletion501A70(address, value));
        List<byte> commands = [];
        for (int frame = 0; frame < 3; frame++)
        {
            NativeBattleVictoryMusicStep waiting = NativeBattleVictoryMusicLoader.Advance503040(3, null, descriptors.Tasks[caller].Payload[2], null, null);
            Assert.Null(waiting.ResourceRows);
            auxiliary = auxiliary.Advance(new()
            {
                AdvanceMusicLoad501A70 = (phase, address) =>
                {
                    NativeBattleMusicLoadStep step = process.AdvanceLoad501A70(sound, staging, phase, address, (target, value) => descriptors = descriptors.WriteCompletion501A70(target, value));
                    process = step.State;
                    sound = step.Sound;
                    return (step.Phase, step.Completed);
                }
            }).State;
            NativeBattleMusicCommandStep drained = process.DrainCommands501B60(sound);
            process = drained.State;
            sound = drained.Sound;
            if (drained.Receipt is { } receipt)
            {
                commands.Add(receipt.Command);
            }
            // The independent save retains shared bytes, globals and the physical caller, rather than a task-local completion ledger.
            staging = JsonSerializer.Deserialize<NativeBattleFileStaging>(JsonSerializer.Serialize(staging))!;
            process = JsonSerializer.Deserialize<NativeBattleMusicProcessState>(JsonSerializer.Serialize(process))!;
            sound = JsonSerializer.Deserialize<NativeGlobalSoundProcess>(JsonSerializer.Serialize(sound))!;
            descriptors = JsonSerializer.Deserialize<NativeBattleDescriptorState>(JsonSerializer.Serialize(descriptors))!;
            auxiliary = JsonSerializer.Deserialize<NativeBattleAuxiliaryState>(JsonSerializer.Serialize(auxiliary))!;
        }
        Assert.Equal(new byte[] { 1, 2 }, commands);
        Assert.Equal((byte)1, descriptors.Tasks[caller].Payload[2]);
        Assert.Null(auxiliary.Head);
        NativeBattleVictoryMusicStep victory = NativeBattleVictoryMusicLoader.Advance503040(3, null, 255, staging, process);
        Assert.Equal(unchecked(NativeBattleFileStaging.Address + BinaryPrimitives.ReadUInt32LittleEndian(file.Bytes.AsSpan()[0x24..])), victory.ResourceRows);
        NativeBattleMusicCommandStep gained = victory.Music!.DrainCommands501B60(sound);
        Assert.Equal((byte)3, gained.Receipt!.Command);
        Assert.Equal(127, gained.Sound.MusicPlayback!.ReadGain(0));
        Assert.Equal(1, gained.Sound.MusicPlayback.CurrentMusicId0);
        // ResourceRows is an explicit handoff. No model/camera completion or native115 return2 is fabricated.
        Assert.Equal((byte)3, victory.Phase);
    }

    [Fact]
    public void Pending_model_completion_reads_the_same_staging_bytes_overwritten_by_original_file766()
    {
        using BattleAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleModelDatSource model = assets.ReadNativeModelDatSource(0, false, 0);
        NativeBattleModelLoadStep request = NativeBattleModelLoader.Queue507080(0, 0, 0, 0, 0).Poll507070(-1, [model], NativeBattleFileStaging.Unknown());
        NativeBattleModelLoadStep prepared = request.State.Poll507070(model.Bytes.Length, [model], request.Staging);
        NativeBattleArchiveFile victory = assets.ReadNativeBattleFile(766);
        NativeBattleFileStaging overwritten = prepared.Staging.Write(NativeBattleFileStaging.Address, victory.Bytes.AsSpan());
        NativeBattleModelLoadStep completion = prepared.State.Poll507070(victory.Bytes.Length, [model], overwritten);
        NativeBattleModelResourceBind bound = Assert.IsType<NativeBattleModelResourceBind>(completion.Bind);
        Assert.Equal(victory.Bytes.ToArray(), bound.PreparedDat.Bytes.ToArray());
        Assert.Equal(model.FileId, bound.Task.File!.FileId);
        Assert.Equal(model.Name, bound.PreparedDat.Name);
        Assert.Equal(victory.Bytes.ToArray(), completion.Staging.ReadDat(NativeBattleFileStaging.Address).ToArray());
        Assert.Equal(model.Bytes.Length, prepared.Staging.ReadDat(NativeBattleFileStaging.Address).Length);
    }

    [Fact]
    public void Original_file766_selects_the_actual_second_nested_AKAO_resource_for501A20()
    {
        using BattleAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleArchiveFile file = assets.ReadNativeBattleFile(766);
        Assert.Equal("r0win.dat", file.Name);
        Assert.Equal(163916, file.Bytes.Length);
        Assert.Equal("385EAC9AEFE970A7536BB332BA7733DF437908B1AEBC99D50F52264FC0F7A584", Convert.ToHexString(SHA256.HashData(file.Bytes.AsSpan())));
        int first = file.Section(0);
        NativeBattleMusicLoadSource source = NativeBattleMusicLoadSource.Read501A20(file.Bytes.Slice(first, file.Section(1) - first));
        Assert.Equal(126144, source.SoundBank.Length);
        Assert.Equal(4280, source.Music.Length);
        Assert.Equal("FBBBCCD24F1D2CFBA7F39EB9807426B4E74C5835E564C541ADDDFA730BBBA923", Convert.ToHexString(SHA256.HashData(source.Music.AsSpan())));
        Assert.Equal(new NativeMusicBufferIdentity(2, NativeMusicBufferProducer.Battle501A70), source.ReadIdentity());
    }
}
