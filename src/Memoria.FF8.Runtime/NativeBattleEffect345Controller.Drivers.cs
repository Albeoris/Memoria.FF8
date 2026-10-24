namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect345Controller
{
    public static uint Workspace(NativeBattleEffect345Frame frame) => Dword345(ReadBytes(frame, 0x1524658, 4), 0);

    public static int ContactIndex(NativeBattleEffect345Frame frame, NativeBattleModulePoolTask task, NativeBattleEffect345Inputs inputs)
    {
        if (task.ReadDword(0x0C) != 0x1D99A78)
        {
            throw new NotSupportedException("Effect345 requires its current action parameter owner.");
        }
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        int header = checked(Parameters(frame).HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
        if ((uint)header >= packets.Headers.Length)
        {
            throw new InvalidDataException("Effect345 signed header selector leaves its physical packet storage.");
        }
        int contact = checked((packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect345 requires its physical contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
        _ = packets.ReadContact(contact);
        return contact;
    }

    private static NativeBattleEffect345Step AdvanceTarget(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect345UpdateTarget8DC610(address));
        frame = inputs.Execute(frame, new NativeBattleEffect345UpdateBounds8DC870(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (NativePhase(task, 4))
        {
            case 0:
                frame = InitializeWorkspace705F00(frame, address, inputs);
                (frame, _) = AppendHierarchy(frame, 2, 0x705FF0, 72, address, inputs);
                frame = IncrementPhase(frame, address);
                break;
            case 1:
                if (Word345(ReadBytes(frame, Workspace(frame) + 0x48, 2), 0) == 1)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect345Contact506690(ContactIndex(frame, task, inputs)));
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 2:
                if (task.ReadByte(0x28) == 0 || Word345(ReadBytes(frame, Workspace(frame) + 0x4C, 2), 0) == 1)
                {
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 3:
                frame = MarkComplete(frame, address);
                uint root = Read(frame, address).ReadDword(0x10);
                frame = Write(frame, root, Read(frame, root).WriteByte(0x63, 0));
                frame = IncrementPhase(frame, address);
                break;
            case 4:
                break;
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect345Frame InitializeWorkspace705F00(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Inputs inputs)
    {
        uint workspace = Workspace(frame);
        frame = ClearBytes(frame, workspace, 0x54);
        byte actor = Read(frame, address).ReadByte(0x2D);
        uint modelAddress = PhysicalModelAddress(actor);
        NativeBattleEffect345PointStep anchor = inputs.Point502170(frame, modelAddress, 241, 0, ReadBytes(frame, workspace + 8, 8));
        frame = WriteBytes(anchor.Frame, workspace + 8, anchor.Output8, 8);
        NativeBattleEffectArena model = inputs.ReadModelArena9C(frame, modelAddress);
        NativeBattleEffectArena state = ReadBytes(frame, workspace, 0x54);
        state = CopyBytes(model, 0x1C, state, 0, 4);
        state = CopyBytes(model, 0x20, state, 4, 4).Write(2, BitConverter.GetBytes((short)0)).Write(0xA, BitConverter.GetBytes((short)0));
        int scaled = unchecked(Word345(model, 0x26) * 0x7D0) >> 12;
        short z = unchecked((short)(Word345(state, 4) + (actor <= 2 ? -0x200 - scaled : 0x200 + scaled)));
        state = state.Write(4, BitConverter.GetBytes(z));
        short dx = unchecked((short)(Word345(state, 8) - Word345(state, 0)));
        short dz = unchecked((short)(Word345(state, 0xC) - Word345(state, 4)));
        int angle = NativePrecision53.RoundInteger(Math.Atan2(dx, dz) * (double)BitConverter.Int32BitsToSingle(0x4422F983));
        state = state.Write(0x4A, BitConverter.GetBytes(unchecked((ushort)((angle - 0x800) & 0xFFF))));
        return WriteBytes(frame, workspace, state, 0x54);
    }

    public static NativeBattleEffect345Frame TrackWorkspace706060(NativeBattleEffect345Frame frame)
    {
        uint address = Workspace(frame);
        NativeBattleEffectArena state = ReadBytes(frame, address, 0x54);
        state = state.Write(0x46, BitConverter.GetBytes(unchecked((ushort)(Word345(state, 0x46) + 1))));
        ushort current = unchecked((ushort)Word345(state, 0x42));
        if (Word345(state, 0x40) != unchecked((short)current))
        {
            state = state.Write(0x40, BitConverter.GetBytes(current)).Write(0x46, BitConverter.GetBytes((ushort)0));
        }
        ushort requested = unchecked((ushort)Word345(state, 0x44));
        if (current != requested)
        {
            state = state.Write(0x42, BitConverter.GetBytes(requested));
        }
        return WriteBytes(frame, address, state, 0x54);
    }

    private static NativeBattleEffect345Step AdvanceSetup(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        frame = TrackWorkspace706060(frame);
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (NativePhase(task, 3))
        {
            case 0:
                if (task.ReadByte(0x2E) == 0)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect345UploadTim505E30(NativeBattleEffect345Resources.EmbeddedTim0Address));
                    frame = inputs.Execute(frame, new NativeBattleEffect345UploadTim505E30(NativeBattleEffect345Resources.EmbeddedTim1Address));
                }
                frame = Write(frame, address, Read(frame, address).WriteWord(0x44, 1));
                frame = IncrementPhase(frame, address);
                break;
            case 1:
                task = task.WriteWord(0x44, unchecked((ushort)(task.ReadWord(0x44) - 1)));
                frame = Write(frame, address, task);
                if (Signed(task, 0x44) <= 0)
                {
                    (frame, uint model) = AppendHierarchy(frame, 7, 0x706130, 320, address, inputs);
                    frame = inputs.InitializeOwnedModel8DD0F0(frame, model, resources.Model, 3).Frame;
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 2:
                frame = IncrementPhase(MarkComplete(frame, address), address);
                break;
            case 3:
                break;
        }
        return Finish(frame, address);
    }

    public static (NativeBattleEffect345Frame Frame, uint Address) AppendParticle706A30(NativeBattleEffect345Frame frame, uint parent, uint source, ushort frameGate, ushort lifetime, ushort type, NativeBattleEffect345Inputs inputs)
    {
        (frame, uint child) = AppendHierarchy(frame, 3, 0x706A80, 676, parent, inputs);
        frame = Write(frame, child, Read(frame, child).WriteDword(0x30, source).WriteWord(0x298, frameGate).WriteWord(0x29C, type).WriteWord(0x29E, lifetime));
        return (frame, child);
    }

    private static NativeBattleEffect345Step AdvanceParticles(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (NativePhase(task, 2))
        {
            case 0:
                if (Signed(task, 0x24) >= Signed(task, 0x298))
                {
                    frame = NativeBattleEffect345Particles.Initialize706B10(frame, address, resources, inputs);
                    frame = NativeBattleEffect345Particles.Advance7075D0(frame, address, resources, inputs).Frame;
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 1:
                NativeBattleEffect345Step result = NativeBattleEffect345Particles.Advance7075D0(frame, address, resources, inputs);
                frame = result.Frame;
                if (result.Result != 0)
                {
                    frame = IncrementPhase(MarkComplete(frame, address), address);
                }
                break;
            case 2:
                break;
        }
        return Finish(frame, address);
    }
}
