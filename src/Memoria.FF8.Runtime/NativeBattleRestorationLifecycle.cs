using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRestorationPresentationStep(NativeBattleRestorationFrame Frame, NativeCrtRandomState Random, NativeBattleLogicalOrdering Ordering, NativeBattleGfGeometryBatch Batch);

/// <summary>The typed Esuna/Full-life owner over the single normalized birth/update/draw lifecycle.</summary>
public static class NativeBattleRestorationLifecycle
{
    public static NativeBattleRestorationPresentationStep AdvanceAndDraw(NativeBattleRestorationFrame frame, uint contextTask, NativeCrtRandomState random, Func<int, NativeBattleGraphicsModel> readModel, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors? colors = null)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        using NativeBattleLogicalOrderingTransaction? orderingOperation = NativeBattleTaskOperation.ProtectOrdering(ordering);
        ordering = orderingOperation?.View ?? ordering;
        using NativeBattleRestorationNodeMemory memory = new(frame, contextTask);
        NativeBattleRestorationExecutableState retained = frame.Memory.GetRestorationExecutable(memory.Profile.Effect) ?? throw new NotSupportedException("Native restoration node lifecycle requires its own retained executable bank.");
        retained.Validate();
        NativeBattleNodeSource nodeSource = retained.Data ?? throw new NotSupportedException("Native restoration node lifecycle requires its executed context resource initialization.");
        NativeBattleTaskOperation.AdmitCurrentRetainedArena(nodeSource.Arena);
        using NativeBattleEffectNodeBank bank = new(memory.Profile.ExecutableAddress, nodeSource);
        NativeBattleEffectNodeInputs inputs = new(frame.State.CopiedCamera, readModel, AllocateEmitter, AllocateGeometry);
        NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, inputs);
        NativeBattlePilotVisualIdentity? identity = memory.Profile.Effect == 24 ? NativeBattlePilotVisualLifetimes.Identity(24, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(contextTask)) : null;
        NativeBattleGfGeometryBatch batch = lifecycle.AdvanceAndDraw(ref random, projection, cameraRoll, PrepareMesh, pilotIdentity: identity, preparePilotMesh: identity.HasValue ? PreparePilotMesh : null);
        NativeBattleRestorationFrame result = memory.Flush();
        bank.Publish();
        NativeBattleRestorationExecutableState written = retained with { Data = bank.Source };
        result = result with { Memory = result.Memory.WithRestorationExecutable(written) };
        if (!batch.Attempts.IsEmpty)
        {
            int cursor = result.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native restoration drawing requires its actual primary packet cursor.");
            NativeBattleEffectPacketWrite packets = NativeBattleEffectPacketArena.Append(result.Memory, cursor, ordering, batch);
            result = result with { Memory = packets.Memory, State = result.State with { PrimaryDrawCursor = packets.Cursor } };
            ordering = packets.Ordering;
        }
        return NativeBattleTaskOperation.CompleteStandalone<NativeBattleRestorationPresentationStep>(operation, new(result, random, NativeBattleTaskOperation.CompleteOrdering(orderingOperation, ordering), batch));

        int? AllocateEmitter(uint parent, sbyte source, sbyte target)
        {
            ushort cursor = memory.State.EmitterScanCursor ?? throw new NotSupportedException("Native restoration emitter scan WORD is unknown.");
            int? node = NativeBattleEffectNodeAllocation.AllocateEmitter(memory, memory.State.Emitters!.Value, memory.Profile.EmitterBytes / 108, ref cursor, parent, source, target);
            memory.State = memory.State with { EmitterScanCursor = cursor };
            return node;
        }

        int? AllocateGeometry(int emitter, sbyte source)
        {
            ushort cursor = memory.State.GeometryScanCursor ?? throw new NotSupportedException("Native restoration geometry scan WORD is unknown.");
            int? node = NativeBattleEffectNodeAllocation.AllocateGeometry(memory, memory.State.Geometry!.Value, memory.Profile.GeometryBytes / 672, ref cursor, emitter, source);
            memory.State = memory.State with { GeometryScanCursor = cursor };
            return node;
        }

        NativeBattleEffectNodeMeshInstance PrepareMesh(int node, byte[] source)
        {
            NativeBattleEffectNodePilotMeshInstance prepared = PreparePilotMesh(node, source);
            return (bytes, matrix) => prepared(bytes, matrix, default);
        }

        NativeBattleEffectNodePilotMeshInstance PreparePilotMesh(int node, byte[] source)
        {
            short sourceFlags = BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x3A));
            short fade = unchecked((short)memory.Word(node + 0x1CE));
            uint? farColor = fade == 0 ? null : memory.Dword(node + 0x16C);
            if (memory.Profile.UsesEsunaMesh)
            {
                sbyte sourceIndex = unchecked((sbyte)memory.Byte(node + 0x1D6));
                NativeBattleEsunaMeshOperands prepared = NativeBattleRestorationMeshPackets.Prepare88E090(memory.ContextWord(0x2A), sourceIndex, sourceFlags, fade, farColor);
                if (sourceIndex is 1 or 2)
                {
                    memory.ContextWord(0x2A, prepared.NextUvCounter);
                }
                return (bytes, matrix, owner) => NativeBattleRestorationMeshPackets.EmitBatch88E240(bytes, matrix, projection, prepared.Context, colors, pilotIdentity: identity.HasValue ? owner : null);
            }
            uint flags = sourceFlags == 0 ? 0x30u : 0;
            if (fade != 0)
            {
                flags |= 0xC0;
            }
            NativeBattleFullLifeMeshContext context = new(flags, farColor, fade == 0 ? null : fade, null, null, null, 17, 2);
            return (bytes, matrix, _) => NativeBattleRestorationMeshPackets.EmitBatch884370(bytes, matrix, projection, context, colors);
        }
    }
}
