using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffectMemory ObserveNativeBoneAnchor(NativeBattleEffectMemory memory, NativeBattleBoneAnchorProducer producer)
    {
        if (!producer.Enabled)
        {
            return memory;
        }
        NativeBattleActionMatrix matrix = producer.Matrix!;
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("The Aqua anchor requires its retained physical temporary memory."), 52);
        NativeBattleFileStaging staging = temporary.Staging;
        byte[] rotation = new byte[18];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(index * 2), matrix.Rotation[index]);
        }
        staging = staging.Write(temporary.Address, rotation);
        staging = staging.MarkUnknown(temporary.Address + 18, 2);
        byte[] translation = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(translation, matrix.X);
        BinaryPrimitives.WriteInt32LittleEndian(translation.AsSpan(4), matrix.Y);
        BinaryPrimitives.WriteInt32LittleEndian(translation.AsSpan(8), matrix.Z);
        staging = staging.Write(temporary.Address + 20, translation);
        (byte[] paddingBytes, bool[] paddingKnown) = staging.ReadSnapshot(temporary.Address + 18, 2);
        short? padding = paddingKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(paddingBytes) : null;
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadAnchorScratchZ(memory.GeometryRegisters ?? throw new NotSupportedException("The Aqua anchor requires its actual retained scratch vector."), producer.Distance);
        geometry = NativeBattleGeometry.LoadMatrix(geometry, matrix, true, true, padding);
        NativeBattleGeometryRegisterStep step = NativeBattleGeometry.LoadAnchorVector(new(geometry, memory.ScriptedColorRegisters ?? throw new NotSupportedException("The Aqua anchor requires its actual retained input registers.")));
        step = NativeBattleGeometry.Transform(step);
        byte[] outputs = new byte[12];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(outputs.AsSpan(index * 4), step.Colors.Mac[index] ?? throw new InvalidDataException("The Aqua anchor did not publish its MAC output."));
        }
        staging = staging.Write(temporary.Address + 32, outputs);
        byte[] flags = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(flags, step.Colors.Flags ?? throw new InvalidDataException("The Aqua anchor did not publish its flags."));
        staging = staging.Write(temporary.Address + 48, flags);
        NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(staging, 52);
        return memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors };
    }

    private NativeBattleGeometryRegisterStep ObserveNativeGeometryRotation(NativeBattleGeometryRegisterStep step, NativeBattleActionMatrix matrix, char axisName, short angle, ushort? paddingWord)
    {
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging ?? throw new NotSupportedException("Native geometry rotation requires its retained physical temporary memory."), 36);
        NativeFileStaging = temporary.Staging;
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleActionMatrix axis = axisName switch
        {
            'X' => NativeBattleCuragaMatrices.RotateX8DD7E0(identity, angle),
            'Y' => NativeBattleCuragaMatrices.RotateY8DD8A0(identity, angle),
            'Z' => NativeBattleCuragaMatrices.RotateZ8DD960(identity, angle),
            _ => throw new InvalidDataException("The Aqua rotation requires its native axis.")
        };
        byte[] rotation = new byte[18];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(index * 2), axis.Rotation[index]);
        }
        NativeFileStaging = NativeFileStaging.Write(temporary.Address, rotation);
        byte[] trigonometry = new byte[4];
        BinaryPrimitives.WriteInt16LittleEndian(trigonometry, unchecked((short)NativeBattleCameraMath.Sine(angle)));
        BinaryPrimitives.WriteInt16LittleEndian(trigonometry.AsSpan(2), unchecked((short)NativeBattleCuragaMatrices.Cosine8DDRotation(angle)));
        NativeFileStaging = NativeFileStaging.Write(temporary.Address + 32, trigonometry);
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, matrix, true, false, paddingWord is { } padding ? unchecked((short)padding) : null) };
        for (int column = 0; column < 3; column++)
        {
            step = NativeBattleGeometry.LoadIrColumn(step, unchecked((ushort)axis.Rotation[column]), unchecked((ushort)axis.Rotation[column + 3]), unchecked((ushort)axis.Rotation[column + 6]));
            step = NativeBattleGeometry.Transform(step, false, true);
        }
        NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(NativeFileStaging, 36);
        return step;
    }

    private NativeBattleAquaFrame ExecuteNativeAquaGeometry(NativeBattleAquaFrame frame, NativeBattleAquaOperation operation, Func<int, short?> readTemporaryWord)
    {
        NativeBattleGeometryRegisterStep step = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("The Aqua module requires its retained geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("The Aqua module requires its retained color registers."));
        switch (operation)
        {
            case NativeBattleAquaMatrix matrix:
                step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, matrix.Matrix, matrix.Rotation, matrix.Translation, Padding(matrix.Padding18, matrix.TemporaryMatrixOffset)) };
                break;
            case NativeBattleAquaMatrixProduct product:
                step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, product.Left, true, false, Padding(product.Padding18, product.TemporaryLeftMatrixOffset)) };
                Columns(product.Right);
                break;
            case NativeBattleAquaLongTransform transform:
                step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, transform.Matrix, transform.LoadRotation, true, Padding(transform.Padding18, transform.TemporaryMatrixOffset)) };
                step = NativeBattleGeometry.LoadVector(step, 0, transform.Point, transform.Point.Z < 0 ? (short)-1 : (short)0);
                step = NativeBattleGeometry.Transform(step);
                break;
            case NativeBattleAquaTransform transform:
                Transform(transform.Matrix, transform.Point, transform.Translation, transform.Padding18, transform.W);
                break;
            case NativeBattleAquaTransforms transforms:
                if (!transforms.Ws.IsDefault && transforms.Ws.Length != transforms.Points.Length)
                {
                    throw new InvalidDataException("The Aqua transformed vectors require matching packed input lanes.");
                }
                for (int index = 0; index < transforms.Points.Length; index++)
                {
                    Transform(transforms.Matrix, transforms.Points[index], transforms.Translation, transforms.Padding18, transforms.Ws.IsDefault ? null : transforms.Ws[index]);
                }
                break;
            case NativeBattleAquaRotate rotate:
                step = ObserveNativeGeometryRotation(step, rotate.Matrix, rotate.Axis, rotate.Angle, rotate.Padding18);
                break;
            case NativeBattleAquaProjection projection:
                if (projection.Points.Length is not (1 or 3) || (!projection.Ws.IsDefault && projection.Ws.Length != projection.Points.Length))
                {
                    throw new InvalidDataException("The Aqua projection requires its reached single or triple packed input.");
                }
                for (int index = 0; index < projection.Points.Length; index++)
                {
                    short? w = projection.TemporaryVectorOffset is { } offset ? readTemporaryWord(offset + index * 8 + 6) : projection.Ws.IsDefault ? null : projection.Ws[index];
                    step = NativeBattleGeometry.LoadVector(step, index, projection.Points[index], w);
                }
                NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("The Aqua projection requires its current geometry controls.");
                step = projection.Points.Length == 3 ? NativeBattleGeometry.ProjectTriple(step, projection.Projection, controls.Dqa, controls.Dqb) : NativeBattleGeometry.ProjectSingle(step, projection.Projection, controls.Dqa, controls.Dqb);
                break;
            case NativeBattleAquaClip:
                step = NativeBattleGeometry.Clip45EE10(step);
                break;
            case NativeBattleAquaAverageDepth:
                step = NativeBattleGeometry.Average45E610(step, frame.Memory.ScriptedGraphics?.Zsf4 ?? throw new NotSupportedException("The Aqua depth average requires its current ZSF4."));
                break;
            case NativeBattleAquaFarColor far:
                NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("The Aqua far color requires its canonical renderer owner.");
                frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process.PublishModelColor(far.Color) } };
                break;
            case NativeBattleAquaColor color:
                NativeBattleModelRendererRegisters distant = frame.Memory.PrimitiveProcess?.ModelRendererRegisters ?? throw new NotSupportedException("The Aqua color instruction requires its actual far controls.");
                NativeBattleSummonColors constants = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("The Aqua color instruction requires its original constants.");
                foreach (uint material in color.Materials)
                {
                    step = step with { Colors = NativeBattleScriptedDpcs.Transform45F270(step.Colors, material, unchecked((uint)color.Cue), distant, constants).Registers };
                }
                break;
            default:
                throw new NotSupportedException($"The Aqua geometry operation {operation.GetType().Name} requires its native producer.");
        }
        return frame with { Memory = frame.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors } };

        short? Padding(ushort? value, int? temporaryOffset) => temporaryOffset is { } offset ? readTemporaryWord(offset + 18) : value is { } word ? unchecked((short)word) : null;

        void Transform(NativeBattleActionMatrix matrix, NativeBattleVector point, bool translation, ushort? padding, short? w)
        {
            step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, matrix, true, false, padding is { } word ? unchecked((short)word) : null) };
            step = NativeBattleGeometry.LoadVector(step, 0, point, w);
            step = NativeBattleGeometry.Transform(step, translation);
        }

        void Columns(NativeBattleActionMatrix right)
        {
            for (int column = 0; column < 3; column++)
            {
                step = NativeBattleGeometry.LoadIrColumn(step, unchecked((ushort)right.Rotation[column]), unchecked((ushort)right.Rotation[column + 3]), unchecked((ushort)right.Rotation[column + 6]));
                step = NativeBattleGeometry.Transform(step, false, true);
            }
        }

    }
}
