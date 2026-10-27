using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco100SecondarySettings([property: JsonRequired] ImmutableArray<byte> Bytes, [property: JsonRequired] ImmutableArray<bool> Known)
{
    public static NativeBattleChoco100SecondarySettings FromColdProcess() => new(new byte[0x26].ToImmutableArray(), Enumerable.Repeat(true, 0x26).ToImmutableArray());
    public static NativeBattleChoco100SecondarySettings Unknown() => new(new byte[0x26].ToImmutableArray(), new bool[0x26].ToImmutableArray());

    public void Validate()
    {
        if (Bytes.IsDefault || Known.IsDefault || Bytes.Length != 0x26 || Known.Length != 0x26)
        {
            throw new InvalidDataException("Native Choco secondary settings require their physical byte and validity owners.");
        }
    }

    public uint WorkAddress()
    {
        Validate();
        if (!Known.Skip(4).Take(4).All(value => value))
        {
            throw new NotSupportedException("Native Choco secondary settings have no executed work pointer store.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan().Slice(4));
    }

    public NativeBattleChoco100SecondarySettings Initialize717450(uint work)
    {
        Validate();
        byte[] bytes = Bytes.ToArray();
        bool[] known = Known.ToArray();
        Dword(4, work);
        Word(0x14, 0);
        Word(0x16, 0);
        Word(0x18, 0x140);
        Word(0x1A, 0);
        for (int index = 0x1C; index <= 0x1E; index++)
        {
            bytes[index] = 0x80;
            known[index] = true;
        }
        Dword(0x20, uint.MaxValue);
        Word(0x24, 0);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());

        void Word(int offset, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(known, true, offset, 2);
        }
        void Dword(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(known, true, offset, 4);
        }
    }
}

public sealed record NativeBattleChoco100SecondaryFrame(NativeBattleModulePoolTask Task, NativeBattleGraphicsModel Actor, NativeBattleChoco100SecondarySettings? Settings);
public sealed record NativeBattleChoco100SecondaryStep(NativeBattleChoco100SecondaryFrame Frame, bool ReadyToComplete);

public static class NativeBattleChoco100Secondary
{
    public static NativeBattleChoco100SecondaryStep Advance716C20(NativeBattleChoco100SecondaryFrame frame, uint workAddress, Func<NativeBattleChoco100SecondaryFrame, NativeBattleChoco100SecondaryFrame> draw)
    {
        if (frame.Task.Callback != 0x716C20 || frame.Task.PayloadLength != 0xD0 - 12 || frame.Actor.Index != frame.Task.ReadByte(0x2D) || frame.Actor.Index is < 0 or > 6)
        {
            throw new InvalidDataException("Native Choco actor secondary requires its complete selected actor and task.");
        }
        switch (unchecked((sbyte)frame.Task.ReadByte(0x29)))
        {
            case 0:
                Store(frame.Task.WriteWord(0xCC, 4));
                Next();
                break;
            case 1:
                if (Countdown() <= 0)
                {
                    for (int offset = 0x6C; offset < 0xCC; offset += 2)
                    {
                        Store(frame.Task.WriteWord(offset, 0));
                    }
                    Store(frame.Task.WriteDword(0xAC, workAddress).WriteDword(0xB0, 0x2565890).WriteDword(0xB4, checked(0x1D972C0u + 0x9Cu * (uint)frame.Actor.Index)).WriteDword(0xB8, 0x1D8E054).WriteDword(0xBC, 0x1D8E054));
                    frame = frame with { Settings = (frame.Settings ?? NativeBattleChoco100SecondarySettings.Unknown()).Initialize717450(workAddress), Actor = frame.Actor with { Flags0 = (ushort)(frame.Actor.Flags0 | 4), Rotation12 = 0xC00 } };
                    NativeBattleEffectLocalBounds bounds = frame.Actor.GeometryBounds52 ?? throw new NotSupportedException("Native Choco actor secondary requires its retained bounds.");
                    short height = unchecked((short)(bounds.Maximum.Y - bounds.Minimum.Y));
                    ushort frequency = height <= 0x500 ? (ushort)8 : height <= 0x700 ? (ushort)7 : height <= 0x900 ? (ushort)6 : height <= 0xB00 ? (ushort)5 : height <= 0xD00 ? (ushort)4 : height <= 0xF00 ? (ushort)3 : (ushort)2;
                    Store(frame.Task.WriteWord(0x26, (ushort)(frame.Task.ReadWord(0x26) | 8)).WriteWord(0xC2, (ushort)Math.Clamp((int)height, 0x400, 0x1000)).WriteWord(0xC8, frequency).WriteWord(0xC6, 0x1000).WriteWord(0xC4, 0).WriteWord(0xCC, 45));
                    Next();
                }
                break;
            case 2:
                if (Countdown() <= 0)
                {
                    Next();
                }
                break;
            case 3:
                short rotation = (short)(((frame.Actor.Rotation12 ?? throw new NotSupportedException("Native Choco secondary requires its actual actor rotation.")) + 0x40) & 0xFFF);
                if (rotation <= 0x800)
                {
                    rotation = 0;
                    Next();
                }
                frame = frame with { Actor = frame.Actor with { Rotation12 = rotation } };
                short radius = unchecked((short)(frame.Task.ReadWord(0xC4) + 0x200));
                Store(frame.Task.WriteWord(0xC4, radius >= 0x1000 ? (ushort)0x1000 : unchecked((ushort)radius)));
                break;
            case 4:
                short falling = unchecked((short)(frame.Task.ReadWord(0xC4) - 0x200));
                if (falling <= 0)
                {
                    falling = 0;
                    Next();
                }
                Store(frame.Task.WriteWord(0xC4, unchecked((ushort)falling)).WriteWord(0xC6, unchecked((ushort)falling)));
                break;
            case 5:
                if (Countdown() <= 0)
                {
                    frame = frame with { Actor = frame.Actor with { Flags0 = (ushort)(frame.Actor.Flags0 & 0xFFFB) } };
                    Store(frame.Task.WriteWord(0x26, (ushort)((frame.Task.ReadWord(0x26) & 0xFFF7) | 1)));
                    Next();
                }
                break;
            case 6:
                break;
            default:
                throw new NotSupportedException("Native Choco actor secondary leaves its seven original phases.");
        }
        if ((frame.Task.ReadByte(0x26) & 8) != 0)
        {
            Store(frame.Task.WriteWord(0xC0, (ushort)((frame.Task.ReadWord(0xC0) + 0x180) & 0xFFF)));
            frame = draw(frame);
        }
        Store(frame.Task.WriteWord(0x24, unchecked((ushort)(frame.Task.ReadWord(0x24) + 1))));
        return new(frame, (frame.Task.ReadByte(0x26) & 1) != 0 && frame.Task.ReadByte(0x28) == 0);

        void Store(NativeBattleModulePoolTask task) => frame = frame with { Task = task };
        void Next() => Store(frame.Task.WriteByte(0x29, unchecked((byte)(frame.Task.ReadByte(0x29) + 1))));
        short Countdown()
        {
            ushort timer = unchecked((ushort)(frame.Task.ReadWord(0xCC) - 1));
            Store(frame.Task.WriteWord(0xCC, timer));
            return unchecked((short)timer);
        }
    }
}
