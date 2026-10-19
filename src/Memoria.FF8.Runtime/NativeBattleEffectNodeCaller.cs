using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleEffectNodeCallerPolicy(uint SourceAddress, int SourceLength, uint Callback, int EmitterCapacity, int GeometryCapacity, bool FullLife, int WorkOffset = 0x10000, bool CommonSprites = false, bool AllowMode3 = false, bool PrivateMeshes = false)
{
    internal int EmitterBytes => checked(EmitterCapacity * 0x6C);
    internal int GeometryBytes => checked(GeometryCapacity * 0x2A0);
}
internal static class NativeBattleEffectNodeCaller
{
    public static NativeBattleRecover93NodeFrame ClearStorage(NativeBattleEffectNodeCallerPolicy profile, NativeBattleRecover93NodeFrame frame, int initializerTimArenaOffset)
    {
        frame.State.Validate(profile.SourceLength, profile.EmitterBytes, profile.GeometryBytes);
        int first = checked(initializerTimArenaOffset + profile.WorkOffset);
        int second = checked(first + profile.EmitterBytes);
        if (initializerTimArenaOffset < 0 || first > NativeBattleEffectArena.Length - profile.EmitterBytes - profile.GeometryBytes)
        {
            throw new NotSupportedException("Native Recover's actual initializer tail does not fit its1MB arena owner.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Recover nodes require the executed571B80 arena resource owner.");
        arena = arena.Write(first, new byte[profile.EmitterBytes]).Write(second, new byte[profile.GeometryBytes]);
        return frame with { Memory = frame.Memory with { Arena = arena }, State = frame.State with { Emitters = first, Geometry = second, EmitterScanCursor = 0, GeometryScanCursor = 0, Word2594BB0 = 0, Word2592550 = 0 } };
    }

    public static NativeBattleRecover93NodeFrame BeginPass(NativeBattleEffectNodeCallerPolicy profile, NativeBattleRecover93NodeFrame frame, NativeBattleActionMatrix copiedCamera, int privateDrawCursor)
    {
        copiedCamera.Validate();
        if ((uint)privateDrawCursor >= NativeBattleEffectArena.Length)
        {
            throw new InvalidDataException("Native Recover requires its actual private sprite packet cursor.");
        }
        return frame with { State = frame.State with { PassEmitterCount = 0, PassGeometryCount = 0, CopiedCamera = copiedCamera, PrivateDrawCursor = privateDrawCursor } };
    }

    public static NativeBattleRecover93NodeFrame Initialize74EC00(NativeBattleEffectNodeCallerPolicy profile, NativeBattleRecover93NodeFrame frame, uint taskAddress, NativeBattleRecover93NodeResource resource, NativeBattleActionPacketsState packets, Func<int, NativeBattleGraphicsModel> readModel, Func<int, NativeBattlePoseSource> readPoseSource, NativeBattleEffectNodeDogOrigins? dogOrigins = null)
    {
        frame.State.Validate(profile.SourceLength, profile.EmitterBytes, profile.GeometryBytes);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(taskAddress);
        if (task.Callback != profile.Callback || task.ReadDword(0x30) != profile.SourceAddress)
        {
            throw new InvalidDataException("Native Recover context has a different callback or source owner.");
        }
        if (task.ReadWord(0x29C) != 0 && !(profile.AllowMode3 && task.ReadWord(0x29C) == 3) && (task.ReadWord(0x29C) != 2 || dogOrigins is null))
        {
            throw new NotSupportedException("Native Recover's first proved node context requires mode0; Dog mode2 is not supplied.");
        }
        if (resource.Data.IsDefault || resource.Data.Length != profile.SourceLength || BinaryPrimitives.ReadUInt32LittleEndian(resource.Data.AsSpan()) != 0)
        {
            throw new InvalidDataException("Native Recover requires its complete authenticated cold node resource.");
        }
        NativeBattleRecover93NodeState retained = frame.State;
        if (retained.InitializationFlag == 0)
        {
            byte[] fixedBytes = NativeBattleEffectNodeBankFixups.Relocate(retained.Data ?? resource.Data, profile.SourceAddress);
            BinaryPrimitives.WriteUInt32LittleEndian(fixedBytes, 1);
            retained = retained with { InitializationFlag = 1, Data = fixedBytes.ToImmutableArray() };
        }
        task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Native Recover requires its actual action parameters."), packets, readModel, readPoseSource, profile.SourceAddress, dogOrigins: dogOrigins);
        return frame with { State = retained, Effect = frame.Effect with { Pools = hierarchy.Write(taskAddress, task).Pools } };
    }

    public static NativeBattleRecover93NodeFrame InitializeCurrent(NativeBattleEffectNodeCallerPolicy profile, NativeBattleRecover93NodeFrame frame, uint taskAddress, NativeBattleActionPacketsState packets, Func<int, NativeBattleGraphicsModel> readModel, Func<int, NativeBattlePoseSource> readPoseSource)
    {
        frame.State.Validate(profile.SourceLength, profile.EmitterBytes, profile.GeometryBytes);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(taskAddress);
        if (task.Callback != profile.Callback || task.ReadDword(0x30) != profile.SourceAddress)
        {
            throw new InvalidDataException("Native node context has a different callback or current source owner.");
        }
        ushort mode = task.ReadWord(0x29C);
        if (mode != 0 && !(profile.AllowMode3 && mode == 3))
        {
            throw new NotSupportedException("Native node current-source initialization requires a closed placement mode.");
        }
        ImmutableArray<byte> current = frame.State.Data ?? throw new NotSupportedException("Native node initialization requires its actual mutable source bytes.");
        uint flag = BinaryPrimitives.ReadUInt32LittleEndian(current.AsSpan());
        if (flag != frame.State.InitializationFlag)
        {
            throw new InvalidDataException("Native node relocation flag differs from its current source header.");
        }
        if (flag == 0)
        {
            byte[] relocated = NativeBattleEffectNodeBankFixups.Relocate(current, profile.SourceAddress);
            BinaryPrimitives.WriteUInt32LittleEndian(relocated, 1);
            frame = frame with { State = frame.State with { InitializationFlag = 1, Data = relocated.ToImmutableArray() } };
        }
        task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Native nodes require their actual action parameters."), packets, readModel, readPoseSource, profile.SourceAddress);
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(taskAddress, task).Pools } };
    }

    public static NativeBattleRecover93NodeStep Advance74F6E0(NativeBattleEffectNodeCallerPolicy profile, NativeBattleRecover93NodeFrame frame, uint taskAddress, Func<int, NativeBattleGraphicsModel> readModel, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors colors, Func<NativeBattleEffectNodeBank, uint, int, byte, ImmutableArray<byte>>? resourceUpdate = null, Func<NativeBattleEffectNodeMemory, int, byte[], NativeBattleEffectNodeMeshInstance>? prepareMeshOverride = null, NativeBattleEffectNodeSpawnOrigin? spawnOrigin = null)
    {
        frame.State.Validate(profile.SourceLength, profile.EmitterBytes, profile.GeometryBytes);
        if (new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(taskAddress).Callback != profile.Callback)
        {
            throw new InvalidDataException("Native Recover node advancement requires its original context callback.");
        }
        List<NativeBattleGfGeometryBatch> privateSprites = [];
        List<NativeBattleGfGeometryBatch> commonMeshes = [];
        var context = NativeBattleEffectNodeLifecycle.AdvanceContext(ReadTask, WriteTask, () =>
        {
            int first = frame.State.Emitters ?? throw new NotSupportedException("Native Recover emitter storage has not been cleared.");
            int second = frame.State.Geometry ?? throw new NotSupportedException("Native Recover geometry storage has not been cleared.");
            NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, taskAddress, first, profile.EmitterBytes, second, profile.GeometryBytes, useSharedScratch: true);
            NativeBattleEffectNodeBank bank = new(profile.SourceAddress, frame.State.Data ?? throw new NotSupportedException("Native Recover has no fixed mutable node bank."));
            ushort emitterCursor = frame.State.EmitterScanCursor ?? throw new NotSupportedException("Native Recover emitter scan WORD is unknown.");
            ushort geometryCursor = frame.State.GeometryScanCursor ?? throw new NotSupportedException("Native Recover geometry scan WORD is unknown.");
            NativeBattleEffectNodeInputs inputs = new(frame.State.CopiedCamera ?? throw new NotSupportedException("Native Recover copied-camera owner is missing."), readModel, (parent, source, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, first, profile.EmitterCapacity, ref emitterCursor, parent, source, target), (emitter, source) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, second, profile.GeometryCapacity, ref geometryCursor, emitter, source));
            inputs = inputs with { ResourceUpdate = resourceUpdate, SpawnOrigin = spawnOrigin };
            NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, inputs);
            List<(bool Mesh, NativeBattleGfGeometryBatch Batch)> deliveries = [];
            NativeCrtRandomState random = frame.Random;
            _ = lifecycle.AdvanceAndDraw(ref random, projection, cameraRoll, PrepareMesh, (mesh, batch) => deliveries.Add((mesh, batch)));
            NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
            frame = frame with { Memory = written.Memory, Effect = written.Effect, Random = random, State = frame.State with { Data = bank.Data, EmitterScanCursor = emitterCursor, GeometryScanCursor = geometryCursor } };
            foreach ((bool mesh, NativeBattleGfGeometryBatch batch) in deliveries)
            {
                if (mesh && !profile.PrivateMeshes || !mesh && profile.CommonSprites)
                {
                    commonMeshes.Add(batch);
                    NativeBattleCommonPacketWrite packets = NativeBattleCommonPacketArena.Append(frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native nodes require their actual common packet owner."), ordering, batch);
                    frame = frame with { Memory = frame.Memory with { PrimitiveProcess = packets.Process } };
                    ordering = packets.Ordering;
                }
                else
                {
                    privateSprites.Add(batch);
                    NativeBattleEffectPacketWrite packets = NativeBattleEffectPacketArena.Append(frame.Memory, frame.State.PrivateDrawCursor ?? throw new NotSupportedException("Native Recover sprites require their current private cursor."), ordering, batch);
                    frame = frame with { Memory = packets.Memory, State = frame.State with { PrivateDrawCursor = packets.Cursor } };
                    ordering = packets.Ordering;
                }
            }

            NativeBattleEffectNodeMeshInstance PrepareMesh(int node, byte[] source)
            {
                if (prepareMeshOverride is not null)
                {
                    return prepareMeshOverride(memory, node, source);
                }
                short sourceFlags = BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(0x3A));
                short fade = unchecked((short)memory.Word(node + 0x1CE));
                uint flags = sourceFlags == 0 ? 0x30u : 0;
                if (fade != 0)
                {
                    flags |= 0xC0;
                }
                if (profile.FullLife)
                {
                    NativeBattleFullLifeMeshContext fullLife = new(flags, fade == 0 ? null : memory.Dword(node + 0x16C), fade == 0 ? null : fade, null, null, null, 17, 2);
                    return (bytes, matrix) => NativeBattleRestorationMeshPackets.EmitBatch884370(bytes, matrix, projection, fullLife, colors, observeFirstThreeFlags: memory.WriteProjectionFlags);
                }
                NativeBattleEsunaMeshContext prepared = new(flags, fade == 0 ? null : memory.Dword(node + 0x16C), fade == 0 ? null : fade, 0, 0, 0, new(0, 0, 256, 256), new(0, 0, 256, 256), 17, 2);
                return (bytes, matrix) => NativeBattleRestorationMeshPackets.EmitBatch88E240(bytes, matrix, projection, prepared, colors, observeFirstThreeFlags: memory.WriteProjectionFlags);
            }
        });
        ushort emitters = frame.State.PassEmitterCount ?? throw new NotSupportedException("Native Recover per-pass emitter count was not reset.");
        ushort geometry = frame.State.PassGeometryCount ?? throw new NotSupportedException("Native Recover per-pass geometry count was not reset.");
        frame = frame with { State = frame.State with { PassEmitterCount = unchecked((ushort)(emitters + context.EmitterCount)), PassGeometryCount = unchecked((ushort)(geometry + context.GeometryCount)) } };
        return new(frame, context.Complete, ordering, privateSprites.ToImmutableArray(), commonMeshes.ToImmutableArray());

        NativeBattleModulePoolTask ReadTask() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(taskAddress);
        void WriteTask(NativeBattleModulePoolTask task)
        {
            NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Write(taskAddress, task).Pools } };
        }
    }
}
