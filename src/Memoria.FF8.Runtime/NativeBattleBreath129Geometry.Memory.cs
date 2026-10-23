using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBreath129Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBreath129Geometry
{
    private static NativeBattleEffectArena Get(NativeBattleBreath129Frame frame, uint address, int length) => address >= NativeBattleTemporaryMemory.Address && (ulong)address + (uint)length <= NativeBattleTemporaryMemory.Address + NativeBattleTemporaryMemory.ReachedLength ? Context(frame, address, length) : ReadBytes(frame, address, length);
    private static NativeBattleBreath129Frame Put(NativeBattleBreath129Frame frame, uint address, NativeBattleEffectArena bytes, int length) => address >= NativeBattleTemporaryMemory.Address && (ulong)address + (uint)length <= NativeBattleTemporaryMemory.Address + NativeBattleTemporaryMemory.ReachedLength ? PublishContext(frame, address, bytes, length) : WriteBytes(frame, address, bytes, length);
    private static short Word(NativeBattleBreath129Frame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(Get(frame, address, 2).Read(0, 2));
    private static uint Dword(NativeBattleBreath129Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Get(frame, address, 4).Read(0, 4));
    private static NativeBattleBreath129Frame Word(NativeBattleBreath129Frame frame, uint address, int value) => Put(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((short)value))), 2);
    private static NativeBattleBreath129Frame Dword(NativeBattleBreath129Frame frame, uint address, uint value) => Put(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 4);
    private static NativeBattleVector Vector(NativeBattleEffectArena point) => new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
    private static NativeBattleEffectArena Point(NativeBattleVector vector) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(vector.X)).Write(2, BitConverter.GetBytes(vector.Y)).Write(4, BitConverter.GetBytes(vector.Z));
    private static NativeBattleGeometryRegisterStep Registers(NativeBattleBreath129Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Breath geometry requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Breath geometry requires its current arithmetic registers."));
    private static NativeBattleBreath129Frame Publish(NativeBattleBreath129Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    private static short? W(NativeBattleEffectArena point)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }

    private static NativeBattleBreath129Frame LoadRotation(NativeBattleBreath129Frame frame, NativeBattleEffectArena packed)
    {
        (byte[] bytes, bool[] known) = packed.ReadSnapshot(0, 20);
        return frame with { Memory = frame.Memory with { GeometryRegisters = Registers(frame).Geometry with { Rotation20 = bytes.Select((value, index) => known[index] ? (byte?)value : null).ToImmutableArray() } } };
    }

    private static NativeBattleBreath129Frame Load(NativeBattleBreath129Frame frame, NativeBattlePackedMatrix32 packed) => NativeBattleBreath129Controller.LoadMatrix(frame, packed);
    private static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena arena)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleBreath129Frame Project(NativeBattleBreath129Frame frame, NativeBattleEffectArena point, NativeBattleBreath129Inputs inputs)
    {
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector(point), W(point));
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Breath geometry requires its current projection controls.");
        return Publish(frame, NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb));
    }

    private static NativeBattleBreath129Frame Append(NativeBattleBreath129Frame frame, NativeBattleEffectArena packet, int length, int bucket, NativeBattleBreath129Inputs inputs)
    {
        uint address = frame.State.ReadDword(0x252117C);
        (byte[] bytes, bool[] known) = packet.ReadSnapshot(0, length);
        NativeBattleGfPacketByteState[] mask = Enumerable.Range(0, length).Select(index => index is 38 or 39 or 50 or 51 ? NativeBattleGfPacketByteState.Untouched : known[index] ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Unknown).ToArray();
        ImmutableArray<int> depths = Registers(frame).Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Breath packet insertion requires all four actual retained SZ values.")))).ToImmutableArray();
        frame = frame with { State = frame.State.WriteDword(0x252117C, checked(address + (uint)length)) };
        return inputs.Execute(frame, new NativeBattleBreath129AppendPacket(address, length, length, bucket, 0, bytes, known) { WriteMask = mask, VertexPayload4 = depths });
    }
}
