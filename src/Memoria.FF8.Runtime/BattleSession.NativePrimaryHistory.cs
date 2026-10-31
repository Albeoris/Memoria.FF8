using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleGraphicsState Graphics) DrawNativePrimaryHistory76(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleFileStaging? currentStaging, NativeBattleGraphicsState graphics, int actor, NativeBattlePilotVisualIdentity? callerPilotIdentity = null)
    {
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Primary body history requires its current skeleton.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Primary body history requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Primary body history requires its current projection.");
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Primary body history requires its current CPU resources.");
        int capacity = checked((cpu.ScratchMaximum ?? throw new NotSupportedException("Primary body history requires its current projected workspace capacity.")) * 8);
        uint scratch = checked(0x196B018u - (uint)capacity);
        NativeBattleFileStaging staging = currentStaging ?? throw new NotSupportedException("Primary body history requires its current temporary owner.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 76);
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Primary body history requires its current world matrix.");
        NativeBattlePackedMatrix32 composed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(camera.CapturePackedView1D97778().ReadMatrix(), world), null);
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model, composed.ReadMatrix());
        graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
        uint color = model.Color40 ?? throw new NotSupportedException("Primary body history requires its current packed model color.");
        uint mask = model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Primary body history requires its current object mask.");
        NativeBattleEffectArena temporary = NativeDeformedBodyView(allocation.Staging, allocation.Address, 76);
        temporary = temporary.Write(0, composed.Bytes.AsSpan().Slice(0, 18)).Write(20, composed.Bytes.AsSpan().Slice(20, 12));
        temporary = temporary.Write(0x24, BitConverter.GetBytes(scratch)).Write(0x34, new byte[4]).Write(0x38, BitConverter.GetBytes((ushort)320)).Write(0x3A, BitConverter.GetBytes((ushort)216)).Write(0x3C, BitConverter.GetBytes(color)).Write(0x40, BitConverter.GetBytes(mask));
        NativeBattleEffectArena modelBytes = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
        if (model.Alpha7 is byte alpha)
        {
            modelBytes = modelBytes.Write(7, new byte[] { alpha });
        }
        for (int offset = 0x48; offset <= 0x4A; offset++)
        {
            temporary = NativeBattleEffectNodeContextMemory.Copy(modelBytes, 7, temporary, offset, 1);
        }
        temporary = NativeBattleEffectNodeContextMemory.Copy(NativeDeformedBodyView(allocation.Staging, 0x1D969A8, 4), 0, temporary, 0x30, 4);
        staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 76);
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Primary body history requires its current COMMON process.");
        var gate = process.Enter5099D0(definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Primary body history requires its current renderer capability."));
        process = gate.Process;
        if (gate.Draw)
        {
            NativeBattleResourcePointer pointer = GeometryPointer();
            ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(Read, source.BoneCount);
            NativeBattlePilotVisualIdentity? pilotIdentity = callerPilotIdentity is { } identity ? identity with { ResourceGeneration = unchecked(identity.ResourceGeneration * 397 ^ (pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? 0)) } : null;
            process = process.PublishModelColor(color);
            (byte[] retained, _) = staging.ReadSnapshot(scratch, capacity);
            NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(geometry, NativeBattleBodyBoneMatrices.Require(model, source), projection, color, mask, retained.ToImmutableArray(), definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Primary body history requires its color arithmetic."), 4, new(0, 0, 320, 216), 17, pilotIdentity: pilotIdentity, pilotContinuous: callerPilotIdentity?.Effect != 174);
            if (callerPilotIdentity?.Effect == 174)
            {
                drawn = drawn with { Packets = drawn.Packets with { Attempts = drawn.Packets.Attempts.Select(attempt => attempt.Primitive is { } polygon ? attempt with { Primitive = NativeBattleEffect174Presentation.PreserveNativeDepths(polygon) } : attempt).ToImmutableArray() } };
            }
            if (drawn.ProjectedBytesWritten != 0)
            {
                staging = staging.Write(scratch, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten));
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Primary body history requires its current ordering table.");
            if (callerPilotIdentity is { Effect: 324 } && graphics.OriginalEffect is { Binding.Effect: 324 } selected)
            {
                drawn = drawn with { Packets = NativeBattleActor33Presentation.Depths(drawn.Packets) };
                selected = selected with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(selected.PresentationPackets, process.PacketBank!, process.PacketBuffer, process.ByteCursor!.Value, drawn.Packets) };
                graphics = graphics with { OriginalEffect = selected };
            }
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, drawn.Packets);
            process = written.Process.Publish50FDF0(CommonAddress(written.Process), null);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };

            byte[] Read(int offset, int length)
            {
                if (pointer.Length is int available && (offset < 0 || length < 0 || offset > available - length))
                {
                    throw new InvalidDataException("Primary body history geometry read leaves its current resource.");
                }
                if (pointer.Cpu is { } cpuPointer)
                {
                    return NativeBattleModelEnemyHeap.Read(cpu, staging, cpuPointer with { Offset = checked(cpuPointer.Offset + offset) }, length);
                }
                uint address = checked((pointer.StagingAddress ?? throw new NotSupportedException("Primary body history requires its addressed geometry.")) + (uint)offset);
                if (NativeBattleEffectTaskMemory.TryRead(effect.Pools, address, length, out byte[] bytes))
                {
                    return bytes;
                }
                return IsNativeEffectArenaAddress(address) ? memory.Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, length), length) : staging.Read(address, length);
            }
        }
        return (memory with { PrimitiveProcess = process }, NativeBattleTemporaryMemory.Release5082D0(staging, 76), graphics);

        NativeBattleResourcePointer GeometryPointer()
        {
            if (model.ActiveStagedResourceRow100 is uint row)
            {
                uint address = checked(row + 4);
                if (NativeBattleEffectTaskMemory.TryReadPointer(effect.Pools, address, out NativeBattleResourcePointer? selected))
                {
                    return selected ?? throw new NotSupportedException("Primary body history geometry pointer is NULL.");
                }
                return (IsNativeEffectArenaAddress(address) ? memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : staging.ReadResourcePointer(address)) ?? throw new NotSupportedException("Primary body history geometry pointer is unobserved.");
            }
            NativeBattleModelCpuResourceRow selectedRow = cpu.Rows[model.ActiveResourceRow100 ?? throw new NotSupportedException("Primary body history requires its current CPU row.")];
            return new(selectedRow.Require(16), null, selectedRow.Lengths.TryGetValue(16, out int length) ? length : null);
        }

        static uint CommonAddress(NativeBattlePrimitiveProcess current)
        {
            uint bank = current.PacketBank switch { "off_B6D070" => 0x1901018, "off_B6D094" => 0x199B018, _ => throw new NotSupportedException("Primary body history requires its actual COMMON packet bank.") };
            return checked(bank + (uint)current.PacketBuffer * (uint)current.CommonPacketNominalCapacity + (uint)(current.ByteCursor ?? throw new NotSupportedException("Primary body history requires its actual COMMON cursor.")));
        }
    }
}
