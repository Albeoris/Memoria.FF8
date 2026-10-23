using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBreath157Geometry
{
    public static NativeBattleBreath157Frame LoadPose(NativeBattleBreath157Frame frame, uint address)
    {
        (byte[] rotation, bool[] known) = frame.State.ReadView(0x23ECE20, 20).ReadSnapshot(0, 20);
        NativeBattleGeometryRegisters geometry = frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Breath requires its current geometry registers.");
        geometry = geometry with { Rotation20 = rotation.Select((value, index) => known[index] ? (byte?)value : null).ToImmutableArray() };
        (byte[] bytes, bool[] poseKnown) = frame.State.ReadView(address, 32).ReadSnapshot(0, 32);
        NativeBattleEffectMemory memory = NativeBattleLightBackTransform.LoadBone(frame.Memory with { GeometryRegisters = geometry }, new(bytes.ToImmutableArray(), poseKnown.ToImmutableArray()));
        return frame with { Memory = memory };
    }

    public static NativeBattleBreath157Frame TransformBillboard(NativeBattleBreath157Frame frame, NativeBattleEffectArena point8, short size, bool depthOffset)
    {
        NativeBattleGeometryRegisterStep step = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Breath requires its current geometry controls."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Breath requires its current arithmetic registers."));
        (byte[] bytes, bool[] known) = point8.ReadSnapshot(0, 8);
        NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(point8.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point8.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point8.Read(4, 2)));
        short? w = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : null;
        step = NativeBattleGeometry.LoadVector(step, 0, point, w);
        frame = frame with { Memory = NativeBattleLightBackTransform.Transform(frame.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors }, 0) };
        frame = Scale64DDD0(frame, size);
        NativeBattleScriptedColorRegisters colors = frame.Memory.ScriptedColorRegisters!;
        int[] mac = colors.Mac.Select(value => value ?? throw new NotSupportedException("Breath requires its actual transformed full MAC values.")).ToArray();
        for (int axis = 0; axis < 3; axis++)
        {
            frame = frame with { State = frame.State.WriteDword(0x23ECE34u + (uint)(axis * 4), unchecked((uint)mac[axis])) };
        }
        if (!depthOffset)
        {
            return Translation64DE90(frame);
        }
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(mac[0], mac[1], mac[2]);
        int[] normalized = [direction.X, direction.Y, direction.Z];
        int depth = -(size >> 3);
        for (int axis = 0; axis < 3; axis++)
        {
            mac[axis] = unchecked(mac[axis] + (unchecked(normalized[axis] * depth) >> 12));
            frame = frame with { State = frame.State.WriteDword(0x23ECE34u + (uint)(axis * 4), unchecked((uint)mac[axis])) };
        }
        return frame with { Memory = frame.Memory with { GeometryRegisters = frame.Memory.GeometryRegisters! with { Translation = mac.Select(value => (int?)value).ToImmutableArray() } } };
    }

    public static NativeBattleBreath157Frame Scale64DDD0(NativeBattleBreath157Frame frame, short scale)
    {
        NativeBattleGeometryRegisters geometry = frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Breath scale requires its current rotation controls.");
        byte?[] bytes = geometry.Rotation20.ToArray();
        byte[] value = BitConverter.GetBytes((uint)unchecked((ushort)scale));
        foreach (int offset in new[] { 0, 8, 16 })
        {
            for (int index = 0; index < 4; index++)
            {
                bytes[offset + index] = value[index];
            }
        }
        return frame with { Memory = frame.Memory with { GeometryRegisters = geometry with { Rotation20 = bytes.ToImmutableArray() } } };
    }

    public static NativeBattleBreath157Frame Translation64DE90(NativeBattleBreath157Frame frame)
    {
        NativeBattleGeometryRegisters geometry = frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Breath translation requires its current geometry controls.");
        NativeBattleScriptedColorRegisters colors = frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Breath translation requires its current IR registers.");
        return frame with { Memory = frame.Memory with { GeometryRegisters = geometry with { Translation = colors.Ir } } };
    }
}
