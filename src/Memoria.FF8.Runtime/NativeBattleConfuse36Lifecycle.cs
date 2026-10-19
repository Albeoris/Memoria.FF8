using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleConfuse36PresentationStep(NativeBattleConfuse36Frame Frame, NativeCrtRandomState Random, NativeBattleLogicalOrdering Ordering, NativeBattleGfGeometryBatch Batch);

/// <summary>The Confuse owner over the shared original birth/update/draw lifecycle.</summary>
public static class NativeBattleConfuse36Lifecycle
{
    public static NativeBattleConfuse36PresentationStep AdvanceAndDraw(NativeBattleConfuse36Frame frame, NativeBattleConfuse36Resources resources, uint contextTask, NativeCrtRandomState random, Func<int, NativeBattleGraphicsModel> readModel, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors? colors = null)
    {
        NativeBattleConfuse36NodeMemory memory = new(frame, contextTask);
        NativeBattleEffectNodeBank bank = new(0x15D97FC, frame.Memory.Confuse36Executable ?? throw new NotSupportedException("Native Confuse requires its executed original actor context initialization."), resources.ReadConstantChannel);
        NativeBattleEffectNodeInputs inputs = new(frame.State.CopiedCamera, readModel, AllocateEmitter, AllocateGeometry);
        NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, inputs);
        NativeBattleGfGeometryBatch batch = lifecycle.AdvanceAndDraw(ref random, projection, cameraRoll, PrepareMesh);
        NativeBattleConfuse36Frame result = memory.Flush();
        result = result with { Memory = result.Memory with { Confuse36Executable = bank.Data } };
        if (!batch.Attempts.IsEmpty)
        {
            int cursor = result.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native Confuse36 drawing requires its actual primary packet cursor.");
            NativeBattleEffectPacketWrite packets = NativeBattleEffectPacketArena.Append(result.Memory, cursor, ordering, batch);
            result = result with { Memory = packets.Memory, State = result.State with { PrimaryDrawCursor = packets.Cursor } };
            ordering = packets.Ordering;
        }
        return new(result, random, ordering, batch);

        int? AllocateEmitter(uint parent, sbyte source, sbyte target)
        {
            ushort cursor = memory.State.EmitterScanCursor ?? throw new NotSupportedException("Native Confuse36 emitter scan WORD is unknown.");
            int? node = NativeBattleEffectNodeAllocation.AllocateEmitter(memory, memory.State.Emitters!.Value, memory.Profile.EmitterBytes / 108, ref cursor, parent, source, target);
            memory.State = memory.State with { EmitterScanCursor = cursor };
            return node;
        }

        int? AllocateGeometry(int emitter, sbyte source)
        {
            ushort cursor = memory.State.GeometryScanCursor ?? throw new NotSupportedException("Native Confuse36 geometry scan WORD is unknown.");
            int? node = NativeBattleEffectNodeAllocation.AllocateGeometry(memory, memory.State.Geometry!.Value, memory.Profile.GeometryBytes / 672, ref cursor, emitter, source);
            memory.State = memory.State with { GeometryScanCursor = cursor };
            return node;
        }

        NativeBattleEffectNodeMeshInstance PrepareMesh(int node, byte[] source)
        {
            short sourceFlags = BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x3A));
            short fade = unchecked((short)memory.Word(node + 0x1CE));
            uint? farColor = fade == 0 ? null : memory.Dword(node + 0x16C);
            uint flags = sourceFlags == 0 ? 0x30u : 0;
            if (fade != 0)
            {
                flags |= 0xC0;
            }
            NativeBattleFullLifeMeshContext context = new(flags, farColor, fade == 0 ? null : fade, null, null, null, 17, 2);
            return (bytes, matrix) => NativeBattleRestorationMeshPackets.EmitBatch884370(bytes, matrix, projection, context, colors);
        }
    }
}
