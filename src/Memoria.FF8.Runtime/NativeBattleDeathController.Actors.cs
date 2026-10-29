using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDeathController
{
    private sealed partial class Execution
    {
        private void Actors(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            if (Stage(task) == 0)
            {
                if (S(task, 0x24) < S(task, 0x298))
                {
                    return;
                }
                uint source = task.ReadDword(0x30);
                using (NativeBattleNodeInitialization<NativeBattleNodeSource> initialization = NativeBattleActorBankInitialization.Prepare(ActorBank(source), source))
                {
                    NativeBattleVector actorOrigin = new(unchecked((short)Word(0x27423F8)), unchecked((short)Word(0x27423FA)), unchecked((short)Word(0x27423FC)));
                    NativeBattleVector targetOrigin = new(unchecked((short)Word(0x2742400)), unchecked((short)Word(0x2742402)), unchecked((short)Word(0x2742404)));
                    task = NativeBattleEffectNodeContext.Initialize(task, Read(task.ReadDword(0x10)), current.Effect.Parameters!, inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, source, singleSelectedTarget: profile.SingleSelectedTarget, dogOrigins: new(actorOrigin, targetOrigin));
                    Store(address, task);
                    SetActorBank(source, initialization.Publish());
                }
                _ = AdvanceActorContext(address);
                Store(address, Next(Read(address)));
            }
            else if (Stage(task) == 1)
            {
                if (AdvanceActorContext(address))
                {
                    Store(address, Next(Read(address).WriteByte(0x26, (byte)(Read(address).ReadByte(0x26) | 1))));
                }
            }
            else if (Stage(task) != 2)
            {
                throw new NotSupportedException("Native Death actor task reached an unproved state.");
            }
        }

        private bool AdvanceActorContext(uint address)
        {
            (bool complete, ushort emitters, ushort geometry) = NativeBattleEffectNodeLifecycle.AdvanceContext(() => Read(address), task => Store(address, task), Draw);
            SetState(State with { PassEmitters = unchecked((ushort)(State.PassEmitters + emitters)), PassGeometry = unchecked((ushort)(State.PassGeometry + geometry)) });
            return complete;

            void Draw()
            {
                uint source = Read(address).ReadDword(0x30);
                using NativeBattleEffectNodeMemory memory = new(current.Memory, current.Effect with { Pools = hierarchy.Pools }, address, State.Emitters ?? throw new NotSupportedException("Native Death emitter work is uninitialized."), 0x870, State.Geometry ?? throw new NotSupportedException("Native Death geometry work is uninitialized."), 0x9D80, useSharedScratch: true);
                NativeBattleNodeSource actorBank = NativeBattleTaskOperation.AdmitCurrentRetainedMemoryStorage(ActorBank(source));
                using NativeBattleEffectNodeBank bank = new(source, actorBank, ReadExternal);
                ushort emitterScan = State.EmitterScan;
                ushort geometryScan = State.GeometryScan;
                NativeBattleEffectNodeInputs nodeInputs = new((current.Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Native Death requires its actual copied camera.")).ReadMatrix(), inputs.ReadModel, (parent, actor, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, State.Emitters!.Value, 20, ref emitterScan, parent, actor, target), (emitter, actor) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, State.Geometry!.Value, 60, ref geometryScan, emitter, actor));
                NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, nodeInputs);
                NativeCrtRandomState random = current.Random;
                bool releaseMesh = false;
                _ = lifecycle.AdvanceAndDraw(ref random, inputs.Projection, inputs.CameraRoll, PrepareMesh, Deliver, current.Effect.Binding.Effect == 14 ? NativeBattlePilotVisualLifetimes.Identity(14, Read(address)) : null, PreparePilotMesh);
                NativeBattleEffectNodeMemoryWrite flushed = memory.FlushStorage();
                current = current with { Memory = flushed.Memory, Effect = flushed.Effect, Random = random };
                hierarchy = new(current.Effect.Pools);
                bank.Publish();
                SetActorBank(source, bank.Source);
                SetState(State with { EmitterScan = emitterScan, GeometryScan = geometryScan });

                NativeBattleEffectNodeMeshInstance PrepareMesh(int node, byte[] descriptor)
                {
                    NativeBattleEffectNodePilotMeshInstance emit = PreparePilotMesh(node, descriptor);
                    return (bytes, matrix) => emit(bytes, matrix, default);
                }

                NativeBattleEffectNodePilotMeshInstance PreparePilotMesh(int node, byte[] descriptor)
                {
                    current = current with { Memory = memory.CaptureProcessMemory() };
                    short fade = unchecked((short)memory.Word(node + 0x1CE));
                    uint meshAddress = memory.Dword(node + 0x170);
                    current = NativeBattleDeathNodeDrawing.Begin(current, meshAddress, BinaryPrimitives.ReadInt16LittleEndian(descriptor.AsSpan(0x3A)), fade, fade == 0 ? null : memory.Dword(node + 0x16C));
                    memory.PublishProcessMemory(current.Memory);
                    int count = unchecked((sbyte)memory.Byte(node + 0x1D8));
                    int instance = 0;
                    if (count <= 0)
                    {
                        current = NativeBattleDeathNodeDrawing.End(current);
                        memory.PublishProcessMemory(current.Memory);
                    }
                    return (bytes, matrix, identity) =>
                    {
                        NativeBattleEffectNodeMemoryWrite live = memory.FlushStorage();
                        (byte[] padding, bool[] known) = live.Memory.Arena!.ReadSnapshot(node + 0xAC + 18, 2);
                        short? matrixPadding = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(padding) : null;
                        NativeBattleDeathNodeMeshStep emitted = NativeBattleDeathNodeDrawing.Emit(current with { Memory = memory.CaptureProcessMemory() }, bytes, meshAddress, matrix, inputs.Projection, inputs.Colors, matrixPadding, current.Effect.Binding.Effect == 14 ? identity : null);
                        current = emitted.Frame;
                        releaseMesh = ++instance == count;
                        memory.PublishProcessMemory(current.Memory);
                        return emitted.Batch;
                    };
                }

                void Deliver(bool meshes, NativeBattleGfGeometryBatch batch)
                {
                    current = current with { Memory = memory.CaptureProcessMemory() };
                    bank.Publish();
                    SetActorBank(source, bank.Source);
                    Emit(new NativeBattleDeathPackets(batch));
                    if (releaseMesh)
                    {
                        current = NativeBattleDeathNodeDrawing.End(current);
                        releaseMesh = false;
                    }
                    memory.PublishProcessMemory(current.Memory);
                }
            }
        }

        private NativeBattleNodeSource ActorBank(uint source)
        {
            if (source == profile.AuraAddress)
            {
                return State.AuraActors ?? throw new NotSupportedException("Native Death family requires its retained aura source.");
            }
            if (source == profile.DarkAddress)
            {
                return State.DarkActors ?? throw new NotSupportedException("Native Death family requires its retained dark source.");
            }
            throw new NotSupportedException("Native Death family source leaves its addressed node banks.");
        }

        private void SetActorBank(uint source, NativeBattleNodeSource bytes)
        {
            if (source == profile.AuraAddress)
            {
                SetState(State with { AuraActors = bytes });
            }
            else if (source == profile.DarkAddress)
            {
                SetState(State with { DarkActors = bytes });
            }
            else
            {
                throw new NotSupportedException("Native Death family store leaves its addressed node banks.");
            }
        }

        private byte[] ReadExternal(uint address, int count)
        {
            if (address < profile.ModuleBase || count < 0 || (ulong)address + (uint)count > (ulong)profile.ModuleBase + (uint)profile.ModuleLength)
            {
                throw new NotSupportedException("Native Death curve leaves its retained executable module owner.");
            }
            return State.Module.Read(checked((int)(address - profile.ModuleBase)), count);
        }
    }
}
