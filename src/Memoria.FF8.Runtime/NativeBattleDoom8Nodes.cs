using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoom8NodePackets(bool Common, NativeBattleGfGeometryBatch Batch) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8NodeAppend(NativeBattleDoom8Frame Frame, uint Address);
public sealed record NativeBattleDoom8NodeInputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, NativeBattleCameraProjection Projection, short CameraRoll, NativeBattleSummonColors Colors, Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> Execute)
{
    public Func<uint, int, byte[]>? ReadExternal { get; init; }
}

public static class NativeBattleDoom8Nodes
{
    public const uint Callback = 0x8C88D0;
    public const uint SourceAddress = 0x1818B78;
    public const int SourceLength = 0x4434;
    public const int EmitterBytes = 10 * 108;
    public const int GeometryBytes = 40 * 672;

    public static NativeBattleDoom8NodeAppend Append8C5810(NativeBattleDoom8Frame frame, uint parent, uint callback, uint source, short delay, short duration, short mode, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(3, callback, 676, parent, selectActors);
        if (appended.Address == 0)
        {
            throw new InvalidOperationException("Doom node constructor exhausted its original676-byte task pool.");
        }
        NativeBattleModulePoolTask task = appended.Hierarchy.Read(appended.Address).WriteDword(0x30, source).WriteWord(0x298, unchecked((ushort)delay)).WriteWord(0x29E, unchecked((ushort)duration)).WriteWord(0x29C, unchecked((ushort)mode));
        hierarchy = appended.Hierarchy.Write(appended.Address, task);
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, appended.Address);
    }

    public static NativeBattleDoom8Step Advance8C88D0(NativeBattleDoom8Frame frame, uint address, NativeBattleCuragaExecutableSource source, NativeBattleDoom8NodeInputs inputs)
    {
        Require(frame, address);
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (unchecked((short)task.ReadWord(0x24)) >= unchecked((short)task.ReadWord(0x298)))
                {
                    NativeBattleCuragaExecutableState program = frame.State.NodeProgram ?? throw new NotSupportedException("Doom nodes require their actual retained executable bank.");
                    program = program.Initialize87EA20(source);
                    frame = frame with { State = frame.State with { NodeProgram = program } };
                    task = NativeBattleEffectNodeContext.Initialize(task, NativeBattleDoom8Controller.Read(frame, task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Doom nodes require their actual captured action parameters."), inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, SourceAddress, dogOrigins: new(SharedVector(frame.State, 0), SharedVector(frame.State, 8)));
                    frame = NativeBattleDoom8Controller.Write(frame, address, task);
                    frame = AdvanceContext(frame, address, inputs).Frame;
                    task = NativeBattleDoom8Controller.Read(frame, address);
                    frame = NativeBattleDoom8Controller.Write(frame, address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                break;
            case 1:
                (NativeBattleDoom8Frame advanced, bool complete) = AdvanceContext(frame, address, inputs);
                frame = advanced;
                if (complete)
                {
                    task = NativeBattleDoom8Controller.Read(frame, address);
                    frame = NativeBattleDoom8Controller.Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Doom node phase leaves its original three-entry table.");
        }
        return NativeBattleDoom8Controller.Finish(frame, address);
    }

    private static (NativeBattleDoom8Frame Frame, bool Complete) AdvanceContext(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8NodeInputs inputs)
    {
        var step = NativeBattleEffectNodeLifecycle.AdvanceContext(() => NativeBattleDoom8Controller.Read(frame, address), task => frame = NativeBattleDoom8Controller.Write(frame, address, task), Draw);
        frame = frame with { State = frame.State.WriteWord(0x275F910, unchecked((ushort)(frame.State.ReadWord(0x275F910) + step.EmitterCount))).WriteWord(0x275F3E8, unchecked((ushort)(frame.State.ReadWord(0x275F3E8) + step.GeometryCount))) };
        return (frame, step.Complete);

        void Draw()
        {
            int emitters = ArenaOffset(frame.State.ReadDword(0x275F3EC));
            int geometry = ArenaOffset(frame.State.ReadDword(0x275F980));
            NativeBattleCuragaExecutableState program = frame.State.NodeProgram ?? throw new NotSupportedException("Doom node program is unknown.");
            program.Validate();
            using NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, address, emitters, EmitterBytes, geometry, GeometryBytes, useSharedScratch: true);
            NativeBattleEffectNodeBank bank = new(SourceAddress, program.Data ?? throw new NotSupportedException("Doom nodes require their current materialized source bytes."), inputs.ReadExternal ?? ((pointer, count) => frame.State.Read(pointer, count)));
            ushort emitterScan = frame.State.ReadWord(0x275ABD8);
            ushort geometryScan = frame.State.ReadWord(0x275F508);
            NativeBattleActionMatrix camera = (frame.Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Doom nodes require their actual copied packed camera.")).ReadMatrix();
            NativeBattleEffectNodeInputs operands = new(camera, inputs.ReadModel, (parent, actor, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, emitters, 10, ref emitterScan, parent, actor, target), (emitter, actor) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, geometry, 40, ref geometryScan, emitter, actor));
            NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, operands);
            NativeCrtRandomState random = frame.Random;
            _ = lifecycle.AdvanceAndDraw(ref random, inputs.Projection, inputs.CameraRoll, PrepareMesh, Deliver);
            NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
            frame = frame with { Memory = written.Memory, Effect = written.Effect, Random = random, State = frame.State with { NodeProgram = program with { Data = bank.Data } } };
            frame = frame with { State = frame.State.WriteWord(0x275ABD8, emitterScan).WriteWord(0x275F508, geometryScan) };

            NativeBattleEffectNodeMeshInstance PrepareMesh(int node, byte[] descriptor)
            {
                NativeBattleDoom8Frame current = frame with { Memory = memory.CaptureProcessMemory() };
                short fade = unchecked((short)memory.Word(node + 0x1CE));
                uint meshAddress = memory.Dword(node + 0x170);
                current = NativeBattleDoom8NodeDrawing.Begin(current, meshAddress, BinaryPrimitives.ReadInt16LittleEndian(descriptor.AsSpan(0x3A)), fade, fade == 0 ? null : memory.Dword(node + 0x16C));
                frame = frame with { State = current.State };
                memory.PublishProcessMemory(current.Memory);
                int count = unchecked((sbyte)memory.Byte(node + 0x1D8));
                int instance = 0;
                if (count <= 0)
                {
                    frame = frame with { State = NativeBattleDoom8NodeDrawing.End(current).State };
                }
                return (bytes, matrix) =>
                {
                    NativeBattleEffectNodeMemoryWrite live = memory.FlushStorage();
                    (byte[] padding, bool[] known) = live.Memory.Arena!.ReadSnapshot(node + 0xAC + 18, 2);
                    short? matrixPadding = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(padding) : null;
                    NativeBattleDoom8NodeMeshStep emitted = NativeBattleDoom8NodeDrawing.Emit(frame with { Memory = memory.CaptureProcessMemory() }, bytes, meshAddress, matrix, inputs.Projection, inputs.Colors, matrixPadding);
                    memory.PublishProcessMemory(emitted.Frame.Memory);
                    if (++instance == count)
                    {
                        frame = frame with { State = NativeBattleDoom8NodeDrawing.End(emitted.Frame).State };
                    }
                    return emitted.Batch;
                };
            }

            void Deliver(bool common, NativeBattleGfGeometryBatch batch)
            {
                frame = inputs.Execute(frame with { Memory = memory.CaptureProcessMemory(), State = frame.State with { NodeProgram = program with { Data = bank.Data } } }, new NativeBattleDoom8NodePackets(common, batch));
                memory.PublishProcessMemory(frame.Memory);
            }
        }
    }

    private static NativeBattleVector SharedVector(NativeBattleDoom8State state, int offset)
    {
        return new(unchecked((short)state.SharedWord(offset)), unchecked((short)state.SharedWord(offset + 2)), unchecked((short)state.SharedWord(offset + 4)));
    }

    private static int ArenaOffset(uint address)
    {
        if (address < NativeBattleEffectNodeMemory.ArenaAddress)
        {
            throw new InvalidDataException("Doom nodes require their actual effect-arena allocation pointers.");
        }
        return checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
    }

    private static void Require(NativeBattleDoom8Frame frame, uint address)
    {
        frame.State.Validate();
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x275E1F8);
        if (frame.Effect.Binding.Effect != 8 || frame.Effect.Binding.Entry != NativeBattleDoom8Controller.Entry || task.Callback != Callback || task.ReadDword(0x30) != SourceAddress || task.ReadWord(0x29C) != 2 || pool.BankAddress != 0x275DF50 || pool.Stride != 676 || pool.Slots.Length != 1 || address != pool.BankAddress)
        {
            throw new InvalidDataException("Doom nodes require their original mode2 callback, source and physical task bank.");
        }
    }
}
