using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleDuel274Frame Frame, NativeBattleGraphicsState Graphics) DrawNativeDuel274Echo(NativeBattleDuel274Frame frame, NativeBattleGraphicsState graphics, int actor, uint echo, NativeBattleActionMatrix view, NativeBattleCameraProjection projection)
    {
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native Duel echo requires its actual CPU resource owner.");
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattleResourcePointer pointer;
        if (model.ActiveStagedResourceRow100 is uint row)
        {
            uint address = checked(row + 4);
            if (!NativeBattleEffectTaskMemory.TryReadPointer(frame.Effect.Pools, address, out NativeBattleResourcePointer? retained))
            {
                retained = IsNativeEffectArenaAddress(address) ? (frame.Memory.Arena ?? throw new NotSupportedException("Native Duel echo requires its actual arena row.")).ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : frame.Staging.ReadResourcePointer(address);
            }
            pointer = retained ?? throw new NotSupportedException("Native Duel echo requires its actual selected geometry pointer.");
        }
        else
        {
            NativeBattleModelCpuResourceRow selected = cpu.Rows[model.ActiveResourceRow100 ?? throw new NotSupportedException("Native Duel echo requires its actual selected CPU resource row.")];
            pointer = new(selected.Require(16), null, selected.Lengths.TryGetValue(16, out int length) ? length : null);
        }
        uint section = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Native Duel echo requires its physical geometry source.");
        ushort maximum = cpu.ScratchMaximum ?? throw new NotSupportedException("Native Duel echo requires its actually loaded scratch maximum.");
        NativeBattleScriptedGraphicsMemory registers = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Duel echo requires its current GTE registers.");
        frame = NativeBattleDuel274Echo.Draw68A7B0(frame, model, echo, section, Read, maximum, view, projection, Submit, registers.Zsf3 ?? throw new NotSupportedException("Native Duel echo requires its actual ZSF3."), registers.Zsf4 ?? throw new NotSupportedException("Native Duel echo requires its actual ZSF4."));
        return (frame, graphics);

        byte[] Read(NativeBattleDuel274Frame current, int offset, int length)
        {
            if (offset < 0 || length < 0 || pointer.Length is int limit && offset > limit - length)
            {
                throw new InvalidDataException("Native Duel echo read leaves its actual geometry extent.");
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
            return IsNativeEffectArenaAddress(physical) ? (current.Memory.Arena ?? throw new NotSupportedException("Native Duel echo requires its physical arena source.")).Read(NativeBattleWaterExecutionFields.ArenaOffset(physical, length), length) : current.Staging.Read(physical, length);
        }
        NativeBattleDuel274Frame Submit(NativeBattleDuel274Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Duel echo requires its current ordering table.");
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Duel echo requires its current COMMON packet owner.");
            NativeBattlePrimitiveReference start = NativeBattleDuel28Presentation.Start(process);
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, batch);
            current = current with { Effect = current.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(current.Effect.PresentationPackets, start.Bank, start.Buffer, start.ByteOffset, batch) } };
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }
    }
}
