using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSiren95ScreenServices(Func<NativeBattleSiren95Frame, uint, uint, NativeBattleSiren95Frame> Render);

public static class NativeBattleSiren95Screens
{
    public static NativeBattleSiren95TaskStep Advance745AD0(NativeBattleSiren95Frame frame, uint address, NativeBattleSiren95ScreenServices services)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x745AD0)
        {
            throw new InvalidDataException("Native Siren screen actor requires its actual callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                NativeBattleSiren95State state = State(frame);
                uint director = state.DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren screen actor requires its director pointer.");
                task = task.WriteWord(0x1C, 128).WriteWord(0x1E, 0xFB00).WriteWord(0x20, state.ReadWord(director + 4));
                short clock = unchecked((short)task.ReadWord(0x24));
                switch (unchecked((short)task.ReadWord(0xDE)))
                {
                    case 0 when clock >= 45:
                    case 1 when clock >= 65:
                        Next();
                        break;
                    case 2:
                        if (clock >= 85)
                        {
                            Next();
                        }
                        if (clock >= 105)
                        {
                            Next();
                        }
                        break;
                    case 3 when clock >= 105:
                        Next();
                        break;
                }
                break;
            case 1:
                frame = Initialize745BF0(frame, Row(), unchecked((short)task.ReadWord(0xDE)));
                Next();
                break;
            case 2:
                Draw();
                if (Spawned() >= 7)
                {
                    task = task.WriteWord(0xDC, 0);
                    Next();
                }
                else if (State(frame).ReadWord(unchecked(Row() + 0xCC)) == 10)
                {
                    short selector = unchecked((short)task.ReadWord(0xDE));
                    if (selector == 1)
                    {
                        task = task.WriteWord(0xDC, 0);
                    }
                    else if (selector is 0 or 2 or 3)
                    {
                        frame = frame with { Random = frame.Random.Draw(out ushort random) };
                        int basis = selector == 0 ? 2 : selector == 2 ? 4 : 6;
                        task = task.WriteWord(0xDC, unchecked((ushort)((random & 3) + basis)));
                    }
                    Next();
                }
                break;
            case 3:
                Draw();
                task = task.WriteWord(0xDC, unchecked((ushort)(task.ReadWord(0xDC) - 1)));
                if (Spawned() >= 7 || unchecked((short)task.ReadWord(0xDC)) <= 0)
                {
                    Next();
                }
                break;
            case 4:
                Draw();
                var fade = Fade746870(frame, Row());
                frame = fade.Frame;
                if (fade.Complete)
                {
                    if (Spawned() >= 7)
                    {
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                        Next();
                    }
                    else
                    {
                        task = task.WriteByte(0x29, 1);
                    }
                }
                break;
            case 5:
                break;
            default:
                throw new NotSupportedException($"Native Siren screen signed phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        frame = Put(frame, address, task);
        return new(frame, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        uint Row() => unchecked(0x2585AE8u + (uint)(unchecked((short)task.ReadWord(0xDE)) * 0xD4));
        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        short Spawned()
        {
            NativeBattleSiren95State current = State(frame);
            return unchecked((short)current.ReadWord((current.DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren screen actor requires its director pointer.")) + 0x40));
        }
        void Draw()
        {
            frame = services.Render(Put(frame, address, task), address, Row());
            task = Read(frame, address);
        }
    }

    public static NativeBattleSiren95Frame Initialize745BF0(NativeBattleSiren95Frame frame, uint address, short selector)
    {
        byte[] row = new byte[0xD4];
        Word(0, 0x0C000000);
        Word(4, 0x3E808080);
        foreach (int offset in new[] { 0x10, 0x1C, 0x28 })
        {
            Word(offset, 0x00808080);
        }
        Word(0xC, 0x3E548055);
        Word(0x18, 0x00BC807F);
        Word(0x24, 0x0000FF55);
        Word(0x30, 0x0000FF7F);
        Word(0x44, 0x3E000000);
        Word(0x48, 0x3E404040);
        Word(0x4C, 0x3E808080);
        Half(0x54, 256);
        Half(0x56, 256);
        Half(0x5A, 128);
        Half(0x5C, 128);
        Half(0x5E, 128);
        uint[][] uv = [[0x3E548000, 0x3E54802B, 0x3E548055], [0x00BC802B, 0x00BC8055, 0x00BC807F], [0x0000FF00, 0x0000FF2B, 0x0000FF55], [0x0000FF2B, 0x0000FF55, 0x0000FF7F]];
        for (int group = 0; group < uv.Length; group++)
        {
            for (int index = 0; index < 3; index++)
            {
                Word(0x60 + group * 24 + index * 4, uv[group][index]);
                Word(0x6C + group * 24 + index * 4, uv[group][index]);
            }
        }
        NativeBattleSiren95State state = State(frame).Write(address, row);
        for (int depth = 0; depth < 11; depth++)
        {
            int width = 192 + depth * 96;
            for (int column = 0; column < 4; column++)
            {
                byte[] point = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(point, unchecked((short)(width * 3 / 2 - column * width)));
                BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(4), unchecked((short)(-depth * 1024)));
                state = state.Write(unchecked(0x258EE38u + (uint)((depth * 4 + column) * 8)), point);
            }
        }
        frame = frame with { Random = frame.Random.Draw(out ushort first) };
        frame = frame with { Random = frame.Random.Draw(out ushort second) };
        state = state.Write(unchecked(address + 0xCE), BitConverter.GetBytes((ushort)(first & 4095))).Write(unchecked(address + 0xD0), BitConverter.GetBytes((ushort)(second & 4095)));
        if (selector is >= 0 and <= 3)
        {
            state = state.Write(unchecked(address + 0x3C), BitConverter.GetBytes((ushort)64)).Write(unchecked(address + 0x3E), BitConverter.GetBytes(new ushort[] { 384, 128, 3968, 3712 }[selector]));
        }
        return frame with { Memory = frame.Memory with { Siren95 = state } };

        void Word(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(row.AsSpan(offset), value);
        void Half(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan(offset), value);
    }

    public static (NativeBattleSiren95Frame Frame, bool Complete) Fade746870(NativeBattleSiren95Frame frame, uint address)
    {
        NativeBattleSiren95State state = State(frame);
        if (state.Read(unchecked(address + 0x4C), 1)[0] == 0)
        {
            return (frame, true);
        }
        short stage = unchecked((short)state.ReadWord((state.DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren screen fade requires its director pointer.")) + 0x40));
        for (int channel = 0; channel < 3; channel++)
        {
            uint far = unchecked(address + 0x4Cu + (uint)channel);
            uint near = unchecked(address + 0x48u + (uint)channel);
            state = state.Write(far, [unchecked((byte)(state.Read(far, 1)[0] - (stage >= 7 ? 8 : 4)))]).Write(near, [unchecked((byte)(state.Read(near, 1)[0] - (stage >= 7 ? 4 : 2)))]);
        }
        if (state.Read(unchecked(address + 0x4C), 1)[0] > 224)
        {
            state = state.Write(unchecked(address + 0x48), new byte[3]).Write(unchecked(address + 0x4C), new byte[3]);
        }
        return (frame with { Memory = frame.Memory with { Siren95 = state } }, false);
    }

    private static NativeBattleSiren95State State(NativeBattleSiren95Frame frame) => frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren screen actor requires its retained module bytes.");
    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Put(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
