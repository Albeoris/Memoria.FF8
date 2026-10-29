using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBlindDrawing
{
    public static NativeBattleBlindFrame Part(NativeBattleBlindFrame frame, NativeBattleBlindResources resources, NativeBattleGfAnimationNode node, NativeBattleVector anchor, NativeBattleBlindInputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return frame;
        }
        ImmutableArray<byte> data = resources.Layout.Data;
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(checked(8 + unchecked((short)node.GeometryIndex) * 4)));
        int count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(geometry + 4));
        if (count is < 0 or > 4096)
        {
            throw new InvalidDataException("Native Blind requires its bounded original vertex count.");
        }
        ImmutableArray<byte> selected;
        uint selectedAddress;
        if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            int offset = checked(geometry + 12 + shape * count * 8);
            selected = data.Slice(offset, checked(count * 8));
            selectedAddress = checked(resources.Layout.Address + (uint)offset);
        }
        else
        {
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Blind morph requires its current vertex workspace.");
            selected = NativeBattleGfMorphGeometry.Morph701390(data, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(0x18B4, checked(count * 8)).ToImmutableArray());
            for (int index = 0; index < count; index++)
            {
                arena = arena.Write(0x18B4 + index * 8, selected.AsSpan().Slice(index * 8, 6));
            }
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
            selectedAddress = 0x20DFAB8 + 0x18B4;
        }
        NativeBattleActionMatrix matrix = Matrix(node, anchor, inputs.Projection.View ?? throw new NotSupportedException("Native Blind requires its actual camera matrix."));
        NativeBattleFileStaging staging = frame.Staging;
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 0x58);
        staging = allocation.Staging;
        uint header = allocation.Address;
        Store(0, checked(resources.Layout.Address + (uint)geometry));
        Store(4, selectedAddress);
        Store(12, unchecked((uint)(int)node.Scalar36));
        Store(28, node.Scalar36 == 0 ? 0x2030u : 0x20F0u);
        if (node.Scalar36 != 0)
        {
            Store(8, node.Color32);
        }
        NativeBattlePilotVisualIdentity? identity = pilotIdentity is { } owner ? owner with { Primitive = unchecked(owner.Primitive * 397 + node.NodeIndex) } : null;
        NativeBattleGfGeometryBatch batch;
        if (node.GeometryIndex is 0 or 3)
        {
            batch = Additive(data, geometry, selected, matrix, inputs.Projection, node.Scalar36, node.Color32, inputs.Colors, identity);
        }
        else
        {
            uint uvOffset = BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(checked(header + 24), 4));
            batch = NativeBattleModulePackets.EmitBatch572200(data, geometry, selected, matrix, inputs.Projection, new(node.Scalar36 == 0 ? 0x2030u : 0x20F0u, node.Scalar36 == 0 ? null : node.Color32, node.Scalar36, null, null, uvOffset, 17, 3), inputs.Colors, pilotIdentity: identity);
        }
        if (node.Scalar36 != 0)
        {
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Blind requires its retained color registers.");
            NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
            frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers.PublishModelColor(node.Color32) } } };
        }
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 0x58);
        return inputs.Execute(frame with { Staging = staging }, new NativeBattleBlindPackets(batch));

        void Store(int offset, uint value)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            staging = staging.Write(checked(header + (uint)offset), bytes);
        }
    }

    public static NativeBattleActionMatrix Matrix(NativeBattleGfAnimationNode node, NativeBattleVector anchor, NativeBattleActionMatrix camera)
    {
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector offset = (node.Header & 0x800) != 0 ? NativeBattleGfMatrices.RotateShort56C4F0(matrix, node.Vector8) : node.Vector8;
        int x;
        int y;
        int z;
        if ((node.Header & 0x200) != 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(camera, anchor);
            x = unchecked(x + offset.X);
            y = unchecked(y + offset.Y);
            z = unchecked(z + offset.Z);
        }
        else
        {
            NativeBattleVector position = new(unchecked((short)(offset.X + anchor.X)), unchecked((short)(offset.Y + anchor.Y)), unchecked((short)(offset.Z + anchor.Z)));
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(camera, position);
            matrix = NativeBattleGfMatrices.Multiply56C090(camera, matrix);
        }
        matrix = matrix with { X = unchecked(x + camera.X), Y = unchecked(y + camera.Y), Z = unchecked(z + camera.Z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = (node.Header & 0x100) != 0
                ? matrix with { Rotation = NativeBattleGfMatrices.Multiply56C090(matrix, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)).Rotation }
                : NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        return matrix;
    }

    public static NativeBattleGfGeometryBatch Additive(ImmutableArray<byte> source, int geometry, ImmutableArray<byte> vertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, int fade, uint color, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return NativeBattleAdditiveQuadPackets.Emit(source, geometry, vertices, matrix, projection, fade, color, colors, true, 0, 3, pilotIdentity);
    }

}
