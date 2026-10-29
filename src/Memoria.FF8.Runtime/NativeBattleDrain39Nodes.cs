
namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrain39NodeStep(NativeBattleDrain39Frame Frame, NativeCrtRandomState Random, NativeBattleLogicalOrdering Ordering, int Result);

public static class NativeBattleDrain39Nodes
{
    public const uint BankAddress = 0x15D193C;
    public const int BankLength = 0x4258;
    public const uint DirectoryAddress = 0x15D5A90;

    public static NativeBattleDrain39NodeStep Advance(NativeBattleDrain39Frame frame, uint address, NativeBattleDrain39Resources resources, NativeBattleDrain39ControllerInputs inputs, NativeCrtRandomState random, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors colors)
    {
        using NativeBattleTaskOperation? operation = NativeBattleTaskOperation.BeginStandalone(frame.Memory, frame.Effect);
        using NativeBattleLogicalOrderingTransaction? orderingScope = NativeBattleTaskOperation.ProtectOrdering(ordering);
        ordering = orderingScope?.View ?? ordering;
        NativeBattleDrainFamilyProfile profile = resources.Profile;
        if (frame.Effect.Binding.Effect != profile.Effect || frame.Effect.Binding.Entry != profile.Entry)
        {
            throw new InvalidDataException("The siphon node profile contradicts its selected module.");
        }
        NativeBattleModulePoolTask task = ReadTask();
        if (task.Callback != profile.GenericCallback || task.ReadDword(0x30) != profile.DirectoryAddress)
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
                    task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(0x10)), frame.Effect.Parameters!, inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, profile.DirectoryAddress);
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
        return NativeBattleTaskOperation.CompleteStandalone(operation, new NativeBattleDrain39NodeStep(frame, random, NativeBattleTaskOperation.CompleteOrdering(orderingScope, ordering), complete ? 2 : 0));

        void RequireBank()
        {
            NativeBattleDrain39BankState retained = profile.RequireBank(frame.Memory);
            NativeBattleNodeSource bytes = retained.Data ?? new(resources.NodeBank);
            if (bytes.Length != profile.BankLength || bytes.ReadUInt32((int)(profile.DirectoryAddress - profile.BankAddress)) != 1)
            {
                throw new InvalidDataException("Native Drain requires its preinitialized absolute actor directory and complete backward-addressed source bank.");
            }
            NativeBattleTaskOperation.AdmitCurrentRetainedMemoryStorage(bytes);
            if (retained.Data is null)
            {
                frame = frame with { Memory = profile.WithBank(frame.Memory, retained with { Data = bytes }) };
            }
        }
        bool AdvanceContext()
        {
            var context = NativeBattleEffectNodeLifecycle.AdvanceContext(ReadTask, WriteTask, () =>
            {
                RequireBank();
                int first = frame.State.Emitters ?? throw new NotSupportedException("Native Drain requires its cleared40-emitter pool.");
                int second = frame.State.Geometry ?? throw new NotSupportedException("Native Drain requires its cleared140-geometry pool.");
                using NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, address, first, 0x10E0, second, 0x16F80, useSharedScratch: true);
                using NativeBattleEffectNodeBank bank = new(profile.BankAddress, profile.RequireBank(frame.Memory).Data!, resources.ReadChannels);
                ushort emitterCursor = frame.State.EmitterCursor ?? throw new NotSupportedException("Native Drain emitter scan WORD is unknown.");
                ushort geometryCursor = frame.State.GeometryCursor ?? throw new NotSupportedException("Native Drain geometry scan WORD is unknown.");
                NativeBattleEffectNodeInputs callbacks = new((frame.Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("The siphon nodes require their actual packed shared view.")).ReadMatrix(), inputs.ReadModel, (parent, source, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, first, 40, ref emitterCursor, parent, source, target), (emitter, source) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, second, 140, ref geometryCursor, emitter, source));
                NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, callbacks);
                NativeBattlePilotVisualIdentity? pilotIdentity = profile.Effect is 39 or 56 ? NativeBattlePilotVisualLifetimes.Identity(profile.Effect, task) : null;
                NativeBattleEffectNodePreparePilotMesh? preparePilotMesh = pilotIdentity.HasValue ? (node, source) => NativeBattleEffect88MeshPreparation.PreparePilot(memory, node, source, projection, colors) : null;
                _ = lifecycle.AdvanceAndDraw(ref random, projection, cameraRoll, (node, source) => NativeBattleEffect88MeshPreparation.Prepare(memory, node, source, projection, colors), (_, batch) =>
                {
                    NativeBattleEffectPacketWrite packets = NativeBattleEffectPacketArena.Append(memory.CaptureProcessMemory(), frame.State.PrimaryDrawCursor ?? throw new NotSupportedException("The siphon nodes require their current private packet cursor."), ordering, batch);
                    memory.PublishProcessMemory(packets.Memory);
                    frame = frame with { State = frame.State with { PrimaryDrawCursor = packets.Cursor } };
                    ordering = packets.Ordering;
                }, pilotIdentity, preparePilotMesh);
                NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
                bank.Publish();
                frame = frame with { Memory = written.Memory, Effect = written.Effect, State = frame.State with { EmitterCursor = emitterCursor, GeometryCursor = geometryCursor } };

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
