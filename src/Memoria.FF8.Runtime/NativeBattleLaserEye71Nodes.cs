using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleLaserEye71Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLaserEye71NodeInputs(NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, NativeBattleCameraProjection Projection, short CameraRoll, NativeBattleSummonColors Colors, Func<NativeBattleLaserEye71Frame, NativeBattlePackedMatrix32, NativeBattleVector, NativeBattleVector, NativeBattleLaserEye71MatrixStep> LookBasis8DDA50, Func<NativeBattleLaserEye71Frame, NativeBattleGfGeometryBatch, NativeBattleLaserEye71Frame> DeliverPrivate)
{
    public Func<NativeBattleLaserEye71Frame, int, byte, NativeBattleLaserEye71Frame>? ObserveBone { get; init; }
}

public static class NativeBattleLaserEye71Nodes
{
    public static NativeBattleLaserEye71TaskStep Advance(NativeBattleLaserEye71Frame frame, uint address, NativeBattleLaserEye71Resources resources, NativeBattleLaserEye71NodeInputs inputs)
    {
        Require(frame, address);
        NativeBattleModulePoolTask task = NativeBattleLaserEye71Controller.Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (unchecked((short)task.ReadWord(0x24)) >= unchecked((short)task.ReadWord(0x298)))
                {
                    NativeBattleLaserEye71State state = frame.State.SeedColdSource(resources);
                    ImmutableArray<byte> bytes = state.ExecutableBytes ?? throw new NotSupportedException("Laser Eye nodes require their current executable source bytes.");
                    if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan()) == 0)
                    {
                        byte[] relocated = NativeBattleEffectNodeBankFixups.Relocate(bytes, SourceAddress);
                        BinaryPrimitives.WriteUInt32LittleEndian(relocated, 1);
                        state = state with { ExecutableBytes = relocated.ToImmutableArray() };
                    }
                    frame = frame with { State = state.WriteDword(0x25FCDD4, address + 0x34) };
                    task = NativeBattleEffectNodeContext.Initialize(task, NativeBattleLaserEye71Controller.Read(frame, task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Laser Eye nodes require their captured action parameters."), inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, SourceAddress, observeBone: (actor, selector) => frame = (inputs.ObserveBone ?? throw new NotSupportedException("Laser Eye node initialization requires its reached bone-anchor publication."))(frame, actor, selector));
                    frame = NativeBattleLaserEye71Controller.Write(frame, address, task);
                    frame = AdvanceContext(frame, address, inputs).Frame;
                    task = NativeBattleLaserEye71Controller.Read(frame, address);
                    frame = NativeBattleLaserEye71Controller.Write(frame, address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                break;
            case 1:
                (NativeBattleLaserEye71Frame advanced, bool complete) = AdvanceContext(frame, address, inputs);
                frame = advanced;
                if (complete)
                {
                    task = NativeBattleLaserEye71Controller.Read(frame, address);
                    frame = NativeBattleLaserEye71Controller.Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Laser Eye node phase leaves its original three-entry table.");
        }
        return NativeBattleLaserEye71Controller.Finish(frame, address);
    }

    private static (NativeBattleLaserEye71Frame Frame, bool Complete) AdvanceContext(NativeBattleLaserEye71Frame frame, uint address, NativeBattleLaserEye71NodeInputs inputs)
    {
        frame = frame with { State = frame.State.WriteDword(0x25FCDD4, address + 0x34) };
        var step = NativeBattleEffectNodeLifecycle.AdvanceContext(() => NativeBattleLaserEye71Controller.Read(frame, address), task => frame = NativeBattleLaserEye71Controller.Write(frame, address, task), Draw);
        frame = frame with { State = frame.State.WriteWord(0x25FCD9A, unchecked((ushort)(frame.State.ReadWord(0x25FCD9A) + step.EmitterCount))).WriteWord(0x25FCD8A, unchecked((ushort)(frame.State.ReadWord(0x25FCD8A) + step.GeometryCount))) };
        return (frame, step.Complete);

        void Draw()
        {
            int emitters = ArenaOffset(frame.State.ReadDword(0x25FCD8C), EmitterBytes);
            int geometry = ArenaOffset(frame.State.ReadDword(0x25FCDC8), GeometryBytes);
            NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, address, emitters, EmitterBytes, geometry, GeometryBytes, useSharedScratch: true);
            NativeBattleEffectNodeBank bank = new(SourceAddress, frame.State.ExecutableBytes ?? throw new NotSupportedException("Laser Eye nodes require their materialized executable source."));
            ushort emitterScan = frame.State.ReadWord(0x25FAAE0);
            ushort geometryScan = frame.State.ReadWord(0x25FCD98);
            NativeBattleActionMatrix camera = (frame.Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Laser Eye nodes require their actual copied packed camera.")).ReadMatrix();
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
            frame = frame with { State = frame.State.WriteWord(0x25FAAE0, emitterScan).WriteWord(0x25FCD98, geometryScan) };

            void Look(NativeBattleEffectNodeMemory owner, int destination, NativeBattleVector direction, NativeBattleVector up)
            {
                NativeBattleEffectNodeMemoryWrite current = owner.FlushStorage();
                (byte[] data, bool[] known) = current.Memory.Arena!.ReadSnapshot(destination, 32);
                NativeBattleLaserEye71MatrixStep result = inputs.LookBasis8DDA50(frame with { Memory = current.Memory, Effect = current.Effect }, new(data.ToImmutableArray(), known.ToImmutableArray()), direction, up);
                frame = result.Frame;
                result.Matrix.Validate();
                for (int index = 0; index < 18; index += 2)
                {
                    if (!result.Matrix.Known[index] || !result.Matrix.Known[index + 1])
                    {
                        throw new NotSupportedException("Laser Eye look basis did not publish its reached rotation WORD.");
                    }
                    owner.Word(destination + index, BinaryPrimitives.ReadUInt16LittleEndian(result.Matrix.Bytes.AsSpan().Slice(index, 2)));
                }
                owner.PublishProcessMemory(result.Frame.Memory);
            }

            NativeBattleEffectNodeMeshInstance PrepareMesh(int node, byte[] descriptor)
            {
                if (frame.State.ReadDword(0x25FA42C) != 0x2793E58)
                {
                    throw new NotSupportedException("Laser Eye mesh requires its actual copied-view reservation pointer.");
                }
                uint meshAddress = memory.Dword(node + 0x170);
                short fade = unchecked((short)memory.Word(node + 0x1CE));
                NativeBattleEffectMemory process = memory.CaptureProcessMemory();
                NativeBattleEffectArena view = NativeBattleEffectNodeContextMemory.Capture(process);
                NativeBattleEffectArena context = Context(view);
                context = NativeBattleCuraga88Mesh.PrepareContext(context, meshAddress, BinaryPrimitives.ReadInt16LittleEndian(descriptor.AsSpan(0x3A)), fade, fade == 0 ? null : memory.Dword(node + 0x16C));
                view = NativeBattleEffectNodeContextMemory.Copy(context, 0, view, NativeBattleCuraga88Mesh.SharedViewOffset, 88);
                memory.PublishProcessMemory(NativeBattleEffectNodeContextMemory.Publish(process, view));
                frame = frame with { State = frame.State.WriteDword(0x25FA42C, NativeBattleCuraga88Mesh.ContextAddress) };
                int instances = unchecked((sbyte)memory.Byte(node + 0x1D8));
                int index = 0;
                if (instances <= 0)
                {
                    frame = frame with { State = frame.State.WriteDword(0x25FA42C, 0x2793E58) };
                }
                return (bytes, matrix) =>
                {
                    NativeBattleEffectNodeMemoryWrite current = memory.FlushStorage();
                    (byte[] padding, bool[] known) = current.Memory.Arena!.ReadSnapshot(node + 0xAC + 18, 2);
                    short? matrixPadding = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(padding) : null;
                    NativeBattleEffectMemory live = memory.CaptureProcessMemory();
                    NativeBattlePrimitiveProcess primitive = live.PrimitiveProcess ?? throw new NotSupportedException("Laser Eye mesh requires its retained renderer process.");
                    NativeBattleScriptedGraphicsMemory controls = live.ScriptedGraphics ?? throw new NotSupportedException("Laser Eye mesh requires its current projection controls.");
                    NativeBattleEffectArena liveView = NativeBattleEffectNodeContextMemory.Capture(live);
                    NativeBattleCuraga88MeshStep emitted = NativeBattleCuraga88Mesh.EmitBatch(bytes, meshAddress, matrix, inputs.Projection, Context(liveView), new(live.GeometryRegisters ?? throw new NotSupportedException("Laser Eye mesh requires its geometry registers."), live.ScriptedColorRegisters ?? throw new NotSupportedException("Laser Eye mesh requires its data registers.")), primitive.ModelRendererRegisters ?? throw new NotSupportedException("Laser Eye mesh requires its FAR controls."), controls.Dqa, controls.Dqb, controls.Zsf4, inputs.Colors, matrixPadding, controls.Zsf3);
                    liveView = NativeBattleEffectNodeContextMemory.Copy(emitted.Context88, 0, liveView, NativeBattleCuraga88Mesh.SharedViewOffset, 88);
                    live = NativeBattleEffectNodeContextMemory.Publish(live, liveView);
                    memory.PublishProcessMemory(live with { GeometryRegisters = emitted.Registers.Geometry, ScriptedColorRegisters = emitted.Registers.Colors, PrimitiveProcess = primitive with { ModelRendererRegisters = emitted.FarControls } });
                    if (++index == instances)
                    {
                        frame = frame with { State = frame.State.WriteDword(0x25FA42C, 0x2793E58) };
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
    private static int ArenaOffset(uint pointer, int length) => pointer >= ArenaAddress && pointer - ArenaAddress <= NativeBattleEffectArena.Length - length ? checked((int)(pointer - ArenaAddress)) : throw new InvalidDataException("Laser Eye node storage leaves its current arena.");
    private static void Require(NativeBattleLaserEye71Frame frame, uint address)
    {
        frame.State.Validate();
        NativeBattleModulePoolTask task = NativeBattleLaserEye71Controller.Read(frame, address);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == Pools[2].Descriptor);
        if (frame.Effect.Binding.Effect != 71 || frame.Effect.Binding.Entry != Entry || task.Callback != NativeBattleLaserEye71Controller.NodeCallback || task.ReadDword(0x30) != SourceAddress || task.ReadWord(0x29C) != 0 || pool.BankAddress != Pools[2].Bank || pool.Stride != 676 || pool.Slots.Length != 3)
        {
            throw new InvalidDataException("Laser Eye nodes require their original callback, mode and addressed task bank.");
        }
    }
}
