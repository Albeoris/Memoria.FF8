using static Memoria.FF8.Runtime.NativeBattleSigh46Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSigh46Controller
{
    private static (NativeBattleSigh46Frame Frame, int Result) AdvanceRoot(NativeBattleSigh46Frame frame, uint address, NativeBattleSigh46Resources resources, NativeBattleSigh46Inputs inputs)
    {
        frame = frame with { Memory = frame.Memory with { SharedEffectView2793E58 = inputs.ReadView(frame) } };
        frame = Dword(Dword(frame, 0x26527A0, 0x2793E58), 0x264EC84, 0x2793E58);
        bool odd = (Read(frame, address).ReadWord(0x5C) & 1) != 0;
        frame = Dword(Dword(frame, PrimaryCursorGlobal, frame.State.ReadDword(odd ? 0x2652784u : 0x2652780u)), SecondaryCursorGlobal, frame.State.ReadDword(odd ? 0x2651B7Cu : 0x2651B78u));
        frame = inputs.Execute(frame, new NativeBattleSigh46UpdateCaster8DC740(address));
        switch (Phase(frame, address))
        {
            case 0:
            case 1:
                frame = Next(frame, address);
                break;
            case 2:
                if (Read(frame, address).ReadByte(0x28) == 0)
                {
                    uint cursor = frame.State.ReadDword(0x26527AC);
                    frame = Dword(Dword(Dword(frame, 0x265278C, cursor), 0x26527A4, unchecked(cursor + 0x870)), 0x26527AC, unchecked(cursor + 0x10EF0));
                    frame = WriteBytes(frame, cursor, NativeBattleEffectArena.Clear(), 0x870);
                    frame = WriteBytes(frame, unchecked(cursor + 0x870), NativeBattleEffectArena.Clear(), 0x10680);
                    frame = Word(Word(Word(Word(frame, 0x264EFA0, 0), 0x2652798, 0), 0x2652788, 0), 0x264EC68, 0);
                    (frame, _) = Append(frame, 0x2652600, 0x831EC0, 64, address, inputs.ReadPackets(frame));
                    frame = Next(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, Read(frame, address).WriteByte(0x63, 1));
                (frame, _) = Append(frame, 0x2652610, 0x831FE0, 88, address, inputs.ReadPackets(frame));
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
                        frame = Next(Write(frame, address, root.WriteWord(0x60, 5)), address);
                    }
                }
                break;
            case 5:
                ushort remaining = unchecked((ushort)(Read(frame, address).ReadWord(0x60) - 1));
                frame = Write(frame, address, Read(frame, address).WriteWord(0x60, remaining));
                if (unchecked((short)remaining) <= 0)
                {
                    (frame, _) = Append(frame, 0x2652600, 0x836420, 64, address, inputs.ReadPackets(frame));
                    frame = Next(frame, address);
                }
                break;
            case 6:
                if (Read(frame, address).ReadWord(0x5E) == 0)
                {
                    frame = Next(frame, address);
                }
                break;
            case 7:
                frame = inputs.Execute(frame, new NativeBattleSigh46CommonTextureLoad508630(frame.State.ReadDword(0x264EC60), checked(address + 0x62)));
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
                throw new NotSupportedException("Sigh root phase exceeds its eleven-entry table.");
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, 0));
        frame = Word(Word(frame, 0x265279A, 0), 0x265278A, 0);
        for (int pool = 1; pool < 6; pool++)
        {
            (frame, int survivors) = AdvancePool(frame, pool, resources, inputs);
            frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, unchecked((ushort)(Read(frame, address).ReadWord(0x5E) + survivors))));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5C, unchecked((ushort)(Read(frame, address).ReadWord(0x5C) + 1))));
        return Finish(frame, address);
    }

    private static (NativeBattleSigh46Frame Frame, int Result) AdvanceTarget(NativeBattleSigh46Frame frame, uint address, NativeBattleSigh46Resources resources, NativeBattleSigh46Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleSigh46UpdateTargetBounds8DC610_8DC870(address));
        switch (Phase(frame, address))
        {
            case 0:
                (frame, uint engine) = Append(frame, 0x2652370, 0x832100, 676, address, inputs.ReadPackets(frame));
                frame = Write(frame, engine, Read(frame, engine).WriteDword(0x30, 0x15C3398).WriteWord(0x298, 0).WriteWord(0x29C, 0).WriteWord(0x29E, 45));
                (frame, _) = Append(frame, 0x264ED60, 0x8359C0, 140, address, inputs.ReadPackets(frame));
                frame = Next(frame, address);
                break;
            case 1:
                if (Read(frame, address).ReadWord(0x24) == 40)
                {
                    uint parent = Read(frame, address).ReadDword(0x10);
                    frame = Write(frame, parent, Read(frame, parent).WriteByte(0x63, 0));
                    frame = Next(frame, address);
                }
                break;
            case 2:
                NativeBattleModulePoolTask task = Read(frame, address);
                if (Signed(task, 0x24) >= 40)
                {
                    int index = 0;
                    NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
                    NativeBattleActionHeaderSlot header = Header(frame, task, packets);
                    if (header.ReadByte(16) != 0)
                    {
                        do
                        {
                            packets = inputs.ReadPackets(frame);
                            header = Header(frame, Read(frame, address), packets);
                            int contact = checked((header.FirstContactIndex ?? throw new NotSupportedException("Sigh requires its actual contact pointer.")) + index);
                            _ = packets.ReadContact(contact);
                            frame = inputs.Execute(frame, new NativeBattleSigh46Contact506690(contact));
                            index++;
                            packets = inputs.ReadPackets(frame);
                            header = Header(frame, Read(frame, address), packets);
                        }
                        while (index < header.ReadByte(16));
                    }
                    frame = Done(frame, address);
                }
                break;
            case 3:
                break;
            default:
                throw new NotSupportedException("Sigh target phase exceeds its four-entry table.");
        }
        ushort age = Read(frame, address).ReadWord(0x24);
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleSigh46Sound501330(resources.Sound, 0, 128));
        }
        return Finish(frame, address);
    }

    private static (NativeBattleSigh46Frame Frame, int Result) AdvanceEngine(NativeBattleSigh46Frame frame, uint address, NativeBattleSigh46Resources resources, NativeBattleSigh46Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                if (Signed(Read(frame, address), 0x24) >= Signed(Read(frame, address), 0x298))
                {
                    frame = InitializeEngine832190(frame, address, resources, inputs);
                    (frame, _) = AdvanceEngine832BE0(frame, address, resources, inputs);
                    frame = Next(frame, address);
                }
                break;
            case 1:
                (frame, int result) = AdvanceEngine832BE0(frame, address, resources, inputs);
                if (result != 0)
                {
                    frame = Done(frame, address);
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Sigh engine phase exceeds its three-entry table.");
        }
        return Finish(frame, address);
    }

    private static (NativeBattleSigh46Frame Frame, int Result) AdvanceOpeningStage(NativeBattleSigh46Frame frame, uint address, NativeBattleSigh46Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, 0));
                for (int group = 0; group < 4; group++)
                {
                    frame = inputs.Execute(frame, new NativeBattleSigh46StageColorDepth(group, 0));
                    frame = inputs.Execute(frame, new NativeBattleSigh46StageRgb(group, 0, 0, 0));
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
                throw new NotSupportedException("Sigh opening stage phase exceeds its three-entry table.");
        }
        return Finish(frame, address);
    }

    private static (NativeBattleSigh46Frame Frame, int Result) AdvanceClosingStage(NativeBattleSigh46Frame frame, uint address, NativeBattleSigh46Inputs inputs)
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
                throw new NotSupportedException("Sigh closing stage phase exceeds its three-entry table.");
        }
        return Finish(frame, address);
    }

    private static NativeBattleSigh46Frame PublishStageDepth(NativeBattleSigh46Frame frame, uint address, NativeBattleSigh46Inputs inputs)
    {
        for (int group = 0; group < 4; group++)
        {
            frame = inputs.Execute(frame, new NativeBattleSigh46StageColorDepth(group, Signed(Read(frame, address), 0x1C)));
        }
        return frame;
    }

    private static NativeBattleActionHeaderSlot Header(NativeBattleSigh46Frame frame, NativeBattleModulePoolTask task, NativeBattleActionPacketsState packets)
    {
        if (task.ReadDword(0x0C) != 0x1D99A78)
        {
            throw new NotSupportedException("Sigh requires its actual captured parameter pointer.");
        }
        int index = checked(Parameters(frame).HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
        if ((uint)index >= packets.Headers.Length)
        {
            throw new NotSupportedException("Sigh signed header offset leaves its physical action arena.");
        }
        return packets.Headers[index];
    }
}
