using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record PreparedNativeBattleMusicEntry(NativeBattleMusicEntryPreparation Native, SceneEventState SourceScene, NativeMusicProcessState SourceBuffers, NativeGlobalSoundProcess SourceSound, byte? SourceMusic203, NativeMusicChannelState? SourceChannel108);

public sealed partial class EventEngine
{
    public PreparedNativeBattleMusicEntry PrepareNativeBattleMusicEntry(ushort encounter, ushort flags, NativeBattleMusicEntryResources resources, ushort? priorReturnChannel = null)
    {
        NativeMusicProcessState buffers = Scene.MusicProcess ?? throw new NotSupportedException("Native52CE30 requires its current physical music buffer/selector owner.");
        NativeGlobalSoundProcess sound = RequireMusicSoundProcess();
        bool keep = (flags & 0x10) != 0;
        ushort? field = NativeRuntimeProfile is { IsVerified: true } && Scene.NativeFieldInitialization is { } proof && proof.FieldId == FieldId && proof.ProgramHash == programHash && FieldId is >= 72 and <= ushort.MaxValue ? (ushort)FieldId : null;
        NativeBattleMusicEntryPreparation native = NativeBattleMusicEntry.Prepare52CE30(encounter, flags, buffers, keep ? null : Scene.NativeBattleMusic203, keep ? null : Scene.MusicChannel108?.ChannelId, sound, resources, priorReturnChannel, field);
        return new(native, Scene, buffers, sound, keep ? null : Scene.NativeBattleMusic203, keep ? null : Scene.MusicChannel108);
    }

    public void CommitNativeBattleMusicEntry(PreparedNativeBattleMusicEntry prepared, Action<NativeBattleMusicEntryPreparation> publishNativeBattle)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(publishNativeBattle);
        if (!ReferenceEquals(Scene, prepared.SourceScene) || !ReferenceEquals(Scene.MusicProcess, prepared.SourceBuffers) || !ReferenceEquals(services.GlobalSoundProcess, prepared.SourceSound) || !prepared.Native.Receipt.KeptMusic && (Scene.NativeBattleMusic203 != prepared.SourceMusic203 || Scene.MusicChannel108 != prepared.SourceChannel108))
        {
            throw new InvalidOperationException("Prepared native battle music no longer matches its actual source owners.");
        }
        prepared.Native.MusicProcess.Validate();
        prepared.Native.SoundProcess.Validate();
        prepared.Native.Receipt.Validate();
        // The host atomically publishes the constructed BattleSnapshot and its same live global sound owner first.
        publishNativeBattle(prepared.Native);
        Scene.MusicProcess = prepared.Native.MusicProcess.Buffers;
        Scene.NativeBattleMusicEntry = prepared.Native.Receipt;
        AudioCommandExecuted?.Invoke(prepared.Native.Receipt);
    }

    public void SynchronizeNativeBattleMusicProcess(NativeBattleMusicProcessState? process)
    {
        if (process is null)
        {
            return;
        }
        process.Validate();
        Scene.MusicProcess = process.Buffers;
    }
}
