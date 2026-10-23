using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWildCannonBlow156Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleWildCannonBlow156Children
{
    public static (NativeBattleWildCannonBlow156Frame Frame, int Result) Advance605DF0(NativeBattleWildCannonBlow156Frame frame, uint address, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs) => AdvanceMesh(frame, address, 0, resources, inputs);
    public static (NativeBattleWildCannonBlow156Frame Frame, int Result) Advance606200(NativeBattleWildCannonBlow156Frame frame, uint address, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs) => AdvanceMesh(frame, address, 1, resources, inputs);
    public static (NativeBattleWildCannonBlow156Frame Frame, int Result) Advance6067B0(NativeBattleWildCannonBlow156Frame frame, uint address, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs) => AdvanceMesh(frame, address, 2, resources, inputs);
    public static (NativeBattleWildCannonBlow156Frame Frame, int Result) Advance606910(NativeBattleWildCannonBlow156Frame frame, uint address, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs) => AdvanceMesh(frame, address, 3, resources, inputs);

    private static (NativeBattleWildCannonBlow156Frame Frame, int Result) AdvanceMesh(NativeBattleWildCannonBlow156Frame frame, uint address, int kind, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short age = Signed(task, 12);
        short angle = Signed(task, 0x18);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(kind == 0 ? new(0, 0, angle) : new(0, angle, 0));
        if (kind == 1)
        {
            uint matrix = MatrixAddress(task);
            local = local with { X = unchecked((int)frame.State.ReadDword(matrix + 20)), Y = 0, Z = unchecked((int)frame.State.ReadDword(matrix + 28)) };
        }
        else if (kind >= 2)
        {
            local = local with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        }
        int scale = Signed(task, 0x1C);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, scale, scale);
        if (kind == 0)
        {
            local = NativeBattleGfMatrices.Compose56C2F0(ReadRetainedMatrix(frame, MatrixAddress(task)).ReadMatrix(), local);
        }
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(camera, null));
        uint source = kind switch { 0 => 0xDB6DFC, 1 => 0xDB68EC, 2 => 0xDB54AC, _ => 0xDB7484 };
        uint flags = kind == 2 ? 3u : 0x33u;
        int fadeStart = kind switch { 0 => 4, 1 => 0, 2 => 8, _ => 3 };
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(source)).Write(8, BitConverter.GetBytes(0u));
        if (age >= fadeStart)
        {
            int cue = unchecked((age - fadeStart) * (kind == 1 ? 1024 : kind == 3 ? 409 : 512));
            context = context.Write(12, BitConverter.GetBytes(cue));
            flags |= 0xC0;
        }
        context = context.Write(0x1C, BitConverter.GetBytes(flags));
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleWildCannonBlow156DrawMesh88(temporary, source));
        frame = Release(frame, 88);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        if (kind != 1)
        {
            short speed = Signed(task, 0x1E);
            task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + speed)));
            int divisor = kind == 0 ? 10 : kind == 2 ? 5 : 7;
            task = task.WriteWord(0x1E, unchecked((ushort)(speed - speed / divisor)));
            if (kind == 2)
            {
                short spin = Signed(task, 0x1A);
                task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + spin))).WriteWord(0x1A, unchecked((ushort)(spin - (spin >> 3))));
            }
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        bool done = kind switch { 0 => Signed(task, 12) >= 12, 1 => Signed(task, 12) >= 4, 2 => Signed(task, 12) > 16, _ => Signed(task, 12) > 13 };
        return (Write(frame, address, task), done ? 2 : 0);
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static uint MatrixAddress(NativeBattleModulePoolTask task) => checked((uint)(0x23F0338 + Signed(task, 14) * 32));
    private static NativeBattlePackedMatrix32 ReadRetainedMatrix(NativeBattleWildCannonBlow156Frame frame, uint address)
    {
        (byte[] bytes, bool[] known) = frame.State.ReadView(address, 32).ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
}
