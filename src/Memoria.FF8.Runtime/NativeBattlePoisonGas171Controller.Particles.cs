using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattlePoisonGas171Controller
{
    public static (NativeBattlePoisonGas171Frame Frame, int Result) AdvanceSlots(NativeBattlePoisonGas171Frame frame, uint address, NativeBattlePoisonGas171Resources resources, NativeBattlePoisonGas171Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int kind = task.Callback switch { 0x5DBED0 => 0, 0x5DC270 => 1, 0x5DC700 => 2, _ => throw new NotSupportedException("Poison Gas particle callback is unsupported.") };
        uint bank = kind == 1 ? 0x23AD728u : 0x23AE088u;
        uint source = kind == 2 ? 0xD656F4u : 0xD658A0u;
        (frame, uint temporary) = Allocate(frame, 180);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(source)).Write(0x24, new byte[2]), 180);
        int survivors = 0;
        for (int index = 0; index < 100; index++)
        {
            uint row = bank + (uint)(index * 24);
            if ((frame.State.ReadDword(row) & unchecked((uint)(int)Signed(task, 14))) == 0)
            {
                continue;
            }
            short age = ParticleWord171(frame, row + 4);
            short size = ParticleWord171(frame, row + 6);
            frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(4, BitConverter.GetBytes(unchecked((ushort)(kind == 0 ? age >> 1 : age)))), 180);
            frame = SetupSprite171(frame, inputs, frame.State.ReadView(row + 8, 8), size);
            frame = inputs.Execute(frame, new NativeBattlePoisonGas171DrawSprite180(temporary, source) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePoisonGas171VisualLifetimes.Identity(task, bank, index, kind, source) : null });
            if ((inputs.GraphicsFlags & 0x201) != 0)
            {
                continue;
            }
            frame = PutParticle171(frame, row + 4, age + 1);
            if (BinaryPrimitives.ReadInt16LittleEndian(Context(frame, temporary, 180).Read(0x28, 2)) < 0)
            {
                frame = frame with { State = frame.State.WriteDword(row, 0) };
                continue;
            }
            if (kind != 2)
            {
                frame = PutParticle171(frame, row + 6, size + (size >> 5));
                for (int axis = 0; axis < 3; axis++)
                {
                    uint velocityAddress = row + 16 + (uint)(axis * 2);
                    short velocity = ParticleWord171(frame, velocityAddress);
                    uint pointAddress = row + 8 + (uint)(axis * 2);
                    frame = PutParticle171(frame, pointAddress, ParticleWord171(frame, pointAddress) + velocity);
                    frame = PutParticle171(frame, velocityAddress, velocity - (velocity >> 5));
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
        short taskAge = Signed(task, 12);
        if (taskAge >= 0 && taskAge <= (kind == 2 ? 13 : 16))
        {
            NativeBattleEffectArena start = TaskView(task, 0x10, 8);
            int[] direction = [0, 0, 0];
            if (kind != 2)
            {
                int emitter = Signed(task, 0x18);
                NativeBattleVector first = ReadEmitter171(resources, emitter);
                NativeBattleVector second = ReadEmitter171(resources, emitter + 1);
                if (kind == 0)
                {
                    second = second with { X = unchecked((short)(second.X + second.X / 32 * taskAge)), Z = unchecked((short)(second.Z + second.Z / 10 * taskAge)) };
                }
                NativeBattleActionMatrix world = inputs.ReadModelWorld(frame, checked((int)frame.State.ReadDword(0x23AF80C))).ReadMatrix();
                first = AddEmitter171(NativeBattleGfMatrices.RotateShort56C4F0(world, first), task);
                second = AddEmitter171(NativeBattleGfMatrices.RotateShort56C4F0(world, second), task);
                var normal = NativeBattleActionGeometryRules.NormalizeVector56BC50(second.X - first.X, second.Y - first.Y, second.Z - first.Z);
                direction = [normal.X, normal.Y, normal.Z];
                start = PointView(first);
            }
            for (int birth = 0; birth < (kind == 2 ? 1 : 2); birth++)
            {
                uint row = FreeParticle171(frame, bank);
                if (row == 0)
                {
                    break;
                }
                frame = frame with { State = frame.State.WriteDword(row, unchecked((uint)(int)Signed(task, 14))).WriteWord(row + 4, 0) };
                frame = DrawRandom(frame, out ushort sizeDraw);
                frame = PutParticle171(frame, row + 6, kind == 2 ? sizeDraw % 2304 + 768 : (sizeDraw & 1023) + 1664);
                frame = frame with { State = frame.State.WriteView(row + 8, start, 8) };
                if (kind == 2)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        frame = DrawRandom(frame, out ushort jitter);
                        uint at = row + 8 + (uint)(axis * 2);
                        frame = PutParticle171(frame, at, ParticleWord171(frame, at) + jitter % 1000 - 500);
                    }
                }
                else
                {
                    frame = DrawRandom(frame, out ushort speedDraw);
                    int speed = speedDraw % 95 + 40;
                    for (int axis = 0; axis < 3; axis++)
                    {
                        frame = DrawRandom(frame, out ushort jitter);
                        int width = kind == 0 ? 40 : axis == 1 ? 60 : 80;
                        frame = PutParticle171(frame, row + 16 + (uint)(axis * 2), (unchecked(speed * direction[axis]) >> 12) + jitter % width - width / 2);
                    }
                }
                if (inputs.CapturePresentation)
                {
                    NativeBattlePoisonGas171VisualLifetimes.Allocated(task, bank, checked((int)((row - bank) / 24)));
                }
            }
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 4 && survivors == 0 ? 2 : 0);
    }

    private static NativeBattleVector ReadEmitter171(NativeBattlePoisonGas171Resources resources, int index)
    {
        ReadOnlySpan<byte> bytes = resources.EmitterPoints.AsSpan().Slice(checked(index * 8), 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
    }
    private static NativeBattleVector AddEmitter171(NativeBattleVector point, NativeBattleModulePoolTask task) => new(unchecked((short)(point.X + Signed(task, 0x10))), unchecked((short)(point.Y + Signed(task, 0x12))), unchecked((short)(point.Z + Signed(task, 0x14))));
    private static short ParticleWord171(NativeBattlePoisonGas171Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattlePoisonGas171Frame PutParticle171(NativeBattlePoisonGas171Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };
    private static uint FreeParticle171(NativeBattlePoisonGas171Frame frame, uint bank)
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
