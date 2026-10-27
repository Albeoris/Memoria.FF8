using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRecoveryPresentationStep(NativeBattleRecoveryFrame Frame, NativeCrtRandomState Random, NativeBattleLogicalOrdering Ordering, NativeBattleGfGeometryBatch Batch);

/// <summary>The typed Mega-Potion/Elixir/Megalixir owner over the single normalized birth/update/draw lifecycle.</summary>
public static class NativeBattleRecoveryLifecycle
{
    public static NativeBattleRecoveryPresentationStep AdvanceAndDraw(NativeBattleRecoveryFrame frame, uint contextTask, NativeCrtRandomState random, Func<int, NativeBattleGraphicsModel> readModel, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors? colors = null)
    {
        using NativeBattleRecoveryNodeMemory memory = new(frame, contextTask);
        NativeBattleRecoveryExecutableState retained = frame.Memory.GetRecoveryExecutable(memory.Profile.Effect) ?? throw new NotSupportedException("Native recovery node lifecycle requires its own retained executable bank.");
        retained.Validate();
        NativeBattleEffectNodeBank bank = new(memory.Profile.ExecutableAddress, retained.Data, retained.Read);
        NativeBattleEffectNodeInputs inputs = new(frame.State.CopiedCamera, readModel, AllocateEmitter, AllocateGeometry);
        NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, inputs);
        NativeBattleGfGeometryBatch batch = lifecycle.AdvanceAndDraw(ref random, projection, cameraRoll, PrepareMesh);
        NativeBattleRecoveryFrame result = memory.Flush();
        NativeBattleRecoveryExecutableState written = retained with { Data = bank.Data };
        result = result with { Memory = result.Memory.WithRecoveryExecutable(written) };
        if (!batch.Attempts.IsEmpty)
        {
            int cursor = result.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native recovery drawing requires its actual primary packet cursor.");
            NativeBattleEffectPacketWrite packets = NativeBattleEffectPacketArena.Append(result.Memory, cursor, ordering, batch);
            result = result with { Memory = packets.Memory, State = result.State with { PrimaryDrawCursor = packets.Cursor } };
            ordering = packets.Ordering;
        }
        return new(result, random, ordering, batch);

        int? AllocateEmitter(uint parent, sbyte source, sbyte target)
        {
            ushort cursor = memory.State.EmitterScanCursor ?? throw new NotSupportedException("Native recovery emitter scan WORD is unknown.");
            int? node = NativeBattleEffectNodeAllocation.AllocateEmitter(memory, memory.State.Emitters!.Value, memory.Profile.EmitterBytes / 108, ref cursor, parent, source, target);
            memory.State = memory.State with { EmitterScanCursor = cursor };
            return node;
        }

        int? AllocateGeometry(int emitter, sbyte source)
        {
            ushort cursor = memory.State.GeometryScanCursor ?? throw new NotSupportedException("Native recovery geometry scan WORD is unknown.");
            int? node = NativeBattleEffectNodeAllocation.AllocateGeometry(memory, memory.State.Geometry!.Value, memory.Profile.GeometryBytes / 672, ref cursor, emitter, source);
            memory.State = memory.State with { GeometryScanCursor = cursor };
            return node;
        }

        NativeBattleEffectNodeMeshInstance PrepareMesh(int node, byte[] source)
        {
            short sourceFlags = BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x3A));
            short fade = unchecked((short)memory.Word(node + 0x1CE));
            uint? farColor = fade == 0 ? null : memory.Dword(node + 0x16C);
            if (memory.Profile.Effect == 7)
            {
                sbyte sourceIndex = unchecked((sbyte)memory.Byte(node + 0x1D6));
                NativeBattleEsunaMeshOperands prepared = NativeBattleRecoveryMeshPackets.Prepare8CF4C0(memory.ContextWord(0x2A), sourceIndex, sourceFlags, fade, farColor);
                if (sourceIndex == 0)
                {
                    memory.ContextWord(0x2A, prepared.NextUvCounter);
                }
                return (bytes, matrix) => NativeBattleRestorationMeshPackets.EmitBatch88E240(bytes, matrix, projection, prepared.Context, colors);
            }
            uint flags = sourceFlags == 0 ? 0x30u : 0;
            if (fade != 0)
            {
                flags |= 0xC0;
            }
            NativeBattleFullLifeMeshContext context = new(flags, farColor, fade == 0 ? null : fade, null, null, null, 17, 2);
            return (bytes, matrix) => NativeBattleRecoveryMeshPackets.EmitBatch8131A0(bytes, matrix, projection, context, colors);
        }
    }
}
