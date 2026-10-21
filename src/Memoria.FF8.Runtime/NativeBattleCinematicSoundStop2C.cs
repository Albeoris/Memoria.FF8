using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicSoundStop2C
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeGlobalSoundProcess process, Action<byte> stopVoice)
    {
        ArgumentNullException.ThrowIfNull(stopVoice);
        if (CoreDword(frame, 0x27973B8) != frame.TemporaryFrame.Address + 0x100)
        {
            throw new NotSupportedException("Native cinematic sound stop requires its current callback control binding.");
        }
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) != 0x2C)
        {
            throw new InvalidDataException("Native cinematic sound stop requires its selected instruction.");
        }
        ushort mask = unchecked((ushort)((instruction >> 9) & 15));
        foreach (byte voice in SelectedVoices(process, mask))
        {
            stopVoice(voice);
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }

    public static ImmutableArray<byte> SelectedVoices(NativeGlobalSoundProcess process, ushort mask)
    {
        ArgumentNullException.ThrowIfNull(process);
        process.Validate();
        if (mask > 15)
        {
            throw new InvalidDataException("Native cinematic sound stop requires its four-bit voice mask.");
        }
        if (mask != 0)
        {
            ImmutableArray<byte>.Builder voices = ImmutableArray.CreateBuilder<byte>();
            for (byte voice = 1; voice <= 4; voice++)
            {
                if ((mask & (1 << (voice - 1))) != 0)
                {
                    voices.Add(voice);
                }
            }
            return voices.ToImmutable();
        }
        for (byte voice = 0; voice < 30; voice++)
        {
            uint identity = process.OriginalEffectIds[voice] ?? throw new NotSupportedException("Native cinematic sound lookup encountered an unknown earlier voice identity.");
            if (identity == 0)
            {
                return [voice];
            }
        }
        return [];
    }
}
