using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMagmaBreath48Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMagmaBreath48Controller
{
    private static (NativeBattleMagmaBreath48Frame Frame, int Result) AdvanceRoot(NativeBattleMagmaBreath48Frame frame, uint address, NativeBattleMagmaBreath48Resources resources, NativeBattleMagmaBreath48Inputs inputs)
    {
        frame = frame with { Memory = frame.Memory with { SharedEffectView2793E58 = inputs.ReadView(frame) } };
        frame = Dword(Dword(frame, 0x26495D8, 0x2793E58), 0x26481D8, 0x2793E58);
        bool odd = (Read(frame, address).ReadWord(0x5C) & 1) != 0;
        frame = Dword(Dword(frame, PrimaryCursorGlobal, frame.State.ReadDword(odd ? 0x26495BCu : 0x26495B8u)), SecondaryCursorGlobal, frame.State.ReadDword(odd ? 0x26489B4u : 0x26489B0u));
        frame = inputs.Execute(frame, new NativeBattleMagmaBreath48UpdateCaster8DC740(address));
        switch (Phase(frame, address))
        {
            case 0:
            case 1:
            case 7:
            case 8:
                frame = Next(frame, address);
                break;
            case 2:
                if (Read(frame, address).ReadByte(0x28) == 0)
                {
                    uint cursor = frame.State.ReadDword(0x26495E4);
                    frame = Dword(Dword(Dword(frame, 0x26495C4, cursor), 0x26495DC, unchecked(cursor + 0x870)), 0x26495E4, unchecked(cursor + 0x10EF0));
                    frame = WriteBytes(frame, cursor, NativeBattleEffectArena.Clear(), 0x870);
                    frame = WriteBytes(frame, unchecked(cursor + 0x870), NativeBattleEffectArena.Clear(), 0x10680);
                    frame = Word(Word(Word(Word(frame, 0x26489A8, 0), 0x26495D0, 0), 0x26495C0, 0), 0x26481BC, 0);
                    (frame, _) = Append(frame, 0x2649438, 0x825050, 64, address, inputs.Packets);
                    frame = Next(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, Read(frame, address).WriteByte(0x63, 1));
                (frame, _) = Append(frame, 0x2649448, 0x825170, 88, address, inputs.Packets);
                frame = Next(frame, address);
                break;
            case 4:
                NativeBattleModulePoolTask root = Read(frame, address);
                if (root.ReadByte(0x63) == 0)
                {
                    if (unchecked((sbyte)root.ReadByte(0x2A)) < Signed(root, 0x58))
                    {
                        frame = Write(frame, address, root.WriteByte(0x2A, unchecked((byte)(root.ReadByte(0x2A) + 1))).WriteByte(0x2E, unchecked((byte)(root.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(root.ReadByte(0x29) - 1))));
                    }
                    else
                    {
                        frame = Next(Write(frame, address, root.WriteWord(0x60, 27)), address);
                    }
                }
                break;
            case 5:
                ushort remaining = unchecked((ushort)(Read(frame, address).ReadWord(0x60) - 1));
                frame = Write(frame, address, Read(frame, address).WriteWord(0x60, remaining));
                if (unchecked((short)remaining) <= 0)
                {
                    (frame, _) = Append(frame, 0x2649438, 0x82A6A0, 64, address, inputs.Packets);
                    frame = Next(frame, address);
                }
                break;
            case 6:
                if (Read(frame, address).ReadWord(0x5E) == 0)
                {
                    frame = Next(frame, address);
                }
                break;
            case 9:
                frame = Done(frame, address);
                break;
            case 10:
                break;
            default:
                throw new NotSupportedException("Magma Breath root phase exceeds its eleven-entry table.");
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, 0));
        frame = Word(Word(frame, 0x26495D2, 0), 0x26495C2, 0);
        for (int pool = 1; pool < 6; pool++)
        {
            (frame, int survivors) = AdvancePool(frame, pool, resources, inputs);
            frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, unchecked((ushort)(Read(frame, address).ReadWord(0x5E) + survivors))));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5C, unchecked((ushort)(Read(frame, address).ReadWord(0x5C) + 1))));
        return Finish(frame, address);
    }

    private static (NativeBattleMagmaBreath48Frame Frame, int Result) AdvanceTarget(NativeBattleMagmaBreath48Frame frame, uint address, NativeBattleMagmaBreath48Resources resources, NativeBattleMagmaBreath48Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleMagmaBreath48UpdateTargetBounds8DC610_8DC870(address));
        switch (Phase(frame, address))
        {
            case 0:
                (frame, uint engine) = Append(frame, 0x26491A8, 0x8253B0, 676, address, inputs.Packets);
                frame = Write(frame, engine, Read(frame, engine).WriteDword(0x30, 0x15B7C88).WriteWord(0x298, 15).WriteWord(0x29C, 0).WriteWord(0x29E, 40));
                (frame, _) = Append(frame, 0x26482B8, 0x82A540, 88, address, inputs.Packets);
                frame = Next(frame, address);
                break;
            case 1:
                if (Read(frame, address).ReadWord(0x24) == 28)
                {
                    uint parent = Read(frame, address).ReadDword(0x10);
                    frame = Write(frame, parent, Read(frame, parent).WriteByte(0x63, 0));
                    frame = Next(frame, address);
                }
                break;
            case 2:
                NativeBattleModulePoolTask task = Read(frame, address);
                if (Signed(task, 0x24) >= 55)
                {
                    NativeBattleActionHeaderSlot header = Header(frame, task, inputs.Packets);
                    int contact = checked((header.FirstContactIndex ?? throw new NotSupportedException("Magma Breath requires its physical contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                    _ = inputs.Packets.ReadContact(contact);
                    frame = inputs.Execute(frame, new NativeBattleMagmaBreath48Contact506690(contact));
                    frame = Done(frame, address);
                }
                break;
            case 3:
                break;
            default:
                throw new NotSupportedException("Magma Breath target phase exceeds its four-entry table.");
        }
        ushort age = Read(frame, address).ReadWord(0x24);
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleMagmaBreath48Sound501330(resources.Sound, 0, 128));
        }
        if (age == 4)
        {
            (frame, uint color) = Append(frame, 0x26480F0, 0x825220, 60, address, inputs.Packets);
            frame = Write(frame, color, Read(frame, color).WriteDword(0x30, 0x15ADC28));
        }
        return Finish(frame, address);
    }

    private static (NativeBattleMagmaBreath48Frame Frame, int Result) AdvanceEngine(NativeBattleMagmaBreath48Frame frame, uint address, NativeBattleMagmaBreath48Resources resources, NativeBattleMagmaBreath48Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                if (Signed(Read(frame, address), 0x24) >= Signed(Read(frame, address), 0x298))
                {
                    frame = InitializeEngine825440(frame, address, resources, inputs);
                    (frame, _) = AdvanceEngine825E90(frame, address, resources, inputs);
                    frame = Next(frame, address);
                }
                break;
            case 1:
                (frame, int result) = AdvanceEngine825E90(frame, address, resources, inputs);
                if (result != 0)
                {
                    frame = Done(frame, address);
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Magma Breath engine phase exceeds its three-entry table.");
        }
        return Finish(frame, address);
    }

    private static (NativeBattleMagmaBreath48Frame Frame, int Result) AdvanceMiscellaneous(NativeBattleMagmaBreath48Frame frame, uint address)
    {
        switch (Phase(frame, address))
        {
            case 0:
                frame = Done(frame, address);
                break;
            case 1:
                break;
            default:
                throw new NotSupportedException("Magma Breath miscellaneous phase exceeds its two-entry table.");
        }
        return Finish(frame, address);
    }

    private static (NativeBattleMagmaBreath48Frame Frame, int Result) AdvanceOpeningStage(NativeBattleMagmaBreath48Frame frame, uint address, NativeBattleMagmaBreath48Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, 0));
                for (int group = 0; group < 4; group++)
                {
                    frame = inputs.Execute(frame, new NativeBattleMagmaBreath48StageColorDepth(group, 0));
                    frame = inputs.Execute(frame, new NativeBattleMagmaBreath48StageRgb(group, 0, 0, 0));
                }
                frame = Next(frame, address);
                break;
            case 1:
                ushort value = unchecked((ushort)(Read(frame, address).ReadWord(0x1C) + 256));
                frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, value));
                if (unchecked((short)value) >= 1024)
                {
                    frame = Done(Write(frame, address, Read(frame, address).WriteWord(0x1C, 1024)), address);
                }
                frame = PublishStageDepth(frame, address, inputs);
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Magma Breath opening stage phase exceeds its three-entry table.");
        }
        return Finish(frame, address);
    }

    private static (NativeBattleMagmaBreath48Frame Frame, int Result) AdvanceClosingStage(NativeBattleMagmaBreath48Frame frame, uint address, NativeBattleMagmaBreath48Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                frame = Next(Write(frame, address, Read(frame, address).WriteWord(0x1C, 1024)), address);
                break;
            case 1:
                ushort value = unchecked((ushort)(Read(frame, address).ReadWord(0x1C) - 256));
                frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, value));
                if (unchecked((short)value) <= 0)
                {
                    frame = Done(Write(frame, address, Read(frame, address).WriteWord(0x1C, 0)), address);
                }
                frame = PublishStageDepth(frame, address, inputs);
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Magma Breath closing stage phase exceeds its three-entry table.");
        }
        return Finish(frame, address);
    }

    private static NativeBattleMagmaBreath48Frame PublishStageDepth(NativeBattleMagmaBreath48Frame frame, uint address, NativeBattleMagmaBreath48Inputs inputs)
    {
        for (int group = 0; group < 4; group++)
        {
            frame = inputs.Execute(frame, new NativeBattleMagmaBreath48StageColorDepth(group, Signed(Read(frame, address), 0x1C)));
        }
        return frame;
    }

    private static (NativeBattleMagmaBreath48Frame Frame, int Result) AdvanceColors(NativeBattleMagmaBreath48Frame frame, uint address, NativeBattleMagmaBreath48Resources resources, NativeBattleMagmaBreath48Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadDword(0x30) != 0x15ADC28)
        {
            throw new NotSupportedException("Magma Breath requires its actual color script pointer.");
        }
        int index = Signed(task, 0x38);
        uint command = ReadColor(index);
        if ((command >> 24) == 0xFF)
        {
            frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
            return Finish(frame, address);
        }
        if ((command >> 24) == 0xFE)
        {
            if (Signed(task, 0x24) >= (byte)(command >> 16))
            {
                index = unchecked((short)(index + 1));
                task = task.WriteWord(0x38, unchecked((ushort)index)).WriteDword(0x34, ReadColor(index));
            }
        }
        else
        {
            task = task.WriteDword(0x34, command).WriteWord(0x38, unchecked((ushort)(index + 1)));
        }
        frame = Write(frame, address, task);
        uint color = (task.ReadDword(0x34) & 0xFFFFFF) | 0x2A000000;
        NativeBattleGeometryPrimitive quad = new(7, [0x05000000, color, 0, 320, 0xD80000, 0xD80140], []);
        NativeBattleGeometryPrimitive mode = new(7, [0x02000000, 0xE100002A, 0], [0, 0, 0, 0]);
        SkinWrites quadWrites = new(24, 0);
        for (int word = 0; word < quad.Words.Length; word++)
        {
            quadWrites.Word(word * 4, quad.Words[word]);
        }
        SkinWrites modeWrites = new(12, 24);
        modeWrites.Byte(3, 2);
        modeWrites.Word(4, 0xE100002A);
        modeWrites.Word(8, 0);
        NativeBattleModulePacketBatch batch = new([new(quad, 0, 1), new(mode, 24, 0)], 36)
        {
            CapturedWrites = new([quadWrites.Attempt(quad), modeWrites.Attempt(mode)], 36)
        };
        frame = inputs.Execute(frame, new NativeBattleMagmaBreath48SubmitPackets(batch));
        return Finish(frame, address);

        uint ReadColor(int at)
        {
            if ((uint)at >= resources.ColorScript.Length)
            {
                throw new InvalidDataException("Magma Breath signed color index leaves its actual script.");
            }
            return resources.ColorScript[at];
        }
    }

    private static NativeBattleActionHeaderSlot Header(NativeBattleMagmaBreath48Frame frame, NativeBattleModulePoolTask task, NativeBattleActionPacketsState packets)
    {
        if (task.ReadDword(0x0C) != 0x1D99A78)
        {
            throw new NotSupportedException("Magma Breath requires its actual captured parameter pointer.");
        }
        int index = checked(Parameters(frame).HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
        if ((uint)index >= packets.Headers.Length)
        {
            throw new NotSupportedException("Magma Breath signed header offset leaves its physical action arena.");
        }
        return packets.Headers[index];
    }
}
