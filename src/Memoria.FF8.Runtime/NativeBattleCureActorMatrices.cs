using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCureActorMatrices(NativeBattleActionMatrix MovedModelWorld, NativeBattleActionMatrix MainCamera, NativeBattleActionMatrix OverlayCamera);

public static partial class NativeBattlePoseMatrices
{
    /// <summary>8D7200/8D72C0/8DDA50's copied actor matrices and independently projected overlay basis.</summary>
    public static NativeBattleCureActorMatrices CureActorMatrices8D7110(NativeBattleActionMatrix originalModelWorld, NativeBattleActionMatrix camera, NativeBattleVector localOffset, short translationMode, NativeBattleVector effectCenter, int projectionDistance)
    {
        originalModelWorld.Validate();
        camera.Validate();
        Matrix world = new(NativeBattleTelemetry.Materialize(1122, originalModelWorld.Rotation.AsSpan()), originalModelWorld.X, originalModelWorld.Y, originalModelWorld.Z);
        NativeBattleActionMatrix movedAction = CureMovedModelWorld8D7200(originalModelWorld, localOffset, translationMode);
        Matrix main = Compose(new(NativeBattleTelemetry.Materialize(1123, camera.Rotation.AsSpan()), camera.X, camera.Y, camera.Z), world);
        return new(movedAction, ToActionMatrix(main), CureOverlayMatrix8D72C0(originalModelWorld, movedAction, effectCenter, projectionDistance));
    }

    /// <summary>8D72C0 consumes the actual original and moved actor matrices for its projected overlay basis.</summary>
    public static NativeBattleActionMatrix CureOverlayMatrix8D72C0(NativeBattleActionMatrix originalModelWorld, NativeBattleActionMatrix movedModelWorld, NativeBattleVector effectCenter, int projectionDistance)
    {
        originalModelWorld.Validate();
        movedModelWorld.Validate();
        Matrix world = new(NativeBattleTelemetry.Materialize(1124, originalModelWorld.Rotation.AsSpan()), originalModelWorld.X, originalModelWorld.Y, originalModelWorld.Z);
        Matrix moved = new(NativeBattleTelemetry.Materialize(1125, movedModelWorld.Rotation.AsSpan()), movedModelWorld.X, movedModelWorld.Y, movedModelWorld.Z);
        //8D72C0 copies only WORDs from the moved DWORD translation into its temporary origin.
        NativeBattleVector origin = new(unchecked((short)moved.X), unchecked((short)moved.Y), unchecked((short)moved.Z));
        NativeBattleVector direction = NormalizeLong56BD20(origin.X - effectCenter.X, origin.Y - effectCenter.Y, origin.Z - effectCenter.Z);
        //8DDA50(output,direction,up) computes (0,4096,0) cross direction, stores those rows, then transposes;
        //the following56C040 in8D72C0 transposes it back before the three GTE column transforms.
        Matrix basis = new([direction.Z, 0, unchecked((short)-direction.X), 0, 4096, 0, direction.X, direction.Y, direction.Z]);
        short[] rotation = new short[9];
        for (int column = 0; column < 3; column++)
        {
            (int x, int y, int z) = ApplyRounded(basis, world.Rotation[column], world.Rotation[3 + column], world.Rotation[6 + column]);
            rotation[column] = (short)Math.Clamp(x, short.MinValue, short.MaxValue);
            rotation[3 + column] = (short)Math.Clamp(y, short.MinValue, short.MaxValue);
            rotation[6 + column] = (short)Math.Clamp(z, short.MinValue, short.MaxValue);
        }
        //45E060 reads the low WORD of each DWORD difference before460840 publishes MAC values.
        short dx = unchecked((short)(world.X - origin.X));
        short dy = unchecked((short)(world.Y - origin.Y));
        short dz = unchecked((short)(world.Z - origin.Z));
        (int tx, int ty, int tz) = ApplyRounded(basis, dx, dy, dz);
        Matrix overlay = new(rotation, tx, ty, unchecked(tz + projectionDistance));
        return ToActionMatrix(overlay);
    }

    /// <summary>8D7200's copied world matrix and optional local or world translation.</summary>
    public static NativeBattleActionMatrix CureMovedModelWorld8D7200(NativeBattleActionMatrix originalModelWorld, NativeBattleVector localOffset, short translationMode)
    {
        originalModelWorld.Validate();
        Matrix world = new(NativeBattleTelemetry.Materialize(1126, originalModelWorld.Rotation.AsSpan()), originalModelWorld.X, originalModelWorld.Y, originalModelWorld.Z);
        Matrix moved = world;
        if (translationMode == 0)
        {
            (int x, int y, int z) = ApplyRounded(world, localOffset.X, localOffset.Y, localOffset.Z);
            moved = world with { X = unchecked(world.X + (short)x), Y = unchecked(world.Y + (short)y), Z = unchecked(world.Z + (short)z) };
        }
        else if (translationMode == 1)
        {
            moved = world with { X = unchecked(world.X + localOffset.X), Y = unchecked(world.Y + localOffset.Y), Z = unchecked(world.Z + localOffset.Z) };
        }
        return ToActionMatrix(moved);
    }

    /// <summary>56BD20 retains X as an x87 value but stores normalized Y/Z through single-precision locals.</summary>
    public static NativeBattleVector NormalizeLong56BD20(int x, int y, int z)
    {
        float fy = y;
        float fz = z;
        double length = Math.Sqrt((double)fz * fz + (double)fy * fy + (double)x * x);
        double nx = x;
        if (length != 0)
        {
            double scale = 4096d / length;
            nx *= scale;
            fy = (float)(fy * scale);
            fz = (float)(fz * scale);
        }
        return new(unchecked((short)ConvertRounded(nx)), unchecked((short)ConvertRounded(fy)), unchecked((short)ConvertRounded(fz)));
    }

    private static NativeBattleActionMatrix ToActionMatrix(Matrix matrix) => new(NativeBattleTelemetry.Immutable(1127, matrix.Rotation), matrix.X, matrix.Y, matrix.Z, 4096);
}
