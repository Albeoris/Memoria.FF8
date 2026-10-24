using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect80Controller
{
    public static NativeBattleEffect80Step AdvanceBody(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint target = PhysicalModelAddress(task.ReadByte(0x2D));
        uint caster = PhysicalModelAddress(task.ReadByte(0x2C));
        switch (Phase(frame, address))
        {
            case 0:
                frame = InitializeBody7B4480(frame, target, task.ReadByte(0x2D));
                frame = inputs.Execute(frame, new NativeBattleEffect80TargetFlags(target, 0xFF, 4, false));
                frame = inputs.Execute(frame, new NativeBattleEffect80TargetFlags(caster, 0xFFFB, 0, true));
                frame = inputs.Execute(frame, new NativeBattleEffect80TargetFlags(target, 0xFF, 8, false, 1));
                frame = inputs.Execute(frame, new NativeBattleEffect80TargetFlags(caster, 0xFF, 8, false, 1));
                frame = NativeBattleEffect80Trails.Initialize7B43F0(frame, resources);
                frame = BirthBodyNode(frame, address, 0x1749C64, 0, inputs);
                frame = inputs.Execute(frame, new NativeBattleEffect80Sound501330(resources.Sounds[0x157B2E4], 0, 128));
                frame = NextPhase(frame, address);
                break;
            case 1:
                if (unchecked((short)frame.State.ReadWord(TargetAddress + 0x40)) >= 3)
                {
                    (frame, _) = SpawnAct7ABF40(frame, address, 0x7B4540, 0x1757B2C, 0xD8, 1, 0, inputs);
                    frame = Write(frame, address, Read(frame, address).WriteWord(0x9C, 2));
                    frame = NextPhase(frame, address);
                }
                break;
            case 2:
            case 4:
                short remaining = unchecked((short)(Read(frame, address).ReadWord(0x9C) - 1));
                frame = Write(frame, address, Read(frame, address).WriteWord(0x9C, unchecked((ushort)remaining)));
                if (remaining <= 0)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 8)));
                frame = inputs.Execute(frame, new NativeBattleEffect80TargetFlags(target, 0xFF, 4, false));
                frame = inputs.Execute(frame, new NativeBattleEffect80BodyAnchor7B4750(address, BodyContextAddress));
                frame = IdentityBody(frame);
                frame = SetBodyScale(frame, 0x200);
                frame = WriteBytes(frame, BodyContextAddress + 0x78, inputs.ReadPosition8(frame, task.ReadByte(0x2C)), 8);
                frame = frame with { State = frame.State.WriteWord(BodyContextAddress + 0xC2, 0x1000).WriteWord(BodyContextAddress + 0xA0, 0x1000) };
                frame = BirthBodyNode(frame, address, 0x174F0BC, 12, inputs);
                frame = inputs.Execute(frame, new NativeBattleEffect80Sound501330(resources.Sounds[0x157B2E8], 0, 128));
                frame = Write(frame, address, Read(frame, address).WriteWord(0x9C, 30));
                frame = NextPhase(frame, address);
                break;
            case 5:
                frame = inputs.Execute(frame, new NativeBattleEffect80BodyAnchor7B4750(address, BodyContextAddress));
                int growing = frame.State.ReadWord(BodyContextAddress + 0xA4) + 0x40;
                frame = SetBodyScale(frame, (ushort)Math.Min(growing, 0x500));
                if (growing >= 0x500)
                {
                    frame = Write(frame, address, Read(frame, address).WriteWord(0x9C, 0));
                    frame = NextPhase(frame, address);
                }
                break;
            case 6:
                frame = AdvanceBodyFade(frame, address, target, resources, inputs);
                break;
            case 7:
                frame = Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) & ~8)));
                frame = inputs.Execute(frame, new NativeBattleEffect80TargetFlags(target, 0xFFFB, 0, true));
                frame = NextPhase(frame, address);
                break;
            case 8:
                frame = NextPhase(MarkComplete(frame, address), address);
                break;
            case 9:
                break;
            default:
                throw new InvalidDataException("Effect80 body signed phase leaves its ten handlers.");
        }
        frame = NativeBattleEffect80Trails.Advance7AF4C0(frame, resources, inputs);
        if ((Read(frame, address).ReadByte(0x26) & 8) != 0)
        {
            short X(int offset) => unchecked((short)frame.State.ReadWord(BodyContextAddress + (uint)offset));
            NativeBattleVector point = new(unchecked((short)(frame.State.ReadDword(BodyContextAddress + 0x78) - frame.State.ReadDword(BodyContextAddress + 0x70) + frame.State.ReadDword(BodyContextAddress + 0xB4))), unchecked((short)(X(0x7A) - X(0x72) + X(0xB6))), unchecked((short)(frame.State.ReadDword(BodyContextAddress + 0x7C) - frame.State.ReadDword(BodyContextAddress + 0x74) + frame.State.ReadDword(BodyContextAddress + 0xB8))));
            frame = inputs.Execute(frame, new NativeBattleEffect80SetActorPosition(target, point, true));
            frame = inputs.Execute(frame, new NativeBattleEffect80DrawBody7AFEC0(BodyContextAddress));
        }
        frame = NativeBattleEffect80Particles.Emit7B06A0(frame, address, resources, inputs);
        return Finish(frame, address);
    }

    public static NativeBattleEffect80Frame InitializeBody7B4480(NativeBattleEffect80Frame frame, uint model, int actor)
    {
        frame = ClearBytes(frame, BodyContextAddress, BodyContextLength);
        NativeBattleEffect80State state = frame.State.WriteDword(BodyContextAddress + 0x60, DescriptorAddress).WriteDword(BodyContextAddress + 0x64, model).WriteDword(BodyContextAddress + 0x68, 0x1D8E054).WriteDword(BodyContextAddress + 0x6C, 0x1D8E054).WriteWord(BodyContextAddress + 0xA8, checked((ushort)actor));
        state = state.WriteDword(DescriptorAddress + 4, RowsAddress).WriteWord(DescriptorAddress + 0x14, 0).WriteWord(DescriptorAddress + 0x16, 0).WriteWord(DescriptorAddress + 0x18, 0x140).WriteWord(DescriptorAddress + 0x1A, 0).Write(DescriptorAddress + 0x1C, [0x80, 0x80, 0x80]).WriteDword(DescriptorAddress + 0x20, uint.MaxValue).WriteWord(DescriptorAddress + 0x24, 0);
        return frame with { State = state };
    }

    private static NativeBattleEffect80Frame IdentityBody(NativeBattleEffect80Frame frame)
    {
        NativeBattleEffectArena matrix = ReadBytes(frame, BodyContextAddress, 32);
        NativeBattlePackedMatrix32 identity = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        matrix = matrix.Write(0, identity.Bytes.AsSpan().Slice(0, 18)).Write(20, identity.Bytes.AsSpan().Slice(20, 12));
        return WriteBytes(frame, BodyContextAddress, matrix, 32);
    }

    private static NativeBattleEffect80Frame SetBodyScale(NativeBattleEffect80Frame frame, ushort value) => frame with { State = frame.State.WriteWord(BodyContextAddress + 0xA2, value).WriteWord(BodyContextAddress + 0xA4, value).WriteWord(BodyContextAddress + 0xA6, value) };

    private static NativeBattleEffect80Frame BirthBodyNode(NativeBattleEffect80Frame frame, uint parent, uint source, ushort delay, NativeBattleEffect80Inputs inputs)
    {
        uint child;
        (frame, child) = SpawnNode7B0A90(frame, parent, source, delay, 15, 4, inputs);
        return WriteBytes(frame, child + 0x290, inputs.ReadPosition8(frame, Read(frame, parent).ReadByte(0x2C)), 8);
    }

    private static NativeBattleEffect80Frame AdvanceBodyFade(NativeBattleEffect80Frame frame, uint address, uint target, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        short timer = unchecked((short)(Read(frame, address).ReadWord(0x9C) + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(0x9C, unchecked((ushort)timer)));
        frame = frame with { State = frame.State.WriteWord(BodyContextAddress + 0x98, timer <= 45 ? (ushort)((frame.State.ReadWord(BodyContextAddress + 0x98) + 0x5B) & 0xFFF) : (ushort)0) };
        if (timer == 45)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect80SetSequence505C00(target, 26));
            frame = BirthBodyNode(frame, address, 0x1750660, 14, inputs);
        }
        foreach (uint offset in new uint[] { 0xB4, 0xB8, 0xB6 })
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random, State = frame.State.WriteWord(BodyContextAddress + offset, unchecked((ushort)((value & 0xFF) - 0x80))) };
        }
        frame = inputs.Execute(frame, new NativeBattleEffect80BodyAnchor7B4750(address, BodyContextAddress));
        frame = SetBodyScale(frame, (ushort)Math.Min(frame.State.ReadWord(BodyContextAddress + 0xA4) + 0x40, 0x600));
        short alpha = unchecked((short)(frame.State.ReadWord(BodyContextAddress + 0xC2) - 0x40));
        frame = frame with { State = frame.State.WriteWord(BodyContextAddress + 0xC2, unchecked((ushort)Math.Max(0, (int)alpha))).WriteWord(BodyContextAddress + 0xA0, unchecked((ushort)Math.Max(0, (int)alpha))) };
        if (alpha <= 0)
        {
            frame = frame with { State = frame.State.WriteWord(BodyContextAddress + 0xB4, 0).WriteWord(BodyContextAddress + 0xB6, 0).WriteWord(BodyContextAddress + 0xB8, 0).WriteWord(BodyContextAddress + 0x98, 0) };
            frame = NextPhase(frame, address);
        }
        return frame;
    }
}

