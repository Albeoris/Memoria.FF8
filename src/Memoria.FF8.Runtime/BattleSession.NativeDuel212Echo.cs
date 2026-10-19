using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleDuel212Frame Frame, NativeBattleGraphicsState Graphics) DrawNativeDuel212Echo(NativeBattleDuel212Frame frame, NativeBattleGraphicsState graphics, int actor, NativeBattleActionMatrix view, NativeBattleCameraProjection projection)
    {
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native Burning Rave echo requires its actual CPU resources.");
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattleResourcePointer pointer;
        if (model.ActiveStagedResourceRow100 is uint row)
        {
            uint address = checked(row + 4);
            if (!NativeBattleEffectTaskMemory.TryReadPointer(frame.Effect.Pools, address, out NativeBattleResourcePointer? retained))
            {
                retained = IsNativeEffectArenaAddress(address) ? frame.Memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : frame.Staging.ReadResourcePointer(address);
            }
            pointer = retained ?? throw new NotSupportedException("Native Burning Rave echo requires its actual selected geometry pointer.");
        }
        else
        {
            NativeBattleModelCpuResourceRow selected = cpu.Rows[model.ActiveResourceRow100 ?? throw new NotSupportedException("Native Burning Rave echo requires its actual CPU row.")];
            pointer = new(selected.Require(16), null, selected.Lengths.TryGetValue(16, out int length) ? length : null);
        }
        uint section = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Native Burning Rave echo requires its physical geometry source.");
        ushort maximum = cpu.ScratchMaximum ?? throw new NotSupportedException("Native Burning Rave echo requires its actually loaded CPU maximum.");
        NativeBattleScriptedGraphicsMemory registers = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Burning Rave echo requires its actual GTE registers.");
        NativeBattleEffectTaskPool target = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDuel212Controller.Targets);
        uint echo = checked(target.BankAddress + 336);
        frame = NativeBattleDuelEchoDrawing.Draw6A2760(frame, model, echo, 336, ReadEcho, StoreEcho, section, Read, maximum, view, projection, (current, address, bytes) => current with { Staging = current.Staging.Write(address, bytes.AsSpan()) }, (current, address, length) => current.Staging.ReadSnapshot(address, length), Submit, registers.Zsf3 ?? throw new NotSupportedException("Native Burning Rave echo requires its actual ZSF3."), registers.Zsf4 ?? throw new NotSupportedException("Native Burning Rave echo requires its actual ZSF4."), current => current.Staging, (current, staging) => current with { Staging = staging });
        return (frame, graphics);

        NativeBattleModulePoolTask ReadEcho(NativeBattleDuel212Frame current) => current.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDuel212Controller.Targets).Slots[0];
        NativeBattleDuel212Frame StoreEcho(NativeBattleDuel212Frame current, NativeBattleModulePoolTask written, IReadOnlyList<(int Offset, int Length)> ranges)
        {
            NativeBattleEffectTaskPool pool = current.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDuel212Controller.Targets);
            NativeBattleModulePoolTask task = pool.Slots[0];
            foreach ((int offset, int length) in ranges)
            {
                if (offset < 0 || length < 0 || offset > 1160 - length)
                {
                    throw new InvalidDataException("Native Burning Rave echo writes outside its actual inline owner.");
                }
                for (int index = 0; index < length; index++)
                {
                    int physical = 336 + offset + index;
                    bool known = written.UnknownPayloadRanges?.Any(range => range.Offset <= physical && physical < range.Offset + range.Length) != true;
                    task = known ? task.WriteByte(physical, written.ReadByte(physical)) : task.MarkPayloadUnknown(physical, 1);
                }
            }
            pool = pool with { Slots = pool.Slots.SetItem(0, task) };
            return current with { Effect = current.Effect with { Pools = current.Effect.Pools.Select(bank => bank.DescriptorAddress == pool.DescriptorAddress ? pool : bank).ToImmutableArray() } };
        }
        byte[] Read(NativeBattleDuel212Frame current, int offset, int length)
        {
            if (offset < 0 || length < 0 || pointer.Length is int limit && offset > limit - length)
            {
                throw new InvalidDataException("Native Burning Rave echo read leaves its actual source extent.");
            }
            if (pointer.Cpu is { } source)
            {
                return NativeBattleModelEnemyHeap.Read(cpu, current.Staging, source with { Offset = checked(source.Offset + offset) }, length);
            }
            uint physical = checked(pointer.StagingAddress!.Value + (uint)offset);
            if (NativeBattleEffectTaskMemory.TryRead(current.Effect.Pools, physical, length, out byte[] bytes))
            {
                return bytes;
            }
            return IsNativeEffectArenaAddress(physical) ? current.Memory.Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(physical, length), length) : current.Staging.Read(physical, length);
        }
        NativeBattleDuel212Frame Submit(NativeBattleDuel212Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Burning Rave echo requires its current ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Burning Rave echo requires its COMMON packet owner."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }
    }
}
