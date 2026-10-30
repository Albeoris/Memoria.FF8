using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAdditional182Controller
{
    public static (NativeBattleAdditional182Frame Frame, int Result) AdvanceSlots(NativeBattleAdditional182Frame frame, uint address, NativeBattleAdditional182Resources resources, NativeBattleAdditional182Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool falling = task.Callback == 0x5CA490;
        if (!falling && task.Callback != 0x5CAED0)
        {
            throw new NotSupportedException("Additional effect particle callback is unsupported.");
        }
        if (!falling && Signed(task, 0x18) > 0)
        {
            if ((inputs.GraphicsFlags & 0x201) == 0)
            {
                frame = Write(frame, address, task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) - 1))));
            }
            return (frame, 0);
        }
        uint bank = falling ? 0x2304D88u : 0x2304248u;
        int count = falling ? 100 : 120;
        uint source = falling ? 0xD3ACCCu : 0xD3ABC0u;
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(source)).Write(0x24, BitConverter.GetBytes((ushort)(falling ? 4 : 8)));
        frame = PublishContext(frame, temporary, context, 180);
        if (!falling && Signed(task, 12) < 8)
        {
            short size = Signed(task, 0x1C);
            frame = SetupParticle182(frame, inputs, TaskView(task, 0x10, 8), size, unchecked((short)-(size >> 3)));
            frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(4, BitConverter.GetBytes(task.ReadWord(12))), 180);
            frame = inputs.Execute(frame, new NativeBattleAdditional182DrawSprite180(temporary, source) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(182, task) with { Primitive = 0x10000, ResourceGeneration = source } : null });
        }
        int survivors = 0;
        for (int index = 0; index < count; index++)
        {
            uint row = checked(bank + (uint)(index * 24));
            uint flags = frame.State.ReadDword(row);
            uint mask = falling ? 1u : unchecked((uint)(int)Signed(task, 14));
            if ((flags & mask) == 0)
            {
                continue;
            }
            short phase = ParticleWord182(frame, row + 4);
            short size = ParticleWord182(frame, row + 6);
            context = Context(frame, temporary, 180).Write(4, BitConverter.GetBytes(unchecked((ushort)phase)));
            if (falling)
            {
                context = context.Write(0x1C, BitConverter.GetBytes(resources.Colors[phase % 8]));
            }
            frame = PublishContext(frame, temporary, context, 180);
            frame = SetupParticle182(frame, inputs, frame.State.ReadView(row + 8, 8), size, unchecked((short)-(size >> 4)));
            frame = inputs.Execute(frame, new NativeBattleAdditional182DrawSprite180(temporary, source) { PilotIdentity = inputs.CapturePresentation ? NativeBattleAdditional182Visuals.Particle(frame, task, bank, index, source) : null });
            if ((inputs.GraphicsFlags & 0x201) != 0)
            {
                continue;
            }
            frame = PutParticleWord182(frame, row + 4, phase + 1);
            if (BinaryPrimitives.ReadInt16LittleEndian(Context(frame, temporary, 180).Read(0x28, 2)) < 0)
            {
                frame = frame with { State = frame.State.WriteDword(row, 0) };
                continue;
            }
            if (falling)
            {
                short velocity = ParticleWord182(frame, row + 18);
                frame = PutParticleWord182(frame, row + 10, ParticleWord182(frame, row + 10) + velocity);
                frame = PutParticleWord182(frame, row + 18, velocity - (velocity >> 4));
            }
            survivors++;
        }
        frame = Release(frame, 180);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        short age = Signed(task, 12);
        if (age >= 0 && age <= (falling ? 14 : Signed(task, 0x1A) + 5))
        {
            for (int birth = 0; birth < 3; birth++)
            {
                uint row = FindFreeParticle182(frame, bank, count);
                if (row == 0)
                {
                    break;
                }
                frame = frame with { State = frame.State.WriteDword(row, falling ? 1u : unchecked((uint)(int)Signed(task, 14))).WriteWord(row + 4, (ushort)(falling ? 0 : 6)) };
                frame = DrawRandom(frame, out ushort sizeDraw);
                int size = falling ? unchecked((sizeDraw % 5120 + 5888) * Signed(task, 0x1C)) >> 12 : sizeDraw % 640 + 1024;
                frame = PutParticleWord182(frame, row + 6, size);
                if (!falling)
                {
                    frame = frame with { State = frame.State.WriteView(row + 8, TaskView(task, 0x10, 8), 8) };
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    frame = DrawRandom(frame, out ushort position);
                    int value;
                    if (falling)
                    {
                        int width = Signed(task, 0x1E);
                        value = axis == 1 ? Signed(task, 0x16) - position % (width >> 1) : Signed(task, axis == 0 ? 0x10 : 0x14) + position % (width * 2) - width;
                    }
                    else
                    {
                        int width = axis == 1 ? 130 : 260;
                        value = ParticleWord182(frame, row + 8 + (uint)(axis * 2)) + position % width - width / 2;
                    }
                    frame = PutParticleWord182(frame, row + 8 + (uint)(axis * 2), value);
                }
                if (falling)
                {
                    frame = DrawRandom(frame, out ushort velocity);
                    frame = PutParticleWord182(frame, row + 18, -60 - velocity % 120);
                }
                if (inputs.CapturePresentation)
                {
                    NativeBattlePilotVisualLifetimes.Allocated(frame.State.Globals, bank, checked((int)((row - bank) / 24)));
                }
            }
        }
        task = Read(frame, address);
        if (!falling)
        {
            short speed = Signed(task, 0x1E);
            task = task.WriteWord(0x12, unchecked((ushort)(task.ReadWord(0x12) - speed))).WriteWord(0x1E, unchecked((ushort)(speed - speed / 10)));
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 4 && survivors == 0 ? 2 : 0);
    }

    private static NativeBattleAdditional182Frame SetupParticle182(NativeBattleAdditional182Frame frame, NativeBattleAdditional182Inputs inputs, NativeBattleEffectArena point, short size, short depth)
    {
        (byte[] wBytes, bool[] wKnown) = point.ReadSnapshot(6, 2);
        short? w = wKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(wBytes) : null;
        NativeBattleVector xyz = new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), xyz, w, size, depth) };
    }

    private static short ParticleWord182(NativeBattleAdditional182Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattleAdditional182Frame PutParticleWord182(NativeBattleAdditional182Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };

    private static uint FindFreeParticle182(NativeBattleAdditional182Frame frame, uint bank, int count)
    {
        for (int index = 0; index < count; index++)
        {
            uint row = checked(bank + (uint)(index * 24));
            if (frame.State.ReadDword(row) == 0)
            {
                return row;
            }
        }
        return 0;
    }
}
