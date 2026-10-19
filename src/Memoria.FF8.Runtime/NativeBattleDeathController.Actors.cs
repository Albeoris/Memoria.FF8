using System.Buffers.Binary;
using System.Collections.Immutable;

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
                ImmutableArray<byte> bank = ActorBank(source);
                if (BinaryPrimitives.ReadUInt32LittleEndian(bank.AsSpan()) == 0)
                {
                    byte[] fixedBytes = NativeBattleEffectNodeBankFixups.Relocate(bank, source);
                    BinaryPrimitives.WriteUInt32LittleEndian(fixedBytes, 1);
                    SetActorBank(source, fixedBytes.ToImmutableArray());
                }
                NativeBattleVector actorOrigin = new(unchecked((short)Word(0x27423F8)), unchecked((short)Word(0x27423FA)), unchecked((short)Word(0x27423FC)));
                NativeBattleVector targetOrigin = new(unchecked((short)Word(0x2742400)), unchecked((short)Word(0x2742402)), unchecked((short)Word(0x2742404)));
                task = NativeBattleEffectNodeContext.Initialize(task, Read(task.ReadDword(0x10)), current.Effect.Parameters!, inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, source, dogOrigins: new(actorOrigin, targetOrigin));
                Store(address, task);
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
                NativeBattleEffectNodeMemory memory = new(current.Memory, current.Effect with { Pools = hierarchy.Pools }, address, State.Emitters ?? throw new NotSupportedException("Native Death emitter work is uninitialized."), 0x870, State.Geometry ?? throw new NotSupportedException("Native Death geometry work is uninitialized."), 0x9D80, useSharedScratch: true);
                NativeBattleEffectNodeBank bank = new(source, ActorBank(source), ReadExternal);
                ushort emitterScan = State.EmitterScan;
                ushort geometryScan = State.GeometryScan;
                NativeBattleEffectNodeInputs nodeInputs = new(inputs.Projection.View ?? throw new NotSupportedException("Native Death requires its current camera copy."), inputs.ReadModel, (parent, actor, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, State.Emitters!.Value, 20, ref emitterScan, parent, actor, target), (emitter, actor) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, State.Geometry!.Value, 60, ref geometryScan, emitter, actor));
                NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, nodeInputs);
                NativeCrtRandomState random = current.Random;
                NativeBattleGfGeometryBatch batch = lifecycle.AdvanceAndDraw(ref random, inputs.Projection, inputs.CameraRoll, PrepareMesh);
                NativeBattleEffectNodeMemoryWrite flushed = memory.FlushStorage();
                current = current with { Memory = flushed.Memory, Effect = flushed.Effect, Random = random };
                hierarchy = new(current.Effect.Pools);
                SetActorBank(source, bank.Data);
                SetState(State with { EmitterScan = emitterScan, GeometryScan = geometryScan });
                Emit(new NativeBattleDeathPackets(batch));

                NativeBattleEffectNodeMeshInstance PrepareMesh(int node, byte[] descriptor)
                {
                    short fade = unchecked((short)memory.Word(node + 0x1CE));
                    uint flags = BinaryPrimitives.ReadInt16LittleEndian(descriptor.AsSpan(0x3A)) == 0 ? 0x30u : 0;
                    if (fade != 0)
                    {
                        flags |= 0xC0;
                    }
                    NativeBattleEsunaMeshContext context = new(flags, fade == 0 ? null : memory.Dword(node + 0x16C), fade == 0 ? null : fade, 0, 0, 0, new(0, 0, 256, 256), new(0, 0, 256, 256), 17, 2);
                    return (bytes, matrix) => NativeBattleRestorationMeshPackets.EmitBatch88E240(bytes, matrix, inputs.Projection, context, inputs.Colors, observeFirstThreeFlags: memory.WriteProjectionFlags);
                }
            }
        }

        private ImmutableArray<byte> ActorBank(uint source) => source switch
        {
            0x1801FD0 => State.AuraActors ?? throw new NotSupportedException("Native Death requires its retained aura source."),
            0x18069C4 => State.DarkActors ?? throw new NotSupportedException("Native Death requires its retained dark source."),
            _ => throw new NotSupportedException("Native Death actor source leaves its original addressed banks.")
        };

        private void SetActorBank(uint source, ImmutableArray<byte> bytes)
        {
            SetState(source == 0x1801FD0 ? State with { AuraActors = bytes } : source == 0x18069C4 ? State with { DarkActors = bytes } : throw new NotSupportedException("Native Death actor store leaves its original addressed banks."));
        }

        private byte[] ReadExternal(uint address, int count)
        {
            if (address < ModuleBase || count < 0 || (ulong)address + (uint)count > 0x27428A0)
            {
                throw new NotSupportedException("Native Death curve leaves its retained executable module owner.");
            }
            return State.Module.Read(checked((int)(address - ModuleBase)), count);
        }
    }
}
