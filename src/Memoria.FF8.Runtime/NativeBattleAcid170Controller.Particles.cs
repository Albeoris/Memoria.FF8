using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAcid170Controller
{
    public static (NativeBattleAcid170Frame Frame, int Result) AdvanceSlots(NativeBattleAcid170Frame frame, uint address, NativeBattleAcid170Resources resources, NativeBattleAcid170Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool raw = task.Callback == 0x5DEA10;
        if (!raw && task.Callback != 0x5DDE90)
        {
            throw new NotSupportedException("Acid particle callback is unsupported.");
        }
        (frame, uint temporary) = Allocate(frame, 180);
        uint source = raw ? 0xD68DF8u : 0xD68EA0u;
        frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(source)).Write(0x24, BitConverter.GetBytes(raw ? (ushort)4 : (ushort)0)), 180);
        if (raw)
        {
            var normal = NativeBattleActionGeometryRules.NormalizeVector56BC50(Word170(frame, 0x23B1340) - Word170(frame, 0x23B0FC8), Word170(frame, 0x23B1342) - Word170(frame, 0x23B0FCA), Word170(frame, 0x23B1344) - Word170(frame, 0x23B0FCC));
            NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, 0, -4096, normal.X, normal.Y, normal.Z);
            NativeBattleActionMatrix local = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), Word170(frame, 0x23B1340), Word170(frame, 0x23B1342), Word170(frame, 0x23B1344), 4096);
            NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null);
            (byte[] rotation, bool[] known) = frame.State.ReadView(0x23B1350, 20).ReadSnapshot(0, 20);
            NativeBattleGeometryRegisters geometry = frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Acid requires its current geometry controls.");
            frame = frame with { Memory = NativeBattleLightBackTransform.LoadBone(frame.Memory with { GeometryRegisters = geometry with { Rotation20 = rotation.Select((value, index) => known[index] ? (byte?)value : null).ToImmutableArray() } }, matrix) };
        }
        int survivors = 0;
        for (int index = 0; index < 100; index++)
        {
            uint row = 0x23AF848 + (uint)(index * 24);
            if ((frame.State.ReadDword(row) & unchecked((uint)(int)Signed(task, 14))) == 0)
            {
                continue;
            }
            short age = Word170(frame, row + 4);
            short size = Word170(frame, row + 6);
            NativeBattleEffectArena context = Context(frame, temporary, 180).Write(4, BitConverter.GetBytes(unchecked((short)(age >> 1))));
            if (raw)
            {
                int gray = age < 3 ? 128 : (11 - age) << 4;
                context = context.Write(0x1C, BitConverter.GetBytes(unchecked((uint)(gray | gray << 8 | gray << 16))));
                frame = RawSprite170(frame, frame.State.ReadView(row + 8, 8), size);
            }
            else
            {
                frame = SetupSprite170(frame, inputs, frame.State.ReadView(row + 8, 8), size, unchecked((short)-(size >> 4)));
            }
            frame = PublishContext(frame, temporary, context, 180);
            frame = inputs.Execute(frame, new NativeBattleAcid170DrawSprite180(temporary, source));
            if ((inputs.GraphicsFlags & 0x201) != 0)
            {
                continue;
            }
            frame = PutWord170(frame, row + 4, age + 1);
            bool dead = raw ? Word170(frame, row + 4) >= 11 : BinaryPrimitives.ReadInt16LittleEndian(Context(frame, temporary, 180).Read(0x28, 2)) < 0;
            if (dead)
            {
                frame = frame with { State = frame.State.WriteDword(row, 0) };
                continue;
            }
            for (int axis = raw ? 0 : 1; axis < (raw ? 3 : 2); axis++)
            {
                uint velocity = row + 16 + (uint)(axis * 2);
                short speed = Word170(frame, velocity);
                frame = PutWord170(frame, row + 8 + (uint)(axis * 2), Word170(frame, row + 8 + (uint)(axis * 2)) + speed);
                frame = PutWord170(frame, velocity, speed - (speed >> (raw ? 2 : 4)));
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
        int births = raw ? taskAge == 0 ? 16 : 0 : taskAge >= 0 && taskAge <= 12 ? resources.DripCounts[taskAge] : 0;
        for (int birth = 0; birth < births; birth++)
        {
            uint row = 0;
            for (int index = 0; index < 100; index++)
            {
                uint candidate = 0x23AF848 + (uint)(index * 24);
                if (frame.State.ReadDword(candidate) == 0)
                {
                    row = candidate;
                    break;
                }
            }
            if (row == 0)
            {
                break;
            }
            frame = frame with { State = frame.State.WriteDword(row, unchecked((uint)(int)Signed(task, 14))).WriteWord(row + 4, 0) };
            frame = DrawRandom(frame, out ushort size);
            frame = PutWord170(frame, row + 6, raw ? (size & 63) + 96 : size % 2560 + 1792);
            if (raw)
            {
                frame = DrawRandom(frame, out ushort x);
                frame = PutWord170(frame, row + 8, x % 200 - 100);
                frame = PutWord170(frame, row + 10, 0);
                frame = DrawRandom(frame, out ushort z);
                frame = PutWord170(frame, row + 12, z % 200 - 100);
                frame = DrawRandom(frame, out ushort angle);
                int heading = angle & 4095;
                frame = DrawRandom(frame, out ushort speed);
                int magnitude = speed % 70 + 50;
                frame = PutWord170(frame, row + 16, unchecked(NativeBattleCameraMath.Sine(heading) * magnitude) >> 12);
                frame = DrawRandom(frame, out ushort y);
                frame = PutWord170(frame, row + 18, -60 - y % 80);
                frame = PutWord170(frame, row + 20, unchecked(NativeBattleCameraMath.Cosine(heading) * magnitude) >> 12);
            }
            else
            {
                frame = frame with { State = frame.State.WriteView(row + 8, TaskView(task, 16, 8), 8) };
                frame = DrawRandom(frame, out ushort x);
                frame = PutWord170(frame, row + 8, Word170(frame, row + 8) + x % 1400 - 700);
                frame = DrawRandom(frame, out ushort y);
                frame = PutWord170(frame, row + 10, Word170(frame, row + 10) - 100 - y % 300);
                frame = DrawRandom(frame, out ushort z);
                frame = PutWord170(frame, row + 12, Word170(frame, row + 12) + z % 1400 - 700);
                frame = DrawRandom(frame, out ushort velocity);
                frame = PutWord170(frame, row + 18, -20 - velocity % 50);
            }
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 4 && survivors == 0 ? 2 : 0);
    }

    private static NativeBattleAcid170Frame RawSprite170(NativeBattleAcid170Frame frame, NativeBattleEffectArena point, short size)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(0, 8);
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters!);
        registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2))), known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : null);
        frame = frame with { Memory = NativeBattleLightBackTransform.Transform(frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, 0) };
        byte?[] rotation = frame.Memory.GeometryRegisters!.Rotation20.ToArray();
        byte[] scale = BitConverter.GetBytes((uint)unchecked((ushort)size));
        foreach (int at in new[] { 0, 8, 16 })
        {
            for (int index = 0; index < 4; index++)
            {
                rotation[at + index] = scale[index];
            }
        }
        int[] mac = frame.Memory.ScriptedColorRegisters!.Mac.Select(value => value ?? throw new NotSupportedException("Acid requires its current full LIGHT/BACK MAC output.")).ToArray();
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(mac[0], mac[1], mac[2]);
        int[] normal = [direction.X, direction.Y, direction.Z];
        for (int axis = 0; axis < 3; axis++)
        {
            mac[axis] = unchecked(mac[axis] + (unchecked(normal[axis] * -(size >> 4)) >> 12));
            frame = frame with { State = frame.State.WriteDword(0x23B1364 + (uint)(axis * 4), unchecked((uint)mac[axis])) };
        }
        return frame with { Memory = frame.Memory with { GeometryRegisters = frame.Memory.GeometryRegisters with { Rotation20 = rotation.ToImmutableArray(), Translation = mac.Select(value => (int?)value).ToImmutableArray() } } };
    }
}
