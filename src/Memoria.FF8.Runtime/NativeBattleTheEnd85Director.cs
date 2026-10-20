using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTheEnd85DirectorInput(NativeBattleModulePoolTask Task, ImmutableArray<byte> Context, ushort IoRelease358, ushort ScriptOpcode, uint IoFreeze25C21D4, uint Pause25BC894);
public sealed record NativeBattleTheEnd85DirectorStep(NativeBattleTheEnd85DirectorInput Input, bool Complete, bool AdvanceScript16);
public sealed record NativeBattleTheEnd85DirectorServices(Func<NativeBattleTheEnd85DirectorInput, NativeBattleTheEnd85DirectorInput> SpawnStage, Func<NativeBattleTheEnd85DirectorInput, uint, NativeBattleTheEnd85DirectorInput> PlaySound, Func<NativeBattleTheEnd85DirectorInput, bool, NativeBattleTheEnd85DirectorInput> SetMode4A8480, Func<NativeBattleTheEnd85DirectorInput, NativeBattleTheEnd85DirectorInput> AppendOverlay);

public static class NativeBattleTheEnd85Director
{
    public static NativeBattleTheEnd85DirectorStep Advance784E30(NativeBattleTheEnd85DirectorInput input, NativeBattleTheEnd85DirectorServices services)
    {
        if (input.Task.Callback != 0x784E30 || input.Context.IsDefault || input.Context.Length != 84)
        {
            throw new InvalidDataException("Native The End director requires its actual task and current84-byte context.");
        }
        byte[] context = input.Context.ToArray();
        bool advanceScript = false;
        Word(70, unchecked((ushort)(Value(70) + 1)));
        if (Value(64) != Value(66))
        {
            Word(64, Value(66));
            Word(70, 0);
            input = services.SpawnStage(Publish());
            context = input.Context.ToArray();
        }
        if (Value(66) != Value(68))
        {
            Word(66, Value(68));
        }
        int phase = unchecked((sbyte)input.Task.ReadByte(41));
        switch (phase)
        {
            case 0:
                input = services.SetMode4A8480(Publish(), false);
                Reload();
                Request(1);
                break;
            case 1:
            case 6:
            case 10:
            case 13:
            case 16:
            case 19:
                int required = phase switch { 1 => 1, 6 => 2, 10 => 3, 13 => 4, 16 => 5, _ => 6 };
                if (Value(64) >= required)
                {
                    if (phase is 1 or 10)
                    {
                        input = services.PlaySound(Publish(), phase == 1 ? 0x156C7F4u : 0x156C7F8u);
                        Reload();
                    }
                    Next();
                }
                break;
            case 2:
                if (input.IoRelease358 == 0 && input.ScriptOpcode == 0)
                {
                    input = input with { IoRelease358 = 1 };
                    advanceScript = true;
                    Next();
                }
                break;
            case 3:
                if (input.IoRelease358 == 0)
                {
                    Freeze(false);
                    Next();
                }
                else if (unchecked((short)Value(70)) >= 74)
                {
                    Freeze(true);
                }
                break;
            case 4:
            case 11:
            case 14:
            case 17:
            case 20:
                if (unchecked((short)Value(70)) >= (phase switch { 4 => 75, 11 => 60, 14 or 17 => 45, _ => 10 }))
                {
                    if (phase == 20)
                    {
                        Word(72, 1);
                        input = services.SetMode4A8480(Publish(), true);
                        Reload();
                    }
                    Next();
                }
                break;
            case 5:
            case 9:
            case 12:
            case 15:
            case 18:
                Request(phase switch { 5 => 2, 9 => 3, 12 => 4, 15 => 5, _ => 6 });
                break;
            case 7:
                if (unchecked((short)Value(70)) >= 14)
                {
                    input = input with { Task = input.Task.WriteWord(68, 6) };
                    input = services.AppendOverlay(Publish());
                    Reload();
                    Next();
                }
                break;
            case 8:
                input = input with { Task = input.Task.WriteWord(68, unchecked((ushort)(input.Task.ReadWord(68) - 1))) };
                if (unchecked((short)input.Task.ReadWord(68)) <= 0)
                {
                    Next();
                }
                break;
            case 21:
                input = input with { Task = input.Task.WriteByte(38, (byte)(input.Task.ReadByte(38) | 1)) };
                Next();
                break;
            case 22:
                break;
            default:
                throw new NotSupportedException($"Native The End signed director phase{phase} is unsupported.");
        }
        input = input with { Task = input.Task.WriteWord(36, unchecked((ushort)(input.Task.ReadWord(36) + 1))) };
        return new(Publish(), (input.Task.ReadByte(38) & 1) != 0 && input.Task.ReadByte(40) == 0, advanceScript);

        ushort Value(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(context.AsSpan(offset));
        void Word(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(context.AsSpan(offset), value);
        NativeBattleTheEnd85DirectorInput Publish() => input with { Context = context.ToImmutableArray() };
        void Reload() => context = input.Context.ToArray();
        void Next() => input = input with { Task = input.Task.WriteByte(41, unchecked((byte)(input.Task.ReadByte(41) + 1))) };
        void Request(int value)
        {
            if (Value(68) == Value(64) && unchecked((short)Value(68)) == value - 1)
            {
                Word(68, (ushort)value);
                Next();
            }
        }
        void Freeze(bool enabled)
        {
            if (enabled && input.IoFreeze25C21D4 == 0 || !enabled && input.IoFreeze25C21D4 == 1)
            {
                input = input with { IoFreeze25C21D4 = enabled ? 1u : 0u, Pause25BC894 = enabled ? 1u : 0u };
            }
        }
    }
}
