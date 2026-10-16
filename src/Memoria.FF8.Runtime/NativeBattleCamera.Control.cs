using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCameraInputs
{
    public Func<ushort>? GlobalRandom { get; init; }
    public Func<byte, int>? ReadExternal { get; init; }
    public Action<byte>? ScheduleBlend { get; init; }
}

public sealed partial record NativeBattleCameraState
{
    /// <summary>509610/509810 execute the stage/action control program before5035E0 track callbacks.</summary>
    public NativeBattleCameraState AdvanceControl(NativeBattleCameraBank bank, ImmutableArray<byte> program, NativeBattleCameraInputs inputs)
    {
        Validate();
        if (ControlCursor is not int cursor)
        {
            return this;
        }
        NativeBattleCameraState result = this;
        NativeBattleScriptExpressionState expressions = new();
        for (int instruction = 0; instruction < 100_000; instruction++)
        {
            int origin = cursor;
            byte opcode = ReadByte();
            if (opcode >= 0xC0)
            {
                NativeBattleScriptExpressions.Execute(opcode, origin, ref cursor, expressions, ReadByte, ReadVariable, WriteVariable);
                continue;
            }
            switch (opcode)
            {
                case 0:
                    result = result.Start(bank, ReadByte());
                    break;
                case 1:
                    return result with { ControlCursor = cursor };
                case 2:
                    return result with { ControlCursor = null };
                case 3:
                    result = result with { ForceCamera = false, PoseCaptureCount = checked(result.PoseCaptureCount + 1), CapturedPose = result.CurrentPose };
                    break;
                case 4:
                    (inputs.ScheduleBlend ?? throw new NotSupportedException("Native camera command4 requires its506C10/509930 auxiliary task producer."))(ReadByte());
                    break;
                case 5:
                    result = result.Start(bank, unchecked((byte)ReadVariable(ReadByte())));
                    break;
                case 6:
                    result = result with { ForceCamera = true };
                    break;
                case 7:
                    result = result with { ForceCamera = false };
                    break;
                case 8:
                    result = result with { Mask = 0, BlendProgress = 4096 };
                    break;
                case 9:
                    _ = ReadByte();
                    break;
                default:
                    return result with { Mask = 0, BlendProgress = 4096, ControlCursor = null };
            }
        }
        throw new InvalidDataException("Native camera control program exceeded the bounded instruction budget without yielding.");

        byte ReadByte()
        {
            if (program.IsDefault || cursor < 0 || cursor >= program.Length)
            {
                throw new InvalidDataException("Native camera control cursor exceeds its original resource.");
            }
            return program[cursor++];
        }

        int ReadVariable(byte variable)
        {
            if (variable >= 128)
            {
                throw new InvalidDataException("Native camera direct variable selector addresses outside its original global WORD owners.");
            }
            if (variable < 8)
            {
                return result.Variables[variable];
            }
            if (variable >= 120)
            {
                return result.Scratch[127 - variable] ?? throw new InvalidDataException("Native camera reads an uninitialized global scratch WORD.");
            }
            return variable switch
            {
                16 => result.Mask & 255,
                17 => (inputs.GlobalRandom ?? throw new NotSupportedException("Native camera requires its actual534AA0 random owner."))(),
                >= 28 and <= 34 => (inputs.GlobalRandom ?? throw new NotSupportedException("Native camera requires its actual534AA0 random owner."))() % (variable - 26),
                19 or 21 or 22 or 23 or 24 or 25 or 26 or 27 or 35 => (inputs.ReadExternal ?? throw new NotSupportedException($"Native camera variable{variable} requires its original graphical owner."))(variable),
                //509640's default returns EAX from LEA EAX,[selector-10h].
                _ => variable - 16
            };
        }

        void WriteVariable(byte variable, int value)
        {
            if (variable >= 128)
            {
                throw new InvalidDataException("Native camera direct variable selector addresses outside its original global WORD owners.");
            }
            if (variable < 8)
            {
                result = result with { Variables = result.Variables.SetItem(variable, unchecked((short)value)) };
            }
            else if (variable >= 120)
            {
                result = result with { Scratch = result.Scratch.SetItem(127 - variable, unchecked((short)value)) };
            }
            else if (variable == 16)
            {
                result = result with { Mask = unchecked((ushort)value) };
            }
        }
    }
}
