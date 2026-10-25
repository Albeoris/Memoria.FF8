using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect31Controller
{
    private static NativeBattleEffect31Step AdvanceRoot(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        frame = WriteBytes(frame, 0x2793E58, PackedArena(inputs.ReadView(frame)), 32);
        frame = frame with { State = frame.State.WriteDword(ScratchCursorGlobal, 0x2793E58).WriteDword(ViewGlobal, 0x2793E58) };
        bool odd = (Read(frame, address).ReadByte(0x5C) & 1) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, frame.State.ReadDword(odd ? 0x26D2DA4u : 0x26D2DA0u)).WriteDword(AuxiliaryCursorGlobal, frame.State.ReadDword(odd ? 0x26D219Cu : 0x26D2198u)) };
        frame = inputs.Execute(frame, new NativeBattleEffect31UpdateCaster8DC740(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (NativePhase(task, 10))
        {
            case 0:
            case 1:
            case 7:
            case 8:
                frame = IncrementPhase(frame, address);
                break;
            case 2:
                if (task.ReadByte(0x28) == 0)
                {
                    uint emitters = frame.State.ReadDword(AllocationCursorGlobal);
                    uint particles = checked(emitters + 0x438);
                    frame = frame with { State = frame.State.WriteDword(EmitterBankGlobal, emitters).WriteDword(ParticleBankGlobal, particles).WriteDword(AllocationCursorGlobal, checked(particles + 0x8340)) };
                    frame = ClearBytes(ClearBytes(frame, emitters, 0x438), particles, 0x8340);
                    frame = frame with { State = frame.State.WriteWord(EmitterIndexGlobal, 0).WriteWord(ParticleIndexGlobal, 0).WriteWord(0x26D2DA8, 0).WriteWord(0x26CDD94, 0) };
                    (frame, _) = AppendHierarchy(frame, 6, 0x86CD70, 0x40, address, inputs);
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, task.WriteByte(0x63, 1));
                (frame, _) = AppendHierarchy(frame, 1, 0x86CE90, 0x58, address, inputs);
                frame = IncrementPhase(frame, address);
                break;
            case 4:
                if (task.ReadByte(0x63) == 0)
                {
                    sbyte group = unchecked((sbyte)task.ReadByte(0x2A));
                    if (group < unchecked((short)task.ReadWord(0x58)))
                    {
                        frame = Write(frame, address, task.WriteByte(0x2A, unchecked((byte)(group + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1))));
                    }
                    else
                    {
                        frame = IncrementPhase(Write(frame, address, task.WriteWord(0x60, 10)), address);
                    }
                }
                break;
            case 5:
                short countdown = unchecked((short)(task.ReadWord(0x60) - 1));
                frame = Write(frame, address, task.WriteWord(0x60, unchecked((ushort)countdown)));
                if (countdown <= 0)
                {
                    (frame, _) = AppendHierarchy(frame, 6, 0x8734A0, 0x40, address, inputs);
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 6:
                if (task.ReadWord(0x5E) == 0)
                {
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 9:
                frame = IncrementPhase(MarkComplete(frame, address), address);
                break;
            case 10:
                break;
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, 0));
        frame = frame with { State = frame.State.WriteWord(0x26D33C8, 0).WriteWord(0x26D2DAA, 0) };
        for (int index = 1; index < Pools.Length; index++)
        {
            (frame, int survivors) = Scan(frame, index, resources, inputs);
            task = Read(frame, address);
            frame = Write(frame, address, task.WriteWord(0x5E, unchecked((ushort)(task.ReadWord(0x5E) + survivors))));
        }
        task = Read(frame, address);
        return Finish(Write(frame, address, task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1)))), address);
    }

    private static NativeBattleEffect31Step AdvanceFade(NativeBattleEffect31Frame frame, uint address, bool outgoing, NativeBattleEffect31Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (NativePhase(task, 2))
        {
            case 0:
                frame = IncrementPhase(Write(frame, address, task.WriteWord(0x1C, outgoing ? (ushort)0x400 : (ushort)0)), address);
                if (!outgoing)
                {
                    for (int group = 0; group < 4; group++)
                    {
                        frame = inputs.Execute(frame, new NativeBattleEffect31StageTransition(group, 0, true));
                    }
                }
                break;
            case 1:
                short amount = unchecked((short)(task.ReadWord(0x1C) + (outgoing ? -0x100 : 0x100)));
                bool complete = outgoing ? amount <= 0 : amount >= 0x400;
                if (complete)
                {
                    amount = outgoing ? (short)0 : (short)0x400;
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 2);
                }
                frame = Write(frame, address, task.WriteWord(0x1C, unchecked((ushort)amount)));
                for (int group = 0; group < 4; group++)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect31StageTransition(group, unchecked((ushort)amount), false));
                }
                break;
            case 2:
                break;
        }
        return Finish(frame, address);
    }

    private static NativeBattleEffect31Step AdvanceTarget(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect31UpdateTarget8DC610(address));
        frame = inputs.Execute(frame, new NativeBattleEffect31UpdateBounds8DC870(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (NativePhase(task, 3))
        {
            case 0:
                (uint Callback, uint Data, ushort Clear, ushort Mode)[] actors = [(0x871F60, 0x15F98F8, 0x100, 0), (0x872920, 0x1602658, 0x2C, 1), (0x872B10, 0x1608024, 0x7C, 2), (0x8731D0, 0x160AF24, 0x6C, 3)];
                foreach ((uint callback, uint data, ushort clear, ushort mode) in actors)
                {
                    (frame, uint child) = AppendHierarchy(frame, 4, callback, 0x1B0, address, inputs);
                    frame = Write(frame, child, Read(frame, child).WriteWord(0x80, mode).WriteDword(0x74, data).WriteDword(0x78, unchecked((uint)(int)(short)clear)).WriteWord(0x82, 0));
                }
                (frame, uint custom) = AppendHierarchy(frame, 3, 0x86D1D0, 0x2A4, address, inputs);
                frame = Write(frame, custom, Read(frame, custom).WriteDword(0x30, CustomDirectoryAddress).WriteWord(0x298, 0).WriteWord(0x29C, 0).WriteWord(0x29E, 0x2D));
                (frame, _) = AppendHierarchy(frame, 5, 0x86D100, 0x80, address, inputs);
                frame = IncrementPhase(frame, address);
                break;
            case 1:
                if (task.ReadWord(0x24) == 30)
                {
                    uint root = task.ReadDword(0x10);
                    frame = Write(frame, root, Read(frame, root).WriteByte(0x63, 0));
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 2:
                if (unchecked((short)task.ReadWord(0x24)) >= 30)
                {
                    int group = checked(Parameters(frame).HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                    int first = inputs.ReadPackets(frame).Headers[group].FirstContactIndex ?? throw new NotSupportedException("Effect31 requires the selected group's current contact pointer.");
                    int contact = checked(first + unchecked((sbyte)task.ReadByte(0x2B)));
                    frame = inputs.Execute(frame, new NativeBattleEffect31Contact506690(contact));
                    frame = IncrementPhase(MarkComplete(frame, address), address);
                }
                break;
            case 3:
                break;
        }
        task = Read(frame, address);
        ushort age = task.ReadWord(0x24);
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect31Sound501330(SoundAddress, 0, 0x80));
        }
        if (age is 17 or 26)
        {
            (frame, uint child) = AppendHierarchy(frame, 2, 0x86CF60, 0x3C, address, inputs);
            frame = Write(frame, child, Read(frame, child).WriteDword(0x30, age == 17 ? 0x15F8DC8u : 0x15F8DD8u));
        }
        return Finish(frame, address);
    }

    private static NativeBattleEffect31Step AdvanceEmpty(NativeBattleEffect31Frame frame, uint address)
    {
        if (NativePhase(Read(frame, address), 1) == 0)
        {
            frame = IncrementPhase(MarkComplete(frame, address), address);
        }
        return Finish(frame, address);
    }

    private static NativeBattleEffect31Step AdvanceOverlay(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint pointer = unchecked(task.ReadDword(0x30) + (uint)(unchecked((short)task.ReadWord(0x38)) * 4));
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(pointer, 4));
        byte marker = unchecked((byte)(value >> 24));
        if (marker == 0xFF)
        {
            frame = MarkComplete(frame, address);
        }
        else
        {
            if (marker != 0xFE || unchecked((short)task.ReadWord(0x24)) >= unchecked((byte)(value >> 16)))
            {
                task = task.WriteWord(0x38, unchecked((ushort)(task.ReadWord(0x38) + 1)));
                if (marker == 0xFE)
                {
                    value = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(unchecked(pointer + 4), 4));
                }
                task = task.WriteDword(0x34, value);
                frame = Write(frame, address, task);
            }
            frame = inputs.Execute(frame, new NativeBattleEffect31DrawOverlay(Read(frame, address).ReadDword(0x34)));
        }
        return Finish(frame, address);
    }
}
