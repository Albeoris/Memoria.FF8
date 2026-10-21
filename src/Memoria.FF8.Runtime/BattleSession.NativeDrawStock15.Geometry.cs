using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffectMemory ExecuteNativeDrawStock15Geometry(NativeBattleEffectMemory memory, NativeBattleDrawStock15ParticleOperation operation, uint? temporary)
    {
        NativeBattleGeometryRegisterStep step = new(memory.GeometryRegisters ?? throw new NotSupportedException("Draw Stock requires its retained geometry controls."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Draw Stock requires its retained geometry data registers."));
        switch (operation)
        {
            case NativeBattleDrawStock15Matrix matrix:
                step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, matrix.Matrix, matrix.Rotation, matrix.Translation, Padding(matrix.Padding18, matrix.TemporaryMatrixOffset)) };
                break;
            case NativeBattleDrawStock15MatrixProduct product:
                step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, product.Left, true, false, Padding(product.Padding18, product.TemporaryLeftMatrixOffset)) };
                for (int column = 0; column < 3; column++)
                {
                    step = NativeBattleGeometry.LoadIrColumn(step, unchecked((ushort)product.Right.Rotation[column]), unchecked((ushort)product.Right.Rotation[column + 3]), unchecked((ushort)product.Right.Rotation[column + 6]));
                    step = NativeBattleGeometry.Transform(step, false, true);
                }
                break;
            case NativeBattleDrawStock15LongTransform transform:
                step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, transform.Matrix, transform.LoadRotation, true, Padding(transform.Padding18, transform.TemporaryMatrixOffset)) };
                step = NativeBattleGeometry.LoadVector(step, 0, transform.Point, transform.Point.Z < 0 ? (short)-1 : (short)0);
                step = NativeBattleGeometry.Transform(step);
                break;
            case NativeBattleDrawStock15Projection projection:
                NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Draw Stock requires its current geometry projection controls.");
                bool triple = projection.Points.Length == 3;
                if (!projection.Ws.IsDefault && projection.Ws.Length != projection.Points.Length)
                {
                    throw new InvalidDataException("Draw Stock requires matching packed projection input lanes.");
                }
                for (int index = 0; index < projection.Points.Length; index++)
                {
                    short? w = projection.TemporaryVectorOffset is { } offset ? ReadWord(offset + index * 8 + 6) : projection.Ws.IsDefault ? null : projection.Ws[index];
                    step = NativeBattleGeometry.LoadVector(step, triple ? index : 0, projection.Points[index], w);
                    if (!triple)
                    {
                        step = NativeBattleGeometry.ProjectSingle(step, projection.Projection, controls.Dqa, controls.Dqb);
                    }
                }
                if (triple)
                {
                    step = NativeBattleGeometry.ProjectTriple(step, projection.Projection, controls.Dqa, controls.Dqb);
                    if (temporary is { } address)
                    {
                        byte[] flags = new byte[4];
                        BinaryPrimitives.WriteUInt32LittleEndian(flags, step.Colors.Flags ?? throw new InvalidDataException("Draw Stock projection did not publish its flags."));
                        NativeFileStaging = (NativeFileStaging ?? throw new NotSupportedException("Draw Stock projection requires its actual temporary frame.")).Write(address + 64, flags);
                    }
                }
                break;
            case NativeBattleDrawStock15AverageDepth:
                step = NativeBattleGeometry.Average45E610(step, memory.ScriptedGraphics?.Zsf4 ?? throw new NotSupportedException("Draw Stock requires its current ZSF4."));
                break;
            default:
                throw new NotSupportedException($"Draw Stock particle operation {operation.GetType().Name} requires its native producer.");
        }
        return memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors };

        short? Padding(ushort? value, int? offset) => offset is { } matrixOffset ? ReadWord(matrixOffset + 18) : value is { } word ? unchecked((short)word) : null;

        short? ReadWord(int offset)
        {
            if (offset < 0 || offset > 178)
            {
                throw new InvalidDataException("Draw Stock operand leaves its original sprite frame.");
            }
            NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Draw Stock requires its retained physical temporary memory.");
            (byte[] bytes, bool[] known) = staging.ReadSnapshot(checked((temporary ?? throw new NotSupportedException("Draw Stock requires its active sprite allocation.")) + (uint)offset), 2);
            return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        }
    }
}
