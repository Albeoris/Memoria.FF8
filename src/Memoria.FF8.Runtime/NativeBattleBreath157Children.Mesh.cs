using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBreath157Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBreath157Children
{
    public static (NativeBattleBreath157Frame Frame, int Result) Advance6043A0(NativeBattleBreath157Frame frame, uint address, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs) => AdvanceMesh(frame, address, 0, resources, inputs);
    public static (NativeBattleBreath157Frame Frame, int Result) Advance6049F0(NativeBattleBreath157Frame frame, uint address, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs) => AdvanceMesh(frame, address, 1, resources, inputs);
    public static (NativeBattleBreath157Frame Frame, int Result) Advance604E90(NativeBattleBreath157Frame frame, uint address, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs) => AdvanceMesh(frame, address, 2, resources, inputs);
    public static (NativeBattleBreath157Frame Frame, int Result) Advance6050A0(NativeBattleBreath157Frame frame, uint address, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs) => AdvanceMesh(frame, address, 3, resources, inputs);

    private static (NativeBattleBreath157Frame Frame, int Result) AdvanceMesh(NativeBattleBreath157Frame frame, uint address, int kind, NativeBattleBreath157Resources resources, NativeBattleBreath157Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short age = Signed(task, 12);
        NativeBattleVector angles = kind switch
        {
            0 => new(0, 0, Signed(task, 0x18)),
            1 => new(0, 0, 0),
            2 => new(Signed(task, 0x10), 0, Signed(task, 0x18)),
            _ => new(Signed(task, 0x20), 0, Signed(task, 0x18))
        };
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(angles);
        if (kind >= 2)
        {
            NativeBattleVector point = Point(frame, MovingPoint(task));
            local = local with { X = point.X, Y = point.Y, Z = point.Z };
        }
        int size = Signed(task, 0x1C) + (kind == 3 ? 2560 : 0);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, size, size, size);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(Packed(frame, kind == 0 ? 0x23ECDB0u : 0x23EB5C0u).ReadMatrix(), local);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        (frame, uint temporary) = Allocate(frame, 88);
        uint source = kind switch { 0 => 0xDB0DBC, 1 => 0xDB42C4, 2 => 0xDB20DC, _ => 0xDB3BDC };
        uint flags = kind == 2 ? 3u : 0x33u;
        int? cue = kind switch
        {
            0 when age < 4 => unchecked((4 - age) << 10),
            0 when age >= 22 => unchecked((age - 22) << 9),
            1 when age < 8 => unchecked((8 - age) << 9),
            2 when age >= 8 => unchecked((age - 8) << 9),
            3 when age >= 8 => unchecked((age - 8) * 409),
            _ => null
        };
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(source)).Write(8, new byte[4]);
        if (cue is int fade)
        {
            context = context.Write(12, BitConverter.GetBytes(fade));
            flags |= 0xC0;
        }
        frame = PublishContext(frame, temporary, context.Write(0x1C, BitConverter.GetBytes(flags)), 88);
        frame = inputs.Execute(frame, new NativeBattleBreath157DrawMesh88(temporary, source));
        frame = Release(frame, 88);
        if (kind >= 2)
        {
            NativeBattleVector secondAngles = kind == 2 ? new(0, Signed(task, 0x10), 0) : new(unchecked((short)(Signed(task, 0x20) + 1024)), 0, Signed(task, 0x18));
            local = NativeBattleGfMatrices.Euler56CE30(secondAngles);
            int secondSize = Signed(task, 0x1C) + (kind == 2 ? 672 : 2560);
            local = NativeBattlePoseMatrices.Scale56BEF0(local, secondSize, secondSize, secondSize);
            NativeBattleActionMatrix left = kind == 2 ? inputs.ReadView(frame).ReadMatrix() : Packed(frame, 0x23ECDB0).ReadMatrix();
            matrix = NativeBattleGfMatrices.Compose56C2F0(left, local);
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, null);
            NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters!, matrix, true, false, null);
            geometry = geometry with { Rotation20 = packed.Bytes.Take(20).Select((value, index) => packed.Known[index] ? (byte?)value : null).ToImmutableArray() };
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = geometry } };
            if (kind == 2)
            {
                source = 0xDB2F54;
                frame = PublishContext(frame, temporary, Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(source)), 88);
            }
            frame = inputs.Execute(frame, new NativeBattleBreath157DrawMesh88(temporary, source));
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        if (kind == 0)
        {
            task = Add(task, 0x18, Signed(task, 0x1A));
        }
        else if (kind == 1)
        {
            short speed = Signed(task, 0x1E);
            task = Add(task, 0x1C, -speed).WriteWord(0x1E, unchecked((ushort)(speed - speed / 24)));
        }
        else
        {
            if (kind == 2)
            {
                short velocity = Signed(task, 0x14);
                uint point = MovingPoint(task) + 4;
                frame = PutWord(frame, point, Word(frame, point) - velocity);
                task = task.WriteWord(0x14, unchecked((ushort)(velocity - velocity / 6)));
                task = Add(task, 0x10, Signed(task, 0x12));
            }
            short spin = Signed(task, 0x1A);
            short speed = Signed(task, 0x1E);
            task = Add(Add(task, 0x18, spin), 0x1C, speed).WriteWord(0x1E, unchecked((ushort)(speed - speed / 8)));
            if (kind == 3)
            {
                task = task.WriteWord(0x1A, unchecked((ushort)(spin - (spin >> 4))));
            }
        }
        task = Add(task, 12, 1);
        int end = kind switch { 0 => 30, 1 => 10, 2 => 16, _ => 18 };
        return (Write(frame, address, task), Signed(task, 12) >= end ? 2 : 0);
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(task.ReadWord(offset) + value)));
    private static uint MovingPoint(NativeBattleModulePoolTask task) => checked((uint)(0x23ECE00 + Signed(task, 14) * 8));
    private static NativeBattlePackedMatrix32 Packed(NativeBattleBreath157Frame frame, uint address)
    {
        (byte[] bytes, bool[] known) = frame.State.ReadView(address, 32).ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
    private static short Word(NativeBattleBreath157Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattleVector Point(NativeBattleBreath157Frame frame, uint address) => new(Word(frame, address), Word(frame, address + 2), Word(frame, address + 4));
    private static NativeBattleBreath157Frame PutWord(NativeBattleBreath157Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };
}
