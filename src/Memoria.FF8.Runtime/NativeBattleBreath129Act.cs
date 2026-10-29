using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleBreath129Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBreath129Act
{
    public static NativeBattleBreath129Frame Draw6B57B0(NativeBattleBreath129Frame frame, NativeBattleGfAnimationNode node, NativeBattleGfAnimationSource source, NativeBattleBreath129ActContext context, NativeBattleBreath129Inputs inputs)
    {
        NativeBattleVector scale = node.Vector24;
        if (scale is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return frame;
        }
        (frame, uint temporary) = Allocate(frame, 88);
        int geometryOffset = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(checked(8 + unchecked((short)node.GeometryIndex) * 4), 4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(checked(geometryOffset + 4), 4));
        uint geometry = checked(source.Address + (uint)geometryOffset);
        int shape = node.Word40;
        uint selected;
        if (node.Word40 == node.Word42 || node.Scalar38 == 0 || node.Scalar38 == 4096)
        {
            shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            selected = unchecked(geometry + 12u + (uint)(count * shape * 8));
        }
        else
        {
            int extent = checked(count * 8);
            NativeBattleEffectArena destination = ReadBytes(frame, context.MorphAddress, extent);
            NativeBattleGeometryRegisterStep morph = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Native Breath morph requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Breath morph requires its current data registers."));
            for (int index = 0; index < count; index++)
            {
                int first = checked(geometryOffset + 12 + (count * node.Word40 + index) * 8);
                int second = checked(geometryOffset + 12 + (count * node.Word42 + index) * 8);
                morph = morph with { Colors = morph.Colors with { Ir0 = unchecked((uint)(4096 - node.Scalar38)), Ir = [SourceWord(first), SourceWord(first + 2), SourceWord(first + 4)] } };
                morph = NativeBattleRaldo19BallRendering.Weighted(morph, false, inputs.Colors);
                morph = morph with { Colors = morph.Colors with { Ir0 = unchecked((uint)(int)node.Scalar38), Ir = [SourceWord(second), SourceWord(second + 2), SourceWord(second + 4)] } };
                morph = NativeBattleRaldo19BallRendering.Weighted(morph, true, inputs.Colors);
                for (int lane = 0; lane < 3; lane++)
                {
                    destination = destination.Write(index * 8 + lane * 2, BitConverter.GetBytes(unchecked((short)morph.Colors.Ir[lane]!.Value)));
                }
            }
            frame = WriteBytes(frame, context.MorphAddress, destination, extent);
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = morph.Geometry, ScriptedColorRegisters = morph.Colors } };
            selected = context.MorphAddress;

            int SourceWord(int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Data.AsSpan().Slice(offset, 2));
        }
        NativeBattleActionMatrix local = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleActionMatrix basis = context.Matrix.ReadMatrix();
        int x = node.Vector8.X;
        int y = node.Vector8.Y;
        int z = node.Vector8.Z;
        if ((node.Header & 0x200) == 0)
        {
            NativeBattleGeometryRegisters geometryRegisters = frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Native Breath ACT requires its current geometry registers.");
            geometryRegisters = NativeBattleGeometry.LoadMatrix(geometryRegisters, basis, true, false, null);
            NativeBattleGeometryRegisterStep step = new(geometryRegisters, frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Breath ACT requires its current data registers."));
            step = NativeBattleGeometry.LoadVector(step, 0, node.Vector8, null);
            step = NativeBattleGeometry.Transform(step, false);
            x = step.Colors.Mac[0]!.Value;
            y = step.Colors.Mac[1]!.Value;
            z = step.Colors.Mac[2]!.Value;
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors } };
            local = NativeBattleGfMatrices.Multiply56C090(basis, local);
        }
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            local = (node.Header & 0x100) != 0 ? NativeBattleGfMatrices.Multiply56C090(local, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)) : NativeBattlePoseMatrices.Scale56BEF0(local, scale.X, scale.Y, scale.Z);
        }
        local = local with { X = unchecked(x + basis.X), Y = unchecked(y + basis.Y), Z = unchecked(z + basis.Z) };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(local, null));
        NativeBattleEffectArena draw = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(geometry)).Write(4, BitConverter.GetBytes(selected)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(24, BitConverter.GetBytes(context.DepthBias)).Write(28, BitConverter.GetBytes(node.Scalar36 != 0 ? 0x20F0u : 0x2030u));
        if (node.Scalar36 != 0)
        {
            draw = draw.Write(8, BitConverter.GetBytes(node.Color32));
        }
        frame = PublishContext(frame, temporary, draw, 88);
        frame = inputs.Execute(frame, new NativeBattleBreath129DrawDefaultMesh88(temporary, geometry) { PilotIdentity = inputs.PilotIdentity });
        return Release(frame, 88);
    }
}
