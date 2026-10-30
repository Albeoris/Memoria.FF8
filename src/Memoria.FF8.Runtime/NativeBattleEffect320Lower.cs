using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect320Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect320Lower
{
    private const uint Cursor = 0x250534C;

    public static NativeBattleEffect320Step Advance(NativeBattleEffect320Frame frame, uint address, NativeBattleEffect320Resources resources, NativeBattleEffect320Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x65DA90)
        {
            throw new NotSupportedException("Effect320 requires its selected particle callback.");
        }
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena point = TaskView(task, 0x10, 8);
        (byte[] packed, bool[] known) = point.ReadSnapshot(6, 2);
        short scale = unchecked((short)task.ReadWord(0x16));
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Vector(point), known[0] && known[1] ? BinaryPrimitives.ReadInt16LittleEndian(packed) : null, scale, -256) };
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(0xFE7470u)).Write(4, BitConverter.GetBytes(task.ReadWord(0xC))).Write(0x24, BitConverter.GetBytes((ushort)0));
        NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(320, task) with { ResourceGeneration = 0xFE7470 } : null);
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect320DrawBatch(PreserveNativeDepths(drawn.Batch), Cursor));
        frame = Release(frame, 180);
        task = Read(frame, address);
        short phase = unchecked((short)(task.ReadWord(0xC) + 1));
        task = task.WriteWord(0xC, unchecked((ushort)phase));
        if (phase >= 16)
        {
            return new(Write(frame, address, task), 2);
        }
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(0x10 + axis * 2, unchecked((ushort)(task.ReadWord(0x10 + axis * 2) + task.ReadWord(0x18 + axis * 2))));
        }
        task = task.WriteWord(0x1A, unchecked((ushort)(task.ReadWord(0x1A) - 2)));
        return new(Write(frame, address, task), 0);
    }

    public static NativeBattleEffect320Frame Flash65D900(NativeBattleEffect320Frame frame, uint color, NativeBattleEffect320Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return inputs.Execute(frame, new NativeBattleEffect320DrawBatch(new([Flash(0, color, pilotIdentity)], 24), Cursor));
    }

    public static NativeBattleEffect320Frame Flash65D970(NativeBattleEffect320Frame frame, uint color, NativeBattleEffect320Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return inputs.Execute(frame, new NativeBattleEffect320DrawBatch(new([Mode(0, 8, 0xE10002A0, 8), Flash(8, color, pilotIdentity), Mode(32, 8, 0xE10002C0, 8)], 40), Cursor));
    }

    private static NativeBattleGfGeometryAttempt Flash(int offset, uint color, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        uint[] words = [0x05000000, (color & 0xFFFFFF) | 0x2A000000, 0, 320, 216u << 16, 320 | (216u << 16)];
        byte[] bytes = new byte[24];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        NativeBattleGeometryPrimitive primitive = new(8, words.ToImmutableArray(), [0, 0, 0, 0]);
        if (pilotIdentity is { } identity)
        {
            primitive = primitive with { PilotGeometry = new(identity, new(0, 0, 0), new(320, 0, 0), new(0, 216, 0), new(320, 216, 0), 4, false) { ScreenSpace = true, ScreenScale = 1, SourceDepths = System.Numerics.Vector4.Zero, UseSourceDepths = true } };
        }
        return new(offset, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 24).ToImmutableArray(), primitive);
    }

    private static NativeBattleGfGeometryAttempt Mode(int offset, int reserved, uint command, int bucket)
    {
        byte[] bytes = new byte[reserved];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x01000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), command);
        return new(offset, bytes.ToImmutableArray(), Enumerable.Range(0, reserved).Select(index => index < 8 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray(), new(bucket, [0x01000000, command], [0, 0, 0, 0]));
    }
    internal static NativeBattleGfGeometryBatch PreserveNativeDepths(NativeBattleGfGeometryBatch batch)
    {
        if (!batch.Attempts.Any(attempt => attempt.Primitive?.PilotGeometry is { Identity.Effect: 320, UseSourceDepths: false }))
        {
            return batch;
        }
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { PilotGeometry: { Identity.Effect: 320, UseSourceDepths: false } geometry } primitive && primitive.Words.Length > 2 ? attempt with { Primitive = primitive with { PilotGeometry = geometry with { SourceDepths = Payload(primitive), UseSourceDepths = true } } } : attempt).ToImmutableArray() };

        static System.Numerics.Vector4 Payload(NativeBattleGeometryPrimitive primitive)
        {
            return primitive.VertexPayload4.Length switch
            {
                0 => System.Numerics.Vector4.Zero,
                4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
                _ => throw new NotSupportedException("Effect320 capture requires its actual empty or four-lane native depth payload.")
            };
        }
    }

}
