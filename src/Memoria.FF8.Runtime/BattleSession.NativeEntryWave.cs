using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEntryWaveStep AdvanceNativeEntryWave500AC0(byte phase, byte completion)
    {
        NativeBattleEntryWaveStep step = NativeBattleEntryWaveLoader.Advance500AC0(phase, completion, NativeEffectIoResult, NativeFileStaging);
        if (step.RequestFile)
        {
            NativeBattleArchiveFile file = definition.NativeEntryWaveFile ?? throw new NotSupportedException("Native500AC0 requires its original B84CCC fileA5 B0WAVE.DAT binding.");
            if (file.FileId != NativeBattleEntryWaveLoader.FileId || file.Bytes.IsDefaultOrEmpty)
            {
                throw new InvalidDataException("Native500AC0 requires its actual fileA5 resource bytes.");
            }
            NativeFileStaging = (NativeFileStaging ?? NativeBattleFileStaging.Unknown()).Write(NativeBattleEntryWaveLoader.Destination, file.Bytes.AsSpan());
            NativeEffectIoResult = -1;
            QueueNativeFileIo482610(file.Bytes.Length, 0x508470);
        }
        if (step.TimAddress is uint tim)
        {
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native500AC0 requires its retained graphics memory.");
            NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native505E30 requires its retained texture queue.");
            NativeEffectMemory = memory with { Textures = textures.EnqueueStagingTim505E30(NativeFileStaging!, tim).State };
        }
        return step;
    }
}
