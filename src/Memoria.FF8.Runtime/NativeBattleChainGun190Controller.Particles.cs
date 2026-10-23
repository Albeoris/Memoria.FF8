using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleChainGun190Controller
{
    public static (NativeBattleChainGun190Frame Frame, int Result) AdvanceSlots(NativeBattleChainGun190Frame frame, uint address, NativeBattleChainGun190Resources resources, NativeBattleChainGun190Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int kind = task.Callback switch { 0x5BB6F0 => 0, 0x5BBC70 => 1, 0x5BC1F0 => 2, _ => throw new NotSupportedException("Chain Gun particle callback is unsupported.") };
        uint bank = kind == 2 ? 0x2281B40u : 0x22824A0u;
        uint source = kind == 2 ? 0xD18B48u : 0xD1899Cu;
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(source)).Write(0x24, BitConverter.GetBytes((ushort)(kind == 0 ? 0 : kind == 1 ? 4 : 1)));
        if (kind == 1)
        {
            context = context.Write(0x1C, BitConverter.GetBytes(0x404040u));
        }
        frame = PublishContext(frame, temporary, context, 180);
        int survivors = 0;
        for (int index = 0; index < 100; index++)
        {
            uint row = bank + (uint)(index * 24);
            uint flags = frame.State.ReadDword(row);
            if (kind == 0 ? (flags & 1) == 0 : flags != unchecked((uint)(int)Signed(task, 14)))
            {
                continue;
            }
            short phase = ParticleWord190(frame, row + 4);
            short size = ParticleWord190(frame, row + 6);
            context = Context(frame, temporary, 180).Write(4, BitConverter.GetBytes(unchecked((ushort)phase)));
            if (kind == 2)
            {
                context = context.Write(8, BitConverter.GetBytes((int)ParticleWord190(frame, row + 22)));
            }
            frame = PublishContext(frame, temporary, context, 180);
            frame = SetupSprite190(frame, inputs.ReadView(frame), frame.State.ReadView(row + 8, 8), size, unchecked((short)-(size >> 4)));
            frame = inputs.Execute(frame, new NativeBattleChainGun190DrawSprite180(temporary, source));
            if ((inputs.GraphicsFlags & 0x201) != 0)
            {
                continue;
            }
            frame = PutParticle190(frame, row + 4, phase + 1);
            if (BinaryPrimitives.ReadInt16LittleEndian(Context(frame, temporary, 180).Read(0x28, 2)) < 0)
            {
                frame = frame with { State = frame.State.WriteDword(row, 0) };
                continue;
            }
            if (kind != 2)
            {
                frame = PutParticle190(frame, row + 6, size + (kind == 0 ? 24 : 32));
            }
            for (int axis = 0; axis < 3; axis++)
            {
                if (kind == 1 && axis == 1)
                {
                    continue;
                }
                uint velocityAddress = row + 16 + (uint)(axis * 2);
                short velocity = ParticleWord190(frame, velocityAddress);
                uint pointAddress = row + 8 + (uint)(axis * 2);
                frame = PutParticle190(frame, pointAddress, ParticleWord190(frame, pointAddress) + velocity);
                if (kind == 2 || axis != 1)
                {
                    frame = PutParticle190(frame, velocityAddress, velocity - (kind == 2 ? velocity / 3 : velocity >> 3));
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
        if (kind == 0 ? age >= 0 && age <= 23 : age == 0)
        {
            int maximum = kind == 0 ? 3 : kind == 1 ? 10 : 12;
            for (int birth = 0; birth < maximum; birth++)
            {
                uint row = FreeParticle190(frame, bank);
                if (row == 0)
                {
                    break;
                }
                frame = frame with { State = frame.State.WriteDword(row, kind == 0 ? 1u : unchecked((uint)(int)Signed(task, 14))).WriteWord(row + 4, 0) };
                frame = DrawRandom(frame, out ushort sizeDraw);
                int size = kind == 2 ? (sizeDraw & 1023) + 1280 : sizeDraw % 1536 + 2048;
                frame = PutParticle190(frame, row + 6, size);
                frame = frame with { State = frame.State.WriteView(row + 8, kind == 0 ? frame.State.ReadView(0x2283F38, 8) : TaskView(task, 0x10, 8), 8) };
                if (kind == 1)
                {
                    frame = DrawRandom(frame, out ushort angleDraw);
                    int angle = angleDraw & 4095;
                    int x = NativeBattleCameraMath.Cosine(angle);
                    int z = NativeBattleCameraMath.Sine(angle);
                    frame = DrawRandom(frame, out ushort radiusDraw);
                    int radius = radiusDraw % 50 + 50;
                    frame = PutParticle190(frame, row + 8, ParticleWord190(frame, row + 8) + (unchecked(radius * x) >> 12));
                    frame = PutParticle190(frame, row + 12, ParticleWord190(frame, row + 12) + (unchecked(radius * z) >> 12));
                    frame = DrawRandom(frame, out ushort speedDraw);
                    int speed = speedDraw % 30 + 20;
                    frame = PutParticle190(frame, row + 16, unchecked(speed * x) >> 12);
                    frame = PutParticle190(frame, row + 20, unchecked(speed * z) >> 12);
                }
                else
                {
                    frame = DrawRandom(frame, out ushort x);
                    frame = DrawRandom(frame, out ushort y);
                    frame = DrawRandom(frame, out ushort z);
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50((x & 4095) - 2048, (y & 4095) - 2048, (z & 4095) - 2048);
                    frame = DrawRandom(frame, out ushort radiusDraw);
                    int radius = kind == 0 ? radiusDraw % 200 : radiusDraw % 100 + 90;
                    int[] lanes = [direction.X, direction.Y, direction.Z];
                    for (int axis = 0; axis < 3; axis++)
                    {
                        uint at = row + 8 + (uint)(axis * 2);
                        frame = PutParticle190(frame, at, ParticleWord190(frame, at) + (unchecked(radius * lanes[axis]) >> 12));
                    }
                    frame = DrawRandom(frame, out ushort speedDraw);
                    int speed = kind == 0 ? speedDraw % 80 + 30 : speedDraw % 380 + 130;
                    if (kind == 0)
                    {
                        frame = PutParticle190(frame, row + 16, unchecked(speed * direction.X) >> 12);
                        frame = DrawRandom(frame, out ushort vertical);
                        frame = PutParticle190(frame, row + 18, -15 - vertical % 40);
                        frame = PutParticle190(frame, row + 20, unchecked(speed * direction.Z) >> 12);
                    }
                    else
                    {
                        for (int axis = 0; axis < 3; axis++)
                        {
                            frame = PutParticle190(frame, row + 16 + (uint)(axis * 2), unchecked(speed * lanes[axis]) >> 12);
                        }
                        frame = DrawRandom(frame, out ushort angle);
                        frame = PutParticle190(frame, row + 22, angle & 2047);
                    }
                }
            }
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= (kind == 2 ? 15 : 16) && survivors == 0 ? 2 : 0);
    }

    private static short ParticleWord190(NativeBattleChainGun190Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattleChainGun190Frame PutParticle190(NativeBattleChainGun190Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };

    private static uint FreeParticle190(NativeBattleChainGun190Frame frame, uint bank)
    {
        for (int index = 0; index < 100; index++)
        {
            uint row = bank + (uint)(index * 24);
            if (frame.State.ReadDword(row) == 0)
            {
                return row;
            }
        }
        return 0;
    }
}
