namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect345Controller
{
    private static NativeBattleEffect345Step AdvanceModel(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (NativePhase(task, 5))
        {
            case 0:
                frame = InitializeModel706940(frame, address, resources, inputs);
                break;
            case 1:
                if (task.ReadWord(0x136) == 20)
                {
                    frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & 0xF7)));
                }
                NativeBattleEffect345OwnedModelStep first = inputs.AdvanceOwnedModel8DD220(frame, address);
                frame = first.Frame;
                if (first.Result == 1)
                {
                    task = Read(frame, address);
                    frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & 0xF7)));
                    frame = inputs.SelectOwnedModel8DD190(frame, address, 6).Frame;
                    frame = inputs.Execute(frame, new NativeBattleEffect345Sound501330(0x1524660, 0, 128));
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 2:
                frame = AdvanceModelContact70C900(frame, address, resources, inputs);
                break;
            case 3:
                frame = inputs.AdvanceOwnedModel8DD220(frame, address).Frame;
                task = Read(frame, address);
                short scale = unchecked((short)(task.ReadWord(0x13A) + 0x200));
                if (scale >= 0x1000)
                {
                    scale = 0x1000;
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)).WriteByte(0x29, 4);
                }
                frame = Write(frame, address, task.WriteWord(0x13A, unchecked((ushort)scale)).WriteWord(0x13C, unchecked((ushort)scale)));
                frame = NativeBattleEffect345Body.ApplyMaterial70C590(frame, address, resources, inputs);
                frame = NativeBattleEffect345Body.ApplyColor70C6B0(frame, address, resources, inputs);
                break;
            case 4:
                if (task.ReadByte(0x2E) == task.ReadByte(0x2F))
                {
                    (frame, _) = AppendHierarchy(frame, 4, 0x70D0E0, 112, address, inputs);
                }
                frame = Write(frame, address, Read(frame, address).WriteWord(0x26, (ushort)(Read(frame, address).ReadWord(0x26) | 1)));
                frame = WriteBytes(frame, Workspace(frame) + 0x4C, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)1)), 2);
                frame = IncrementPhase(frame, address);
                break;
            case 5:
                break;
        }
        task = Read(frame, address);
        frame = frame with { Memory = frame.Memory.PublishEffectLight1D99AB0(Packed(TaskView(task, 0x70, 32))) };
        frame = Write(frame, address, task.WriteDword(0x84, unchecked((uint)Signed(task, 0x4C))).WriteDword(0x88, unchecked((uint)Signed(task, 0x4E))).WriteDword(0x8C, unchecked((uint)Signed(task, 0x50))));
        if ((Read(frame, address).ReadByte(0x26) & 4) == 0)
        {
            frame = NativeBattleEffect345Body.Advance7061F0(frame, address, resources, inputs);
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect345Frame InitializeModel706940(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleEffectArena workspace = ReadBytes(frame, Workspace(frame), 0x54);
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteDword(0x4C, Dword345(workspace, 0)).WriteDword(0x50, Dword345(workspace, 4)).WriteWord(0x3E, unchecked((ushort)Word345(workspace, 0x4A))).WriteDword(0x60, checked(address + 0x114)).WriteDword(0x114, 0x1000).WriteDword(0x118, 0x1000).WriteDword(0x11C, 0x1000);
        frame = Write(frame, address, task);
        uint target = Dword345(ReadBytes(frame, 0x15297A0, 4), 0);
        frame = WriteBytes(frame, checked(target + 0x20), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0)), 2);
        if (task.ReadByte(0x2E) == 0)
        {
            frame = inputs.SelectOwnedModel8DD190(frame, address, 5).Frame;
            frame = NativeBattleEffect345Body.InitializeAppearance70C810(frame, address, resources, inputs);
            (frame, _) = AppendParticle706A30(frame, address, 0x1643698, 0, 20, 2, inputs);
            (frame, _) = AppendHierarchy(frame, 4, 0x70C720, 112, address, inputs);
            frame = inputs.Execute(frame, new NativeBattleEffect345Sound501330(0x152465C, 0, 128));
            return IncrementPhase(frame, address);
        }
        frame = inputs.SelectOwnedModel8DD190(frame, address, 6).Frame;
        frame = Write(frame, address, Read(frame, address).WriteWord(0x13A, 0x1000).WriteWord(0x13C, 0x1000));
        frame = NativeBattleEffect345Body.ApplyMaterial70C590(frame, address, resources, inputs);
        frame = NativeBattleEffect345Body.ApplyColor70C6B0(frame, address, resources, inputs);
        frame = inputs.Execute(frame, new NativeBattleEffect345Sound501330(0x1524660, 0, 128));
        return Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 2))));
    }

    private static NativeBattleEffect345Frame AdvanceModelContact70C900(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short fade = unchecked((short)(task.ReadWord(0x13A) - 0x400));
        if (fade <= 0)
        {
            fade = 0;
        }
        frame = Write(frame, address, task.WriteWord(0x13A, unchecked((ushort)fade)).WriteWord(0x13C, unchecked((ushort)fade)));
        frame = NativeBattleEffect345Body.ApplyMaterial70C590(frame, address, resources, inputs);
        frame = NativeBattleEffect345Body.ApplyColor70C6B0(frame, address, resources, inputs);
        task = Read(frame, address);
        if (task.ReadWord(0x136) == 5)
        {
            frame = WriteBytes(frame, Workspace(frame) + 0x48, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)1)), 2);
            int contact = ContactIndex(frame, task, inputs);
            if ((inputs.ReadPackets(frame).ReadContact(contact).Primary.HitFlags3 & 4) == 0)
            {
                (frame, _) = AppendParticle706A30(frame, address, 0x164808C, 0, 20, 0, inputs);
                (frame, _) = AppendHierarchy(frame, 5, 0x70C9E0, 280, address, inputs);
            }
        }
        NativeBattleEffect345OwnedModelStep advanced = inputs.AdvanceOwnedModel8DD220(frame, address);
        frame = advanced.Frame;
        task = Read(frame, address);
        bool final = task.ReadByte(0x2E) == task.ReadByte(0x2F);
        if (final ? unchecked((short)advanced.Result) == 1 : Signed(task, 0x136) >= 11)
        {
            frame = IncrementPhase(frame, address);
        }
        return frame;
    }
}
