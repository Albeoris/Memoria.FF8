using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrain39NodeStep(NativeBattleDrain39Frame Frame, NativeCrtRandomState Random, NativeBattleLogicalOrdering Ordering, int Result);

public static class NativeBattleDrain39Nodes
{
    public const uint BankAddress = 0x15D193C;
    public const int BankLength = 0x4258;
    public const uint DirectoryAddress = 0x15D5A90;

    public static NativeBattleDrain39NodeStep Advance(NativeBattleDrain39Frame frame, uint address, NativeBattleDrain39Resources resources, NativeBattleDrain39ControllerInputs inputs, NativeCrtRandomState random, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = ReadTask();
        if (task.Callback != 0x851A80 || task.ReadDword(0x30) != DirectoryAddress)
        {
            throw new InvalidDataException("Native Drain actor system requires its actual callback and absolute directory owner.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (unchecked((short)task.ReadWord(0x24)) >= unchecked((short)task.ReadWord(0x298)))
                {
                    RequireBank();
                    NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
                    task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(0x10)), frame.Effect.Parameters!, inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, DirectoryAddress);
                    WriteTask(task);
                    AdvanceContext();
                    task = ReadTask();
                    WriteTask(task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                break;
            case 1:
                if (AdvanceContext())
                {
                    task = ReadTask();
                    WriteTask(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Native Drain actor-system phase leaves its original three states.");
        }
        task = ReadTask().WriteWord(0x24, unchecked((ushort)(ReadTask().ReadWord(0x24) + 1)));
        WriteTask(task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Complete8DC530(address).Pools } };
        }
        return new(frame, random, ordering, complete ? 2 : 0);

        void RequireBank()
        {
            NativeBattleDrain39BankState retained = frame.Memory.Drain39Bank ?? throw new NotSupportedException("Native Drain requires its retained executable-bank owner.");
            ImmutableArray<byte> bytes = retained.Data ?? resources.NodeBank;
            if (bytes.IsDefault || bytes.Length != BankLength || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice((int)(DirectoryAddress - BankAddress))) != 1)
            {
                throw new InvalidDataException("Native Drain requires its preinitialized absolute actor directory and complete backward-addressed source bank.");
            }
            frame = frame with { Memory = frame.Memory with { Drain39Bank = retained with { Data = bytes } } };
        }
        bool AdvanceContext()
        {
            var context = NativeBattleEffectNodeLifecycle.AdvanceContext(ReadTask, WriteTask, () =>
            {
                RequireBank();
                int first = frame.State.Emitters ?? throw new NotSupportedException("Native Drain requires its cleared40-emitter pool.");
                int second = frame.State.Geometry ?? throw new NotSupportedException("Native Drain requires its cleared140-geometry pool.");
                NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, address, first, 0x10E0, second, 0x16F80, useSharedScratch: true);
                NativeBattleEffectNodeBank bank = new(BankAddress, frame.Memory.Drain39Bank!.Data!.Value, resources.ReadChannels);
                ushort emitterCursor = frame.State.EmitterCursor ?? throw new NotSupportedException("Native Drain emitter scan WORD is unknown.");
                ushort geometryCursor = frame.State.GeometryCursor ?? throw new NotSupportedException("Native Drain geometry scan WORD is unknown.");
                NativeBattleEffectNodeInputs callbacks = new(frame.State.CopiedCamera, inputs.ReadModel, (parent, source, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, first, 40, ref emitterCursor, parent, source, target), (emitter, source) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, second, 140, ref geometryCursor, emitter, source));
                NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, callbacks);
                List<NativeBattleGfGeometryBatch> deliveries = [];
                _ = lifecycle.AdvanceAndDraw(ref random, projection, cameraRoll, Prepare, (_, batch) => deliveries.Add(batch));
                NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
                frame = frame with { Memory = written.Memory with { Drain39Bank = new(bank.Data) }, Effect = written.Effect, State = frame.State with { EmitterCursor = emitterCursor, GeometryCursor = geometryCursor } };
                foreach (NativeBattleGfGeometryBatch batch in deliveries)
                {
                    NativeBattleEffectPacketWrite packets = NativeBattleEffectPacketArena.Append(frame.Memory, frame.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native Drain requires its current private packet cursor."), ordering, batch);
                    frame = frame with { Memory = packets.Memory, State = frame.State with { PrimaryDrawCursor = packets.Cursor } };
                    ordering = packets.Ordering;
                }

                NativeBattleEffectNodeMeshInstance Prepare(int node, byte[] source)
                {
                    uint flags = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(0x3A)) == 0 ? 0x30u : 0;
                    short fade = unchecked((short)memory.Word(node + 0x1CE));
                    if (fade != 0)
                    {
                        flags |= 0xC0;
                    }
                    uint? far = fade == 0 ? null : memory.Dword(node + 0x16C);
                    return (bytes, matrix) => NativeBattleGfGeometryPackets.EmitBatch(bytes, 0, default, matrix, projection, new(flags, far, fade, null, null, 0, 0, 17, 2), colors, variant: new(null, null, false, false) { ObserveFlags = memory.WriteProjectionFlags });
                }
            });
            ushort emitters = frame.State.PassEmitterCount ?? throw new NotSupportedException("Native Drain requires its reset per-pass emitter count.");
            ushort geometry = frame.State.PassGeometryCount ?? throw new NotSupportedException("Native Drain requires its reset per-pass geometry count.");
            frame = frame with { State = frame.State with { PassEmitterCount = unchecked((ushort)(emitters + context.EmitterCount)), PassGeometryCount = unchecked((ushort)(geometry + context.GeometryCount)) } };
            return context.Complete;
        }
        NativeBattleModulePoolTask ReadTask() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        void WriteTask(NativeBattleModulePoolTask current) => frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, current).Pools } };
    }
}
