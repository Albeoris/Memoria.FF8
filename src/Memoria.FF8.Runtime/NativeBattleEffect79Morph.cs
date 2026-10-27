using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect79MorphStep(NativeBattleEffect79Frame Frame, ImmutableArray<byte> Bank);

public static class NativeBattleEffect79Morph
{
    public static NativeBattleEffect79MorphStep Execute7BA230(NativeBattleEffect79Frame frame, uint sourceAddress, ImmutableArray<byte> bank, uint mesh, uint firstShape, uint secondShape, uint ratioChannel)
    {
        if (firstShape == 0 || secondShape == 0)
        {
            return new(frame, bank);
        }
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        operation?.AdmitStaging(frame.Staging);
        NativeBattleNodeSource source = new(bank);
        using NativeBattleEffectNodeBank current = new(sourceAddress, source);
        NativeBattleEffect79Frame result = Execute7BA230(frame, current, mesh, firstShape, secondShape, ratioChannel);
        current.Publish();
        var completed = new NativeBattleEffect79MorphStep(result, System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(source.Read(0, source.Length)));
        return NativeBattleTaskOperation.CompleteStandalone(operation, completed);
    }

    internal static NativeBattleEffect79Frame Execute7BA230(NativeBattleEffect79Frame frame, NativeBattleEffectNodeBank bank, uint mesh, uint firstShape, uint secondShape, uint ratioChannel)
    {
        if (firstShape == 0 || secondShape == 0)
        {
            return frame;
        }
        NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Effect79 morph requires its current temporary owner."), 8);
        short index = BinaryPrimitives.ReadInt16LittleEndian(Read(secondShape + 0x1CA, 2));
        short ratio = BinaryPrimitives.ReadInt16LittleEndian(Read(unchecked(ratioChannel + (uint)(index * 2)), 2));
        int count = BinaryPrimitives.ReadInt32LittleEndian(Read(mesh + 4, 4));
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocated.Staging, allocated.Address, 8).Write(0, BitConverter.GetBytes(4096 - ratio)).Write(4, BitConverter.GetBytes((int)ratio));
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocated.Staging, allocated.Address, temporary, 8);
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect79 morph requires current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect79 morph requires current arithmetic registers."));
        for (int vertex = 0; vertex < count; vertex++)
        {
            uint offset = checked(8u + (uint)(vertex * 8));
            ReadOnlySpan<byte> a = Read(firstShape + offset, 6);
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - ratio)) } }, BinaryPrimitives.ReadUInt16LittleEndian(a), BinaryPrimitives.ReadUInt16LittleEndian(a[2..]), BinaryPrimitives.ReadUInt16LittleEndian(a[4..]));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, NativeBattleGteColorInterpolation.CodeConstants45F270);
            ReadOnlySpan<byte> b = Read(secondShape + offset, 6);
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(int)ratio) } }, BinaryPrimitives.ReadUInt16LittleEndian(b), BinaryPrimitives.ReadUInt16LittleEndian(b[2..]), BinaryPrimitives.ReadUInt16LittleEndian(b[4..]));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, NativeBattleGteColorInterpolation.CodeConstants45F270);
            int destination = Offset(mesh + offset, 6);
            for (int axis = 0; axis < 3; axis++)
            {
                bank.WriteUInt16(destination + axis * 2, unchecked((ushort)(short)registers.Colors.Ir[axis]!.Value));
            }
        }
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 8);
        frame = frame with { Staging = staging, Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        return frame;

        int Offset(uint address, int length)
        {
            long offset = (long)address - bank.Address;
            if (offset < 0 || length < 0 || offset > bank.Length - length)
            {
                throw new InvalidDataException("Effect79 morph read leaves its current addressed source bank.");
            }
            return (int)offset;
        }
        ReadOnlySpan<byte> Read(uint address, int length) => bank.Read(address, length);
    }
}
