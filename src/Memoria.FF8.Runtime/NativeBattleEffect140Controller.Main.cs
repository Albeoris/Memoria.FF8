using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect140Controller
{
    public static NativeBattleEffect140Step AdvanceMain6A6750(NativeBattleEffect140Frame frame, uint address, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        int age = Signed(Read(frame, address), 0xC);
        int center = unchecked((int)frame.State.ReadDword(0x2517B18));
        bool paused = frame.State.ReadDword(0x2517AAC) != 0;
        int cue = age < 8 ? age * 325 : age > 247 ? (255 - age) * 325 : 2600;
        frame = frame with { State = frame.State.WriteDword(0x2517B20, unchecked((uint)cue)) };
        if ((uint)(age - 1) < 100)
        {
            frame = DrawEarly6A67AB(frame, address, age - 1, center, resources, inputs);
        }
        age = Signed(Read(frame, address), 0xC);
        if ((uint)(age - 93) < 24)
        {
            int relative = age - 93;
            frame = NativeBattleEffect140Screen.Fade(frame, 255, 200, 140, relative < 8 ? 8 - relative : relative - 8, relative < 8 ? 8 : 16, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(140, Read(frame, address), unchecked((int)0x7F000000)) : null);
        }
        if ((uint)(age - 101) < 136)
        {
            if (!paused)
            {
                frame = NativeBattleEffect140OwnedModel.Advance6FBDB0(frame, address + 0x64, resources);
            }
            int relative = age - 101;
            frame = Write(frame, address, Read(frame, address).WriteDword(0xBC, relative < 20 ? (uint)((20 - relative) * 32) : 0));
            frame = inputs.Execute(frame, new NativeBattleEffect140DrawOwnedModel(address + 0x64, frame.State.ReadDword(0x2517B28) + 0x878, 0, relative < 20) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(140, Read(frame, address), relative < 20 ? 0 : 1) with { ResourceGeneration = resources.Model.Address } : null });
        }
        frame = AdvanceProgrammes6A6C65(frame, address, age, center, resources, inputs);
        return AdvanceTimeline6A7119(frame, address, resources, inputs);
    }

    public static NativeBattleEffect140Step AdvanceTimeline6A7119(NativeBattleEffect140Frame frame, uint address, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        int age = Signed(Read(frame, address), 0xC);
        int center = unchecked((int)frame.State.ReadDword(0x2517B18));
        if (age == 0)
        {
            foreach ((short file, short type) in new (short, short)[] { (528, 1), (529, 1), (530, 16), (531, 1), (532, 3) })
            {
                frame = inputs.Execute(frame, new NativeBattleEffect140QueueIo5341D0(file, frame.State.ReadDword(0x2517B28) + 0x1F7A0, type));
            }
        }
        else if (age == 126)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect140QueueIo5341D0(533, frame.State.ReadDword(0x2517B28) + 0x1F7A0, 4));
            frame = inputs.Execute(frame, new NativeBattleEffect140QueueIo5341D0(534, frame.State.ReadDword(0x2517B28) + 0x1F7A0, 2));
        }
        else if (age is 150 or 194)
        {
            NativeBattleEffect140IoStep polled = (inputs.Poll534270 ?? throw new NotSupportedException("Effect140 requires its actual IO completion poll."))(frame);
            frame = polled.Frame;
            if (polled.Busy)
            {
                return new(frame, 0);
            }
            if (age == 150)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect140QueueIo5341D0(535, frame.State.ReadDword(0x2517B28) + 0x1F7A0, 1));
                frame = inputs.Execute(frame, new NativeBattleEffect140QueueIo5341D0(536, frame.State.ReadDword(0x2517B28) + 0x1F7A0, 0));
            }
        }
        frame = inputs.Execute(frame, new NativeBattleEffect140AdvanceIo534210());
        if (age == 101)
        {
            NativeBattleEffect140IoStep polled = (inputs.Poll534270 ?? throw new NotSupportedException("Effect140 requires its actual IO completion poll."))(frame);
            frame = polled.Frame;
            if (polled.Busy)
            {
                return new(frame, 0);
            }
        }
        if (age is 101 or 161)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect140Stream5018C0(128, 127));
        }
        if (age is 0 or 101 or 195)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect140Sound501330(age == 0 ? 0x11D003Cu : age == 101 ? 0x11D0040u : 0x11D0044u, 1, 128));
        }
        frame = AdvanceCamera6A72B8(frame, address, age, center, inputs);
        if (age == 235)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect140CommonTextureLoad508630(frame.State.ReadDword(0x2517B28) + 0x1F7A0, 0x2517B24));
        }
        if (age == 245)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect140ContactAll506BA0());
        }
        else if (age == 251)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect140StopVibration4A2940(unchecked((int)Read(frame, address).ReadDword(0x10))));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)(age + 1))));
        if (unchecked((short)(age + 1)) >= 255)
        {
            int count = Signed(Read(frame, address), 0xE);
            for (int index = 0; index < count; index++)
            {
                NativeBattleModulePoolTask current = Read(frame, address);
                int actor = Actor(current.ReadDword(0x14 + index * 4));
                frame = inputs.Execute(frame, new NativeBattleEffect140ModelFlags(actor, 0xF7FF, 0));
                frame = inputs.Execute(frame, new NativeBattleEffect140ModelColor40(actor, current.ReadDword(0x3C + index * 4)));
            }
            return new(frame with { State = frame.State.WriteDword(0x2517B20, 0) }, 2);
        }
        return new(frame, 0);
    }

    private static NativeBattleEffect140Frame TintRecipients(NativeBattleEffect140Frame frame, uint address, int factor, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint tint = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(0x11D0160, 4));
        int count = Signed(task, 0xE);
        for (int index = 0; index < count; index++)
        {
            int actor = Actor(Read(frame, address).ReadDword(0x14 + 4 * index));
            frame = inputs.Execute(frame, new NativeBattleEffect140ModelFlags(actor, ushort.MaxValue, 0x800));
            uint prior = inputs.ReadModel(frame, actor).Color40 ?? throw new NotSupportedException("Effect140 requires its current recipient color.");
            NativeBattleWeightedColorStep step = NativeBattleWeightedColor56CC00.Blend(frame.Memory, tint, prior, 0, unchecked((short)factor), prior, inputs.Colors);
            frame = frame with { Memory = step.Memory };
            frame = inputs.Execute(frame, new NativeBattleEffect140ModelColor40(actor, step.Color));
        }
        return frame;
    }
}
