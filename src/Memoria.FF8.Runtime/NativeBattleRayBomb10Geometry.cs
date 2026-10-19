namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRayBomb10BeamGeometry(NativeBattleActionMatrix World, NativeBattleModuleAnchor End, int Length, int FullStretch);

public static class NativeBattleRayBomb10Geometry
{
    public static (int X, int Y, int Z) Cross56BBF0((int X, int Y, int Z) first, (int X, int Y, int Z) second)
    {
        int x = unchecked(first.Y * second.Z - second.Y * first.Z) >> 12;
        int y = unchecked(second.X * first.Z - first.X * second.Z) >> 12;
        int z = unchecked(first.X * second.Y - second.X * first.Y) >> 12;
        return (x, y, z);
    }

    public static int Sqrt56BEC0(int value)
    {
        //A negative FSQRT reaches the original FISTP-qword indefinite result; its low DWORD is zero.
        return value < 0 ? 0 : (int)Math.Sqrt(value);
    }

    public static NativeBattleRayBomb10BeamGeometry Beam628B00(NativeBattleVector target, NativeBattleModuleAnchor muzzle, NativeBattleVector eye, short spin, short width, short counter)
    {
        int x = target.X - muzzle.X;
        int y = target.Y - muzzle.Y;
        int z = target.Z - muzzle.Z;
        int squared = NativeBattleSleepingGas168.NormalizeSquare(x, y, z);
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(0, spin, 0));
        direction = NativeBattleGfMatrices.RotateLong56C600(rotation, direction.X, direction.Y, direction.Z);
        NativeBattleModuleAxisAngle align = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
        NativeBattleActionMatrix basis = new(NativeBattleModuleAlignment.Rotation5714F0(align.Angle, align.Axis), muzzle.X, muzzle.Y, muzzle.Z, 4096);
        var down = NativeBattleGfMatrices.RotateLong56C600(basis, 0, -4096, 0);
        var look = NativeBattleActionGeometryRules.NormalizeVector56BC50(eye.X - muzzle.X, eye.Y - muzzle.Y, eye.Z - muzzle.Z);
        var axis = Cross56BBF0(direction, look);
        var second = Cross56BBF0(look, axis);
        align = NativeBattleModuleAlignment.Between571480(down.X, down.Y, down.Z, second.X, second.Y, second.Z);
        int dot = unchecked(direction.Z * align.Axis.Z + direction.Y * align.Axis.Y + direction.X * align.Axis.X);
        int angle = dot < 0 ? unchecked(-align.Angle) : align.Angle;
        NativeBattleActionMatrix roll = new(NativeBattleModuleAlignment.Rotation5714F0(angle, new(unchecked((short)direction.X), unchecked((short)direction.Y), unchecked((short)direction.Z))), 0, 0, 0, 4096);
        basis = basis with { Rotation = NativeBattleGfMatrices.Multiply56C090(roll, basis).Rotation };
        int length = Sqrt56BEC0(squared);
        NativeBattleModuleAnchor end = new(unchecked((short)(muzzle.X + (unchecked(direction.X * length) >> 12))), unchecked((short)(muzzle.Y + (unchecked(direction.Y * length) >> 12))), unchecked((short)(muzzle.Z + (unchecked(direction.Z * length) >> 12))), muzzle.W);
        int full = unchecked(length << 12) / 1046;
        int stretch = counter < 2 ? unchecked(full / 2 * counter) : full;
        basis = NativeBattlePoseMatrices.Scale56BEF0(basis, width, width, stretch);
        return new(basis, end, length, full);
    }
}
