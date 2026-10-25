using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect251PointStep ReadNativeEffect251Bounds(ref NativeBattleGraphicsState graphics, NativeBattleEffect251Frame frame, uint address)
    {
        int actor = NativeEffect301Actor(graphics, address);
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect251 bounds require current physical memory.");
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect251 bounds require the current physical rig.");
        frame = frame with { Staging = NativeFileStaging };
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect251 bounds require the current model world matrix.");
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect251 bounds require the current actor transform.");
        _ = NativeBattleBodyBoneMatrices.Require(model, source);
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model with { GeometryWorldMatrix = world with { Y = 0 } }, world with { Y = 0 });
        model = model with { GeometryWorldMatrix = world with { Y = transform.Position28.Y } };
        graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, source);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Effect251 bounds require their actual temporary owner."), 24);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 24);
        uint visibility = model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Effect251 bounds require the current mesh visibility mask.");
        temporary = temporary.Write(0, BitConverter.GetBytes(65536)).Write(4, BitConverter.GetBytes(-65536)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes((ushort)4096)).Write(20, BitConverter.GetBytes(visibility));
        frame = frame with { Staging = allocation.Staging };
        NativeDeformedBodyBridge bridge = new(frame.Memory, frame.Effect, frame.Random, graphics, model, allocation.Staging, temporary, allocation.Address);
        NativeBattleResourcePointer geometry = NativeDeformedGeometryPointer(bridge, false);
        int ReadInt(int offset) => BinaryPrimitives.ReadInt32LittleEndian(ReadNativeDeformedGeometry(bridge, geometry, offset, 4));
        short ReadWord(int offset) => BinaryPrimitives.ReadInt16LittleEndian(ReadNativeDeformedGeometry(bridge, geometry, offset, 2));
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect251 bounds require their current GTE controls."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect251 bounds require their current GTE data registers."));
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect251 bounds require the current depth-cue controls.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Effect251 bounds require their current projection.");
        int groups = ReadInt(0);
        for (int group = 0; group < groups; group++)
        {
            if ((visibility & (1u << group)) == 0)
            {
                continue;
            }
            int cursor = ReadInt(checked(4 + group * 4));
            int sections = ReadWord(cursor);
            cursor += 2;
            for (int section = 0; section < sections; section++)
            {
                int bone = ReadWord(cursor);
                cursor += 2;
                if ((uint)bone >= (uint)bones.Length)
                {
                    throw new InvalidDataException("Effect251 bounds select an absent physical bone.");
                }
                registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, bones[bone], true, true, ReadNativeEffect251BonePadding(graphics, frame, actor, bone)) };
                int count = ReadWord(cursor);
                cursor += 2;
                if (count < 0)
                {
                    throw new InvalidDataException("Effect251 bounds require a bounded original vertex count.");
                }
                for (int vertex = 0; vertex < count; vertex++)
                {
                    byte[] bytes = ReadNativeDeformedGeometry(bridge, geometry, cursor, 6);
                    cursor = checked(cursor + 6);
                    NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
                    registers = NativeBattleGeometry.LoadVector(registers, 0, point, 0);
                    registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                    short value = unchecked((short)(registers.Colors.Ir[1] ?? throw new NotSupportedException("Effect251 bounds projection did not publish IR2.")));
                    temporary = temporary.Write(16, BitConverter.GetBytes(value));
                    int minimum = BinaryPrimitives.ReadInt32LittleEndian(temporary.Read(0, 4));
                    int maximum = BinaryPrimitives.ReadInt32LittleEndian(temporary.Read(4, 4));
                    if (minimum > value)
                    {
                        temporary = temporary.Write(0, BitConverter.GetBytes((int)value));
                    }
                    if (maximum < value)
                    {
                        temporary = temporary.Write(4, BitConverter.GetBytes((int)value));
                    }
                }
            }
        }
        NativeBattleEffectArena output = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, temporary.Read(4, 2)).Write(2, temporary.Read(0, 2));
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 24);
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 24) };
        frame = RestoreNativeEffect251BoundsPose(ref graphics, frame, actor, source);
        return new(frame, output);
    }

    private NativeBattleResourcePointer NativeEffect251BoundsSkeletonPointer(NativeBattleGraphicsState graphics, NativeBattleEffect251Frame frame, int actor)
    {
        NativeBattleGraphicsModel model = graphics.Models[actor];
        if (model.ActiveStagedResourceRow100 is uint staged)
        {
            NativeBattleResourcePointer? pointer;
            if (!NativeBattleEffectTaskMemory.TryReadPointer(frame.Effect.Pools, staged, out pointer))
            {
                pointer = IsNativeEffectArenaAddress(staged) ? frame.Memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(staged, 4)) : (frame.Staging ?? throw new NotSupportedException("Effect251 bounds require addressed rig memory.")).ReadResourcePointer(staged);
            }
            return pointer ?? throw new NotSupportedException("Effect251 bounds require the current skeleton directory pointer.");
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect251 bounds require current CPU resources.");
        return new(cpu.Rows[model.ActiveResourceRow100 ?? throw new NotSupportedException("Effect251 bounds require the physical resource row.")].Require(12), null);
    }

    private short? ReadNativeEffect251BonePadding(NativeBattleGraphicsState graphics, NativeBattleEffect251Frame frame, int actor, int bone)
    {
        NativeBattleResourcePointer pointer = NativeEffect251BoundsSkeletonPointer(graphics, frame, actor);
        int offset = checked(32 + bone * 48 + 18);
        if (pointer.Cpu is { Bank: < 3 } reference)
        {
            NativeBattleModelCpuBank bank = (NativeModelCpuResources ?? throw new NotSupportedException("Effect251 bounds lost CPU rig memory.")).Cpu.Banks[reference.Bank];
            int first = checked(reference.Offset + offset);
            NativeBattleEffectArena padding = NativeBattleEffectArena.Clear().MarkUnknown(0, 2);
            foreach (NativeBattleEffectMemoryRegion region in bank.KnownRanges)
            {
                int start = Math.Max(first, region.Offset);
                int end = Math.Min(first + 2, region.Offset + region.Data.Length);
                if (start < end)
                {
                    padding = padding.Write(start - first, region.Data.AsSpan(start - region.Offset, end - start));
                }
            }
            (byte[] bytes, bool[] known) = padding.ReadSnapshot(0, 2);
            return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        }
        uint address = checked((pointer.Cpu?.SharedAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect251 bounds require the shared skeleton owner.")) + (uint)offset);
        NativeBattleEffectArena observed = NativeBattleEffect251Controller.ReadBytes(frame, address, 2);
        (byte[] data, bool[] observedKnown) = observed.ReadSnapshot(0, 2);
        return observedKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(data) : null;
    }

    private NativeBattleEffect251Frame RestoreNativeEffect251BoundsPose(ref NativeBattleGraphicsState graphics, NativeBattleEffect251Frame frame, int actor, NativeBattlePoseSource source)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Effect251 bounds require retained pose temporary memory."), 88);
        NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[actor], source, NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88));
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
        graphics = graphics with { Models = graphics.Models.SetItem(actor, prepared.Model) };
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88) };
        NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadDecodedPose(prepared.Model.SequenceState ?? throw new NotSupportedException("Effect251 bounds require the decoded pose owner."), source);
        if (!decoded.HasScale)
        {
            for (int bone = 0; bone < source.BoneCount; bone++)
            {
                if (BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan(16 + bone * 48, 2)) >= 0)
                {
                    continue;
                }
                NativeBattleResourcePointer pointer = NativeEffect251BoundsSkeletonPointer(graphics, frame, actor);
                int offset = checked(32 + bone * 48 + 18);
                if (pointer.Cpu is { Bank: < 3 } reference)
                {
                    NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect251 bounds lost their physical rig owner.");
                    NativeBattleModelCpuBank bank = cpu.Cpu.Banks[reference.Bank].Write(checked(reference.Offset + offset), new byte[2]);
                    NativeModelCpuResources = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(reference.Bank, bank) } };
                }
                else
                {
                    uint address = checked((pointer.Cpu?.SharedAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect251 bounds require the addressed rig owner.")) + (uint)offset);
                    frame = NativeBattleEffect251Controller.WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[2]), 2);
                }
            }
        }
        return frame;
    }
}
