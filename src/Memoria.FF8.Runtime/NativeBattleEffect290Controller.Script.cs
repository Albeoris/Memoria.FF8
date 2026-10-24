using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect290Controller
{
    public static NativeBattleEffect290Frame BirthScript675650(NativeBattleEffect290Frame frame, uint source, NativeBattleEffect290Resources resources)
    {
        (frame, uint task) = AppendRequired(frame, ScriptDescriptor, 0x6756A0);
        frame = ClearDwords701200(frame, task + 12, 6);
        short index = ScriptWord290(ref frame, source, resources);
        uint point = ScriptPoint290(frame, index);
        return Write(frame, task, Read(frame, task).WriteDword(12, point).WriteDword(16, source + 2));
    }

    public static NativeBattleEffect290Step AdvanceScript6756A0(NativeBattleEffect290Frame frame, uint address, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs)
    {
        uint cursor = Read(frame, address).ReadDword(16);
        uint target = Read(frame, address).ReadDword(12);
        while (true)
        {
            short opcode = TakeScriptWord290(ref frame, ref cursor, resources);
            switch (opcode)
            {
                case 0x1000:
                    frame = SetScriptVector290(frame, target, ref cursor, resources, false);
                    break;
                case 0x1001:
                    frame = SetScriptVector290(frame, target, ref cursor, resources, true);
                    break;
                case 0x1002:
                    frame = SetScriptVector290(frame, address + 20, ref cursor, resources, false);
                    break;
                case 0x1003:
                    frame = SetScriptVector290(frame, address + 20, ref cursor, resources, true);
                    break;
                case 0x1004:
                    frame = AddScriptVector290(frame, address + 20, target);
                    if (Word290(ReadBytes(frame, target + 2, 2), 0) > 0)
                    {
                        frame = WriteBytes(frame, target + 2, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)0)), 2);
                        short velocity = Word290(ReadBytes(frame, address + 22, 2), 0);
                        frame = WriteBytes(frame, address + 22, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((short)((velocity >> 2) - velocity)))), 2);
                    }
                    break;
                case 0x1010:
                    frame = SetScriptVector290(frame, target + 8, ref cursor, resources, false);
                    break;
                case 0x1011:
                    frame = SetScriptVector290(frame, target + 8, ref cursor, resources, true);
                    break;
                case 0x1012:
                    frame = SetScriptVector290(frame, address + 28, ref cursor, resources, false);
                    break;
                case 0x1013:
                    frame = SetScriptVector290(frame, address + 28, ref cursor, resources, true);
                    break;
                case 0x1014:
                    frame = AddScriptVector290(frame, address + 28, target + 8);
                    break;
                case 0x1080:
                case 0x1081:
                case 0x1082:
                {
                    short index = TakeScriptWord290(ref frame, ref cursor, resources);
                    uint source = ScriptPoint290(frame, index);
                    int offset = opcode == 0x1082 ? 8 : 0;
                    int length = opcode == 0x1080 ? 16 : 8;
                    frame = WriteBytes(frame, target + (uint)offset, ReadBytes(frame, source + (uint)offset, length), length);
                    break;
                }
                case 0x1090:
                {
                    ImmutableArray<short>.Builder bones = ImmutableArray.CreateBuilder<short>();
                    short bone;
                    while ((bone = TakeScriptWord290(ref frame, ref cursor, resources)) != -1)
                    {
                        bones.Add(bone);
                    }
                    short limit = TakeScriptWord290(ref frame, ref cursor, resources);
                    short speed = TakeScriptWord290(ref frame, ref cursor, resources);
                    short scale = TakeScriptWord290(ref frame, ref cursor, resources);
                    short threshold = TakeScriptWord290(ref frame, ref cursor, resources);
                    frame = inputs.Execute(frame, new NativeBattleEffect290SelectFragments675AA0(frame.State.ReadDword(CasterGlobal), checked(frame.State.ReadDword(ArenaGlobal) + 0x1CF4), bones.ToImmutable(), limit, speed, scale, threshold));
                    break;
                }
                case 0x10A0:
                {
                    short bone = TakeScriptWord290(ref frame, ref cursor, resources);
                    short fraction = TakeScriptWord290(ref frame, ref cursor, resources);
                    cursor = checked(cursor + 2);
                    short scale = TakeScriptWord290(ref frame, ref cursor, resources);
                    frame = inputs.Execute(frame, new NativeBattleEffect290BirthTarget676340(frame.State.ReadDword(CasterGlobal), checked(frame.State.ReadDword(ArenaGlobal) + 0x1CF4), bone, fraction, scale));
                    break;
                }
                case 0x10F0:
                {
                    short count = ScriptWord290(ref frame, cursor, resources);
                    if (count != 0)
                    {
                        if (!NativeBattleEffect290Resources.ScriptCountAddresses.Contains(cursor))
                        {
                            throw new InvalidDataException("Effect290 loop store leaves its current native counter cells.");
                        }
                        frame = frame with { Staging = RequireStaging(frame).Write(cursor, BitConverter.GetBytes(unchecked((short)(count - 1)))) };
                        cursor = checked(cursor + 2);
                        short relative = ScriptWord290(ref frame, cursor, resources);
                        cursor = unchecked((uint)(cursor + relative * 2 - 4));
                    }
                    else
                    {
                        cursor = checked(cursor + 4);
                    }
                    break;
                }
                case 0x1100:
                    return new(frame, 2);
                default:
                    return new(Write(frame, address, Read(frame, address).WriteDword(16, cursor)), 0);
            }
        }
    }

    private static uint ScriptPoint290(NativeBattleEffect290Frame frame, int index)
    {
        if ((uint)index >= 28)
        {
            throw new InvalidDataException("Effect290 script point index leaves its current main rows.");
        }
        return checked(frame.State.ReadDword(ArenaGlobal) + 0x1CF4 + (uint)(index * 16));
    }

    private static short ScriptWord290(ref NativeBattleEffect290Frame frame, uint address, NativeBattleEffect290Resources resources)
    {
        (NativeBattleEffect290State state, NativeBattleFileStaging staging) = frame.State.BindSources(RequireStaging(frame), resources);
        frame = frame with { State = state, Staging = staging };
        return resources.ReadScriptWord(staging, address);
    }

    private static short TakeScriptWord290(ref NativeBattleEffect290Frame frame, ref uint cursor, NativeBattleEffect290Resources resources)
    {
        short value = ScriptWord290(ref frame, cursor, resources);
        cursor = checked(cursor + 2);
        return value;
    }

    private static NativeBattleEffect290Frame SetScriptVector290(NativeBattleEffect290Frame frame, uint destination, ref uint cursor, NativeBattleEffect290Resources resources, bool add)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            short value = TakeScriptWord290(ref frame, ref cursor, resources);
            uint field = destination + (uint)(axis * 2);
            if (add)
            {
                value = unchecked((short)(Word290(ReadBytes(frame, field, 2), 0) + value));
            }
            frame = WriteBytes(frame, field, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2);
        }
        return frame;
    }

    private static NativeBattleEffect290Frame AddScriptVector290(NativeBattleEffect290Frame frame, uint source, uint destination)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            uint offset = (uint)(axis * 2);
            short value = unchecked((short)(Word290(ReadBytes(frame, destination + offset, 2), 0) + Word290(ReadBytes(frame, source + offset, 2), 0)));
            frame = WriteBytes(frame, destination + offset, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2);
        }
        return frame;
    }
}
