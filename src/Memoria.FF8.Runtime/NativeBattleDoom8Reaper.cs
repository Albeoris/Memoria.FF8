namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoom8ReaperInitializeModel(uint TaskAddress, uint ResourceAddress, ushort Argument3, uint DefaultColor44) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8ReaperSelect(uint TaskAddress, short Animation) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8ReaperAdvanceLoop(uint TaskAddress) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8ReaperObserveBone502170(uint ModelAddress, byte Bone, short Distance) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8ReaperPrepareAppearance(uint ScratchAddress, uint VertexScratchAddress, uint DescriptorAddress, uint ModelAddress) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8ReaperDrawModel(uint TaskAddress) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8ReaperPause5084E0(int Value) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8ReaperInputs(uint DefaultColor, uint ShadowColor, Func<NativeBattleDoom8Frame, uint, NativeBattleDoom8Step> Query8DD220, Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> Execute);

public static class NativeBattleDoom8Reaper
{
    public const uint Callback = 0x8CC3A0;

    public static NativeBattleDoom8Frame Initialize(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8ReaperInputs inputs)
    {
        Require(frame, address, inputs);
        return inputs.Execute(frame, new NativeBattleDoom8ReaperInitializeModel(address, 0x1636CE8, 3, inputs.ShadowColor));
    }

    public static NativeBattleDoom8Step Advance(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8ReaperInputs inputs)
    {
        Require(frame, address, inputs);
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteDword(0x4C, frame.State.ReadDword(NativeBattleDoom8State.SharedAddress)).WriteDword(0x50, frame.State.ReadDword(NativeBattleDoom8State.SharedAddress + 4)).WriteWord(0x3E, frame.State.SharedWord(0x4A)).WriteDword(0x60, checked(address + 0x114)).WriteDword(0x114, 4096).WriteDword(0x118, 4096).WriteDword(0x11C, 4096);
                frame = Write(frame, address, task);
                frame = inputs.Execute(frame, new NativeBattleDoom8ReaperSelect(address, 0));
                frame = inputs.Execute(frame, new NativeBattleDoom8ReaperPrepareAppearance(0x2758510, 0x275ABF0, 0x275F9E0, checked(address + 0x30)));
                frame = Write(frame, address, Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 8)));
                frame = frame with { State = frame.State.WriteWord(0x2758564, 0).WriteWord(0x275856A, 256) };
                frame = Next(frame, address);
                break;
            case 1:
                frame = AdvanceAngle(frame);
                ushort threshold = unchecked((ushort)(frame.State.ReadWord(0x2758564) - 72));
                frame = frame with { State = frame.State.WriteWord(0x2758564, threshold) };
                if (unchecked((short)threshold) <= -1280)
                {
                    frame = Write(frame, address, Read(frame, address).WriteWord(0x134, 16));
                    frame = Next(frame, address);
                }
                break;
            case 2:
                frame = AdvanceAngle(frame);
                task = Read(frame, address);
                ushort delay = unchecked((ushort)(task.ReadWord(0x134) - 1));
                task = task.WriteWord(0x134, delay);
                if (unchecked((short)delay) <= 0)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & 0xF7)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                }
                frame = Write(frame, address, task);
                break;
            case 3:
                if (NativeBattleDoom8Director.TryRequest(frame.State, 4, out NativeBattleDoom8State requested))
                {
                    frame = Pause(frame with { State = requested }, 0, inputs);
                    frame = Next(frame, address);
                }
                else
                {
                    frame = Pause(frame, 1, inputs);
                }
                break;
            case 4:
                if (NativeBattleDoom8Director.Reached(frame, 4))
                {
                    frame = inputs.Execute(frame, new NativeBattleDoom8ReaperSelect(address, 1));
                    frame = Next(frame, address);
                }
                break;
            case 5:
                NativeBattleDoom8Step first = inputs.Query8DD220(frame, address);
                frame = first.Frame;
                if (first.Result == 1)
                {
                    frame = inputs.Execute(frame, new NativeBattleDoom8ReaperSelect(address, 2));
                    frame = Next(frame, address);
                }
                break;
            case 6:
                frame = inputs.Execute(frame, new NativeBattleDoom8ReaperObserveBone502170(checked(address + 0x30), 0x1B, 0));
                frame = inputs.Execute(frame, new NativeBattleDoom8ReaperAdvanceLoop(address));
                if (unchecked((short)Read(frame, address).ReadWord(0x138)) >= 1)
                {
                    frame = inputs.Execute(frame, new NativeBattleDoom8ReaperSelect(address, 3));
                    frame = Next(frame, address);
                }
                break;
            case 7:
                NativeBattleDoom8Step second = inputs.Query8DD220(frame, address);
                frame = second.Frame;
                if (second.Result == 1)
                {
                    frame = inputs.Execute(frame, new NativeBattleDoom8ReaperSelect(address, 4));
                    frame = Write(frame, address, Read(frame, address).WriteWord(0x134, 7));
                    frame = Next(frame, address);
                }
                break;
            case 8:
                task = Read(frame, address);
                ushort countdown = unchecked((ushort)(task.ReadWord(0x134) - 1));
                frame = Write(frame, address, task.WriteWord(0x134, countdown));
                if (unchecked((short)countdown) <= 0)
                {
                    frame = Next(frame, address);
                }
                break;
            case 9:
                task = Read(frame, address);
                ushort fade = unchecked((ushort)(task.ReadWord(0x13A) + 256));
                if (unchecked((short)fade) >= 4096)
                {
                    fade = 4096;
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                }
                task = NativeBattleTonberry90Creature.Recolor7678D0(task.WriteWord(0x13A, fade).WriteWord(0x13C, fade), unchecked((short)fade), 3, inputs.DefaultColor);
                for (int channel = 0; channel < 3; channel++)
                {
                    int value = (byte)(inputs.ShadowColor >> (channel * 8));
                    task = task.WriteByte(0x5C + channel, unchecked((byte)(value - unchecked(value * unchecked((short)fade)) / 4096)));
                }
                frame = Write(frame, address, task);
                break;
            case 10:
                break;
            default:
                throw new InvalidDataException("Doom reaper signed phase exceeds its eleven original handlers.");
        }
        if ((Read(frame, address).ReadByte(0x26) & 4) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleDoom8ReaperDrawModel(address));
        }
        return NativeBattleDoom8Controller.Finish(frame, address);
    }

    private static NativeBattleDoom8Frame AdvanceAngle(NativeBattleDoom8Frame frame)
    {
        ushort angle = (ushort)((frame.State.ReadWord(0x2758568) + frame.State.ReadWord(0x275856A)) & 0xFFF);
        return frame with { State = frame.State.WriteWord(0x2758568, angle) };
    }

    private static NativeBattleDoom8Frame Pause(NativeBattleDoom8Frame frame, int value, NativeBattleDoom8ReaperInputs inputs)
    {
        uint previous = frame.State.ReadDword(0x275F9D4);
        if (value == 0 ? previous == 1 : previous == 0)
        {
            frame = frame with { State = frame.State.WriteDword(0x275F9D4, (uint)value).WriteDword(0x275ABDC, (uint)value) };
            frame = inputs.Execute(frame, new NativeBattleDoom8ReaperPause5084E0(value));
        }
        return frame;
    }

    private static NativeBattleModulePoolTask Read(NativeBattleDoom8Frame frame, uint address) => NativeBattleDoom8Controller.Read(frame, address);
    private static NativeBattleDoom8Frame Write(NativeBattleDoom8Frame frame, uint address, NativeBattleModulePoolTask task) => NativeBattleDoom8Controller.Write(frame, address, task);

    private static NativeBattleDoom8Frame Next(NativeBattleDoom8Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        return Write(frame, address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }

    private static void Require(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8ReaperInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs.Query8DD220);
        ArgumentNullException.ThrowIfNull(inputs.Execute);
        frame.State.Validate();
        if (frame.Effect.Binding.Effect != 8 || frame.Effect.Binding.Entry != NativeBattleDoom8Controller.Entry || Read(frame, address).Callback != Callback)
        {
            throw new InvalidDataException("Doom reaper requires its actual module and callback.");
        }
        NativeBattleCureFamilyPool expected = NativeBattleDoom8State.Pools[8];
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == expected.Descriptor);
        if (pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity || address != pool.BankAddress)
        {
            throw new InvalidDataException("Doom reaper requires its original single 320-byte task bank.");
        }
    }
}
