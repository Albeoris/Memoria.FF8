using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDiablos325RawBodyDrawing
{
    public static NativeBattleDiablos325BodyDraw Draw6A79B0(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattleCameraProjection projection, bool hardware)
    {
        NativeBattleEffectArena source = memory.Arena ?? throw new NotSupportedException("Native Diablos ordinary body requires its current model arena.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        NativeBattleEffectArena arena = owned?.View ?? source;
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Diablos ordinary body requires its current prepared camera matrix.");
        int skeleton = NativeBattleWaterExecutionFields.ArenaOffset(task.ReadDword(0x244), 1);
        int count = arena.Read(skeleton, 1)[0];
        short[] rotation = Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(0x14C + index * 2))).ToArray();
        NativeBattleActionMatrix model = new(rotation.ToImmutableArray(), unchecked((int)task.ReadDword(0x160)), unchecked((int)task.ReadDword(0x164)), unchecked((int)task.ReadDword(0x168)), 4096);
        NativeBattleActionMatrix modelView = NativeBattlePoseMatrices.ComposeStageCamera([model], view, 0)[0];
        for (int index = 0; index < count; index++)
        {
            int address = checked(skeleton + 32 + index * 48);
            NativeBattleActionMatrix bone = NativeBattleArenaPose.ReadMatrix(arena, address);
            arena = NativeBattleArenaPose.WriteMatrix(arena, address, NativeBattlePoseMatrices.ComposeStageCamera([bone], modelView, 0)[0], true);
        }
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos ordinary body requires its retained renderer process.");
        var gate = process.Enter5099D0(hardware);
        process = gate.Process;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        if (gate.Draw)
        {
            uint rgb = task.ReadDword(0x134);
            uint mask = task.ReadDword(0x188);
            process = process.PublishModelColor(rgb);
            uint geometry = task.ReadDword(0x248);
            int length = checked((int)(task.ReadDword(0x24C) - geometry));
            ImmutableArray<byte> bytes = arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(geometry, length), length).ToImmutableArray();
            NativeBattleQuezacotlPathModelBatch batch = NativeBattleQuezacotlPathModelPackets.Emit6A79B0(bytes, selector => NativeBattleArenaPose.ReadMatrix(arena, checked(skeleton + 32 + selector * 48)), projection, rgb, mask);
            int scratch = NativeBattleWaterExecutionFields.ArenaOffset(checked(NativeBattleDiablos325Entry.RequireState(memory).ModelBuffer5208 + 0xD94), 0);
            foreach (NativeBattleGfGeometryAttempt write in batch.VertexScratchWrites)
            {
                arena = arena.Write(checked(scratch + write.ByteOffsetRelative), write.Data.AsSpan());
            }
            foreach (NativeBattleGeometryPrimitive primitive in batch.Primitives)
            {
                byte[] packet = new byte[primitive.Words.Length * 4];
                for (int index = 0; index < primitive.Words.Length; index++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(index * 4), primitive.Words[index]);
                }
                ImmutableArray<NativeBattleGfPacketByteState> stores = primitive.PhysicalWriteMask ?? throw new NotSupportedException("Native Diablos ordinary body requires exact raw-packet stores.");
                attempts.Add(new(allocated, packet.ToImmutableArray(), stores, primitive));
                allocated = checked(allocated + packet.Length);
            }
            if (batch.SourceEnd is int end)
            {
                uint cursor = checked((uint)(process.ByteCursor ?? throw new NotSupportedException("Native Diablos ordinary body requires its current COMMON cursor.")));
                process = process.Publish50FDF0(unchecked(cursor + (uint)allocated), unchecked(geometry + (uint)end));
            }
        }
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(arena, skeleton, count);
        arena = NativeBattleArenaPose.RestoreBoneMatrices(arena, skeleton, pose);
        return new(memory with { Arena = owned?.Freeze() ?? arena, PrimitiveProcess = process }, new(attempts.ToImmutable(), allocated));
    }
}
