using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTheEnd85TaskInput(NativeBattleModulePoolTask Task, ImmutableArray<byte> Context, NativeBattleTheEnd85State State, NativeCrtRandomState Random);
public sealed record NativeBattleTheEnd85TaskStep(NativeBattleTheEnd85TaskInput Input, bool Complete);
public sealed record NativeBattleTheEnd85TaskDraw(NativeBattleTheEnd85TaskInput Input, int Remaining);
public sealed record NativeBattleTheEnd85TaskServices(Func<NativeBattleTheEnd85TaskInput, uint, uint, int, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask>, NativeBattleTheEnd85TaskInput> Spawn, Func<NativeBattleTheEnd85TaskInput, uint, short, NativeBattleTheEnd85TaskDraw> Draw, Func<NativeBattleTheEnd85TaskInput, NativeBattleTheEnd85TaskInput> InitializeModel, Func<NativeBattleTheEnd85TaskInput, NativeBattleTheEnd85TaskInput> AppendActors);

public static class NativeBattleTheEnd85ProgramTasks
{
    public static NativeBattleTheEnd85TaskStep Advance(NativeBattleTheEnd85TaskInput input, NativeBattleTheEnd85TaskServices services)
    {
        NativeBattleTheEnd85TaskCursor c = new(input, services);
        switch (input.Task.Callback)
        {
            case 0x78D250:
                switch (c.Phase)
                {
                    case 0:
                        c.W(88, c.S(88) + 8);
                        if (c.S(88) >= 128)
                        {
                            c.W(88, 128);
                            c.Next();
                        }
                        c.Draw(0x78D2E0);
                        break;
                    case 1:
                        c.W(90, c.S(90) + 16);
                        if (c.S(90) >= 128)
                        {
                            c.W(90, 128);
                            c.Next();
                        }
                        c.Draw(0x78D2E0);
                        break;
                    case 2:
                        if (c.Stage(6))
                        {
                            c.End(1);
                        }
                        else
                        {
                            c.Draw(0x78D2E0);
                        }
                        break;
                    case 3:
                        break;
                    default:
                        throw new InvalidDataException("Native The End environmental phase is outside its callback table.");
                }
                break;
            case 0x78D410:
                switch (c.Phase)
                {
                    case 0:
                        if (c.S(36) >= 16)
                        {
                            c.W(30, 24);
                            c.Next();
                        }
                        break;
                    case 1:
                        if (c.Stage(6))
                        {
                            c.W(92, 4);
                            c.Next();
                        }
                        c.Draw(0x78D4D0, c.S(30));
                        break;
                    case 2:
                        c.W(92, c.S(92) - 1);
                        if (c.S(92) <= 0)
                        {
                            c.Next();
                        }
                        c.Draw(0x78D4D0, c.S(30));
                        break;
                    case 3:
                        c.W(30, c.S(30) - 4);
                        if (c.S(30) > 0)
                        {
                            c.Draw(0x78D4D0, c.S(30));
                        }
                        else
                        {
                            c.End(1);
                        }
                        break;
                    case 4:
                        break;
                    default:
                        throw new InvalidDataException("Native The End environmental phase is outside its callback table.");
                }
                break;
            default:
                throw new InvalidDataException("Native The End task callback is not owned by this program helper.");
        }
        return c.Finish();
    }
}

internal sealed class NativeBattleTheEnd85TaskCursor
{
    public NativeBattleTheEnd85TaskInput Input;
    public readonly NativeBattleTheEnd85TaskServices Services;

    public NativeBattleTheEnd85TaskCursor(NativeBattleTheEnd85TaskInput input, NativeBattleTheEnd85TaskServices services)
    {
        if (input.Context.IsDefault || input.Context.Length != 84)
        {
            throw new InvalidDataException("Native The End task requires its current director context.");
        }
        Input = input;
        Services = services;
    }

    public int Phase => unchecked((sbyte)Input.Task.ReadByte(41));
    public short S(int offset) => unchecked((short)Input.Task.ReadWord(offset));
    public ushort U(int offset) => Input.Task.ReadWord(offset);
    public uint D(int offset) => Input.Task.ReadDword(offset);
    public void W(int offset, int value) => Input = Input with { Task = Input.Task.WriteWord(offset, unchecked((ushort)value)) };
    public void DW(int offset, uint value) => Input = Input with { Task = Input.Task.WriteDword(offset, value) };
    public void Byte(int offset, int value) => Input = Input with { Task = Input.Task.WriteByte(offset, unchecked((byte)value)) };
    public bool Stage(ushort required) => BinaryPrimitives.ReadUInt16LittleEndian(Input.Context.AsSpan().Slice(64)) >= required;
    public void Next() => Byte(41, Phase + 1);
    public void Flags(int value) => Byte(38, Input.Task.ReadByte(38) | value);
    public void End(int flags)
    {
        Flags(flags);
        Next();
    }
    public int Draw(uint callback, short parameter = 0)
    {
        NativeBattleTheEnd85TaskDraw result = Services.Draw(Input, callback, parameter);
        Input = result.Input;
        return result.Remaining;
    }
    public int Random()
    {
        NativeCrtRandomState random = Input.Random.Draw(out ushort value);
        Input = Input with { Random = random };
        return value;
    }
    public void Spawn(uint descriptor, uint callback, int size, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> initialize)
    {
        Input = Services.Spawn(Input, descriptor, callback, size, initialize);
    }
    public NativeBattleTheEnd85TaskStep Finish()
    {
        byte flags = Input.Task.ReadByte(38);
        W(36, U(36) + 1);
        return new(Input, (flags & 1) != 0 && Input.Task.ReadByte(40) == 0);
    }
    public void Vector(int offset, NativeBattleVector value)
    {
        W(offset, value.X);
        W(offset + 2, value.Y);
        W(offset + 4, value.Z);
    }
}
