using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrinkMagic37OrbWaveStep(NativeBattleEffectArena Mesh, NativeBattleGeometryRegisterStep Registers);

public static class NativeBattleDrinkMagic37OrbWave
{
    public static NativeBattleDrinkMagic37OrbWaveStep Transform(ImmutableArray<byte> shape, NativeBattleEffectArena mesh, short phase, short rotation, NativeBattleGeometryRegisterStep registers, short? outputScratchW = null)
    {
        ValidateMesh(mesh);
        if (shape.IsDefault || shape.Length != NativeBattleDrinkMagic37Profile.ShapeLength || BinaryPrimitives.ReadInt32LittleEndian(shape.AsSpan().Slice(4)) != 276)
        {
            throw new InvalidDataException("Drink Magic wave requires its original separate vertex-only source.");
        }
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.RotateZ8DD960(NativeBattleCuragaMatrices.Identity8DD770(), rotation);
        for (int vertex = 0; vertex < 276; vertex++)
        {
            int offset = 8 + vertex * 8;
            ReadOnlySpan<byte> source = shape.AsSpan().Slice(offset, 8);
            short x = BinaryPrimitives.ReadInt16LittleEndian(source);
            short y = BinaryPrimitives.ReadInt16LittleEndian(source[2..]);
            short z = BinaryPrimitives.ReadInt16LittleEndian(source[4..]);
            short w = BinaryPrimitives.ReadInt16LittleEndian(source[6..]);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, false, null) };
            registers = NativeBattleGeometry.LoadVector(registers, 0, new(x, y, z), w);
            registers = NativeBattleGeometry.Transform(registers, false);
            int waveX = unchecked(NativeBattleCameraMath.Sine((short)((z + phase) & 0xFFF)) * unchecked((short)(unchecked(z << 9) / 4096))) / 4096;
            int waveY = unchecked(NativeBattleCameraMath.Sine((short)((z / 2 & 0xFFF) / 2)) * unchecked((short)(unchecked(3 * z << 10) / 4096))) / 4096;
            byte[] output = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(output, unchecked((short)(registers.Colors.Ir[0]!.Value + waveX)));
            BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(2), unchecked((short)(registers.Colors.Ir[1]!.Value - waveY)));
            BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(4), unchecked((short)registers.Colors.Ir[2]!.Value));
            mesh = mesh.Write(offset, output);
            if (outputScratchW is { } knownW)
            {
                byte[] packedW = new byte[2];
                BinaryPrimitives.WriteInt16LittleEndian(packedW, knownW);
                mesh = mesh.Write(offset + 6, packedW);
            }
            else
            {
                mesh = mesh.MarkUnknown(offset + 6, 2);
            }
        }
        return new(mesh, registers);
    }

    public static ImmutableArray<short> Bounds(NativeBattleEffectArena mesh)
    {
        ValidateMesh(mesh);
        short[] bounds = [0x7000, 0x7000, 0x7000, unchecked((short)0x9000), unchecked((short)0x9000), unchecked((short)0x9000)];
        for (int vertex = 0; vertex < 276; vertex++)
        {
            ReadOnlySpan<byte> point = mesh.Read(8 + vertex * 8, 6);
            for (int axis = 0; axis < 3; axis++)
            {
                short value = BinaryPrimitives.ReadInt16LittleEndian(point[(axis * 2)..]);
                bounds[axis] = Math.Min(bounds[axis], value);
                bounds[axis + 3] = Math.Max(bounds[axis + 3], value);
            }
        }
        return bounds.ToImmutableArray();
    }

    private static void ValidateMesh(NativeBattleEffectArena mesh)
    {
        mesh.Validate();
        if (mesh.ExceedsExtent(NativeBattleDrinkMagic37Profile.OrbLength) || BinaryPrimitives.ReadUInt32LittleEndian(mesh.Read(4, 4)) != 276)
        {
            throw new InvalidDataException("Drink Magic wave requires its retained addressed orb mesh.");
        }
    }
}
