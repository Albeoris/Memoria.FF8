using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleAuraDrawing
{
    public static NativeBattleAuraFrame Part(NativeBattleAuraFrame frame, NativeBattleAuraResources resources, NativeBattleGfAnimationNode node, NativeBattleActionMatrix placement, NativeBattleAuraInputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleVector scale = node.Vector24;
        if (scale is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return frame;
        }
        ImmutableArray<byte> data = resources.Layout.Data;
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(checked(8 + unchecked((short)node.GeometryIndex) * 4)));
        int count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(geometry + 4));
        if (count is < 0 or > 4096)
        {
            throw new InvalidDataException("Native Aura requires its bounded original geometry.");
        }
        ImmutableArray<byte> selected;
        if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            selected = data.AsSpan().Slice(checked(geometry + 12 + shape * count * 8), checked(count * 8)).ToArray().ToImmutableArray();
        }
        else
        {
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Aura morph requires its current arena bytes.");
            ImmutableArray<byte> retained = arena.Read(0xE20, count * 8).ToImmutableArray();
            selected = NativeBattleGfMorphGeometry.Morph701390(data, geometry, node.Word40, node.Word42, node.Scalar38, retained);
            for (int index = 0; index < count; index++)
            {
                arena = arena.Write(0xE20 + index * 8, selected.AsSpan().Slice(index * 8, 6));
            }
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
        }
        uint flags = resources.PartFlags[node.NodeIndex];
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(node.Vector16) with { X = node.Vector8.X, Y = node.Vector8.Y, Z = node.Vector8.Z };
        NativeBattleActionMatrix matrix;
        if ((flags & 0x10000000) != 0)
        {
            (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(placement, local.X, local.Y, local.Z);
            matrix = local with { X = unchecked(x + placement.X), Y = unchecked(y + placement.Y), Z = unchecked(z + placement.Z) };
        }
        else
        {
            matrix = NativeBattleGfMatrices.Compose56C2F0(placement, local);
        }
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        uint mode = (flags & 0xDFFF) | 0x2000;
        if (node.Scalar36 != 0)
        {
            mode |= 0xC0;
            frame = PublishFar(frame, node.Color32);
        }
        NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(data, geometry, selected, matrix, inputs.Projection, new(mode, node.Scalar36 == 0 ? null : node.Color32, node.Scalar36, null, null, null, 17, 2), inputs.Colors, pilotIdentity: pilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 397 + node.NodeIndex) } : null);
        return inputs.Execute(frame, new NativeBattleAuraPackets(batch));
    }

    public static NativeBattleGfGeometryBatch Flame(NativeBattleModulePoolTask task, NativeBattleAuraResources resources, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        int age = Word(task, 12);
        int strength = Word(task, 28);
        int fade = age < 8 ? 4096 - (strength * (age * 4096 / 8) >> 12) : age > 12 ? 4096 - (strength * (4096 - ((age - 12) * 4096 / 8)) >> 12) : 4096 - strength;
        int radius = Word(task, 22);
        short sine = unchecked((short)(NativeBattleCameraMath.Sine(Word(task, 24)) * radius >> 12));
        short cosine = unchecked((short)(NativeBattleCameraMath.Cosine(Word(task, 24)) * radius >> 12));
        NativeBattleActionMatrix camera = projection.View ?? throw new NotSupportedException("Native Aura flame requires its published camera matrix.");
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(camera, new(Word(task, 16), Word(task, 18), Word(task, 20)));
        NativeBattleActionMatrix matrix = new([cosine, unchecked((short)-sine), 0, sine, cosine, 0, 0, 0, 4096], unchecked(x + camera.X), unchecked(y + camera.Y), unchecked(z + camera.Z - 256), 4096);
        return NativeBattleModulePackets.EmitBatch572200(resources.Flame.Bytes, 0, default, matrix, projection, new(0xF3, 0, fade, null, null, null, 17, 2), colors, pilotIdentity: pilotIdentity);
    }

    public static NativeBattleAuraFrame PublishFar(NativeBattleAuraFrame frame, uint color)
    {
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Aura requires its retained color registers.");
        NativeBattleModelRendererRegisters registers = process.ModelRendererRegisters ?? new(null, null, null, null, null);
        return frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = registers.PublishModelColor(color) } } };
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
