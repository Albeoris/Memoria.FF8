using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleSaliva138Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSaliva138Controller
{
    public static (NativeBattleSaliva138Frame Frame, int Result) AdvanceMain(NativeBattleSaliva138Frame frame, uint address, NativeBattleSaliva138Resources resources, NativeBattleSaliva138Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        int phase = age - 23;
        if ((uint)phase < 14)
        {
            NativeBattleSaliva138PointStep mouth = inputs.ReadMouth(frame);
            frame = mouth.Frame;
            short angle = unchecked((short)-inputs.ReadModelYaw(frame.State.ReadDword(CasterGlobal)));
            NativeBattleActionMatrix local = Yaw701270(angle);
            if (phase < 7)
            {
                for (int index = 0; index < 2; index++)
                {
                    (frame, uint child) = Append(frame, 3, MovingSpriteCallback);
                    if (child == 0)
                    {
                        continue;
                    }
                    frame = DrawRandom(frame, out ushort spin);
                    frame = DrawRandom(frame, out ushort turn);
                    NativeBattleModulePoolTask task = Vector(Read(frame, child).WriteWord(12, spin).WriteWord(14, unchecked((ushort)((turn & 511) - 256))), 16, mouth.Point);
                    frame = DrawRandom(frame, out ushort scale);
                    task = task.WriteWord(22, (ushort)((scale & 1023) + 1024));
                    frame = DrawRandom(frame, out ushort yaw);
                    frame = DrawRandom(frame, out ushort pitch);
                    NativeBattleVector velocity = Direction6ADFA0((pitch & 1023) - 512, yaw);
                    velocity = velocity with { Z = unchecked((short)-velocity.Z) };
                    velocity = NativeBattleGfMatrices.RotateShort56C4F0(local, velocity);
                    frame = DrawRandom(frame, out ushort strength);
                    NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
                    registers = registers with { Colors = registers.Colors with { Ir0 = (uint)((strength & 127) + 128) } };
                    registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)velocity.X), unchecked((ushort)velocity.Y), unchecked((ushort)velocity.Z));
                    registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors);
                    frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = registers.Colors } };
                    velocity = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
                    frame = Write(frame, child, Vector(task, 24, velocity).WriteWord(30, 0));
                }
            }
            local = local with { X = mouth.Point.X, Y = mouth.Point.Y, Z = mouth.Point.Z };
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadCamera(frame).ReadMatrix(), local);
            NativeBattleEffectArena context = ActContext(frame, NativeBattlePackedMatrix32.FromMatrix(composed, 0), -128);
            NativeBattleSaliva138ActStep advanced = inputs.AdvanceAct(frame, address, resources.MainAct, 0x2C, 0xA8, context);
            frame = advanced.Frame;
            advanced = inputs.AdvanceAct(frame, address, resources.SecondaryAct, 0xD4, 0x194, context);
            frame = advanced.Frame;
            if (phase < 3)
            {
                (frame, uint child) = Append(frame, 2, CurveCallback);
                if (child == 0)
                {
                    throw new NotSupportedException("Saliva exhausted its three native curve tasks.");
                }
                NativeBattleModulePoolTask curve = Read(frame, child).WriteWord(18, 0);
                curve = curve.WriteBytes(20, Read(frame, address).ReadBytes(28, 8));
                frame = DrawRandom(frame, out ushort scale);
                frame = Write(frame, child, curve.WriteWord(26, (ushort)((scale & 2047) + 2048)).WriteWord(36, 0));
            }
        }
        int impactPhase = age - 29;
        if ((uint)impactPhase < 26)
        {
            NativeBattlePackedMatrix32 camera = inputs.ReadCamera(frame);
            NativeBattleActionMatrix matrix = camera.ReadMatrix();
            NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), matrix, true, false, null);
            geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => camera.Known[index] ? (byte?)camera.Bytes[index] : null).ToImmutableArray() };
            NativeBattleModulePoolTask main = Read(frame, address);
            NativeBattleGeometryRegisterStep registers = new(geometry, frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
            registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(main, 28), Signed(main, 34));
            registers = NativeBattleGeometry.Transform(registers, false);
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
            matrix = matrix with { X = unchecked(matrix.X + registers.Colors.Mac[0]!.Value), Y = unchecked(matrix.Y + registers.Colors.Mac[1]!.Value), Z = unchecked(matrix.Z + registers.Colors.Mac[2]!.Value) };
            NativeBattleEffectArena context = ActContext(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, (uint)phase < 14 ? (ushort?)0 : null), 0);
            frame = inputs.AdvanceAct(frame, address, resources.ImpactAct, 0x268, 0x118, context).Frame;
            if (impactPhase >= 4 && impactPhase < 12 && (impactPhase & 1) == 0)
            {
                (frame, uint child) = Append(frame, 4, SimpleSpriteCallback);
                if (child != 0)
                {
                    NativeBattleModulePoolTask task = Read(frame, child).WriteBytes(12, Read(frame, address).ReadBytes(28, 8));
                    frame = DrawRandom(frame, out ushort x);
                    frame = DrawRandom(frame, out ushort y);
                    frame = DrawRandom(frame, out ushort z);
                    task = Vector(task, 12, new(unchecked((short)(Signed(task, 12) + (x & 511) - 256)), unchecked((short)(Signed(task, 14) + (y & 511) - 300)), unchecked((short)(Signed(task, 16) + (z & 511) - 256))));
                    frame = Write(frame, child, task.WriteWord(18, 0));
                }
            }
        }
        if (age == 25)
        {
            frame = inputs.Execute(frame, new NativeBattleSaliva138Sound(resources.Sound, 0, 128));
        }
        if (age == 40)
        {
            frame = inputs.Execute(frame, new NativeBattleSaliva138Contact(0));
        }
        if (age >= 55)
        {
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static NativeBattleActionMatrix Yaw701270(short angle)
    {
        short sine = unchecked((short)NativeBattleCameraMath.Sine(-angle));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(-angle));
        return new([cosine, 0, unchecked((short)-sine), 0, 4096, 0, sine, 0, cosine], 0, 0, 0, 4096);
    }
}
