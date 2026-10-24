using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect227Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect227Controller
{
    public static NativeBattleEffect227Step AdvanceMain693A10(NativeBattleEffect227Frame frame, uint address, NativeBattleEffect227Resources resources, NativeBattleEffect227Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        uint model = frame.State.ReadDword(CasterGlobal);
        uint morph = frame.State.ReadDword(ArenaGlobal) + MorphOffset;
        if ((uint)(age - 212) < 50)
        {
            if (age == 212)
            {
                frame = NativeBattleEffect227Programme.Initialize(frame, address + 0x1C, 0x112DCF8, 0x33C, resources);
            }
            int phase = age - 212;
            NativeBattleVector anchor = Vector(frame.State.ReadView(AnchorAddress, 8));
            int lift = phase <= 45 ? -200 : 40 * (phase - 50);
            NativeBattleVector point = anchor with { Y = unchecked((short)(anchor.Y + lift)) };
            frame = NativeBattleEffect227Programme.Advance(frame, address + 0x1C, 0x33C, Caller16(PointView(point), -128, morph), resources, inputs).Frame;
        }
        if ((uint)(age - 262) < 8)
        {
            if (age == 262)
            {
                frame = Write(frame, address, CopyTask(Read(frame, address), 0x14, frame.State.ReadView(AnchorAddress, 8), 8));
                NativeBattleEffectArena raw = inputs.ReadModelArena9C(frame, model);
                NativeBattleEffectArena endpoint = frame.State.ReadView(EndpointAddress, 8);
                NativeBattleVector position = new(BinaryPrimitives.ReadInt16LittleEndian(raw.Read(0x1C, 2)), 0, unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(raw.Read(0x20, 2)) + 1000)));
                frame = frame with { State = frame.State.WriteView(EndpointAddress, PutPoint(endpoint, 0, position), 8) };
            }
            (frame, NativeBattleEffectArena point) = BlendEndpoint(frame, address, age - 262, inputs.Colors);
            frame = NativeBattleEffect227Programme.Advance(frame, address + 0x1C, 0x33C, Caller16(point, -128, morph), resources, inputs).Frame;
        }
        if ((uint)(age - 260) < 18)
        {
            if (age == 260)
            {
                frame = NativeBattleEffect227Programme.Initialize(frame, address + 0x358, 0x112FE80, 0xC0, resources);
            }
            NativeBattleEffectArena raw = inputs.ReadModelArena9C(frame, model);
            NativeBattleVector point = new(unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(raw.Read(0x1C, 2)) + 44)), unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(raw.Read(0x1E, 2)) - 2286)), unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(raw.Read(0x20, 2)) + 410)));
            frame = NativeBattleEffect227Programme.Advance(frame, address + 0x358, 0xC0, Caller16(PointView(point), -128, morph), resources, inputs).Frame;
        }
        if ((uint)(age - 268) < 16)
        {
            if (age == 268)
            {
                frame = NativeBattleEffect227Programme.Initialize(frame, address + 0x418, 0x1130368, 0x84, resources);
            }
            frame = NativeBattleEffect227Programme.Advance(frame, address + 0x418, 0x84, Caller16(frame.State.ReadView(EndpointAddress, 8), 0, morph), resources, inputs).Frame;
        }
        frame = NativeBattleEffect227Records.Draw(frame, resources, inputs);
        frame = NativeBattleEffect227Records.Advance(frame);
        frame = EmitMainEvents(frame, address, age, inputs);
        if ((ushort)age < 95)
        {
            for (int batch = 0; batch < 2; batch++)
            {
                frame = NativeBattleEffect227Records.Birth(frame, 0x1131660, 96, resources, inputs);
                frame = NativeBattleEffect227Records.Birth(frame, 0x1131960, 96, resources, inputs);
            }
        }
        if ((uint)(age - 180) < 22)
        {
            for (int birth = 0; birth < 2; birth++)
            {
                frame = NativeBattleEffect227Records.Birth(frame, 0x1131C60, 119, resources, inputs);
            }
        }
        if ((uint)(age - 222) < 23)
        {
            frame = BirthParticle(frame);
        }
        frame = EmitMainText(frame, age, inputs);
        if (age >= 284)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect227ModelFlags(model, 1, 0xFF, 4));
            return new(frame, 2);
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    public static NativeBattleEffect227Frame EmitMainEvents(NativeBattleEffect227Frame frame, uint address, int age, NativeBattleEffect227Inputs inputs)
    {
        if (age == 0 || age == 212)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect227Sound501330(age == 0 ? 0x112DAA0u : 0x112DAA4u, 0, 128));
        }
        if (age == 280)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect227VibrationStop4A2940(unchecked((int)Read(frame, address).ReadDword(16))));
        }
        return frame;
    }

    public static NativeBattleEffect227Frame EmitMainText(NativeBattleEffect227Frame frame, int age, NativeBattleEffect227Inputs inputs)
    {
        (int index, int duration) = age switch
        {
            38 => (0, 42),
            85 => (1, 30),
            115 => (2, 30),
            165 => (3, 15),
            _ => (-1, 0)
        };
        if (index >= 0)
        {
            uint delta = unchecked(frame.State.ReadDword(CasterGlobal) - 0x1D972C0);
            if (delta % 156 != 0 || delta / 156 >= 7)
            {
                throw new InvalidDataException("Effect227 text requires its actual physical caster.");
            }
            frame = inputs.Execute(frame, new NativeBattleEffect227Text4876F0((int)(delta / 156), index, duration));
        }
        return frame;
    }

    public static NativeBattleEffect227Frame BirthParticle(NativeBattleEffect227Frame frame)
    {
        (frame, uint child) = Append(frame, ParticleDescriptor, ParticleCallback);
        if (child == 0)
        {
            return frame;
        }
        frame = ClearDwords701200(frame, child + 12, 3);
        NativeBattleModulePoolTask task = Read(frame, child).WriteWord(14, (ushort)Roll(ref frame)).WriteWord(16, (ushort)Roll(ref frame));
        int direction = (Roll(ref frame) & 1) != 0 ? -16 : 16;
        int vertical = -37 - (Roll(ref frame) & 31);
        return Write(frame, child, task.WriteWord(18, unchecked((ushort)direction)).WriteWord(20, unchecked((ushort)vertical)));
    }

    public static NativeBattleEffectArena Caller16(NativeBattleEffectArena point8, int depth, uint morph) => CopyBytes(point8, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 16), 0, 8).Write(8, BitConverter.GetBytes(depth)).Write(12, BitConverter.GetBytes(morph));

    public static (NativeBattleEffect227Frame Frame, NativeBattleEffectArena Point8) BlendEndpoint(NativeBattleEffect227Frame frame, uint address, int elapsed, NativeBattleSummonColors colors)
    {
        int fraction = 256 + (elapsed << 9);
        NativeBattleVector from = Vector(TaskView(Read(frame, address), 0x14, 8));
        NativeBattleVector to = Vector(frame.State.ReadView(EndpointAddress, 8));
        NativeBattleWeightedPointsStep result = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, from, to, unchecked((short)(4096 - fraction)), unchecked((short)fraction), colors);
        return (frame with { Memory = result.Memory }, PointView(result.Position));
    }

    public static int Roll(ref NativeBattleEffect227Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }
}
