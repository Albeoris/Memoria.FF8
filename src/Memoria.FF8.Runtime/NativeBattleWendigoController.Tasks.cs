using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleWendigoController
{
    private sealed partial class Execution
    {
        private void Run(uint address)
        {
            uint callback = Read(address).Callback;
            if (callback == (shoot ? 0x83F020u : 0x846900u))
            {
                Emitter(address);
            }
            else if (callback == (shoot ? 0x83F170u : 0x846A20u))
            {
                Actors(address);
            }
            else if (callback == (shoot ? 0x843FC0u : 0x84B8C0u))
            {
                NativeBattleModulePoolTask task = Read(address);
                if (Stage(task) == 0 && Signed(task, 0x24) >= 8)
                {
                    Append(3, shoot ? 0x844050u : 0x84B950u, TaskSize, address);
                    Store(address, Next(Read(address)));
                }
                else if (Stage(task) == 1)
                {
                    Store(address, Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
                }
            }
            else if (callback == (shoot ? 0x844050u : 0x84B950u))
            {
                if (shoot)
                {
                    ShootBall(address);
                }
                else
                {
                    DribbleBall(address);
                }
            }
            else if (callback == (shoot ? 0x8459A0u : 0x84C8E0u))
            {
                DustSpawner(address);
            }
            else if (callback == (shoot ? 0x845B30u : 0x84CA70u))
            {
                Dust(address);
            }
            else if (shoot && callback == 0x844C50)
            {
                BurstSpawner(address);
            }
            else if (shoot && callback == 0x844E30)
            {
                Burst(address);
            }
            else if (shoot && callback is 0x845F20 or 0x8462D0 or 0x846350)
            {
                Prim(address);
            }
            else
            {
                throw new NotSupportedException("Native Wendigo reached an unproved private task.");
            }
        }

        private void Emitter(uint address)
        {
            NativeBattleModulePoolTask task = NativeBattleEffectModelHelpers.UpdateTarget8DC610(Read(address), Enumerable.Range(0, 7).Select(inputs.ReadModel).ToArray(), inputs.ReadPoseSource);
            int actor = task.ReadByte(0x2D);
            NativeBattleSequenceState sequence = inputs.ReadModel(actor).SequenceState ?? throw new NotSupportedException("Native Wendigo requires its selected target's current pose.");
            NativeBattlePoseSource source = inputs.ReadPoseSource(actor);
            NativeBattleDecodedPose pose = sequence.DecodedPose ?? source.Sample(sequence.Pose ?? throw new NotSupportedException("Native Wendigo requires its decoded target pose."), sequence.PoseFrame114 ?? throw new NotSupportedException("Native Wendigo requires its target pose frame."), sequence.PoseFlags109);
            task = NativeBattleEffectModelHelpers.UpdateBounds8DC870(task, source, pose);
            Store(address, task);
            switch (Stage(task))
            {
                case 0:
                    if (shoot)
                    {
                        SpawnPrim(address, 0x845F20, resources.BallPrim ?? throw new NotSupportedException("Native Shoot requires its ball primitive layout."), 0x54, 0, 0);
                        SpawnPrim(address, 0x8462D0, resources.KickPrim ?? throw new NotSupportedException("Native Shoot requires its kick primitive layout."), 0x84, 1, 0);
                    }
                    uint context = Append(2, shoot ? 0x83F170u : 0x846A20u, 0x2A4, address);
                    Store(context, Read(context).WriteDword(0x30, resources.ActorDirectory).WriteWord(0x298, 15).WriteWord(0x29A, 0).WriteWord(0x29C, 0).WriteWord(0x29E, 45));
                    Append(3, shoot ? 0x843FC0u : 0x84B8C0u, TaskSize, address);
                    Store(address, Next(Read(address)));
                    break;
                case 1:
                    if (task.ReadWord(0x24) == (shoot ? 125 : 116))
                    {
                        uint root = task.ReadDword(0x10);
                        Store(root, Read(root).WriteByte(0x63, 0));
                        Store(address, Next(task));
                    }
                    break;
                case 2:
                    if (Signed(task, 0x24) >= (shoot ? 125 : 116))
                    {
                        NativeBattleActionHeaderSlot header = inputs.Packets.Headers[current.Effect.Parameters!.HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A))];
                        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Wendigo requires its actual action contact pointer.");
                        for (int index = 0; index < header.ReadByte(16); index++)
                        {
                            Emit(new NativeBattleWendigoContact(first + index));
                        }
                        Store(address, Next(Read(address).WriteByte(0x26, (byte)(Read(address).ReadByte(0x26) | 1))));
                    }
                    break;
                case 3:
                    break;
                default:
                    throw new NotSupportedException("Native Wendigo emitter reached an unproved state.");
            }
            if (task.ReadWord(0x24) == 17)
            {
                Emit(new NativeBattleWendigoSound(resources.GrabSound, 0));
            }
        }

        private void Actors(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            if (Stage(task) == 0)
            {
                if (Signed(task, 0x24) < Signed(task, 0x298))
                {
                    return;
                }
                NativeBattleNodeSource actorBank = State.ActorBank ?? throw new NotSupportedException("Native Wendigo requires its retained executable actor source.");
                int directory = checked((int)(resources.ActorDirectory - resources.ActorBankAddress));
                using (NativeBattleNodeInitialization<NativeBattleNodeSource> initialization = NativeBattleActorBankInitialization.Prepare(actorBank, resources.ActorBankAddress, directory))
                {
                    task = NativeBattleEffectNodeContext.Initialize(task, Read(task.ReadDword(0x10)), current.Effect.Parameters!, inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, resources.ActorDirectory);
                    Store(address, task);
                    SetState(State with { ActorBank = initialization.Publish() });
                }
                _ = AdvanceActors(address);
                Store(address, Next(Read(address)));
            }
            else if (Stage(task) == 1 && AdvanceActors(address))
            {
                Store(address, Next(Read(address).WriteByte(0x26, (byte)(Read(address).ReadByte(0x26) | 1))));
            }
        }

        private bool AdvanceActors(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            if (task.ReadWord(0x34 + 0x24) == 2)
            {
                throw new NotSupportedException("Native Wendigo actor mode2 requires its actual uninitialized prior stack VECTOR.");
            }
            int actor = Signed(task, 0x34 + 0x1E);
            NativeBattleVector a = Bone(actor, 0x18);
            NativeBattleVector b = Bone(actor, 0x15);
            a = a with { Z = unchecked((short)(a.Z + 256)) };
            b = b with { Z = unchecked((short)(b.Z + 256)) };
            task = Fixed(task, 0x34 + 0x114, a);
            task = Fixed(task, 0x34 + 0x154, b);
            for (int axis = 0; axis < 3; axis++)
            {
                uint first = task.ReadDword(0x34 + 0x114 + axis * 4);
                uint second = task.ReadDword(0x34 + 0x154 + axis * 4);
                task = task.WriteDword(0x34 + 0xD4 + axis * 4, unchecked((uint)(unchecked((int)(first + second)) / 2)));
            }
            if (!shoot)
            {
                task = Fixed(task, 0x34 + 0x204, a with { Y = 0 });
            }
            Store(address, task);
            var step = NativeBattleEffectNodeLifecycle.AdvanceContext(() => Read(address), value => Store(address, value), Draw);
            SetState(State with { PassEmitters = unchecked((ushort)(State.PassEmitters + step.EmitterCount)), PassGeometry = unchecked((ushort)(State.PassGeometry + step.GeometryCount)) });
            return step.Complete;

            void Draw()
            {
                NativeBattleWendigoState owner = State;
                int emitters = owner.Emitters ?? throw new NotSupportedException("Native Wendigo requires its cleared emitter pool.");
                int geometry = owner.Geometry ?? throw new NotSupportedException("Native Wendigo requires its cleared geometry pool.");
                using NativeBattleEffectNodeMemory memory = new(current.Memory, current.Effect with { Pools = hierarchy.Pools }, address, emitters, shoot ? 0x438 : 0x870, geometry, shoot ? 0x4EC0 : 0x8340, true);
                NativeBattleNodeSource actorBank = NativeBattleTaskOperation.AdmitCurrentRetainedMemoryStorage(owner.ActorBank ?? throw new NotSupportedException("Native Wendigo lost its actor data."));
                using NativeBattleEffectNodeBank bank = new(resources.ActorBankAddress, actorBank, ReadExternal);
                ushort emitterScan = owner.EmitterScan;
                ushort geometryScan = owner.GeometryScan;
                NativeBattleEffectNodeInputs nodeInputs = new(inputs.Projection.View, inputs.ReadModel, (parent, source, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, emitters, shoot ? 10 : 20, ref emitterScan, parent, source, target), (emitter, source) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, geometry, shoot ? 30 : 50, ref geometryScan, emitter, source));
                NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, nodeInputs);
                NativeCrtRandomState random = current.Random;
                NativeBattleGfGeometryBatch batch = lifecycle.AdvanceAndDraw(ref random, inputs.Projection, inputs.CameraRoll, Prepare, pilotIdentity: NativeBattlePilotVisualLifetimes.Identity(current.Effect.Binding.Effect, Read(address)), preparePilotMesh: PreparePilot);
                NativeBattleEffectNodeMemoryWrite flushed = memory.FlushStorage();
                current = current with { Memory = flushed.Memory, Effect = flushed.Effect, Random = random };
                hierarchy = new(current.Effect.Pools);
                bank.Publish();
                SetState(State with { ActorBank = bank.Source, EmitterScan = emitterScan, GeometryScan = geometryScan });
                Emit(new NativeBattleWendigoPackets(batch, false));

                NativeBattleEffectNodeMeshInstance Prepare(int node, byte[] descriptor)
                {
                    NativeBattleEffectNodePilotMeshInstance prepared = PreparePilot(node, descriptor);
                    return (bytes, matrix) => prepared(bytes, matrix, default);
                }
                NativeBattleEffectNodePilotMeshInstance PreparePilot(int node, byte[] descriptor)
                {
                    short fade = unchecked((short)memory.Word(node + 0x1CE));
                    uint flags = BinaryPrimitives.ReadUInt16LittleEndian(descriptor.AsSpan(0x3A)) == 0 ? 0x30u : 0;
                    if (fade != 0)
                    {
                        flags |= 0xC0;
                    }
                    NativeBattleCuragaMeshContext context = new(flags, fade == 0 ? null : memory.Dword(node + 0x16C), fade == 0 ? null : fade, null, null, null, 17, 2);
                    return (bytes, matrix, identity) => NativeBattleCuragaMeshPackets.EmitBatch87EEB0(bytes, matrix, inputs.Projection, context, colors: inputs.Colors, pilotIdentity: identity);
                }
            }
        }

        private byte[] ReadExternal(uint address, int count)
        {
            if (address < Base || count < 0 || (ulong)address + (uint)count > Base + (uint)ModuleLength(shoot))
            {
                return resources.ReadConstantChannel(address, count);
            }
            return State.Module.Read(checked((int)(address - Base)), count);
        }
        private static NativeBattleModulePoolTask Fixed(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteDword(offset, unchecked((uint)(value.X << 16))).WriteDword(offset + 4, unchecked((uint)(value.Y << 16))).WriteDword(offset + 8, unchecked((uint)(value.Z << 16)));
    }
}
