using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCurse47NodePackets(bool Common, NativeBattleGfGeometryBatch Batch) : NativeBattleCurse47Operation;
public sealed record NativeBattleCurse47NodeAppend(NativeBattleCurse47Frame Frame, uint Address);
public sealed record NativeBattleCurse47NodeInputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, NativeBattleCameraProjection Projection, short CameraRoll, Func<NativeBattleCurse47Frame, NativeBattleCurse47Operation, NativeBattleCurse47Frame> Execute)
{
    public Func<uint, int, byte[]>? ReadExternal { get; init; }
    public Func<NativeBattleCurse47Frame, int, byte, NativeBattleCurse47Frame>? ObserveBone { get; init; }
}

public static class NativeBattleCurse47Nodes
{
    public const uint Callback = 0x82DF60;
    public const uint SourceAddress = 0x17B97A4;
    public const int SourceLength = 6544;
    public const int EmitterBytes = 10 * 108;
    public const int GeometryBytes = 70 * 672;

    public static NativeBattleCurse47NodeAppend Append82B5A0(NativeBattleCurse47Frame frame, uint parent, uint callback, uint source, short delay, short duration, short mode, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(3, callback, 676, parent, selectActors);
        if (appended.Address == 0)
        {
            throw new InvalidOperationException("Curse node constructor exhausted its original676-byte task pool.");
        }
        NativeBattleModulePoolTask task = appended.Hierarchy.Read(appended.Address).WriteDword(0x30, source).WriteWord(0x298, unchecked((ushort)delay)).WriteWord(0x29E, unchecked((ushort)duration)).WriteWord(0x29C, unchecked((ushort)mode));
        hierarchy = appended.Hierarchy.Write(appended.Address, task);
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, appended.Address);
    }

    public static NativeBattleCurse47Step Advance82DF60(NativeBattleCurse47Frame frame, uint address, NativeBattleCuragaExecutableSource source, NativeBattleCurse47NodeInputs inputs)
    {
        Require(frame, address);
        NativeBattleModulePoolTask task = NativeBattleCurse47Controller.Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (unchecked((short)task.ReadWord(0x24)) >= unchecked((short)task.ReadWord(0x298)))
                {
                    frame = frame with { State = frame.State.WriteDword(0x264EAEC, checked(address + 0x34)) };
                    NativeBattleCuragaExecutableState program = frame.State.NodeProgram ?? throw new NotSupportedException("Curse nodes require their actual retained executable bank.");
                    program = program.Initialize87EA20(source);
                    frame = frame with { State = frame.State with { NodeProgram = program } };
                    task = NativeBattleEffectNodeContext.Initialize(task, NativeBattleCurse47Controller.Read(frame, task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Curse nodes require their actual captured action parameters."), inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, SourceAddress, observeBone: (actor, selector) =>
                    {
                        frame = (inputs.ObserveBone ?? throw new NotSupportedException("Curse context initialization requires its reached native bone-transform observer."))(frame, actor, selector);
                    });
                    frame = NativeBattleCurse47Controller.Write(frame, address, task);
                    frame = AdvanceContext(frame, address, inputs).Frame;
                    task = NativeBattleCurse47Controller.Read(frame, address);
                    frame = NativeBattleCurse47Controller.Write(frame, address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                break;
            case 1:
                (NativeBattleCurse47Frame advanced, bool complete) = AdvanceContext(frame, address, inputs);
                frame = advanced;
                if (complete)
                {
                    task = NativeBattleCurse47Controller.Read(frame, address);
                    frame = NativeBattleCurse47Controller.Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Curse node phase leaves its original three-entry table.");
        }
        return NativeBattleCurse47Controller.Finish(frame, address);
    }

    private static (NativeBattleCurse47Frame Frame, bool Complete) AdvanceContext(NativeBattleCurse47Frame frame, uint address, NativeBattleCurse47NodeInputs inputs)
    {
        frame = frame with { State = frame.State.WriteDword(0x264EAEC, checked(address + 0x34)) };
        var step = NativeBattleEffectNodeLifecycle.AdvanceContext(() => NativeBattleCurse47Controller.Read(frame, address), task => frame = NativeBattleCurse47Controller.Write(frame, address, task), Draw);
        frame = frame with { State = frame.State.WriteWord(0x264EAC0, unchecked((ushort)(frame.State.ReadWord(0x264EAC0) + step.EmitterCount))).WriteWord(0x264E704, unchecked((ushort)(frame.State.ReadWord(0x264E704) + step.GeometryCount))) };
        return (frame, step.Complete);

        void Draw()
        {
            int emitters = ArenaOffset(frame.State.ReadDword(0x264E708));
            int geometry = ArenaOffset(frame.State.ReadDword(0x264EAE0));
            NativeBattleCuragaExecutableState program = frame.State.NodeProgram ?? throw new NotSupportedException("Curse node program is unknown.");
            program.Validate();
            using NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, address, emitters, EmitterBytes, geometry, GeometryBytes, useSharedScratch: true);
            NativeBattleEffectNodeBank bank = new(SourceAddress, program.Data ?? throw new NotSupportedException("Curse nodes require their current materialized source bytes."), inputs.ReadExternal ?? ((pointer, count) => frame.State.Read(pointer, count)));
            ushort emitterScan = frame.State.ReadWord(0x264D828);
            ushort geometryScan = frame.State.ReadWord(0x264E714);
            NativeBattleActionMatrix camera = (frame.Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Curse nodes require their actual copied packed camera.")).ReadMatrix();
            NativeBattleEffectNodeInputs operands = new(camera, inputs.ReadModel, (parent, actor, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, emitters, 10, ref emitterScan, parent, actor, target), (emitter, actor) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, geometry, 70, ref geometryScan, emitter, actor));
            NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, operands);
            NativeCrtRandomState random = frame.Random;
            _ = lifecycle.AdvanceAndDraw(ref random, inputs.Projection, inputs.CameraRoll, PrepareMesh, Deliver);
            NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
            frame = frame with { Memory = written.Memory, Effect = written.Effect, Random = random, State = frame.State with { NodeProgram = program with { Data = bank.Data } } };
            frame = frame with { State = frame.State.WriteWord(0x264D828, emitterScan).WriteWord(0x264E714, geometryScan) };

            NativeBattleEffectNodeMeshInstance PrepareMesh(int node, byte[] descriptor)
            {
                throw new NotSupportedException("Curse's original selected node bank contains sprites only; a mesh requires its separate private caller contract.");
            }

            void Deliver(bool common, NativeBattleGfGeometryBatch batch)
            {
                frame = inputs.Execute(frame with { Memory = memory.CaptureProcessMemory(), State = frame.State with { NodeProgram = program with { Data = bank.Data } } }, new NativeBattleCurse47NodePackets(true, batch));
                memory.PublishProcessMemory(frame.Memory);
            }
        }
    }

    private static int ArenaOffset(uint address)
    {
        if (address < NativeBattleEffectNodeMemory.ArenaAddress)
        {
            throw new InvalidDataException("Curse nodes require their actual effect-arena allocation pointers.");
        }
        return checked((int)(address - NativeBattleEffectNodeMemory.ArenaAddress));
    }

    private static void Require(NativeBattleCurse47Frame frame, uint address)
    {
        frame.State.Validate();
        NativeBattleModulePoolTask task = NativeBattleCurse47Controller.Read(frame, address);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x264E288);
        if (frame.Effect.Binding.Effect != 47 || frame.Effect.Binding.Entry != NativeBattleCurse47Controller.Entry || task.Callback != Callback || task.ReadDword(0x30) != SourceAddress || task.ReadWord(0x29C) != 0 || pool.BankAddress != 0x264DFE0 || pool.Stride != 676 || pool.Slots.Length != 1 || address != pool.BankAddress)
        {
            throw new InvalidDataException("Curse nodes require their original mode0 callback, source and physical task bank.");
        }
    }
}
