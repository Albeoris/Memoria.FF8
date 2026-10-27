using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Shell's typed actor owner over the shared original node birth/update/draw contracts.</summary>
public static class NativeBattleShellLifecycle
{
    public static (NativeBattleShellFrame Frame, bool Complete) Advance(NativeBattleShellFrame frame, uint address, NativeBattleShellInputs inputs)
    {
        NativeBattleShellFrame current = frame;
        NativeBattleShellState state = current.Memory.Shell33 ?? throw new NotSupportedException("Native Shell requires its retained actor owner.");
        state = state with { ContextTask = address };
        current = current with { Memory = current.Memory with { Shell33 = state } };
        var step = NativeBattleEffectNodeLifecycle.AdvanceContext(Read, Write, Draw);
        state = current.Memory.Shell33!;
        state = state with { PassEmitters = unchecked((ushort)(state.PassEmitters + step.EmitterCount)), PassGeometry = unchecked((ushort)(state.PassGeometry + step.GeometryCount)) };
        return (current with { Memory = current.Memory with { Shell33 = state } }, step.Complete);

        NativeBattleModulePoolTask Read() => new NativeBattleEffectHierarchy(current.Effect.Pools).Read(address);
        void Write(NativeBattleModulePoolTask task)
        {
            NativeBattleEffectHierarchy hierarchy = new(current.Effect.Pools);
            current = current with { Effect = current.Effect with { Pools = hierarchy.Write(address, task).Pools } };
        }
        void Draw()
        {
            NativeBattleShellState owner = current.Memory.Shell33!;
            if (owner.PendingColdActorBank || owner.ActorBank.Length != NativeBattleShellResources.ActorBankLength || owner.Channels.Length != NativeBattleShellResources.ChannelLength)
            {
                throw new NotSupportedException("Native Shell actor context requires its initialized addressed resource and curve owners.");
            }
            int emitters = owner.Emitters ?? throw new NotSupportedException("Native Shell requires its cleared emitter allocation.");
            int geometry = owner.Geometry ?? throw new NotSupportedException("Native Shell requires its cleared geometry allocation.");
            using NativeBattleEffectNodeMemory memory = new(current.Memory, current.Effect, address, emitters, 0x654, geometry, 0x8340);
            NativeBattleEffectNodeBank bank = new(NativeBattleShellResources.ActorBankAddress, owner.ActorBank, ReadExternal);
            NativeBattleModulePoolTask context = Read();
            NativeBattleEffectNodeInputs nodeInputs = new(inputs.Camera, inputs.ReadModel, AllocateEmitter, AllocateGeometry)
            {
                ActorScale = new(unchecked((int)context.ReadDword(0x34 + 0x214)), unchecked((int)context.ReadDword(0x34 + 0x218)), unchecked((int)context.ReadDword(0x34 + 0x21C)))
            };
            NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, nodeInputs);
            NativeCrtRandomState random = current.Random;
            NativeBattleGfGeometryBatch batch = lifecycle.AdvanceAndDraw(ref random, inputs.Projection, inputs.CameraRoll, (_, _) => throw new NotSupportedException("Native Shell's authenticated actor descriptors are sprite-only."));
            NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
            owner = owner with { ActorBank = bank.Data };
            current = current with { Memory = written.Memory with { Shell33 = owner }, Effect = written.Effect, Random = random };
            current = inputs.Execute(current, new NativeBattleShellPackets(batch));

            int? AllocateEmitter(uint parent, sbyte source, sbyte target)
            {
                ushort cursor = owner.EmitterScan;
                int? result = NativeBattleEffectNodeAllocation.AllocateEmitter(memory, emitters, 15, ref cursor, parent, source, target);
                owner = owner with { EmitterScan = cursor };
                return result;
            }
            int? AllocateGeometry(int emitter, sbyte source)
            {
                ushort cursor = owner.GeometryScan;
                int? result = NativeBattleEffectNodeAllocation.AllocateGeometry(memory, geometry, 50, ref cursor, emitter, source);
                owner = owner with { GeometryScan = cursor };
                return result;
            }
            byte[] ReadExternal(uint pointer, int count)
            {
                if (count < 0 || pointer < NativeBattleShellResources.ChannelAddress || pointer - NativeBattleShellResources.ChannelAddress > owner.Channels.Length || count > owner.Channels.Length - (pointer - NativeBattleShellResources.ChannelAddress))
                {
                    throw new InvalidDataException("Native Shell curve read leaves its authenticated module channels.");
                }
                return owner.Channels.AsSpan().Slice(checked((int)(pointer - NativeBattleShellResources.ChannelAddress)), count).ToArray();
            }
        }
    }

    public static NativeBattleShellFrame InitializeContext(NativeBattleShellFrame frame, uint address, NativeBattleShellInputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        NativeBattleShellState state = frame.Memory.Shell33 ?? throw new NotSupportedException("Native Shell lost its actor bank.");
        int directory = checked((int)(NativeBattleShellResources.ActorDirectory - NativeBattleShellResources.ActorBankAddress));
        if (BinaryPrimitives.ReadUInt32LittleEndian(state.ActorBank.AsSpan().Slice(directory)) == 0)
        {
            byte[] relocated = NativeBattleEffectNodeBankFixups.Relocate(state.ActorBank, NativeBattleShellResources.ActorBankAddress, directory);
            BinaryPrimitives.WriteUInt32LittleEndian(relocated.AsSpan(directory), 1);
            state = state with { ActorBank = relocated.ToImmutableArray() };
        }
        task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(0x10)), frame.Effect.Parameters ?? throw new NotSupportedException("Native Shell requires its action context."), inputs.Packets, inputs.ReadModel, inputs.ReadPoseSource, NativeBattleShellResources.ActorDirectory);
        task = task.WriteDword(0x34 + 0x214, 4096).WriteDword(0x34 + 0x218, 4096).WriteDword(0x34 + 0x21C, 4096);
        hierarchy = hierarchy.Write(address, task);
        return frame with { Memory = frame.Memory with { Shell33 = state with { ContextTask = address } }, Effect = frame.Effect with { Pools = hierarchy.Pools } };
    }
}
