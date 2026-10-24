using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect290Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect290Lower
{
    public static NativeBattleEffect290Frame DrawSmoke(NativeBattleEffect290Frame frame, uint main, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Put290(Context(frame, temporary, 180), 0, SmokeSourceAddress);
        foreach (int offset in new[] { 0x92, 0x90, 0x8E, 0x8A, 0x88, 0x86, 0x60, 0x58, 0x50, 0x48 })
        {
            context = PutWord290(context, offset, 0);
        }
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattleModuleResource source = resources.ReadProjectedQuads(SmokeSourceAddress);
        for (int index = 0; index < 128; index++)
        {
            uint address = checked(main + 0x5ED8 + (uint)(index * 12));
            NativeBattleEffectArena row = ReadBytes(frame, address, 12);
            int life = Word290(row, 6);
            if (life == 0)
            {
                continue;
            }
            frame = LoadMatrix(frame, inputs.ReadView(frame));
            NativeBattleGeometryRegisterStep step = NativeBattleGeometry.Transform(LoadPacked(Registers(frame), row, 0, 0));
            context = Context(frame, temporary, 180);
            for (int axis = 0; axis < 3; axis++)
            {
                context = Put290(context, 0x98 + axis * 4, unchecked((uint)step.Colors.Mac[axis]!.Value));
                context = PutWord290(context, 0x84 + axis * 8, 4096);
            }
            int directory = checked(2 * (17 - life));
            if (directory < 0 || directory > source.Bytes.Length - 2)
            {
                throw new InvalidDataException("The current smoke life leaves its source directory.");
            }
            int at = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(directory));
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan().Slice(at, 4));
            context = Put290(Put290(context, 0x2C, checked(source.Address + (uint)at + 4)), 0x30, unchecked((uint)count));
            NativeBattleProjectedQuads672DB0Step draw = NativeBattleProjectedQuads672DB0.DrawLoaded(Publish(frame, step).Memory, context, source, at + 4, count, inputs.ReadProjection(frame));
            frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Context180, 180);
            frame = inputs.Execute(frame, new NativeBattleEffect290DrawBatch(draw.Batch, CursorGlobal));
            row = ReadBytes(frame, address, 12);
            row = PutWord290(row, 6, Word290(row, 6) - 1);
            for (int axis = 0; axis < 3; axis++)
            {
                row = PutWord290(row, axis * 2, Word290(row, axis * 2) + (unchecked((sbyte)row.Read(8 + axis, 1)[0]) << 2));
            }
            frame = WriteBytes(frame, address, row, 12);
        }
        return Release(frame, 180);
    }
}
