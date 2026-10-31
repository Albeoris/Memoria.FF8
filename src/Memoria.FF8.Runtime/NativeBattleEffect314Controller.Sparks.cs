using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect314Controller
{
    public static NativeBattleEffect314Frame DrawSparks665160(NativeBattleEffect314Frame frame, uint main, NativeBattleEffect314Resources resources, NativeBattleEffect314Inputs inputs)
    {
        NativeBattleEffect314AddressStep allocation = inputs.AllocateTemporary(frame, 0xB4);
        frame = allocation.Frame;
        uint temporary = allocation.Address;
        NativeBattleEffectArena context = inputs.ReadArena(frame, temporary, 0xB4);
        foreach (int offset in new[] { 0x92, 0x90, 0x8E, 0x8A, 0x88, 0x86, 0x60, 0x58, 0x50, 0x48 })
        {
            context = context.Write(offset, new byte[2]);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect314WriteArena(temporary, context, 0xB4));
        for (int index = 0; index < SparkCapacity; index++)
        {
            uint address = checked(main + SparkOffset + (uint)(index * SparkStride));
            if (I16(ReadBytes(frame, address + 6, 2), 0) != 0)
            {
                frame = DrawSpark6651F0(frame, address, temporary, resources, inputs);
            }
        }
        return inputs.ReleaseTemporary(frame, 0xB4);
    }

    public static NativeBattleEffect314Frame DrawSpark6651F0(NativeBattleEffect314Frame frame, uint address, uint temporary, NativeBattleEffect314Resources resources, NativeBattleEffect314Inputs inputs)
    {
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        NativeBattleEffectArena row = ReadBytes(frame, address, SparkStride);
        short life = I16(row, 6);
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, new(I16(row, 0), I16(row, 2), I16(row, 4)), life));
        frame = Publish(frame, registers);
        NativeBattleEffectArena context = inputs.ReadArena(frame, temporary, 0xB4);
        for (int axis = 0; axis < 3; axis++)
        {
            context = context.Write(0x98 + axis * 4, BitConverter.GetBytes(registers.Colors.Mac[axis] ?? throw new NotSupportedException("Effect314 spark requires current transformed MAC coordinates.")));
            context = context.Write(0x84 + axis * 8, BitConverter.GetBytes(I16(row, 8)));
        }
        int kind = unchecked((sbyte)row.Read(15, 1)[0]);
        NativeBattleModuleResource source = resources.ReadProjectedQuads(kind);
        int frames = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan(8, 2));
        int directory = checked(8 + 2 * (frames - life));
        if (life <= 0 || (uint)kind >= QuadMaximumLife.Length || life > QuadMaximumLife[kind] || directory < 0 || directory > source.Bytes.Length - 2)
        {
            throw new InvalidDataException("Effect314 spark leaves its actual source/life directory domain.");
        }
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan(directory, 2));
        if (offset > source.Bytes.Length - 4)
        {
            throw new InvalidDataException("Effect314 spark frame leaves its original projected source.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan(offset, 4));
        context = context.Write(0, BitConverter.GetBytes(source.Address)).Write(0x2C, BitConverter.GetBytes(checked(source.Address + (uint)offset + 4))).Write(0x30, BitConverter.GetBytes(count));
        frame = inputs.Execute(frame, new NativeBattleEffect314WriteArena(temporary, context, 0xB4));
        NativeBattleEffect318RasterStep draw = NativeBattleEffect318Raster.DrawLoaded(frame.Memory, context, source, inputs.ReadProjection(frame), NativeBattleEffect32Presentation.Resource(NativeBattleEffect32Presentation.TaskInstance(frame.PilotIdentity, frame.Effect, frame.State.ReadDword(WorkGlobal) + MainOffset, frame.State.ReadDword(WorkGlobal) + MainOffset + SparkOffset, checked((int)((address - frame.State.ReadDword(WorkGlobal) - MainOffset - SparkOffset) / SparkStride))), source.Address));
        frame = inputs.Execute(frame with { Memory = draw.Memory }, new NativeBattleEffect314WriteArena(temporary, draw.Temporary180, 0xB4));
        frame = inputs.Execute(frame, new NativeBattleEffect314DrawBatch(draw.Batch, CursorGlobal));
        row = ReadBytes(frame, address, SparkStride).Write(6, BitConverter.GetBytes(unchecked((short)(life - 1))));
        for (int axis = 0; axis < 3; axis++)
        {
            int velocity = unchecked((sbyte)row.Read(12 + axis, 1)[0]);
            row = row.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(I16(row, axis * 2) + (velocity << 2)))));
        }
        return WriteBytes(frame, address, row, SparkStride);
    }
}
