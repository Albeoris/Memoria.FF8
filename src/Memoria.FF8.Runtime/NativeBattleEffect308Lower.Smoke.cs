using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect308Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect308Lower
{
    public static NativeBattleEffect308Frame DrawSmoke669620(NativeBattleEffect308Frame frame, uint main, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180);
        foreach (int offset in new[] { 0x92, 0x90, 0x8E, 0x8A, 0x88, 0x86, 0x60, 0x58, 0x50, 0x48 })
        {
            context = PutWord308(context, offset, 0);
        }
        frame = PublishContext(frame, temporary, context, 180);
        for (int index = 0; index < 128; index++)
        {
            uint row = checked(main + 0x890 + (uint)(index * 12));
            if (Word308(ReadBytes(frame, row, 12), 6) != 0)
            {
                frame = DrawSmoke6696B0(frame, row, temporary, resources, inputs);
            }
        }
        return Release(frame, 180);
    }

    public static NativeBattleEffect308Frame DrawSmoke6696B0(NativeBattleEffect308Frame frame, uint rowAddress, uint temporary, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        NativeBattleEffectArena row = ReadBytes(frame, rowAddress, 12);
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(LoadPacked(Registers(frame), row, 0, 0));
        NativeBattleEffectArena context = Context(frame, temporary, 180);
        for (int axis = 0; axis < 3; axis++)
        {
            context = Put308(context, 0x98 + axis * 4, unchecked((uint)(registers.Colors.Mac[axis] ?? throw new NotSupportedException("Effect308 smoke requires its current transformed point."))));
        }
        foreach (int offset in new[] { 0x94, 0x8C, 0x84 })
        {
            context = PutWord308(context, offset, 4096);
        }
        uint sourceAddress = row.Read(11, 1)[0] == 0 ? 0x1026038u : 0x1025D20u;
        NativeBattleModuleResource source = resources.ReadProjectedQuads(sourceAddress);
        int phase = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(8)) - Word308(row, 6);
        int pointer = checked(8 + phase * 2);
        if (pointer < 0 || pointer > source.Bytes.Length - 2)
        {
            throw new InvalidDataException("Effect308 smoke phase leaves its selected reverse directory.");
        }
        int at = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(pointer));
        if (at > source.Bytes.Length - 4)
        {
            throw new InvalidDataException("Effect308 smoke frame leaves its actual source.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan().Slice(at));
        context = Put308(Put308(Put308(context, 0, sourceAddress), 0x2C, checked(sourceAddress + (uint)at + 4)), 0x30, unchecked((uint)count));
        frame = PublishContext(Publish(frame, registers), temporary, context, 180);
        NativeBattleProjectedQuads672DB0Step draw = NativeBattleProjectedQuads672DB0.DrawLoaded(frame.Memory, context, source, at + 4, count, inputs.ReadProjection(frame), inputs.CapturePresentation ? NativeBattleOffsetBodyVisuals.At(308, frame.Effect.Pools, rowAddress, source.Address) : null);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Context180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect308DrawBatch(draw.Batch, CursorGlobal));
        row = ReadBytes(frame, rowAddress, 12);
        row = PutWord308(row, 6, Word308(row, 6) - 1);
        for (int axis = 0; axis < 3; axis++)
        {
            row = PutWord308(row, axis * 2, Word308(row, axis * 2) + (unchecked((sbyte)row.Read(8 + axis, 1)[0]) << 2));
        }
        return WriteBytes(frame, rowAddress, row, 12);
    }
}
