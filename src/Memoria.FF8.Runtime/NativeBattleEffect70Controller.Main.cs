using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect70Controller
{
    public static NativeBattleEffect70Step AdvanceMain(NativeBattleEffect70Frame frame, uint address, NativeBattleEffect70Resources resources, NativeBattleEffect70Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        uint arena = frame.State.ReadDword(ArenaGlobal);
        int depth = age < 12 ? unchecked(age * 2300) / 12 : age >= 250 ? unchecked((262 - age) * 2300) / 12 : 2300;
        frame = frame with { State = frame.State.WriteDword(DepthGlobal, unchecked((uint)depth)) };
        if (unchecked((uint)(age - 50)) < 30)
        {
            NativeBattleEffectArena caller = Caller(frame, 0, -16384, 0x1334D5C, -128);
            frame = NativeBattleEffect70Programme.Advance(frame, address + 0x5C, 0x1100, 0x1332540, 0x218, age == 50, caller, resources, inputs).Frame;
        }
        int phase = age - 80;
        if (unchecked((uint)phase) < 182)
        {
            int flash = phase - 126;
            if (unchecked((uint)flash) < 8)
            {
                int factor = flash < 7 ? ((flash + 1) << 12) / 7 : 4096;
                frame = inputs.Execute(frame, new NativeBattleEffect70DeliverPrivate(NativeBattleRinoa89FadeDrawing.Draw7040B0(255, 255, 255, factor), CursorGlobal));
            }
            if (phase == 0)
            {
                frame = InitializeCommonPool(frame, 2);
                frame = HideEnabled(frame, inputs);
                frame = InitializeWorkspace(frame, address + 0x5C, 0x1100, resources.Act(0x1327D28), 0x1100);
                int count = Signed(Read(frame, address), 14);
                for (int index = 0; index < count; index++)
                {
                    uint record = checked(address + 0x14 + (uint)index * 24);
                    int actor = Actor(ReadDword(frame, record));
                    (frame, uint child) = Append(frame, 2, 0x6DFF00);
                    frame = inputs.Execute(frame, new NativeBattleEffect70ModelPositionWord(actor, 0x1E, 4096));
                    short angle = (actor & 1) != 0 ? unchecked((short)0xE20B) : (short)0x1DF5;
                    frame = PutWord(PutWord(PutWord(frame, record + 12, angle), record + 14, angle), record + 16, 0);
                    frame = PutWord(PutDword(frame, child + 12, record), child + 16, 0);
                    frame = InitializeWorkspace(frame, child + 20, 0x100, resources.Act(0x1326C68), 0xBC);
                }
                frame = frame with { State = frame.State.WriteDword(DepthGlobal, 4096) };
            }
            else if (unchecked((uint)(phase - 13)) <= 27)
            {
                frame = BirthOrbitSparks(frame);
            }
            else if (unchecked((uint)(phase - 115)) < 16)
            {
                frame = BirthRings(frame, address, inputs);
            }
            int bias = 0;
            if (phase >= 134)
            {
                int fade = phase - 134;
                if (fade == 0)
                {
                    int count = Signed(Read(frame, address), 14);
                    for (int index = 0; index < count; index++)
                    {
                        uint record = checked(address + 0x14 + (uint)index * 24);
                        int actor = Actor(ReadDword(frame, record));
                        frame = inputs.Execute(frame, new NativeBattleEffect70ModelFlags(actor, 0xFFF3, 0, true));
                        frame = inputs.Execute(frame, new NativeBattleEffect70ModelPositionPacked(actor, ReadBytes(frame, record + 4, 8)));
                    }
                    frame = RestoreFlags(frame, inputs);
                }
                if (unchecked((int)frame.State.ReadDword(DepthGlobal)) >= 2300 && fade < 32)
                {
                    frame = frame with { State = frame.State.WriteDword(DepthGlobal, unchecked((uint)(4096 - fade * 1796 / 32))) };
                }
                bias = -1024;
            }
            frame = DrawTargets(frame, address, inputs);
            NativeBattleEffectArena mainCaller = Caller(frame, unchecked(-(int)frame.State.ReadDword(SideGlobal) * 2048), 0, 0x112D1B4, (short)bias);
            frame = NativeBattleEffect70Programme.Advance(frame, address + 0x5C, 0x1100, 0x1327D28, 0x1100, false, mainCaller, resources, inputs).Frame;
        }
        frame = NativeBattleEffect70Particles.DrawSparks(frame, resources, inputs);
        frame = NativeBattleEffect70Particles.DrawRings(frame, resources, inputs);
        frame = NativeBattleEffect70Particles.AdvanceSparks(frame);
        frame = NativeBattleEffect70Particles.AdvanceRings(frame);
        if (age is 0 or 80 or 170)
        {
            uint sound = age == 0 ? 0x1324ECCu : age == 80 ? 0x1324ED0u : 0x1324ED4u;
            frame = inputs.Execute(frame, new NativeBattleEffect70Sound501330(sound, 0, 128));
        }
        if (age == 258)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect70StopVibration(unchecked((int)Read(frame, address).ReadDword(16))));
        }
        int launch = age - 1;
        if (unchecked((uint)launch) < 24 && (launch & 7) == 0 && (launch >> 3) < Signed(Read(frame, address), 14))
        {
            uint record = checked(address + 0x14 + (uint)(launch >> 3) * 24);
            (frame, uint child) = Append(frame, 2, 0x6E0100);
            int actor = Actor(ReadDword(frame, record));
            frame = inputs.Execute(frame, new NativeBattleEffect70ModelFlags(actor, 0xFFFF, 8, false));
            frame = inputs.Execute(frame, new NativeBattleEffect70CaptureBounds6DFD90(actor, record + 20));
            frame = PutDword(PutWord(PutWord(frame, child + 16, 0), child + 18, (short)(50 - launch)), child + 12, record);
            frame = InitializeWorkspace(frame, child + 20, 0x100, resources.Act(0x1325A88), 0x100);
        }
        frame = AdvanceCamera(frame, age, inputs);
        if (age == 213)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect70IoQueue5341D0(360, arena + 0x269C, 1));
            frame = inputs.Execute(frame, new NativeBattleEffect70IoQueue5341D0(361, arena + 0x269C, 0));
        }
        frame = inputs.Execute(frame, new NativeBattleEffect70IoAdvance534210());
        if (age == 252)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect70ConsumeContacts506BA0());
        }
        if (age >= 262)
        {
            frame = RestoreFlags(frame, inputs);
            frame = inputs.Execute(frame, new NativeBattleEffect70CameraFocal(SavedFocal(frame, inputs)));
            frame = frame with { State = frame.State.WriteDword(DepthGlobal, 0) };
            frame = ClearSecondaryLinks(frame);
            NativeBattleEffect70Step io = (inputs.SelectIo5342C0 ?? throw new NotSupportedException("Effect70 requires its actual stream completion."))(frame, 1);
            return new(io.Frame, io.Result != 0 ? 2 : 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)))), 0);
    }

    private static NativeBattleEffect70Frame DrawTargets(NativeBattleEffect70Frame frame, uint address, NativeBattleEffect70Inputs inputs)
    {
        int count = Signed(Read(frame, address), 14);
        for (int index = 0; index < count; index++)
        {
            uint record = checked(address + 0x14 + (uint)index * 24);
            int actor = Actor(ReadDword(frame, record));
            short y = unchecked((short)((ReadShort(frame, record + 20) + ReadShort(frame, record + 22)) >> 1));
            NativeBattleEffectArena point = Point(0, y, 0);
            frame = inputs.Execute(frame, new NativeBattleEffect70DrawActor693170(actor, ReadBytes(frame, record + 12, 8), point, 2048));
            frame = inputs.Execute(frame, new NativeBattleEffect70ModelFlags(actor, 0xFFFF, 4, false));
        }
        return frame;
    }

    private static NativeBattleEffectArena Caller(NativeBattleEffect70Frame frame, int zOffset, short y, uint selectors, short depth)
    {
        short x = unchecked((short)frame.State.ReadWord(CenterAddress));
        short z = unchecked((short)(frame.State.ReadWord(CenterAddress + 4) + zOffset));
        return Point(x, y, z).Write(20, BitConverter.GetBytes(0)).Write(24, BitConverter.GetBytes(selectors)).Write(28, BitConverter.GetBytes(depth)).Write(32, BitConverter.GetBytes(frame.State.ReadDword(ArenaGlobal) + 0x241C));
    }

    private static NativeBattleEffect70Frame InitializeWorkspace(NativeBattleEffect70Frame frame, uint address, int length, NativeBattleGfAnimationSource source, int clear)
    {
        (byte[] bytes, bool[] known) = ReadBytes(frame, address, length).ReadSnapshot(0, length);
        NativeBattleGfAnimationWorkspace workspace = new(bytes.ToImmutableArray());
        workspace = workspace.Initialize7016B0(source, clear);
        NativeBattleEffectArena result = workspace.CopyTo(NativeBattleEffectArena.Clear(), 0);
        for (int index = clear; index < length; index++)
        {
            if (!known[index])
            {
                result = result.MarkUnknown(index, 1);
            }
        }
        return WriteBytes(frame, address, result, length);
    }

    public static uint ReadDword(NativeBattleEffect70Frame frame, uint address) => BitConverter.ToUInt32(ReadBytes(frame, address, 4).Read(0, 4));
    public static short ReadShort(NativeBattleEffect70Frame frame, uint address) => BitConverter.ToInt16(ReadBytes(frame, address, 2).Read(0, 2));
    private static ushort SavedFocal(NativeBattleEffect70Frame frame, NativeBattleEffect70Inputs inputs) => (inputs.ReadSavedFocal ?? throw new NotSupportedException("Effect70 requires its retained focal WORD."))(frame);
    private static NativeBattleEffectArena Point(short x, short y, short z) => NativeBattleEffectArena.Clear().MarkUnknown(0, 36).Write(0, BitConverter.GetBytes(x)).Write(2, BitConverter.GetBytes(y)).Write(4, BitConverter.GetBytes(z));
}
