using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect298Controller
{
    public static NativeBattleEffect298Frame DrawSmoke672C40(NativeBattleEffect298Frame frame, uint main, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180);
        foreach (int offset in new[] { 0x92, 0x90, 0x8E, 0x8A, 0x88, 0x86, 0x60, 0x58, 0x50, 0x48 })
        {
            context = context.Write(offset, new byte[2]);
        }
        frame = PublishContext(frame, temporary, context, 180);
        for (int index = 0; index < 128; index++)
        {
            uint row = main + 0x83EC + (uint)(index * 12);
            if (Word298(ReadBytes(frame, row, 12), 6) != 0)
            {
                frame = DrawSmoke672CD0(frame, row, temporary, resources, inputs);
            }
        }
        return Release(frame, 180);
    }

    public static NativeBattleEffect298Frame DrawSmoke672CD0(NativeBattleEffect298Frame frame, uint rowAddress, uint temporary, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        NativeBattleEffectArena row = ReadBytes(frame, rowAddress, 12);
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers298(frame), 0, Vector298(row), Word298(row, 6)));
        NativeBattleEffectArena context = Context(frame, temporary, 180);
        for (int axis = 0; axis < 3; axis++)
        {
            context = context.Write(0x98 + axis * 4, BitConverter.GetBytes(registers.Colors.Mac[axis] ?? throw new NotSupportedException("Effect298 smoke requires its current camera translation.")));
        }
        foreach (int offset in new[] { 0x94, 0x8C, 0x84 })
        {
            context = context.Write(offset, BitConverter.GetBytes((short)4096));
        }
        uint sourceAddress = row.Read(11, 1)[0] == 0 ? 0x104EA64u : 0x104E74Cu;
        NativeBattleModuleResource source = resources.ReadSprite(sourceAddress);
        int phases = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(8));
        int phase = phases - Word298(row, 6);
        int pointerOffset = checked(8 + phase * 2);
        if (pointerOffset < 0 || pointerOffset > source.Bytes.Length - 2)
        {
            throw new InvalidDataException("Effect298 reversed smoke phase leaves its actual source directory.");
        }
        int at = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(pointerOffset));
        if (at > source.Bytes.Length - 4)
        {
            throw new InvalidDataException("Effect298 smoke frame pointer leaves its actual source.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan().Slice(at));
        context = context.Write(0, BitConverter.GetBytes(sourceAddress)).Write(0x2C, BitConverter.GetBytes(checked(sourceAddress + (uint)at + 4))).Write(0x30, BitConverter.GetBytes(count));
        frame = PublishContext(Publish298(frame, registers), temporary, context, 180);
        frame = DrawSmokeQuads672DB0(frame, temporary, source, at + 4, count, inputs, inputs.CapturePresentation ? NativeBattleOffsetBodyVisuals.At(298, frame.Effect.Pools, rowAddress, sourceAddress) : null);
        row = ReadBytes(frame, rowAddress, 12);
        row = row.Write(6, BitConverter.GetBytes(unchecked((ushort)(Word298(row, 6) - 1))));
        for (int axis = 0; axis < 3; axis++)
        {
            int velocity = unchecked((sbyte)row.Read(8 + axis, 1)[0]);
            row = row.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(Word298(row, axis * 2) + (velocity << 2)))));
        }
        return WriteBytes(frame, rowAddress, row, 12);
    }

    public static NativeBattleEffect298Frame DrawSmokeQuads672DB0(NativeBattleEffect298Frame frame, uint temporary, NativeBattleModuleResource source, int firstQuad, int count, NativeBattleEffect298Inputs inputs)
    {
        return DrawSmokeQuads672DB0(frame, temporary, source, firstQuad, count, inputs, null);
    }

    public static NativeBattleEffect298Frame DrawSmokeQuads672DB0(NativeBattleEffect298Frame frame, uint temporary, NativeBattleModuleResource source, int firstQuad, int count, NativeBattleEffect298Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        if (count <= 0)
        {
            return frame;
        }
        NativeBattleProjectedQuads672DB0Step draw = NativeBattleProjectedQuads672DB0.DrawLoaded(frame.Memory, Context(frame, temporary, 180), source, firstQuad, count, inputs.ReadProjection(frame), pilotIdentity);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Context180, 180);
        return inputs.Execute(frame, new NativeBattleEffect298DeliverBatch(draw.Batch, NativeBattleEffect298Profile.CursorGlobal));
    }
}
