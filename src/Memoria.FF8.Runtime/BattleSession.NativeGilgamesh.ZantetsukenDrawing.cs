using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGilgameshFrame BeginNativeZantetsukenActorDraw(NativeBattleGilgameshFrame frame, uint taskAddress)
    {
        NativeBattleModulePoolTask task = NativeBattleZantetsukenTasks.Read(frame, taskAddress);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireNativeZantetsukenStaging(frame), 472);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 472);
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(0, 0, unchecked((short)task.ReadWord(0x18))));
        NativeBattleVector normal = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, -4096, 0));
        NativeBattleModuleAnchor anchor = new(unchecked((short)task.ReadWord(0x10)), unchecked((short)task.ReadWord(0x12)), unchecked((short)task.ReadWord(0x14)), unchecked((short)task.ReadWord(0x16)));
        temporary = NativeBattleZantetsukenRendering.PreparePlane(temporary, anchor, normal);
        return PublishNativeZantetsukenTemporary(frame, allocation.Staging, allocation.Address, temporary, 472, false);
    }

    private NativeBattleGilgameshFrame DrawNativeZantetsukenActor(ref NativeBattleGraphicsState graphics, NativeBattleGilgameshFrame frame, uint taskAddress)
    {
        NativeBattleModulePoolTask task = NativeBattleZantetsukenTasks.Read(frame, taskAddress);
        ushort selected = task.ReadWord(14);
        if (selected >= graphics.Models.Length)
        {
            throw new InvalidDataException("Zantetsuken body leaves its physical actor rows.");
        }
        int index = selected;
        NativeBattleGraphicsModel actor = graphics.Models[index];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Zantetsuken body requires its actual current skeleton.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Zantetsuken body requires its retained camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Zantetsuken body requires its current projection.");
        NativeBattlePackedMatrix32 packedView = camera.CapturePackedView1D97778();
        if ((actor.Flags0 & 0x20) == 0)
        {
            NativeBattleShadowSource shadowSource = definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Zantetsuken shadow requires its original template.");
            NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(RequireNativeZantetsukenStaging(frame), new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Zantetsuken shadow requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Zantetsuken shadow requires its current arithmetic registers.")), actor, projection, frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Zantetsuken shadow requires its depth-cue controls."), shadowSource, definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Zantetsuken shadow requires its current primitive capabilities."), 4121, 16, NativeBattleGilgameshVisuals.Resource(frame.PresentationIdentity, shadowSource, 0x5088A0, index));
            frame = frame with { Staging = shadow.Staging, Memory = frame.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors } };
            frame = AppendNativeZantetsukenPackets(ref graphics, frame, shadow.Batch);
            frame = PublishNativeZantetsukenTemporary(frame, RequireNativeZantetsukenStaging(frame), shadow.TemporaryAddress, shadow.Temporary78, 0x78, true);
        }
        NativeBattleFileStaging staging = RequireNativeZantetsukenStaging(frame);
        uint temporaryAddress = NativeZantetsukenReservedTemporary(staging);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(staging, temporaryAddress, 472);
        NativeBattleActionMatrix main = NativeBattleGfMatrices.Compose56C2F0(packedView.ReadMatrix(), actor.GeometryWorldMatrix ?? throw new NotSupportedException("Zantetsuken body requires its retained actor world matrix."));
        NativeBattleGilgameshZantetsukenActors actors = frame.Memory.Gilgamesh?.ZantetsukenActors ?? throw new NotSupportedException("Zantetsuken body requires its current actor transform record.");
        NativeBattleActionMatrix alternate = NativeBattleGfMatrices.Compose56C2F0(packedView.ReadMatrix(), actors.ReadMatrix((byte)index).ReadMatrix());
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(actor, source);
        ImmutableArray<NativeBattleZantetsukenRenderObject> objects = DiablosActorGeometry(resource, actor).Select(mesh => new NativeBattleZantetsukenRenderObject(mesh, null)).ToImmutableArray();
        NativeBattlePilotVisualIdentity? actorIdentity = NativeBattleGilgameshVisuals.ActorResource(frame.PresentationIdentity, resource, index);
        uint mask = (actor.SequenceState ?? throw new NotSupportedException("Zantetsuken body requires its actual object mask.")).MeshMask124;
        (uint scratchAddress, int scratchLength) = NativeZantetsukenScratch();
        RequireNativeZantetsukenVertexBounds(objects, mask, scratchLength);
        NativeBattleEffectArena scratch = NativeBattleTemporaryMemory.ReadView(staging, scratchAddress, scratchLength);
        NativeBattleZantetsukenResources resources = RequireNativeZantetsukenResources(frame);
        NativeBattleZantetsukenRenderInputs inputs = new(NativeBattlePackedMatrix32.FromMatrix(main, null), NativeBattlePackedMatrix32.FromMatrix(alternate, null), bones, _ => null, projection, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Zantetsuken body requires its original geometry constants."), temporaryAddress, scratchAddress, null, null, 17, 2, resources.ClipIndices) { PilotIdentity = actorIdentity };
        NativeBattleZantetsukenRenderStep drawn = NativeBattleZantetsukenRendering.DrawBody(frame.Memory, temporary, scratch, objects, inputs, mask, actor.Color40 ?? throw new NotSupportedException("Zantetsuken body requires its current actor color."));
        frame = frame with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, scratchAddress, drawn.VertexScratch, scratchLength) };
        frame = PublishNativeZantetsukenTemporary(frame, RequireNativeZantetsukenStaging(frame), temporaryAddress, drawn.Temporary1D8, 472, false);
        frame = AppendNativeZantetsukenPackets(ref graphics, frame, drawn.Batch);
        NativeBattleTemporaryAllocation boneTemporary = NativeBattleTemporaryMemory.Allocate5082B0(RequireNativeZantetsukenStaging(frame), 88);
        NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(actor, source, NativeBattleTemporaryMemory.ReadView(boneTemporary.Staging, boneTemporary.Address, 88));
        graphics = graphics with { Models = graphics.Models.SetItem(index, restored.Model) };
        frame = PublishNativeZantetsukenTemporary(frame, boneTemporary.Staging, boneTemporary.Address, restored.Temporary58, 88, true);
        return PublishNativeZantetsukenTemporary(frame, RequireNativeZantetsukenStaging(frame), temporaryAddress, drawn.Temporary1D8, 472, true);
    }

    private NativeBattleGilgameshFrame DrawNativeZantetsukenStage(ref NativeBattleGraphicsState graphics, NativeBattleGilgameshFrame frame, uint taskAddress)
    {
        NativeBattleModulePoolTask task = NativeBattleZantetsukenTasks.Read(frame, taskAddress);
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Zantetsuken requires its four retained stage groups.");
        stage.Validate();
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireNativeZantetsukenStaging(frame), 472);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 472);
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(new(0, 0, -96));
        NativeBattleVector normal = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, -4096, 0));
        temporary = NativeBattleZantetsukenRendering.PreparePlane(temporary, new(0, -3000, 0, 0), normal, false);
        frame = PublishNativeZantetsukenTemporary(frame, allocation.Staging, allocation.Address, temporary, 472, false);
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Zantetsuken stage requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Zantetsuken stage requires its published projection.");
        NativeBattlePackedMatrix32 main = camera.CapturePackedView1D97778();
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.Identity8DD770() with { Z = unchecked((short)task.ReadWord(0x20)) };
        NativeBattlePackedMatrix32 alternate = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(main.ReadMatrix(), local), null);
        ImmutableArray<NativeBattleRawMesh> geometry = RequireNativeStageGeometry();
        NativeBattleZantetsukenResources resources = RequireNativeZantetsukenResources(frame);
        (uint scratchAddress, int scratchLength) = NativeZantetsukenScratch();
        temporary = WriteNativeZantetsukenMatrix(temporary, 0x198, main);
        temporary = WriteNativeZantetsukenMatrix(temporary, 0x1B8, alternate);
        temporary = temporary.Write(4, BitConverter.GetBytes(scratchAddress)).Write(0x10, BitConverter.GetBytes(uint.MaxValue));
        frame = PublishNativeZantetsukenTemporary(frame, RequireNativeZantetsukenStaging(frame), allocation.Address, temporary, 472, false);
        for (int groupIndex = 0; groupIndex < 4; groupIndex++)
        {
            NativeBattleStageGroup group = stage.Groups[groupIndex];
            NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Zantetsuken stage requires its current original pose source before the enable test.");
            NativeBattleDecodedPose decoded = source.Sample(group.Animation, group.Counter, group.PoseFlags);
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.StageBoneMatrices(source, decoded);
            NativeBattleTemporaryAllocation boneTemporary = NativeBattleTemporaryMemory.Allocate5082B0(RequireNativeZantetsukenStaging(frame), 88);
            NativeBattleEffectArena boneBytes = NativeBattleBodyBoneTemporary.Prepare508C90(source, decoded, matrices, NativeBattleTemporaryMemory.ReadView(boneTemporary.Staging, boneTemporary.Address, 88));
            frame = PublishNativeZantetsukenTemporary(frame, boneTemporary.Staging, boneTemporary.Address, boneBytes, 88, true);
            group = group with { Matrices = matrices };
            stage = stage with { Groups = stage.Groups.SetItem(groupIndex, group) };
            NativeStage = stage;
            if ((group.Flags & 1) == 0)
            {
                continue;
            }
            ImmutableArray<NativeBattleZantetsukenRenderObject> objects = geometry.Where(mesh => mesh.StageGroup == groupIndex).Select(mesh => new NativeBattleZantetsukenRenderObject(mesh, null)).ToImmutableArray();
            RequireNativeZantetsukenVertexBounds(objects, uint.MaxValue, scratchLength);
            NativeBattleFileStaging staging = RequireNativeZantetsukenStaging(frame);
            temporary = NativeBattleTemporaryMemory.ReadView(staging, allocation.Address, 472);
            NativeBattleEffectArena scratch = NativeBattleTemporaryMemory.ReadView(staging, scratchAddress, scratchLength);
            NativeBattleZantetsukenStageOrdering ordering = resources.StageOrderPairs[groupIndex];
            NativeBattleZantetsukenRenderInputs inputs = new(main, alternate, matrices, bone => NativeZantetsukenBonePadding(source, decoded, bone), projection, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Zantetsuken stage requires its original geometry constants."), allocation.Address, scratchAddress, null, null, checked((int)ordering.Bucket), checked((byte)ordering.Shift), resources.ClipIndices) { PilotIdentity = NativeBattleGilgameshVisuals.Resource(frame.PresentationIdentity, source, 0, groupIndex) };
            NativeBattleZantetsukenRenderStep drawn = NativeBattleZantetsukenRendering.DrawStage(frame.Memory, temporary, scratch, objects, inputs, group.ColorFlags, group.ColorDepth);
            frame = frame with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, scratchAddress, drawn.VertexScratch, scratchLength) };
            frame = PublishNativeZantetsukenTemporary(frame, RequireNativeZantetsukenStaging(frame), allocation.Address, drawn.Temporary1D8, 472, false);
            frame = AppendNativeZantetsukenPackets(ref graphics, frame, drawn.Batch);
        }
        NativeBattleFileStaging finalStaging = RequireNativeZantetsukenStaging(frame);
        return PublishNativeZantetsukenTemporary(frame, finalStaging, allocation.Address, NativeBattleTemporaryMemory.ReadView(finalStaging, allocation.Address, 472), 472, true);
    }

    private static NativeBattleEffectArena WriteNativeZantetsukenMatrix(NativeBattleEffectArena arena, int offset, NativeBattlePackedMatrix32 matrix)
    {
        matrix.Validate();
        arena = arena.Write(offset, matrix.Bytes.AsSpan());
        for (int index = 0; index < 32; index++)
        {
            if (!matrix.Known[index])
            {
                arena = arena.MarkUnknown(offset + index, 1);
            }
        }
        return arena;
    }

    private static NativeBattleFileStaging RequireNativeZantetsukenStaging(NativeBattleGilgameshFrame frame) => frame.Staging ?? throw new NotSupportedException("Zantetsuken requires its current physical temporary and projected memory.");

    private static NativeBattleGilgameshFrame PublishNativeZantetsukenTemporary(NativeBattleGilgameshFrame frame, NativeBattleFileStaging staging, uint address, NativeBattleEffectArena temporary, int length, bool release)
    {
        staging = NativeBattleTemporaryMemory.PublishView(staging, address, temporary, length);
        if (release)
        {
            staging = NativeBattleTemporaryMemory.Release5082D0(staging, length);
        }
        return frame with { Staging = staging };
    }

    private static uint NativeZantetsukenReservedTemporary(NativeBattleFileStaging staging)
    {
        uint cursor = BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(NativeBattleTemporaryMemory.Cursor, 4));
        if (cursor < NativeBattleTemporaryMemory.Address + 472)
        {
            throw new InvalidDataException("Zantetsuken body requires its preceding physical temporary reservation.");
        }
        return cursor - 472;
    }

    private (uint Address, int Length) NativeZantetsukenScratch()
    {
        ushort maximum = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Zantetsuken requires its retained shared vertex maximum.");
        int length = checked(maximum * 8);
        return (checked(0x196B018u - (uint)length), length);
    }

    private static void RequireNativeZantetsukenVertexBounds(ImmutableArray<NativeBattleZantetsukenRenderObject> objects, uint mask, int capacity)
    {
        for (int index = 0; index < objects.Length; index++)
        {
            if ((mask & (1u << (index & 31))) != 0 && objects[index].Mesh.Vertices.Length > capacity / 8)
            {
                throw new InvalidDataException("Zantetsuken object exceeds its actual loaded shared vertex allocation.");
            }
        }
    }

    private static short? NativeZantetsukenBonePadding(NativeBattlePoseSource source, NativeBattleDecodedPose decoded, int bone)
    {
        short parent = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(16 + bone * 48, 2));
        return !decoded.HasScale && parent < 0 ? (short)0 : null;
    }

    private static NativeBattleZantetsukenResources RequireNativeZantetsukenResources(NativeBattleGilgameshFrame frame)
    {
        return frame.Effect.Binding.GilgameshResources?.Zantetsuken ?? throw new NotSupportedException("Zantetsuken requires its original clipping and stage ordering tables.");
    }

    private static NativeBattleGilgameshFrame AppendNativeZantetsukenPackets(ref NativeBattleGraphicsState graphics, NativeBattleGilgameshFrame frame, NativeBattleGfGeometryBatch batch)
    {
        batch = NativeBattleGilgameshVisuals.Depths(batch);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Zantetsuken requires its current COMMON packet memory.");
        frame = frame with { Effect = frame.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(frame.Effect.PresentationPackets, process.PacketBank!, process.PacketBuffer, process.ByteCursor!.Value, batch) } };
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Zantetsuken requires its actual COMMON ordering table.");
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Zantetsuken requires its current COMMON packet memory."), primitive.Pending, batch);
        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        return frame with { Memory = frame.Memory with { PrimitiveProcess = written.Process } };
    }
}
