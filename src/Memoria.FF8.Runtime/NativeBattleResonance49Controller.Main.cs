namespace Memoria.FF8.Runtime;

public static partial class NativeBattleResonance49Controller
{
    private static (NativeBattleResonance49Frame Frame, int Result) AdvanceRoot(NativeBattleResonance49Frame frame, uint address, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs)
    {
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        frame = frame with { Memory = frame.Memory with { SharedEffectView2793E58 = view } };
        frame = Dword(Dword(frame, 0x26480E4, 0x2793E58), 0x26465E0, 0x2793E58);
        bool odd = (Read(frame, address).ReadWord(0x5C) & 1) != 0;
        frame = Dword(Dword(frame, 0x26464DC, frame.State.ReadDword(odd ? 0x264781Cu : 0x2647818u)), 0x26465DC, frame.State.ReadDword(odd ? 0x26472D4u : 0x26472D0u));
        frame = inputs.Execute(frame, new NativeBattleResonance49UpdateCaster8DC740(address));
        switch (Phase(frame, address))
        {
            case 0:
            case 1:
            case 5:
                frame = Next(frame, address);
                break;
            case 2:
                if (Read(frame, address).ReadByte(0x28) == 0)
                {
                    frame = Next(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, Read(frame, address).WriteByte(0x63, 1));
                (frame, _) = Append(frame, 0x26475F0, 0x821840, 88, address, inputs.Packets);
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
                        frame = Next(frame, address);
                    }
                }
                break;
            case 6:
                if (Read(frame, address).ReadWord(0x5E) == 0)
                {
                    frame = Next(frame, address);
                }
                break;
            case 7:
                frame = inputs.Execute(frame, new NativeBattleResonance49CommonTextureLoad508630(frame.State.ReadDword(0x26464D8), checked(address + 0x62)));
                frame = Next(frame, address);
                break;
            case 8:
                if (Read(frame, address).ReadByte(0x62) != 0)
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
                throw new NotSupportedException("Resonance root phase exceeds its eleven-entry native table.");
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, 0));
        foreach (int pool in new[] { 1, 2, 4, 3, 5 })
        {
            (frame, int survivors) = AdvancePool(frame, pool, resources, inputs);
            NativeBattleModulePoolTask task = Read(frame, address);
            frame = Write(frame, address, task.WriteWord(0x5E, unchecked((ushort)(task.ReadWord(0x5E) + survivors))));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5C, unchecked((ushort)(Read(frame, address).ReadWord(0x5C) + 1))));
        return Finish(frame, address);
    }

    private static (NativeBattleResonance49Frame Frame, int Result) AdvanceTarget(NativeBattleResonance49Frame frame, uint address, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleResonance49UpdateTargetBounds8DC610_8DC870(address));
        switch (Phase(frame, address))
        {
            case 0:
                frame = InitializeEffect8218F0(frame, address, inputs);
                (frame, _) = Append(frame, 0x26465B8, 0x821970, 72, address, inputs.Packets);
                frame = Next(frame, address);
                break;
            case 1:
                if (frame.State.ReadWord(0x26478A0 + 0x48) == 1)
                {
                    frame = Next(frame, address);
                }
                break;
            case 2:
                if (Read(frame, address).ReadByte(0x28) == 0 || frame.State.ReadWord(0x26478A0 + 0x4C) == 1)
                {
                    frame = Next(frame, address);
                }
                break;
            case 3:
                NativeBattleModulePoolTask task = Read(frame, address);
                frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                uint root = task.ReadDword(0x10);
                frame = Write(frame, root, Read(frame, root).WriteByte(0x63, 0));
                frame = Next(frame, address);
                break;
            case 4:
                break;
            default:
                throw new NotSupportedException("Resonance target phase exceeds its five-entry native table.");
        }
        return Finish(frame, address);
    }

    private static NativeBattleResonance49Frame InitializeEffect8218F0(NativeBattleResonance49Frame frame, uint address, NativeBattleResonance49Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleModelTransform transform = inputs.ReadModel(frame, task.ReadByte(0x2D)).Transform ?? throw new NotSupportedException("Resonance requires its current target packed model position.");
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().Write(0, new byte[84]);
        view = NativeBattleEffectNodeContextMemory.Copy(PointView(transform.Position28, transform.Vertical34), 0, view, 8, 8);
        int angle = NativePrecision53.RoundInteger(Math.Atan2(transform.Position28.X, transform.Position28.Z) * (double)BitConverter.Int32BitsToSingle(0x4422F983));
        view = view.Write(0x4A, BitConverter.GetBytes(unchecked((ushort)((angle - 0x800) & 0xFFF))));
        return frame with { State = frame.State.WriteView(0x26478A0, view, 84) };
    }

    private static (NativeBattleResonance49Frame Frame, int Result) AdvanceEffect(NativeBattleResonance49Frame frame, uint address, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs)
    {
        frame = Word(frame, 0x26478E6, frame.State.ReadWord(0x26478E6) + 1);
        ushort observed = frame.State.ReadWord(0x26478E2);
        if (frame.State.ReadWord(0x26478E0) != observed)
        {
            frame = Word(Word(frame, 0x26478E0, observed), 0x26478E6, 0);
            if (unchecked((short)observed) == 1)
            {
                frame = SpawnModeOne821B30(frame, address, inputs);
            }
        }
        ushort requested = frame.State.ReadWord(0x26478E4);
        if (frame.State.ReadWord(0x26478E2) != requested)
        {
            frame = Word(frame, 0x26478E2, requested);
        }
        switch (Phase(frame, address))
        {
            case 0:
                if (frame.State.ReadWord(0x26478E4) == frame.State.ReadWord(0x26478E0) && Word(frame, 0x26478E0) == 0)
                {
                    frame = Next(Word(frame, 0x26478E4, 1), address);
                }
                break;
            case 1:
                if (Word(frame, 0x26478E0) >= 1)
                {
                    frame = Next(frame, address);
                }
                break;
            case 2:
                if (Word(frame, 0x26478E6) >= 14)
                {
                    frame = inputs.Execute(frame, new NativeBattleResonance49Sound501330(resources.Sound, 0, 128));
                    frame = Next(frame, address);
                }
                break;
            case 3:
                frame = Word(frame, 0x26478E8, 1);
                frame = Write(frame, address, Read(frame, address).WriteWord(0x26, (ushort)(Read(frame, address).ReadWord(0x26) | 1)));
                frame = Next(frame, address);
                break;
            case 4:
                break;
            default:
                throw new NotSupportedException("Resonance effect phase exceeds its five-entry native table.");
        }
        return Finish(frame, address);
    }

    private static NativeBattleResonance49Frame SpawnModeOne821B30(NativeBattleResonance49Frame frame, uint address, NativeBattleResonance49Inputs inputs)
    {
        (frame, _) = Append(frame, 0x26466D0, 0x824260, 128, address, inputs.Packets);
        frame = SpawnProgramme821C00(frame, address, 0x821C50, 0x17B329C, 0x2D4, 0, 3, inputs.Packets);
        frame = SpawnProgramme821C00(frame, address, 0x824140, 0x17B656C, 0x2C8, 1, 4, inputs.Packets);
        NativeBattleModulePoolTask parent = Read(frame, address);
        NativeBattleActionHeaderSlot header = Header(frame, parent, inputs.Packets);
        int count = header.ReadByte(16);
        for (int index = 0; index < count; index++)
        {
            (frame, uint child) = Append(frame, 0x26466D0, 0x824970, 128, address, inputs.Packets);
            NativeBattleContactPacket contact = inputs.Packets.ReadContact(checked((header.FirstContactIndex ?? throw new NotSupportedException("Resonance requires its physical contact pointer.")) + index));
            frame = Write(frame, child, Read(frame, child).WriteByte(0x2B, unchecked((byte)index)).WriteByte(0x2D, contact.Primary.ActorIndex));
        }
        return frame;
    }

    private static NativeBattleResonance49Frame SpawnProgramme821C00(NativeBattleResonance49Frame frame, uint parent, uint callback, uint source, short bytes, ushort selector, byte actor, NativeBattleActionPacketsState packets)
    {
        (frame, uint address) = Append(frame, 0x26472C0, callback, 872, parent, packets);
        NativeBattleModulePoolTask task = Read(frame, address).WriteDword(0x74, source).WriteDword(0x78, unchecked((uint)(int)bytes)).WriteWord(0x80, selector).WriteWord(0x82, 0).WriteByte(0x2C, actor);
        return Write(frame, address, task);
    }

    private static (NativeBattleResonance49Frame Frame, int Result) AdvanceProgramme(NativeBattleResonance49Frame frame, uint address, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                frame = InitializeProgramme(frame, address, resources, inputs);
                break;
            case 1:
                (frame, int remaining) = DrawProgramme(frame, address, resources, inputs);
                if (remaining == 0)
                {
                    frame = Done(frame, address);
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Resonance programme phase exceeds its three-entry native table.");
        }
        return Finish(frame, address);
    }

    private static (NativeBattleResonance49Frame Frame, int Result) AdvanceScreen(NativeBattleResonance49Frame frame, uint address, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                if (Signed(Read(frame, address), 0x24) >= 32)
                {
                    frame = InitializeScreen824300(frame, address, resources, inputs);
                    frame = Next(frame, address);
                }
                break;
            case 1:
                NativeBattleModulePoolTask task = Read(frame, address);
                foreach (int offset in new[] { 0x60, 0x62, 0x64, 0x70, 0x72, 0x74 })
                {
                    task = task.WriteWord(offset, 0x200);
                }
                task = task.WriteWord(0x58, 0).WriteWord(0x5A, 0x400).WriteWord(0x5C, 0x800).WriteByte(0x7C, 0x80).WriteByte(0x7D, 0x80).WriteByte(0x7E, 0x80);
                frame = Write(frame, address, task);
                frame = DrawScreen8244E0(frame, address, 5, resources, inputs);
                frame = PublishEnvironment824410(frame, resources, inputs);
                frame = Next(frame, address);
                break;
            case 2:
                short up = unchecked((short)(frame.State.ReadWord(0x26466B0) + 0x100));
                if (up >= 4096)
                {
                    up = 4096;
                    frame = Next(frame, address);
                }
                frame = Word(frame, 0x26466B0, up);
                frame = DrawScreen8244E0(frame, address, 5, resources, inputs);
                frame = PublishEnvironment824410(frame, resources, inputs);
                break;
            case 3:
                if (Signed(Read(frame, address), 0x24) > 56)
                {
                    frame = Next(frame, address);
                }
                frame = DrawScreen8244E0(frame, address, 5, resources, inputs);
                frame = PublishEnvironment824410(frame, resources, inputs);
                break;
            case 4:
                short down = unchecked((short)(frame.State.ReadWord(0x26466B0) - 0x80));
                if (down <= 0)
                {
                    down = 0;
                    frame = Next(frame, address);
                }
                frame = Word(frame, 0x26466B0, down);
                frame = DrawScreen8244E0(frame, address, 5, resources, inputs);
                frame = PublishEnvironment824410(frame, resources, inputs);
                break;
            case 5:
                frame = Done(frame, address);
                break;
            case 6:
                break;
            default:
                throw new NotSupportedException("Resonance screen phase exceeds its seven-entry native table.");
        }
        return Finish(frame, address);
    }

    private static (NativeBattleResonance49Frame Frame, int Result) AdvanceReaction(NativeBattleResonance49Frame frame, uint address, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                if (Word(frame, 0x26478E6) >= 28)
                {
                    frame = DrawRandom(frame, out ushort random);
                    int sequence = (random & 1) != 0 ? 5 : 4;
                    frame = Write(frame, address, Read(frame, address).WriteWord(0x78, (ushort)sequence));
                    frame = inputs.Execute(frame, new NativeBattleResonance49SelectSequence505CE0(Read(frame, address).ReadByte(0x2D), sequence));
                    frame = Next(frame, address);
                }
                break;
            case 1:
                if (CurrentSequence(frame, address, inputs) != Read(frame, address).ReadWord(0x78))
                {
                    frame = Write(frame, address, Read(frame, address).WriteWord(0x78, 6));
                    frame = inputs.Execute(frame, new NativeBattleResonance49SelectSequence505CE0(Read(frame, address).ReadByte(0x2D), 6));
                    frame = Next(frame, address);
                }
                break;
            case 2:
                if (Word(frame, 0x26478E6) >= 55)
                {
                    frame = Next(frame, address);
                }
                break;
            case 3:
                if (Word(frame, 0x26478E6) >= 75)
                {
                    frame = Next(frame, address);
                }
                break;
            case 4:
                NativeBattleModulePoolTask task = Read(frame, address);
                if (CurrentSequence(frame, address, inputs) != task.ReadWord(0x78))
                {
                    NativeBattleActionHeaderSlot header = Header(frame, task, inputs.Packets);
                    int contact = checked((header.FirstContactIndex ?? throw new NotSupportedException("Resonance requires its physical contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                    _ = inputs.Packets.ReadContact(contact);
                    frame = inputs.Execute(frame, new NativeBattleResonance49Contact506690(contact));
                    frame = Done(frame, address);
                }
                break;
            case 5:
                break;
            default:
                throw new NotSupportedException("Resonance reaction phase exceeds its six-entry native table.");
        }
        return Finish(frame, address);
    }

    private static byte CurrentSequence(NativeBattleResonance49Frame frame, uint address, NativeBattleResonance49Inputs inputs) => inputs.ReadModel(frame, Read(frame, address).ReadByte(0x2D)).Sequence ?? throw new NotSupportedException("Resonance reaction requires its current resource header sequence BYTE.");

    private static NativeBattleActionHeaderSlot Header(NativeBattleResonance49Frame frame, NativeBattleModulePoolTask task, NativeBattleActionPacketsState packets)
    {
        if (task.ReadDword(0x0C) != 0x1D99A78)
        {
            throw new NotSupportedException("Resonance requires its actual captured parameter pointer.");
        }
        int index = checked(Parameters(frame).HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
        if ((uint)index >= packets.Headers.Length)
        {
            throw new NotSupportedException("Resonance signed header offset leaves its physical action arena.");
        }
        return packets.Headers[index];
    }
}
