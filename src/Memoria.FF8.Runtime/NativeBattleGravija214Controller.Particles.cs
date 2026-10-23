namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGravija214Controller
{
    public static (NativeBattleGravija214Frame Frame, int Result) AdvanceSprite69DF00(NativeBattleGravija214Frame frame, uint address, NativeBattleGravija214Resources resources, NativeBattleGravija214Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleModulePoolTask main = Read(frame, 0x20E01B8);
        int radius = unchecked(Signed(main, 0x0E) * Signed(task, 0x12)) >> 12;
        short angle = Signed(task, 0x10);
        NativeBattleVector local = new(unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12)), 0, unchecked((short)(unchecked(NativeBattleCuragaMatrices.Cosine8DDRotation(angle) * radius) >> 12)));
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(Signed(task, 0x14), Signed(task, 0x16), Signed(task, 0x18)));
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null), true, false);
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Gravija sprite requires its arithmetic owner."));
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, local, null), false);
        NativeBattleVector position = new(unchecked((short)(registers.Colors.Ir[0]!.Value + Signed(main, 0x28))), unchecked((short)(registers.Colors.Ir[1]!.Value + Signed(main, 0x2A))), unchecked((short)(registers.Colors.Ir[2]!.Value + Signed(main, 0x2C))));
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), position, null, 2048, -512) };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(0x1172B40u)).Write(4, BitConverter.GetBytes((ushort)1)).Write(36, BitConverter.GetBytes((ushort)0));
        frame = PublishContext(frame, temporary, context, 180);
        frame = inputs.Execute(frame, new NativeBattleGravija214DrawSprite180(temporary, 0x1172B40));
        frame = Release(frame, 180);
        task = Read(frame, address);
        short age = unchecked((short)(task.ReadWord(0x0C) + 1));
        frame = Write(frame, address, task.WriteWord(0x10, unchecked((ushort)(task.ReadWord(0x10) + 8))).WriteWord(0x0C, unchecked((ushort)age)));
        return (frame, age >= Signed(task, 0x0E) ? 2 : 0);
    }
}
