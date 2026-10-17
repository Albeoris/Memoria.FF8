using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleVictoryFileRequest(ushort FileId, uint DestinationAddress, uint CompletionCallback);
public sealed record NativeBattleVictoryMusicStep(byte Phase, NativeBattleMusicProcessState? Music, NativeBattleVictoryFileRequest? FileRequest = null, bool StartWorker = false, uint? ResourceRows = null);

/// <summary>503040's reached file/music prefix. Resource rows and victory animation remain the caller's physical model contract.</summary>
public static class NativeBattleVictoryMusicLoader
{
    public static NativeBattleVictoryMusicStep Advance503040(byte phase, int? signedIoResult, byte completion, NativeBattleFileStaging? staging, NativeBattleMusicProcessState? music)
    {
        switch (phase)
        {
            case 0:
                return new(1, music, new(766, NativeBattleFileStaging.Address, 0x508470));
            case 1:
                int result = signedIoResult ?? throw new NotSupportedException("Native503040 phase1 requires the actual signed1D999C8 IO result.");
                if (result < 0)
                {
                    return new(phase, music);
                }
                return new(2, RequireMusic(music).QueueFade501B40(3));
            case 2:
                NativeBattleFileStaging memory = staging ?? throw new NotSupportedException("Native503040 phase2 requires the shared199B018 file bytes.");
                uint directory = unchecked(NativeBattleFileStaging.Address + BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(NativeBattleFileStaging.Address + 4, 4)));
                return new(3, RequireMusic(music).StageLoad501A20(memory, directory), StartWorker: true);
            case 3:
                if (completion == 0)
                {
                    return new(phase, music);
                }
                NativeBattleMusicProcessState playing = RequireMusic(music).QueueGain501B10(0, 127);
                NativeBattleFileStaging source = staging ?? throw new NotSupportedException("Native503040 phase3 requires its current shared resource directory.");
                uint rows = unchecked(NativeBattleFileStaging.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Read(NativeBattleFileStaging.Address + 0x24, 4)));
                // TODO(TD-009): — native115 stays closed until its caller performs5022C0/505C00/5064F0 with these physical rows.
                return new(phase, playing, ResourceRows: rows);
            default:
                return new(phase, music);
        }
    }

    private static NativeBattleMusicProcessState RequireMusic(NativeBattleMusicProcessState? music)
    {
        return music ?? throw new NotSupportedException("Native503040 requires the retained501C60 music command owner.");
    }
}
