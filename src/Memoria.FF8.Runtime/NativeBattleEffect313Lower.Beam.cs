using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect313Controller;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect313BeamShape(NativeBattleActionMatrix Matrix, NativeBattleEffectArena Emitter8, int Length);

public static partial class NativeBattleEffect313Lower
{
    public static NativeBattleEffect313Step AdvanceBeam(NativeBattleEffect313Frame frame, uint address, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffect313BeamShape shape = BuildBeam313(ReadBytes(frame, 0x223CC90, 8), inputs.ReadCameraEye8(frame), TaskView(task, 16, 8), Signed(task, 24), Signed(task, 32), Signed(task, 12));
        int age = Signed(task, 12);
        frame = DrawMesh313(frame, shape.Matrix, 0xCD82E0, age >= 3 ? 0xF3u : 0x33u, age >= 3 ? unchecked((age - 3) * 682) : null, resources, inputs, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(313, task) with { ResourceGeneration = 0xCD82E0 } : null);
        if (Paused313(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        short spin = Signed(task, 26);
        task = task.WriteWord(24, unchecked((ushort)(Signed(task, 24) + spin))).WriteWord(26, unchecked((ushort)(spin + (spin >> 4))));
        frame = Write(frame, address, task);
        if (Signed(task, 12) >= 2)
        {
            int count = Random313(ref frame) % 5 + 3;
            for (int index = 0; index < count; index++)
            {
                uint child = Mandatory313(ref frame, 0x59C250);
                NativeBattleModulePoolTask output = CopyTask(Read(frame, child).WriteWord(12, 0), 16, shape.Emitter8, 8);
                output = output.WriteWord(14, unchecked((ushort)(Random313(ref frame) % 8 + 8))).WriteWord(18, 0);
                output = output.WriteWord(16, unchecked((ushort)(Signed(output, 16) + Random313(ref frame) % 1000 - 500)));
                output = output.WriteWord(20, unchecked((ushort)(Signed(output, 20) + Random313(ref frame) % 900 - 400)));
                output = output.WriteWord(22, unchecked((ushort)(-10 - Random313(ref frame) % 20)));
                output = output.WriteWord(28, unchecked((ushort)(3072 + Random313(ref frame) % 1792)));
                frame = Write(frame, child, output);
            }
        }
        task = Read(frame, address);
        short next = unchecked((short)(Signed(task, 12) + 1));
        return new(Write(frame, address, task.WriteWord(12, unchecked((ushort)next))), next > 9 ? 2 : 0);
    }

    public static NativeBattleEffect313BeamShape BuildBeam313(NativeBattleEffectArena anchor8, NativeBattleEffectArena eye8, NativeBattleEffectArena target8, short yaw, short width, short age)
    {
        NativeBattleVector anchor = Vector(anchor8);
        NativeBattleVector target = Vector(target8);
        int dx = target.X - anchor.X;
        int dy = target.Y - anchor.Y;
        int dz = target.Z - anchor.Z;
        int square = NativeBattleSleepingGas168.NormalizeSquare(dx, dy, dz);
        var normalized = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, dy, dz);
        NativeBattleActionMatrix yawMatrix = NativeBattleGfMatrices.Euler56CD50(new(0, yaw, 0));
        (int X, int Y, int Z) direction = NativeBattleGfMatrices.RotateLong56C600(yawMatrix, normalized.X, normalized.Y, normalized.Z);
        NativeBattleModuleAxisAngle aim = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
        NativeBattleActionMatrix aligned = new(NativeBattleModuleAlignment.Rotation5714F0(aim.Angle, aim.Axis), anchor.X, anchor.Y, anchor.Z, 4096);
        var down = NativeBattleGfMatrices.RotateLong56C600(aligned, 0, -4096, 0);
        NativeBattleVector eye = Vector(eye8);
        var eyeNormal = NativeBattleActionGeometryRules.NormalizeVector56BC50(eye.X - anchor.X, eye.Y - anchor.Y, eye.Z - anchor.Z);
        var cross = Cross313(direction, eyeNormal);
        var perpendicular = Cross313(eyeNormal, cross);
        NativeBattleModuleAxisAngle facing = NativeBattleModuleAlignment.Between571480(down.X, down.Y, down.Z, perpendicular.X, perpendicular.Y, perpendicular.Z);
        int dot = unchecked(direction.Z * facing.Axis.Z + direction.Y * facing.Axis.Y + direction.X * facing.Axis.X);
        int angle = dot < 0 ? unchecked(-facing.Angle) : facing.Angle;
        NativeBattleActionMatrix roll = new(NativeBattleModuleAlignment.Rotation5714F0(angle, new(checked((short)direction.X), checked((short)direction.Y), checked((short)direction.Z))), 0, 0, 0, 4096);
        aligned = aligned with { Rotation = NativeBattleGfMatrices.Multiply56C090(roll, aligned).Rotation };
        int length = NativeBattleRayBomb10Geometry.Sqrt56BEC0(square);
        NativeBattleEffectArena emitter = anchor8;
        emitter = PutWord313(emitter, 0, unchecked(anchor.X + (unchecked(direction.X * length) >> 12)));
        emitter = PutWord313(emitter, 2, unchecked(anchor.Y + (unchecked(direction.Y * length) >> 12)));
        emitter = PutWord313(emitter, 4, unchecked(anchor.Z + (unchecked(direction.Z * length) >> 12)));
        int longitudinal = unchecked(length << 12) / 1046;
        if (age < 2)
        {
            longitudinal = unchecked(longitudinal / 2 * age);
        }
        return new(NativeBattlePoseMatrices.Scale56BEF0(aligned, width, width, longitudinal), emitter, length);
    }

    private static (int X, int Y, int Z) Cross313((int X, int Y, int Z) first, (int X, int Y, int Z) second) => (unchecked(first.Y * second.Z - first.Z * second.Y) >> 12, unchecked(first.Z * second.X - first.X * second.Z) >> 12, unchecked(first.X * second.Y - first.Y * second.X) >> 12);
}
