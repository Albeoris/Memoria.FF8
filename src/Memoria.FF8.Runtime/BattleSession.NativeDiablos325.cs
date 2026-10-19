using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeDiablos325(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleDiablos325Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Diablos requires its retained effect memory."), graphics.OriginalEffect ?? throw new InvalidDataException("Native Diablos lost its selected effect."));
        if (initialize)
        {
            frame = NativeBattleDiablos325Entry.Initialize6541E0(frame, nativeActionPackets, graphics.Models);
        }
        else
        {
            NativeBattleDiablos325Inputs inputs = new(nativeActionPackets, current =>
            {
                NativeVibrationBank bank = current.Effect.Binding.Diablos325Resources?.Vibration ?? throw new NotSupportedException("Native Diablos requires its original vibration bank.");
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Diablos requires its retained vibration process.")).Enqueue(bank, 1, 128);
                NativeVibrationProcess = queued.State;
                return new(current, queued.Handle);
            })
            {
                QueueFile = (current, request) =>
                {
                    NativeBattleEffectFileAsset file = current.Effect.Binding.Diablos325Resources?.Files.SingleOrDefault(file => file.FileId == request.FileId) ?? throw new NotSupportedException("Native Diablos requires its requested original archive file.");
                    if (file.Data.IsDefaultOrEmpty)
                    {
                        throw new NotSupportedException("Native Diablos requested archive bytes are unavailable.");
                    }
                    NativeBattleEffectArena arena = (current.Memory.Arena ?? throw new NotSupportedException("Native Diablos file delivery requires its physical arena.")).Write(checked((int)(request.DestinationAddress - NativeBattleEffectNodeMemory.ArenaAddress)), file.Data.AsSpan());
                    NativeFileIo = (NativeFileIo ?? throw new NotSupportedException("Native Diablos file delivery requires its retained IO clock.")).Queue482610(file.Data.Length, request.CompletionCallback, null);
                    return current with { Memory = current.Memory with { Arena = arena } };
                },
                PlaySound = (sound, flags, pan) => PublishNativeModuleSound(new(sound, checked((uint)flags), null) { Pan = checked((byte)pan) })
            };
            frame = NativeBattleDiablos325Controller.Advance654350(frame, (current, flash) =>
            {
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Diablos flash requires its current stage groups.");
                stage.Validate();
                uint previous = NativeBattleDiablos325Entry.RequireState(current.Memory).PreviousFlash51EC;
                NativeStage = stage with
                {
                    Groups = stage.Groups.Select(group => group with
                    {
                        ColorDepth = unchecked((short)flash),
                        ColorFlags = 0,
                        Flags = flash >= 4096 ? (byte)(group.Flags & ~2) : previous >= 4096 ? (byte)(group.Flags | 2) : group.Flags
                    }).ToImmutableArray()
                };
                return current;
            }, inputs);
        }
        NativeEffectMemory = frame.Memory;
        return graphics with { OriginalEffect = frame.Effect };
    }
}
