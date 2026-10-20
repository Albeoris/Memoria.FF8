using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleOriginalEffectState Effect) DrawNativeZombieOverlay(NativeBattleGraphicsState graphics, int index, int age, int reach, NativeBattleZombieResources resources, NativeBattleCameraProjection projection, NativeBattleOriginalEffectState effect)
    {
        var parameters = NativeBattleZombieDrawing.OverlayParameters(age, reach);
        NativeBattleGraphicsModel model = graphics.Models[index];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native Zombie overlay requires its current skeleton.");
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native Zombie overlay requires its actual target model matrix.");
        world = world with { Y = unchecked(world.Y + parameters.Lift) };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(projection.View ?? throw new NotSupportedException("Native Zombie overlay requires its actual camera matrix."), world);
        NativeBattleModuleProjectedVertex origin = NativeBattleModuleProjection.Project45FE10(matrix, resources.Zero, projection);
        NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native Zombie overlay requires its retained temporary memory.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 0x8C);
        staging = allocation.Staging;
        uint address = allocation.Address;
        byte[] packed = new byte[32];
        for (int lane = 0; lane < 9; lane++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(lane * 2), matrix.Rotation[lane]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(20), matrix.X);
        BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(24), matrix.Y);
        BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(28), matrix.Z);
        staging = staging.Write(address, packed.AsSpan(0, 18)).MarkUnknown(address + 18, 2).Write(address + 20, packed.AsSpan(20, 12));
        Word(0x86, unchecked((short)(parameters.Amplitude >> 2)));
        Word(0x84, parameters.Phase);
        Dword(0x7C, origin.Screen);
        Dword(0x88, origin.Depth);
        NativeFileStaging = staging;
        if (origin.Depth > 0)
        {
            ushort maximum = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Zombie overlay requires its actual loaded CPU workspace maximum.");
            int capacity = checked(maximum * 8);
            uint workspace = checked(0x196B018u - (uint)capacity);
            Dword(0x24, workspace);
            Word(0x34, 0);
            Word(0x36, 0);
            Word(0x38, 320);
            Word(0x3A, 216);
            uint tint = NativeBattleDuelSharedPackets.InterpolateColor56CC00(resources.Black, model.Color40 ?? throw new NotSupportedException("Native Zombie overlay requires its actual target tint."), parameters.Weight, 4096 - parameters.Weight, 0);
            Dword(0x3C, tint);
            uint mask = model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native Zombie overlay requires its actual object mask.");
            Dword(0x40, mask);
            model = NativeBattleBodyBoneMatrices.Compose5095B0(model, matrix);
            graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
            NativeFileStaging = staging;
            DrawSource(Pointer(model.ActiveResourceRow100, model.ActiveStagedResourceRow100), NativeBattleBodyBoneMatrices.Require(model, source), source.BoneCount, mask);
            model = NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[index], source);
            graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
            if (model.LinkedStagedResourceRow is not null || model.LinkedTextureResourceIndex is not null)
            {
                NativeBattlePoseSource linked = resource.LinkedPoseSource ?? throw new NotSupportedException("Native Zombie overlay requires its actual linked skeleton.");
                NativeBattleBodyBoneMatrices bones = model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Native Zombie overlay requires its current linked bone owner.");
                _ = NativeBattleBodyBoneMatrices.Require(model with { CurrentBodyBoneMatrices = bones }, linked);
                ImmutableArray<NativeBattleActionMatrix> prepared = NativeBattlePoseMatrices.ComposeStageCamera(bones.Matrices, matrix, 0);
                graphics = graphics with { Models = graphics.Models.SetItem(index, model with { CurrentLinkedBoneMatrices = bones with { Matrices = prepared } }) };
                staging = NativeFileStaging!;
                Dword(0x40, uint.MaxValue);
                NativeFileStaging = staging;
                DrawSource(Pointer(model.LinkedTextureResourceIndex, model.LinkedStagedResourceRow), prepared, linked.BoneCount, uint.MaxValue);
                NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Zombie overlay requires its current linked pose.");
                graphics = graphics with { Models = graphics.Models.SetItem(index, model with { CurrentLinkedBoneMatrices = bones with { Matrices = NativeBattlePoseMatrices.StageBoneMatrices(linked, NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, linked)) } }) };
            }
        }
        NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(NativeFileStaging ?? staging, 0x8C);
        return (graphics, effect);

        void DrawSource(NativeBattleResourcePointer pointer, ImmutableArray<NativeBattleActionMatrix> bones, int boneCount, uint mask)
        {
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Zombie overlay requires its actual retained memory.");
            NativeBattleFileStaging current = NativeFileStaging ?? throw new NotSupportedException("Native Zombie overlay requires its physical CPU geometry.");
            NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native Zombie overlay requires its loaded CPU rows.");
            ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read(Read, boneCount);
            NativeBattleScriptedGraphicsMemory registers = memory.ScriptedGraphics ?? throw new NotSupportedException("Native Zombie overlay requires its GTE interpolation owners.");
            ushort maximum = cpu.ScratchMaximum ?? throw new NotSupportedException("Native Zombie overlay requires its loaded projected maximum.");
            int capacity = checked(maximum * 8);
            uint workspace = checked(0x196B018u - (uint)capacity);
            uint tint = NativeBattleDuelSharedPackets.InterpolateColor56CC00(resources.Black, model.Color40 ?? throw new NotSupportedException("Native Zombie overlay requires its target color."), parameters.Weight, 4096 - parameters.Weight, 0);
            NativeBattleZombieOverlayDraw drawn = NativeBattleZombieOverlay.Draw(meshes, bones, projection, tint, mask, null, workspace, capacity, current, resources.Wave, unchecked((short)origin.Screen), parameters.Phase, unchecked((short)(parameters.Amplitude >> 2)), registers.Dqa, registers.Dqb);
            NativeFileStaging = drawn.Staging;
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, effect.DrawArenaOffset ?? throw new NotSupportedException("Native Zombie overlay lost its selected packet cursor."), graphics.PrimitiveFrame!.Pending, drawn.Packets);
            NativeEffectMemory = written.Memory;
            effect = effect with { DrawArenaOffset = written.Cursor };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };

            byte[] Read(int offset, int length)
            {
                if (pointer.Length is int extent && (offset < 0 || length < 0 || offset > extent - length))
                {
                    throw new InvalidDataException("Native Zombie overlay geometry exceeds its original current extent.");
                }
                if (pointer.Cpu is { } selected)
                {
                    return NativeBattleModelEnemyHeap.Read(cpu, current, selected with { Offset = checked(selected.Offset + offset) }, length);
                }
                uint sourceAddress = checked((pointer.StagingAddress ?? throw new NotSupportedException("Native Zombie overlay requires its actual geometry pointer.")) + (uint)offset);
                if (NativeBattleEffectTaskMemory.TryRead(effect.Pools, sourceAddress, length, out byte[] bytes))
                {
                    return bytes;
                }
                return IsNativeEffectArenaAddress(sourceAddress) ? memory.Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(sourceAddress, length), length) : current.Read(sourceAddress, length);
            }
        }

        NativeBattleResourcePointer Pointer(int? row, uint? staged)
        {
            if (staged is uint physical)
            {
                uint pointer = checked(physical + 4);
                if (NativeBattleEffectTaskMemory.TryReadPointer(effect.Pools, pointer, out NativeBattleResourcePointer? selected))
                {
                    return selected ?? throw new NotSupportedException("Native Zombie overlay geometry pointer is NULL.");
                }
                return (IsNativeEffectArenaAddress(pointer) ? NativeEffectMemory!.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(pointer, 4)) : NativeFileStaging!.ReadResourcePointer(pointer)) ?? throw new NotSupportedException("Native Zombie overlay geometry pointer is unknown.");
            }
            NativeBattleModelCpuResourceRow cpu = (NativeModelCpuResources ?? throw new NotSupportedException("Native Zombie overlay requires its actual CPU row.")).Rows[row ?? throw new NotSupportedException("Native Zombie overlay requires its selected resource row.")];
            return new(cpu.Require(16), null, cpu.Lengths.TryGetValue(16, out int length) ? length : null);
        }
        void Word(int offset, short value)
        {
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
            staging = staging.Write(checked(address + (uint)offset), bytes);
        }
        void Dword(int offset, uint value)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            staging = staging.Write(checked(address + (uint)offset), bytes);
        }
    }
}
