using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect30Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect30NodeInputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, NativeBattleCameraProjection Projection, short CameraRoll, NativeBattleSummonColors Colors, Func<NativeBattleEffect30Frame, NativeBattlePackedMatrix32, NativeBattleVector, NativeBattleVector, NativeBattleEffect30MatrixStep> LookBasis8DDA50, Func<NativeBattleEffect30Frame, NativeBattleGfGeometryBatch, NativeBattleEffect30Frame> DeliverPrivate)
{
    public Func<NativeBattleEffect30Frame, int, byte, NativeBattleEffect30Frame>? ObserveBone { get; init; }
}

public static class NativeBattleEffect30Nodes
{
    public static NativeBattleEffect30TaskStep Advance(NativeBattleEffect30Frame frame, uint address, NativeBattleEffect30Resources resources, NativeBattleEffect30NodeInputs inputs)
    {
        Require(frame, address);
        NativeBattleModulePoolTask task = NativeBattleEffect30Controller.Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (unchecked((short)task.ReadWord(0x24)) >= unchecked((short)task.ReadWord(0x298)))
                {
                    NativeBattleEffect30State state = frame.State.InitializeSource(resources);
                    frame = frame with { State = state.WriteDword(0x26D6A38, address + 0x34) };
                    task = NativeBattleEffectNodeContext.Initialize(task, NativeBattleEffect30Controller.Read(frame, task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Effect30 nodes require their captured action parameters."), inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, SourceAddress, observeBone: (actor, selector) => frame = (inputs.ObserveBone ?? throw new NotSupportedException("Effect30 node initialization requires its reached bone-anchor publication."))(frame, actor, selector));
                    frame = NativeBattleEffect30Controller.Write(frame, address, task);
                    frame = AdvanceContext(frame, address, inputs).Frame;
                    task = NativeBattleEffect30Controller.Read(frame, address);
                    frame = NativeBattleEffect30Controller.Write(frame, address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                break;
            case 1:
                (NativeBattleEffect30Frame advanced, bool complete) = AdvanceContext(frame, address, inputs);
                frame = advanced;
                if (complete)
                {
                    task = NativeBattleEffect30Controller.Read(frame, address);
                    frame = NativeBattleEffect30Controller.Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect30 node phase leaves its original three-entry table.");
        }
        return NativeBattleEffect30Controller.Finish(frame, address);
    }

    private static (NativeBattleEffect30Frame Frame, bool Complete) AdvanceContext(NativeBattleEffect30Frame frame, uint address, NativeBattleEffect30NodeInputs inputs)
    {
        frame = frame with { State = frame.State.WriteDword(0x26D6A38, address + 0x34) };
        var step = NativeBattleEffectNodeLifecycle.AdvanceContext(() => NativeBattleEffect30Controller.Read(frame, address), task => frame = NativeBattleEffect30Controller.Write(frame, address, task), Draw);
        frame = frame with { State = frame.State.WriteWord(0x26D6A22, unchecked((ushort)(frame.State.ReadWord(0x26D6A22) + step.EmitterCount))).WriteWord(0x26D6A12, unchecked((ushort)(frame.State.ReadWord(0x26D6A12) + step.GeometryCount))) };
        return (frame, step.Complete);

        void Draw()
        {
            int emitters = ArenaOffset(frame.State.ReadDword(0x26D6A14), EmitterBytes);
            int geometry = ArenaOffset(frame.State.ReadDword(0x26D6A2C), GeometryBytes);
            NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, address, emitters, EmitterBytes, geometry, GeometryBytes, useSharedScratch: true);
            NativeBattleEffectNodeBank bank = new(SourceAddress, frame.State.ExecutableBytes ?? throw new NotSupportedException("Effect30 nodes require their materialized executable source."));
            ushort emitterScan = frame.State.ReadWord(0x26D4768);
            ushort geometryScan = frame.State.ReadWord(0x26D6A20);
            NativeBattleActionMatrix camera = (frame.Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Effect30 nodes require their actual copied packed camera.")).ReadMatrix();
            NativeBattleEffectNodeInputs operands = new(camera, inputs.ReadModel, (parent, source, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, emitters, EmitterCount, ref emitterScan, parent, source, target), (emitter, source) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, geometry, GeometryCount, ref geometryScan, emitter, source))
            {
                Orientation2Policy = NativeBattleEffectNodeOrientation2Policy.TargetF1MinusCasterF0Look3D,
                LookBasis8DDA50 = Look
            };
            NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, operands);
            NativeCrtRandomState random = frame.Random;
            _ = lifecycle.AdvanceAndDraw(ref random, inputs.Projection, inputs.CameraRoll, PrepareMesh, Deliver);
            NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
            frame = frame with { Memory = written.Memory, Effect = written.Effect, Random = random, State = frame.State with { ExecutableBytes = bank.Data } };
            frame = frame with { State = frame.State.WriteWord(0x26D4768, emitterScan).WriteWord(0x26D6A20, geometryScan) };

            void Look(NativeBattleEffectNodeMemory owner, int destination, NativeBattleVector direction, NativeBattleVector up)
            {
                NativeBattleEffectNodeMemoryWrite current = owner.FlushStorage();
                (byte[] data, bool[] known) = current.Memory.Arena!.ReadSnapshot(destination, 32);
                NativeBattleEffect30MatrixStep result = inputs.LookBasis8DDA50(frame with { Memory = current.Memory, Effect = current.Effect }, new(data.ToImmutableArray(), known.ToImmutableArray()), direction, up);
                frame = result.Frame;
                result.Matrix.Validate();
                for (int index = 0; index < 18; index += 2)
                {
                    if (!result.Matrix.Known[index] || !result.Matrix.Known[index + 1])
                    {
                        throw new NotSupportedException("Effect30 look basis did not publish its reached rotation WORD.");
                    }
                    owner.Word(destination + index, BinaryPrimitives.ReadUInt16LittleEndian(result.Matrix.Bytes.AsSpan().Slice(index, 2)));
                }
                owner.PublishProcessMemory(result.Frame.Memory);
            }

            NativeBattleEffectNodeMeshInstance PrepareMesh(int node, byte[] descriptor)
            {
                if (frame.State.ReadDword(0x26D6A28) != 0x2793E58)
                {
                    throw new NotSupportedException("Effect30 mesh requires its actual copied-view reservation pointer.");
                }
                uint meshAddress = memory.Dword(node + 0x170);
                short fade = unchecked((short)memory.Word(node + 0x1CE));
                NativeBattleEffectMemory process = memory.CaptureProcessMemory();
                NativeBattleEffectArena view = NativeBattleEffectNodeContextMemory.Capture(process);
                NativeBattleEffectArena context = Context(view);
                context = NativeBattleCuraga88Mesh.PrepareContext(context, meshAddress, BinaryPrimitives.ReadInt16LittleEndian(descriptor.AsSpan(0x3A)), fade, fade == 0 ? null : memory.Dword(node + 0x16C));
                view = NativeBattleEffectNodeContextMemory.Copy(context, 0, view, NativeBattleCuraga88Mesh.SharedViewOffset, 88);
                memory.PublishProcessMemory(NativeBattleEffectNodeContextMemory.Publish(process, view));
                frame = frame with { State = frame.State.WriteDword(0x26D6A28, NativeBattleCuraga88Mesh.ContextAddress) };
                int instances = unchecked((sbyte)memory.Byte(node + 0x1D8));
                int index = 0;
                if (instances <= 0)
                {
                    frame = frame with { State = frame.State.WriteDword(0x26D6A28, 0x2793E58) };
                }
                return (bytes, matrix) =>
                {
                    NativeBattleEffectNodeMemoryWrite current = memory.FlushStorage();
                    (byte[] padding, bool[] known) = current.Memory.Arena!.ReadSnapshot(node + 0xAC + 18, 2);
                    short? matrixPadding = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(padding) : null;
                    NativeBattleEffectMemory live = memory.CaptureProcessMemory();
                    NativeBattlePrimitiveProcess primitive = live.PrimitiveProcess ?? throw new NotSupportedException("Effect30 mesh requires its retained renderer process.");
                    NativeBattleScriptedGraphicsMemory controls = live.ScriptedGraphics ?? throw new NotSupportedException("Effect30 mesh requires its current projection controls.");
                    NativeBattleEffectArena liveView = NativeBattleEffectNodeContextMemory.Capture(live);
                    NativeBattleCuraga88MeshStep emitted = NativeBattleCuraga88Mesh.EmitBatch(bytes, meshAddress, matrix, inputs.Projection, Context(liveView), new(live.GeometryRegisters ?? throw new NotSupportedException("Effect30 mesh requires its geometry registers."), live.ScriptedColorRegisters ?? throw new NotSupportedException("Effect30 mesh requires its data registers.")), primitive.ModelRendererRegisters ?? throw new NotSupportedException("Effect30 mesh requires its FAR controls."), controls.Dqa, controls.Dqb, controls.Zsf4, inputs.Colors, matrixPadding, controls.Zsf3);
                    liveView = NativeBattleEffectNodeContextMemory.Copy(emitted.Context88, 0, liveView, NativeBattleCuraga88Mesh.SharedViewOffset, 88);
                    live = NativeBattleEffectNodeContextMemory.Publish(live, liveView);
                    memory.PublishProcessMemory(live with { GeometryRegisters = emitted.Registers.Geometry, ScriptedColorRegisters = emitted.Registers.Colors, PrimitiveProcess = primitive with { ModelRendererRegisters = emitted.FarControls } });
                    if (++index == instances)
                    {
                        frame = frame with { State = frame.State.WriteDword(0x26D6A28, 0x2793E58) };
                    }
                    return emitted.Batch;
                };
            }

            void Deliver(bool mesh, NativeBattleGfGeometryBatch batch)
            {
                NativeBattleEffectNodeMemoryWrite current = memory.FlushStorage();
                frame = inputs.DeliverPrivate(frame with { Memory = current.Memory, Effect = current.Effect, State = frame.State with { ExecutableBytes = bank.Data } }, batch);
                memory.PublishProcessMemory(frame.Memory);
            }
        }
    }

    private static NativeBattleEffectArena Context(NativeBattleEffectArena view) => NativeBattleEffectNodeContextMemory.Copy(view, NativeBattleCuraga88Mesh.SharedViewOffset, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
    private static int ArenaOffset(uint pointer, int length) => pointer >= ArenaAddress && pointer - ArenaAddress <= NativeBattleEffectArena.Length - length ? checked((int)(pointer - ArenaAddress)) : throw new InvalidDataException("Effect30 node storage leaves its current arena.");
    private static void Require(NativeBattleEffect30Frame frame, uint address)
    {
        frame.State.Validate();
        NativeBattleModulePoolTask task = NativeBattleEffect30Controller.Read(frame, address);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == Pools[2].Descriptor);
        if (frame.Effect.Binding.Effect != 30 || frame.Effect.Binding.Entry != Entry || task.Callback != NativeBattleEffect30Profile.NodeCallback || task.ReadDword(0x30) != SourceAddress || task.ReadWord(0x29C) != 0 || pool.BankAddress != Pools[2].Bank || pool.Stride != 676 || pool.Slots.Length != 3)
        {
            throw new InvalidDataException("Effect30 nodes require their original callback, mode and addressed task bank.");
        }
    }
}
