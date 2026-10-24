using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect210Frame DrawNativeEffect210Echo(ref NativeBattleGraphicsState graphics, NativeBattleEffect210Frame frame, NativeBattleEffect210DrawEcho7EE0 operation)
    {
        NativeBattleGraphicsState drawingGraphics = graphics;
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect210 echo requires its current CPU resources.");
        NativeBattleGraphicsModel model = graphics.Models[operation.Actor];
        NativeBattleResourcePointer pointer;
        if (model.ActiveStagedResourceRow100 is uint row)
        {
            uint address = checked(row + 4);
            if (!NativeBattleEffectTaskMemory.TryReadPointer(frame.Effect.Pools, address, out NativeBattleResourcePointer? retained))
            {
                retained = IsNativeEffectArenaAddress(address) ? (frame.Memory.Arena ?? throw new NotSupportedException("Effect210 echo requires its current arena row.")).ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : frame.Staging!.ReadResourcePointer(address);
            }
            pointer = retained ?? throw new NotSupportedException("Effect210 echo requires its selected geometry pointer.");
        }
        else
        {
            NativeBattleModelCpuResourceRow selected = cpu.Rows[model.ActiveResourceRow100 ?? throw new NotSupportedException("Effect210 echo requires its current CPU resource row.")];
            pointer = new(selected.Require(16), null, selected.Lengths.TryGetValue(16, out int length) ? length : null);
        }
        uint section = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect210 echo requires its physical geometry source.");
        ushort maximum = cpu.ScratchMaximum ?? throw new NotSupportedException("Effect210 echo requires its actual projected capacity.");
        NativeBattleScriptedGraphicsMemory registers = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect210 echo requires its current depth registers.");
        uint taskAddress = checked(operation.HeaderAddress - 0x60);
        frame = NativeBattleDuelEchoDrawing.Draw7EE0(frame, model, operation.HeaderAddress, 0x60, current => NativeBattleEffect210Controller.Read(current, taskAddress), (current, task, _) => NativeBattleEffect210Controller.Write(current, taskAddress, task), section, Read, maximum, (NativeCamera ?? throw new NotSupportedException("Effect210 echo requires its camera.")).CapturePackedView1D97778().ReadMatrix(), NativeCamera.Projection ?? throw new NotSupportedException("Effect210 echo requires its current projection."), (current, address, bytes) => current with { Staging = current.Staging!.Write(address, bytes.AsSpan()) }, (current, address, length) => current.Staging!.ReadSnapshot(address, length), Submit, registers.Zsf3 ?? throw new NotSupportedException("Effect210 echo requires ZSF3."), registers.Zsf4 ?? throw new NotSupportedException("Effect210 echo requires ZSF4."), current => current.Staging!, (current, staging) => current with { Staging = staging }, frame.State.ReadDword(FrozenGlobal) != 0);
        graphics = drawingGraphics;
        return frame;

        byte[] Read(NativeBattleEffect210Frame current, int offset, int length)
        {
            if (offset < 0 || length < 0 || pointer.Length is int limit && offset > limit - length)
            {
                throw new InvalidDataException("Effect210 echo leaves its current geometry extent.");
            }
            if (pointer.Cpu is { } source)
            {
                return NativeBattleModelEnemyHeap.Read(cpu, current.Staging ?? throw new NotSupportedException("Effect210 echo requires its current CPU memory."), source with { Offset = checked(source.Offset + offset) }, length);
            }
            uint physical = checked(pointer.StagingAddress!.Value + (uint)offset);
            if (NativeBattleEffectTaskMemory.TryRead(current.Effect.Pools, physical, length, out byte[] bytes))
            {
                return bytes;
            }
            return IsNativeEffectArenaAddress(physical) ? (current.Memory.Arena ?? throw new NotSupportedException("Effect210 echo requires its current arena source.")).Read(NativeBattleWaterExecutionFields.ArenaOffset(physical, length), length) : current.Staging!.Read(physical, length);
        }

        NativeBattleEffect210Frame Submit(NativeBattleEffect210Frame current, NativeBattleGfGeometryBatch batch) => DeliverNativeEffect210Batch(ref drawingGraphics, current, new(batch, CursorGlobal));
    }
}
