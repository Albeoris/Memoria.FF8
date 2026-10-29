using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleLionHeart165Frame Frame, NativeBattleGraphicsState Graphics) DrawNativeLionHeart165Echo(NativeBattleLionHeart165Frame frame, NativeBattleGraphicsState graphics, byte actor, uint temporary, NativeBattleCameraProjection projection, NativeBattlePilotVisualIdentity pilotIdentity)
    {
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Lion Heart echo requires its actual view matrix.");
        if ((model.Flags0 & 0x20) == 0)
        {
            graphics = SubmitNativeShadowPackets5088A0(graphics, model, projection, 4121, false);
            frame = frame with { Memory = NativeEffectMemory ?? throw new NotSupportedException("Native Lion Heart echo lost its process owner."), Staging = NativeFileStaging ?? throw new NotSupportedException("Native Lion Heart echo lost its physical globals.") };
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native Lion Heart echo requires its actual CPU model resource owner.");
        ushort maximum = cpu.ScratchMaximum ?? throw new NotSupportedException("Native Lion Heart echo requires the actual507010 maximum WORD.");
        uint vertices = checked(0x196B018u - (uint)maximum * 8);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native Lion Heart echo requires its actual body skeleton.");
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native Lion Heart echo requires its published model matrix64.");
        frame = frame with { Staging = Matrix(frame.Staging, temporary + 200, world) };
        frame = frame with { Staging = Dword(frame.Staging, 0x23C32E8, 0x1D97778) };
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model, world);
        graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
        uint color = model.Color40 ?? throw new NotSupportedException("Native Lion Heart echo requires its model DWORD40 color.");
        uint mask = model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native Lion Heart echo requires its actual mesh mask.");
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, source);
        NativeBattleResourcePointer geometry = GeometryPointer(model.ActiveResourceRow100, model.ActiveStagedResourceRow100);
        SubmitSource(geometry, bones, source.BoneCount, mask, resource.GeometryRelocated507550 ? null : model.BodyTextureSlots ?? throw new NotSupportedException("Native Lion Heart echo requires its body texture binding."));
        model = NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[actor], source);
        graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
        if (model.LinkedTransform is { } local && (model.Flags0 & 0x200) == 0)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Native Lion Heart echo requires its independent linked skeleton.");
            NativeBattleBodyBoneMatrices linked = model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Native Lion Heart echo requires its current linked matrix publication.");
            _ = NativeBattleBodyBoneMatrices.Require(model with { CurrentBodyBoneMatrices = linked }, linkedSource);
            NativeBattleActionMatrix retained = ReadMatrix(frame.Staging, temporary + 200);
            NativeBattleActionMatrix localMatrix = NativeBattleGfMatrices.Euler56CE30(local.Rotation) with { X = local.Position.X, Y = local.Position.Y, Z = local.Position.Z };
            NativeBattleActionMatrix basis = NativeBattlePoseMatrices.Compose56C2F0(retained, localMatrix);
            frame = frame with { Staging = Matrix(frame.Staging, temporary + 200, basis) };
            ImmutableArray<NativeBattleActionMatrix> linkedBones = NativeBattlePoseMatrices.ComposeStageCamera(linked.Matrices, basis, 0);
            model = model with { CurrentLinkedBoneMatrices = linked with { Matrices = linkedBones } };
            graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
            NativeBattleResourcePointer linkedGeometry = GeometryPointer(model.LinkedTextureResourceIndex, model.LinkedStagedResourceRow);
            SubmitSource(linkedGeometry, linkedBones, linkedSource.BoneCount, uint.MaxValue, resource.GeometryRelocated507550 ? null : model.LinkedTextureSlots ?? throw new NotSupportedException("Native Lion Heart echo requires its linked texture binding."));
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native Lion Heart echo requires its linked pose owner.");
            model = graphics.Models[actor] with { CurrentLinkedBoneMatrices = linked with { Matrices = NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, linkedSource)) } };
            graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
        }
        return (frame, graphics);

        void SubmitSource(NativeBattleResourcePointer pointer, ImmutableArray<NativeBattleActionMatrix> matrices, int boneCount, uint selectedMask, NativeBattleModelTextureBinding? binding)
        {
            uint address = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Native Lion Heart echo geometry requires its actual physical source address.");
            List<int> polygons = [];
            ImmutableArray<NativeBattleRawMesh> objects = NativeBattleRawGeometryReader.Read(Read, boneCount, (_, _, first, _) => polygons.Add(first));
            ImmutableArray<int> selectors = Enumerable.Range(0, 56).Select(index => ReadInt(frame.Staging, 0xD88850u + (uint)index * 4)).ToImmutableArray();
            NativeBattleFileStaging staging = Dword(Dword(Dword(frame.Staging, temporary + 4, vertices), temporary + 16, color), temporary + 20, selectedMask);
            for (int index = 0; index < selectors.Length; index++)
            {
                staging = Dword(staging, temporary + 200 + (uint)index * 4, temporary + 88 + (uint)selectors[index] * 16);
            }
            frame = frame with { Staging = staging };
            for (int index = 0; index < objects.Length; index++)
            {
                if ((selectedMask & (1u << (index & 31))) == 0)
                {
                    continue;
                }
                NativeBattleRawMesh mesh = objects[index];
                if (mesh.Vertices.Length > maximum)
                {
                    throw new InvalidDataException("Native Lion Heart posed vertices exceed the reached507010 workspace maximum.");
                }
                NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Lion Heart echo requires its common packet owner.");
                uint packetAddress = CommonAddress(process);
                uint table = checked(0x1D8E058u + (uint)(process.Buffer80 ?? throw new NotSupportedException("Native Lion Heart echo requires its current OT buffer.")) * 17544 + 68);
                NativeBattleScriptedGraphicsMemory registers = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Lion Heart echo requires its GTE registers.");
                NativeBattleShivaPlane plane = new(ReadShort(frame.Staging, temporary + 184), ReadShort(frame.Staging, temporary + 186), ReadShort(frame.Staging, temporary + 188), ReadShort(frame.Staging, temporary + 190));
                NativeBattleLionHeart165EchoDraw drawn = NativeBattleLionHeart165Controller.DrawEchoMesh5EF0C0(frame, temporary, vertices, checked(address + (uint)polygons[index]), mesh, matrices, view, projection, plane, selectors, color, table, packetAddress, registers.Zsf3 ?? throw new NotSupportedException("Native Lion Heart echo requires ZSF3."), registers.Zsf4 ?? throw new NotSupportedException("Native Lion Heart echo requires ZSF4."), registers.Dqa, registers.Dqb, binding, pilotIdentity: pilotIdentity with { Primitive = unchecked((int)address * 397 + index) });
                frame = drawn.Frame;
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Lion Heart echo requires its initialized ordering table.");
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, drawn.Batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                frame = frame with { Memory = frame.Memory with { PrimitiveProcess = written.Process } };
                if (CommonAddress(written.Process) != drawn.PacketCursor)
                {
                    throw new InvalidDataException("Native Lion Heart echo cursor differs from its physical common owner.");
                }
            }

            byte[] Read(int offset, int length)
            {
                if (pointer.Length is int available && (offset < 0 || length < 0 || offset > available - length))
                {
                    throw new InvalidDataException("Native Lion Heart echo geometry read leaves its physical resource extent.");
                }
                if (pointer.Cpu is { } cpuPointer)
                {
                    return NativeBattleModelEnemyHeap.Read(cpu, frame.Staging, cpuPointer with { Offset = checked(cpuPointer.Offset + offset) }, length);
                }
                uint physical = checked(pointer.StagingAddress!.Value + (uint)offset);
                if (NativeBattleEffectTaskMemory.TryRead(frame.Effect.Pools, physical, length, out byte[] bytes))
                {
                    return bytes;
                }
                return IsNativeEffectArenaAddress(physical) ? (frame.Memory.Arena ?? throw new NotSupportedException("Native Lion Heart echo requires its actual arena resource bytes.")).Read(NativeBattleWaterExecutionFields.ArenaOffset(physical, length), length) : frame.Staging.Read(physical, length);
            }
        }
        NativeBattleResourcePointer GeometryPointer(int? cpuRow, uint? stagedRow)
        {
            if (stagedRow is uint row)
            {
                uint address = checked(row + 4);
                if (NativeBattleEffectTaskMemory.TryReadPointer(frame.Effect.Pools, address, out NativeBattleResourcePointer? pointer))
                {
                    return pointer ?? throw new NotSupportedException("Native Lion Heart echo geometry pointer is NULL.");
                }
                return (IsNativeEffectArenaAddress(address) ? frame.Memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : frame.Staging.ReadResourcePointer(address)) ?? throw new NotSupportedException("Native Lion Heart echo geometry pointer is unobserved.");
            }
            NativeBattleModelCpuResourceRow selected = cpu.Rows[cpuRow ?? throw new NotSupportedException("Native Lion Heart echo requires its actual model resource row.")];
            return new(selected.Require(16), null, selected.Lengths.TryGetValue(16, out int length) ? length : null);
        }
        static uint CommonAddress(NativeBattlePrimitiveProcess process)
        {
            uint address = process.PacketBank switch { "off_B6D070" => 0x1901018, "off_B6D094" => 0x199B018, _ => throw new NotSupportedException("Native Lion Heart echo requires its actual common packet bank.") };
            return checked(address + (uint)process.PacketBuffer * (uint)process.CommonPacketNominalCapacity + (uint)(process.ByteCursor ?? throw new NotSupportedException("Native Lion Heart echo requires its common cursor.")));
        }
        static short ReadShort(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadInt16LittleEndian(staging.Read(address, 2));
        static int ReadInt(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadInt32LittleEndian(staging.Read(address, 4));
        static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value) => staging.Write(address, BitConverter.GetBytes(value));
        static NativeBattleActionMatrix ReadMatrix(NativeBattleFileStaging staging, uint address) => new(Enumerable.Range(0, 9).Select(index => ReadShort(staging, address + (uint)index * 2)).ToImmutableArray(), ReadInt(staging, address + 20), ReadInt(staging, address + 24), ReadInt(staging, address + 28), 4096);
        static NativeBattleFileStaging Matrix(NativeBattleFileStaging staging, uint address, NativeBattleActionMatrix matrix)
        {
            for (int index = 0; index < 9; index++)
            {
                byte[] bytes = new byte[2];
                BinaryPrimitives.WriteInt16LittleEndian(bytes, matrix.Rotation[index]);
                staging = staging.Write(address + (uint)index * 2, bytes);
            }
            return Dword(Dword(Dword(staging.MarkUnknown(address + 18, 2), address + 20, unchecked((uint)matrix.X)), address + 24, unchecked((uint)matrix.Y)), address + 28, unchecked((uint)matrix.Z));
        }
    }
}
