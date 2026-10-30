using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAdditional181Controller
{
    public static (NativeBattleAdditional181Frame Frame, int Result) AdvanceSlots(NativeBattleAdditional181Frame frame, uint address, NativeBattleAdditional181Resources resources, NativeBattleAdditional181Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool moving = task.Callback == 0x5CC2A0;
        if (!moving && task.Callback != 0x5CC560)
        {
            throw new NotSupportedException("Additional particle callback is unsupported.");
        }
        uint source = moving ? 0xD3E3A0u : 0xD3E1F4u;
        uint mask = moving ? 2u : 4u;
        (frame, uint temporary) = Allocate(frame, 180);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(source)).Write(0x24, new byte[2]), 180);
        int survivors = 0;
        for (int index = 0; index < 100; index++)
        {
            uint row = 0x2306600u + (uint)(index * 24);
            if ((frame.State.ReadDword(row) & mask) == 0)
            {
                continue;
            }
            short phase = ParticleWord181(frame, row + 4);
            short size = ParticleWord181(frame, row + 6);
            frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(4, BitConverter.GetBytes(unchecked((ushort)phase))), 180);
            frame = SetupSprite181(frame, inputs.ReadView(frame), frame.State.ReadView(row + 8, 8), size, unchecked((short)-(size >> 4)));
            frame = inputs.Execute(frame, new NativeBattleAdditional181DrawSprite180(temporary, source) { PilotIdentity = inputs.CapturePresentation ? NativeBattleShotVisualLifetimes.Resource(NativeBattleShotVisualLifetimes.Identity(181, task, NativeBattlePilotVisualLifetimes.Identity(181, task), unchecked((int)row)), source) : null });
            if ((inputs.GraphicsFlags & 0x201) != 0)
            {
                continue;
            }
            frame = PutParticle181(frame, row + 4, phase + 1);
            if (BinaryPrimitives.ReadInt16LittleEndian(Context(frame, temporary, 180).Read(0x28, 2)) < 0)
            {
                frame = frame with { State = frame.State.WriteDword(row, 0) };
                continue;
            }
            if (moving)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    uint velocityAddress = row + 16 + (uint)(axis * 2);
                    short velocity = ParticleWord181(frame, velocityAddress);
                    uint pointAddress = row + 8 + (uint)(axis * 2);
                    frame = PutParticle181(frame, pointAddress, ParticleWord181(frame, pointAddress) + velocity);
                    frame = PutParticle181(frame, velocityAddress, velocity - (velocity >> 2));
                }
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
        if (age >= 0 && age <= (moving ? 7 : 13))
        {
            for (int birth = 0; birth < (moving ? 7 : 2); birth++)
            {
                uint row = FreeParticle181(frame);
                if (row == 0)
                {
                    break;
                }
                frame = frame with { State = frame.State.WriteDword(row, mask).WriteWord(row + 4, 0) };
                if (inputs.CapturePresentation)
                {
                    NativeBattleShotVisualLifetimes.Allocated(181, task, unchecked((int)row));
                }
                frame = DrawRandom(frame, out ushort sizeDraw);
                frame = PutParticle181(frame, row + 6, moving ? (sizeDraw & 4095) + 2048 : sizeDraw % 3840 + 2560);
                frame = frame with { State = frame.State.WriteView(row + 8, TaskView(task, 0x10, 8), 8) };
                frame = DrawRandom(frame, out ushort x);
                frame = DrawRandom(frame, out ushort y);
                frame = DrawRandom(frame, out ushort z);
                if (moving)
                {
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50((x & 4095) - 2048, (y & 4095) - 2048, (z & 4095) - 2048);
                    frame = DrawRandom(frame, out ushort radiusDraw);
                    int radius = radiusDraw % 600 + 400;
                    frame = DrawRandom(frame, out ushort speedDraw);
                    int speed = speedDraw % 250 + 290;
                    int[] lanes = [direction.X, direction.Y, direction.Z];
                    for (int axis = 0; axis < 3; axis++)
                    {
                        uint pointAddress = row + 8 + (uint)(axis * 2);
                        frame = PutParticle181(frame, pointAddress, ParticleWord181(frame, pointAddress) + (unchecked(radius * lanes[axis]) >> 12));
                        frame = PutParticle181(frame, row + 16 + (uint)(axis * 2), unchecked(speed * lanes[axis]) >> 12);
                    }
                }
                else
                {
                    int[] draws = [x, y, z];
                    for (int axis = 0; axis < 3; axis++)
                    {
                        uint pointAddress = row + 8 + (uint)(axis * 2);
                        frame = PutParticle181(frame, pointAddress, ParticleWord181(frame, pointAddress) + draws[axis] % 1200 - 600);
                    }
                }
            }
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 4 && survivors == 0 ? 2 : 0);
    }

    private static short ParticleWord181(NativeBattleAdditional181Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattleAdditional181Frame PutParticle181(NativeBattleAdditional181Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };
    private static uint FreeParticle181(NativeBattleAdditional181Frame frame)
    {
        for (int index = 0; index < 100; index++)
        {
            uint row = 0x2306600u + (uint)(index * 24);
            if (frame.State.ReadDword(row) == 0)
            {
                return row;
            }
        }
        return 0;
    }
}
