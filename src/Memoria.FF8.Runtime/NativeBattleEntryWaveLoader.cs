using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEntryWaveStep(byte Phase, byte Completion, bool Completed, bool RequestFile, uint? TimAddress, uint? SoundSourceAddress);

/// <summary>500AC0's command9 task, including the PC5011A0 completion-only stub.</summary>
public static class NativeBattleEntryWaveLoader
{
    public const ushort FileId = 0xA5;
    public const uint Destination = NativeBattleFileStaging.Address + 0x900;

    public static NativeBattleEntryWaveStep Advance500AC0(byte phase, byte completion, int? ioResult, NativeBattleFileStaging? staging)
    {
        if (phase == 0)
        {
            return new(1, completion, false, true, null, null);
        }
        if (phase == 1)
        {
            int result = ioResult ?? throw new NotSupportedException("Native500AC0 requires its current signed1D999C8 IO result.");
            if (result < 0)
            {
                return new(phase, completion, false, false, null, null);
            }
            NativeBattleFileStaging current = staging ?? throw new NotSupportedException("Native500AC0 requires its current199B018 staging allocation.");
            uint tim = unchecked(Destination + BinaryPrimitives.ReadUInt32LittleEndian(current.Read(Destination + 4, 4)));
            uint sound = unchecked(Destination + BinaryPrimitives.ReadUInt32LittleEndian(current.Read(Destination + 12, 4)));
            //5011A0 ignores its source operand and writes BYTE[completion]=1 on this PC build.
            return new(2, 1, true, false, tim, sound);
        }
        return new(phase, completion, phase == 2 && completion != 0, false, null, null);
    }
}
