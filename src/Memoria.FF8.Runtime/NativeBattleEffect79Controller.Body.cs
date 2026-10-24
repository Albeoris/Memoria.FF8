using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect79Controller
{
    public static NativeBattleEffect79Step AdvanceBody(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int caster = task.ReadByte(0x2C);
        int target = task.ReadByte(0x2D);
        switch (Phase(frame, address))
        {
            case 0:
                frame = inputs.Execute(frame, new NativeBattleEffect79ModelPatch(caster, Sequence: 17));
                frame = ClearBytes(frame, BodyContextAddress, BodyContextLength);
                frame = frame with { State = frame.State.WriteDword(BodyContextAddress + 0x60, DescriptorAddress).WriteDword(BodyContextAddress + 0x64, PhysicalModelAddress(caster)).WriteDword(BodyContextAddress + 0x68, 0x1D8E054).WriteDword(BodyContextAddress + 0x6C, 0x1D8E054).WriteWord(BodyContextAddress + 0xA8, (ushort)caster).WriteDword(DescriptorAddress + 4, TransformedAddress).WriteWord(DescriptorAddress + 0x14, 0).WriteWord(DescriptorAddress + 0x16, 0).WriteWord(DescriptorAddress + 0x18, 320).WriteWord(DescriptorAddress + 0x1A, 0).WriteByte(DescriptorAddress + 0x1C, 128).WriteByte(DescriptorAddress + 0x1D, 128).WriteByte(DescriptorAddress + 0x1E, 128).WriteDword(DescriptorAddress + 0x20, uint.MaxValue).WriteWord(DescriptorAddress + 0x24, 0) };
                frame = StoreModelPosition(frame, caster, BodyContextAddress + 0xBC, inputs);
                frame = SpawnNode(frame, address, 0x1762E40, 0, 15, 0, resources, inputs);
                frame = inputs.Execute(frame, new NativeBattleEffect79Sound501330(0x1580B24, 0, 128));
                frame = NextPhase(frame, address);
                break;
            case 1:
                if (Acknowledged(frame, 2))
                {
                    frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 8)).WriteWord(0xB0, 16));
                    frame = inputs.Execute(frame, new NativeBattleEffect79TargetFlags(PhysicalModelAddress(caster), 0xFFFF, 4, false));
                    frame = frame with { State = frame.State.WriteWord(BodyContextAddress + 0x92, 0xD000).WriteWord(BodyContextAddress + 0x94, 0xE000).WriteWord(BodyContextAddress + 0xC6, 0x200).WriteWord(BodyContextAddress + 0xA2, 502).WriteWord(BodyContextAddress + 0xA4, 502).WriteWord(BodyContextAddress + 0xA6, 502) };
                    frame = inputs.Execute(frame, new NativeBattleEffect79ObserveCasterAnchor(address, BodyContextAddress));
                    frame = InitializeBodyMatrix(frame);
                    frame = StoreModelPosition(frame, caster, BodyContextAddress + 0x78, inputs);
                    frame = AddStateWord(frame, BodyContextAddress + 0x7A, unchecked((short)frame.State.ReadWord(BodyContextAddress + 0x72)));
                    frame = NextPhase(frame, address);
                }
                break;
            case 2:
                frame = MoveBody(frame, Signed(task, 0xB0) < 12);
                frame = inputs.Execute(frame, new NativeBattleEffect79ObserveCasterAnchor(address, BodyContextAddress));
                short countdown = unchecked((short)(Signed(task, 0xB0) - 1));
                frame = Write(frame, address, Read(frame, address).WriteWord(0xB0, unchecked((ushort)countdown)));
                if (countdown <= 0)
                {
                    frame = SpawnProgramme(frame, address, 0x7BDAD0, frame.State.ReadDword(ControlAddress + 0x130), 0x218, 3, inputs);
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
            case 4:
            case 5:
                int phase = Phase(frame, address);
                frame = MoveBody(frame, true);
                frame = inputs.Execute(frame, new NativeBattleEffect79ObserveCasterAnchor(address, BodyContextAddress));
                if (phase == 3)
                {
                    short weight = unchecked((short)(frame.State.ReadWord(BodyContextAddress + 0xA0) + 128));
                    frame = frame with { State = frame.State.WriteWord(BodyContextAddress + 0xA0, unchecked((ushort)Math.Min(4096, (int)weight))) };
                    if (weight >= 4096)
                    {
                        frame = frame with { State = frame.State.WriteWord(BodyContextAddress + 0xC6, 0xFE00) };
                        frame = NextPhase(frame, address);
                    }
                }
                else if (phase == 4 && unchecked((short)frame.State.ReadWord(BodyContextAddress + 0x92)) <= -2048)
                {
                    frame = frame with { State = frame.State.WriteWord(BodyContextAddress + 0xC6, 256) };
                    frame = NextPhase(frame, address);
                }
                else if (phase == 5 && unchecked((short)frame.State.ReadWord(BodyContextAddress + 0x92)) >= 0)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 6:
                frame = inputs.Execute(frame, new NativeBattleEffect79ObserveCasterAnchor(address, BodyContextAddress));
                frame = SpawnNode(frame, address, 0x176C45C, 0, 15, 0, resources, inputs);
                frame = SetTargetScale(frame, target, 2048, inputs, false, true);
                frame = FadeModel(frame, target, Signed(task, 0xAC), inputs);
                frame = NextPhase(frame, address);
                break;
            case 7:
                frame = Write(frame, address, task.WriteWord(0xAC, 4096));
                NativeBattleVector position = new(unchecked((short)(frame.State.ReadWord(BodyContextAddress + 0x78) + 128)), unchecked((short)(frame.State.ReadWord(BodyContextAddress + 0x7A) - 32)), unchecked((short)(frame.State.ReadWord(BodyContextAddress + 0x7C) - 336)));
                frame = inputs.Execute(frame, new NativeBattleEffect79ModelPatch(target, Position: position, WorldTranslation: position));
                frame = inputs.Execute(frame, new NativeBattleEffect79TargetFlags(PhysicalModelAddress(target), 0xFFFB, 0x800, true));
                frame = FadeModel(frame, target, 4096, inputs);
                frame = inputs.Execute(frame, new NativeBattleEffect79Sound501330(0x1580B2C, 0, 128));
                frame = NextPhase(frame, address);
                break;
            case 8:
                short fade = unchecked((short)(Signed(task, 0xAE) + 512));
                fade = (short)Math.Min(4096, (int)fade);
                frame = Write(frame, address, task.WriteWord(0xAE, unchecked((ushort)fade)));
                frame = FadeModel(frame, caster, fade, inputs);
                ushort radius = frame.State.ReadWord(BodyContextAddress + 0xA2);
                frame = frame with { State = frame.State.WriteWord(BodyContextAddress + 0xA4, radius).WriteWord(BodyContextAddress + 0xA6, radius) };
                int scale = unchecked((int)frame.State.ReadDword(BodyContextAddress + 0xB4)) + 256;
                frame = SetTargetScale(frame, target, Math.Min(4096, scale), inputs, scale >= 4096);
                if (scale >= 4096)
                {
                    frame = Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 4)));
                    frame = NextPhase(frame, address);
                }
                break;
            case 9:
                short remaining = unchecked((short)(Signed(task, 0xAC) - 512));
                if (remaining <= 0)
                {
                    remaining = 0;
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                frame = Write(frame, address, Read(frame, address).WriteWord(0xAC, unchecked((ushort)remaining)));
                frame = FadeModel(frame, target, remaining, inputs);
                break;
            case 10:
                break;
            default:
                throw new InvalidDataException("Effect79 caster body phase leaves its eleven native handlers.");
        }
        task = Read(frame, address);
        bool drewBody = (task.ReadByte(0x26) & 12) == 8;
        if (drewBody)
        {
            NativeBattleVector body = new(unchecked((short)(frame.State.ReadDword(BodyContextAddress + 0x78) - frame.State.ReadDword(BodyContextAddress + 0x70))), unchecked((short)(frame.State.ReadWord(BodyContextAddress + 0x7A) - frame.State.ReadWord(BodyContextAddress + 0x72))), unchecked((short)(frame.State.ReadDword(BodyContextAddress + 0x7C) - frame.State.ReadDword(BodyContextAddress + 0x74))));
            frame = inputs.Execute(frame, new NativeBattleEffect79ModelPatch(caster, Position: body, WorldTranslation: body));
            frame = inputs.Execute(frame, new NativeBattleEffect79DrawBody7BD020(BodyContextAddress));
        }
        frame = inputs.Execute(frame, new NativeBattleEffect79ObserveBodyStack(address, drewBody));
        return Finish(frame, address);
    }

    private static NativeBattleEffect79Frame AddStateWord(NativeBattleEffect79Frame frame, uint address, int amount) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)(frame.State.ReadWord(address) + amount))) };

    private static NativeBattleEffect79Frame MoveBody(NativeBattleEffect79Frame frame, bool damp)
    {
        frame = AddStateWord(frame, BodyContextAddress + 0x92, unchecked((short)frame.State.ReadWord(BodyContextAddress + 0xC6)));
        for (int axis = 0; axis < 3; axis++)
        {
            uint velocity = BodyContextAddress + 0x90u + (uint)(axis * 2);
            int value = unchecked((short)frame.State.ReadWord(velocity));
            if (damp)
            {
                value = unchecked((short)(value - value / 16));
                frame = frame with { State = frame.State.WriteWord(velocity, unchecked((ushort)value)) };
            }
            frame = AddStateWord(frame, BodyContextAddress + 0x78u + (uint)(axis * 2), value / 16);
        }
        return frame with { State = frame.State.WriteWord(BodyContextAddress + 0x98, (ushort)((frame.State.ReadWord(BodyContextAddress + 0x98) - 128) & 4095)) };
    }

    private static NativeBattleEffect79Frame StoreModelPosition(NativeBattleEffect79Frame frame, int actor, uint address, NativeBattleEffect79Inputs inputs)
    {
        NativeBattleModelTransform transform = inputs.ReadModel(frame, actor).Transform ?? throw new NotSupportedException("Effect79 requires its current model transform.");
        return frame with { State = frame.State.WriteWord(address, unchecked((ushort)transform.Position28.X)).WriteWord(address + 2, unchecked((ushort)transform.Position28.Y)).WriteWord(address + 4, unchecked((ushort)transform.Position28.Z)).WriteWord(address + 6, unchecked((ushort)transform.Vertical34)) };
    }

    private static NativeBattleEffect79Frame InitializeBodyMatrix(NativeBattleEffect79Frame frame)
    {
        short[] rotation = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096];
        for (int index = 0; index < rotation.Length; index++)
        {
            frame = frame with { State = frame.State.WriteWord(BodyContextAddress + (uint)(index * 2), unchecked((ushort)rotation[index])) };
        }
        return frame with { State = frame.State.WriteDword(BodyContextAddress + 20, 0).WriteDword(BodyContextAddress + 24, 0).WriteDword(BodyContextAddress + 28, 0) };
    }

    private static NativeBattleEffect79Frame SetTargetScale(NativeBattleEffect79Frame frame, int target, int scale, NativeBattleEffect79Inputs inputs, bool detach, bool publishPointer = false)
    {
        frame = frame with { State = frame.State.WriteDword(BodyContextAddress + 0xAC, unchecked((uint)scale)).WriteDword(BodyContextAddress + 0xB0, unchecked((uint)scale)).WriteDword(BodyContextAddress + 0xB4, unchecked((uint)scale)) };
        if (detach || publishPointer)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect79ModelPatch(target, ScalePointer: detach ? 0u : BodyContextAddress + 0xAC));
        }
        return frame;
    }

    private static NativeBattleEffect79Frame FadeModel(NativeBattleEffect79Frame frame, int actor, short factor, NativeBattleEffect79Inputs inputs)
    {
        uint color = inputs.ReadColor(frame, 0xB8B7D8);
        if (factor == 0)
        {
            return inputs.Execute(frame, new NativeBattleEffect79ModelPatch(actor, Color40: color & 0xFFFFFF, ColorMode43: 0, Alpha7: 0));
        }
        uint scaled = color & 0xFF000000;
        for (int channel = 0; channel < 3; channel++)
        {
            int value = (int)(color >> (channel * 8)) & 255;
            scaled |= (uint)unchecked((byte)(value - value * factor / 4096)) << (channel * 8);
        }
        return inputs.Execute(frame, new NativeBattleEffect79ModelPatch(actor, Color40: (scaled & 0xFFFFFF) | ((inputs.ReadModel(frame, actor).Color40 ?? throw new NotSupportedException("Effect79 requires its current model color.")) & 0xFF000000)));
    }
}
