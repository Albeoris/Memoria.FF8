using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect70Controller
{
    public static NativeBattleEffect70Step AdvanceRoot(NativeBattleEffect70Frame frame, uint address, NativeBattleEffect70Resources resources, NativeBattleEffect70Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool alternate = task.ReadByte(14) != 0;
        uint arena = frame.State.ReadDword(ArenaGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, arena + (alternate ? 0xAE9Cu : 0x1AE9Cu)).WriteDword(SecondaryCursorGlobal, arena + (alternate ? 0x1AE9Cu : 0x2AE9Cu)).WriteDword(DepthGlobal, 0) };
        frame = Write(frame, address, task.WriteByte(14, alternate ? (byte)0 : (byte)1));
        if (task.ReadWord(12) == 1 && task.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            frame = CaptureFlags(frame, inputs);
            frame = NativeBattleEffect70Particles.InitializeSparks(frame);
            frame = NativeBattleEffect70Particles.InitializeRings(frame);
            frame = inputs.Execute(frame, new NativeBattleEffect70IoInitialize534110(arena + 0x678));
            frame = InitializeCommonPool(frame, 1);
            frame = InitializeCommonPool(frame, 2);
            (frame, uint main) = Append(frame, 1, 0x6DDD50);
            frame = ClearBytes(frame, main + 12, 0x1150);
            frame = inputs.Execute(frame, new NativeBattleEffect70StartVibration(resources.Vibration, main, 16));
            frame = CaptureTargets(frame, main, inputs);
        }
        int survivors = 1;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            NativeBattlePackedMatrix32 packed = inputs.ReadView(frame);
            NativeBattleEffectArena view = frame.State.ReadView(ViewAddress, 32);
            view = NativeBattleEffectNodeContextMemory.Copy(PackedArena(packed), 20, view, 20, 12);
            NativeBattleActionMatrix normalized = NativeBattleGfMatrices.Normalize67AAE0(packed.ReadMatrix());
            for (int index = 0; index < 9; index++)
            {
                view = view.Write(index * 2, BitConverter.GetBytes(normalized.Rotation[index]));
            }
            frame = frame with { State = frame.State.WriteView(ViewAddress, view, 32) };
            (frame, survivors) = Scan(frame, 1, resources, inputs);
            (frame, _) = Scan(frame, 2, resources, inputs);
        }
        int depth = unchecked((int)frame.State.ReadDword(DepthGlobal));
        int previous = unchecked((int)frame.State.ReadDword(PreviousDepthGlobal));
        if (depth != previous)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect70StageIntensity5713E0(depth, 0));
            if (depth >= 4096 || previous >= 4096)
            {
                for (int group = 0; group < 4; group++)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect70StageFlags(group, depth >= 4096 ? (byte)0xFD : (byte)0xFF, depth >= 4096 ? (byte)0 : (byte)2));
                }
            }
            frame = frame with { State = frame.State.WriteDword(PreviousDepthGlobal, unchecked((uint)depth)) };
        }
        if (Read(frame, address).ReadByte(15) != 0 && survivors == 0)
        {
            return new(inputs.Execute(frame, new NativeBattleEffect70StageIntensity5713E0(0, 0)), 2);
        }
        task = Read(frame, address);
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)))), 0);
    }

    public static NativeBattleEffect70Frame CaptureFlags(NativeBattleEffect70Frame frame, NativeBattleEffect70Inputs inputs)
    {
        for (int actor = 0; actor < 7; actor++)
        {
            frame = frame with { State = frame.State.WriteDword(SavedFlagsAddress + (uint)actor * 4, inputs.ReadModel(frame, actor).Flags0) };
        }
        return frame;
    }

    public static NativeBattleEffect70Frame HideEnabled(NativeBattleEffect70Frame frame, NativeBattleEffect70Inputs inputs)
    {
        for (int actor = 0; actor < 7; actor++)
        {
            if ((inputs.ReadModel(frame, actor).Flags0 & 2) != 0)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect70ModelFlags(actor, 0xFFFF, 4, true));
            }
        }
        return frame;
    }

    public static NativeBattleEffect70Frame RestoreFlags(NativeBattleEffect70Frame frame, NativeBattleEffect70Inputs inputs)
    {
        for (int actor = 0; actor < 7; actor++)
        {
            if ((inputs.ReadModel(frame, actor).Flags0 & 2) != 0)
            {
                ushort bit = (ushort)(frame.State.ReadDword(SavedFlagsAddress + (uint)actor * 4) & 4);
                frame = inputs.Execute(frame, new NativeBattleEffect70ModelFlags(actor, 0xFFFB, bit, true));
            }
        }
        return frame;
    }

    private static NativeBattleEffect70Frame CaptureTargets(NativeBattleEffect70Frame frame, uint main, NativeBattleEffect70Inputs inputs)
    {
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        int headerIndex = Parameters(frame).HeaderIndex7C;
        if ((uint)headerIndex >= packets.Headers.Length)
        {
            throw new InvalidDataException("Effect70 selected an absent physical action header.");
        }
        NativeBattleActionHeaderSlot header = packets.Headers[headerIndex];
        int count = header.ReadByte(16);
        frame = Write(frame, main, Read(frame, main).WriteWord(14, (ushort)count));
        short[] minimum = [32767, 32767, 32767];
        short[] maximum = [-32767, -32767, -32767];
        for (int index = 0; index < count; index++)
        {
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect70 requires its actual current contact pointer.");
            int actor = packets.ReadContact(checked(first + index)).Primary.ActorIndex;
            uint record = checked(main + 0x14 + (uint)index * 24);
            frame = PutDword(frame, record, PhysicalModelAddress(actor));
            frame = WriteBytes(frame, record + 4, (inputs.ReadPosition8 ?? throw new NotSupportedException("Effect70 requires actual packed model position bytes."))(frame, actor), 8);
            frame = WriteBytes(frame, record + 12, frame.State.ReadView(RetainedFirstGlobal, 8), 8);
            NativeBattleEffect70PointStep step = (inputs.ReadAnchor571400 ?? throw new NotSupportedException("Effect70 requires its current target point producer."))(frame, actor);
            frame = step.Frame;
            NativeBattleVector anchor = Vector(step.Point8);
            short[] xyz = [anchor.X, anchor.Y, anchor.Z];
            for (int axis = 0; axis < 3; axis++)
            {
                minimum[axis] = Math.Min(minimum[axis], xyz[axis]);
                maximum[axis] = Math.Max(maximum[axis], xyz[axis]);
            }
            frame = frame with { State = frame.State.WriteDword(SideGlobal, actor >= 3 ? 0xFFFFFFFF : 1) };
        }
        for (int axis = 0; axis < 3; axis++)
        {
            frame = frame with { State = frame.State.WriteWord(CenterAddress + (uint)axis * 2, unchecked((ushort)((minimum[axis] + maximum[axis]) / 2))) };
        }
        return frame;
    }

    public static NativeBattleEffect70Frame PutDword(NativeBattleEffect70Frame frame, uint address, uint value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 4);
    public static NativeBattleEffect70Frame PutWord(NativeBattleEffect70Frame frame, uint address, short value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2);
    public static NativeBattleEffect70Frame PutByte(NativeBattleEffect70Frame frame, uint address, byte value) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1);
}
