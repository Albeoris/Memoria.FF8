using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect290Controller;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect290Frame DrawNativeEffect290Body(ref NativeBattleGraphicsState graphics, NativeBattleEffect290Frame frame, NativeBattleEffect290DrawBody676B40 operation, NativeBattleEffect290Inputs inputs)
    {
        NativeBattleGraphicsState currentGraphics = graphics;
        int actor = NativeEffect290Actor(graphics, operation.ModelAddress);
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect290 requires its current addressed memory.");
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect290 requires its current caster skeleton.");
        NativeBattleEffectArena offsets = ReadBytes(frame, operation.OffsetRowsAddress, checked(source.BoneCount * 16));
        int scratchLength = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect290 requires its current projection capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, 152);
        NativeBattleEffect298BodyFrame body = new(frame.Memory, allocation.Staging, graphics.Models[actor], NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 152));
        body = NativeBattleEffect298BodyDrawing.DrawOuter(body, inputs.ReadView(frame), operation.OffsetRowsAddress, offsets, scratchAddress, new(Shadow, Prepare, DrawSource, Restore));
        graphics = currentGraphics with { Models = currentGraphics.Models.SetItem(actor, body.Model) };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(body.Staging, allocation.Address, body.Temporary152, 152);
        return frame with { Memory = body.Memory, Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 152) };

        NativeBattleEffect298BodyFrame Shadow(NativeBattleEffect298BodyFrame selected)
        {
            NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(selected.Staging, new(selected.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect290 shadow requires its current GTE matrix."), selected.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect290 shadow requires its current arithmetic registers.")), selected.Model, inputs.ReadProjection(frame), selected.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect290 shadow requires its current depth controls."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Effect290 shadow requires its original seeds."), definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Effect290 shadow requires its actual primitive capabilities."), 4112, 16, operation.PilotIdentity);
            NativeBattleEffect290Frame delivered = DeliverNativeEffect290Batch(ref currentGraphics, frame with { Memory = selected.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, Staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78) }, shadow.Batch, 0x1D8E054);
            frame = frame with { Effect = delivered.Effect };
            return selected with { Memory = delivered.Memory, Staging = NativeBattleTemporaryMemory.Release5082D0(delivered.Staging!, 0x78) };
        }

        NativeBattleEffect298BodyFrame Prepare(NativeBattleEffect298BodyFrame selected)
        {
            currentGraphics = currentGraphics with { Models = currentGraphics.Models.SetItem(actor, selected.Model) };
            NativeBattleEffect290Frame prepared = PrepareNativeEffect290Pose(ref currentGraphics, frame with { Memory = selected.Memory, Staging = selected.Staging }, actor, source, offsets);
            return selected with { Memory = prepared.Memory, Staging = prepared.Staging!, Model = currentGraphics.Models[actor] };
        }

        NativeBattleEffect298BodyFrame DrawSource(NativeBattleEffect298BodyFrame selected)
        {
            currentGraphics = currentGraphics with { Models = currentGraphics.Models.SetItem(actor, selected.Model) };
            NativeBattleEffect290Frame current = frame with { Memory = selected.Memory, Staging = selected.Staging };
            (uint address, Func<int, int, byte[]> read) = ReadNativeEffect290Geometry(currentGraphics, current, actor);
            ImmutableArray<NativeBattlePackedMatrix32> bones = ReadNativeEffect290PackedBones(currentGraphics, current, actor, source);
            NativeBattleEffectArena scratch = NativeBattleTemporaryMemory.ReadView(selected.Staging, scratchAddress, scratchLength);
            NativeBattleEffect298BodyStep drawn = NativeBattleEffect298BodyDrawing.DrawBody(selected.Memory, selected.Temporary152, scratch, ReadGeometry, address, bones, offsets, inputs.ReadProjection(current), selected.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect290 requires its current projection controls."), scratchAddress, scratchLength, 17, 0, operation.PilotIdentity);
            NativeBattleFileStaging staged = NativeBattleTemporaryMemory.PublishView(selected.Staging, scratchAddress, drawn.VertexScratch, scratchLength);
            staged = NativeBattleTemporaryMemory.PublishView(staged, allocation.Address, drawn.Temporary152, 152);
            NativeBattleEffect290Frame delivered = DeliverNativeEffect290Batch(ref currentGraphics, current with { Memory = drawn.Memory, Staging = staged }, drawn.Batch, 0x1D8E054);
            frame = frame with { Effect = delivered.Effect };
            return selected with { Memory = delivered.Memory, Staging = delivered.Staging!, Temporary152 = drawn.Temporary152 };

            NativeBattleEffectArena ReadGeometry(uint selectedAddress, int length)
            {
                return NativeBattleEffectArena.Clear().Write(0, read(checked((int)(selectedAddress - address)), length));
            }
        }

        NativeBattleEffect298BodyFrame Restore(NativeBattleEffect298BodyFrame selected)
        {
            currentGraphics = currentGraphics with { Models = currentGraphics.Models.SetItem(actor, selected.Model) };
            NativeBattleEffect290Frame restored = RestoreNativeEffect290Pose(ref currentGraphics, frame with { Memory = selected.Memory, Staging = selected.Staging }, actor, source);
            return selected with { Memory = restored.Memory, Staging = restored.Staging!, Model = currentGraphics.Models[actor] };
        }
    }

    private NativeBattleEffect290Frame SelectNativeEffect290Fragments(ref NativeBattleGraphicsState graphics, NativeBattleEffect290Frame frame, NativeBattleEffect290SelectFragments675AA0 operation, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs)
    {
        int actor = NativeEffect290Actor(graphics, operation.ModelAddress);
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect290 requires its current addressed memory.");
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect290 requires its current caster skeleton.");
        NativeBattleEffectArena offsets = ReadBytes(frame, operation.OffsetRowsAddress, checked(source.BoneCount * 16));
        (frame, uint temporary) = Allocate(frame, 180);
        uint main = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleEffect290Profile.MainDescriptor).BankAddress;
        int scratchLength = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect290 requires its current projection capacity.")) * 8);
        uint scratch = checked(0x196B018u - (uint)scratchLength);
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0x38, BitConverter.GetBytes(0u)).Write(0x50, BitConverter.GetBytes(main)).Write(0x24, BitConverter.GetBytes(scratch)).Write(0x2C, BitConverter.GetBytes(model.Color40 ?? throw new NotSupportedException("Effect290 requires its current caster color."))).Write(0x30, BitConverter.GetBytes((model.SequenceState ?? throw new NotSupportedException("Effect290 requires its current object mask.")).MeshMask124));
        context = context.Write(0x34, BitConverter.GetBytes((int)operation.ChunkLimit)).Write(0x3C, BitConverter.GetBytes((int)operation.NormalSpeed)).Write(0x40, BitConverter.GetBytes((int)operation.ScaleBase)).Write(0x44, BitConverter.GetBytes((int)operation.RandomThreshold));
        frame = PublishContext(frame, temporary, context, 180);
        frame = PrepareNativeEffect290Pose(ref graphics, frame, actor, source, offsets);
        model = graphics.Models[actor];
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect290 requires its current caster world matrix.");
        NativeBattleBodyBoneMatrices publication = model.CurrentBodyBoneMatrices ?? throw new NotSupportedException("Effect290 requires its just-published offset pose.");
        var matrices = ImmutableArray.CreateBuilder<NativeBattleActionMatrix>(source.BoneCount);
        for (int bone = 0; bone < source.BoneCount; bone++)
        {
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(world, publication.Matrices[bone]);
            if (NativeEffect290BodyWord(offsets, bone * 16 + 14) != 0)
            {
                NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(NativeEffect290BodyWord(offsets, bone * 16 + 8), NativeEffect290BodyWord(offsets, bone * 16 + 10), NativeEffect290BodyWord(offsets, bone * 16 + 12)));
                NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(rotation, matrix);
                matrix = composed with { X = matrix.X, Y = matrix.Y, Z = matrix.Z };
            }
            matrices.Add(matrix with { X = unchecked(matrix.X + NativeEffect290BodyWord(offsets, bone * 16)), Y = unchecked(matrix.Y + NativeEffect290BodyWord(offsets, bone * 16 + 2)), Z = unchecked(matrix.Z + NativeEffect290BodyWord(offsets, bone * 16 + 4)) });
            frame = WriteNativeEffect290BonePadding(graphics, frame, actor, bone, 0);
        }
        graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { CurrentBodyBoneMatrices = publication with { Matrices = matrices.MoveToImmutable() } }) };
        foreach (short bone in operation.BoneSelectors)
        {
            if ((uint)bone >= source.BoneCount)
            {
                throw new InvalidDataException("Effect290 fragment selector leaves its actual skeleton.");
            }
            frame = WriteNativeEffect290BonePadding(graphics, frame, actor, bone, 1);
            frame = WriteBytes(frame, checked(operation.OffsetRowsAddress + (uint)(bone * 16 + 6)), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)2)), 2);
        }
        (uint address, Func<int, int, byte[]> read) = ReadNativeEffect290Geometry(graphics, frame, actor);
        frame = NativeBattleEffect290Fragments.Collect(frame, read, address, ReadNativeEffect290PackedBones(graphics, frame, actor, source), scratch, temporary + 0x20, resources, inputs);
        frame = RestoreNativeEffect290Pose(ref graphics, frame, actor, source);
        context = Context(frame, temporary, 180);
        if (BinaryPrimitives.ReadInt32LittleEndian(context.Read(0x38, 4)) > 0)
        {
            uint record = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0x48, 4));
            frame = WriteBytes(frame, record + 6, NativeBattleEffectArena.Clear().Write(0, new byte[2]), 2);
            frame = WriteBytes(frame, main + 0x64D8, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(record)), 4);
        }
        return Release(frame, 180);
    }

    private NativeBattleEffect290Frame BirthNativeEffect290Target(ref NativeBattleGraphicsState graphics, NativeBattleEffect290Frame frame, NativeBattleEffect290BirthTarget676340 operation, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs)
    {
        (frame, uint address) = NativeBattleEffect290Lower.TryBirthTarget(frame, resources);
        if (address == 0)
        {
            return frame;
        }
        int actor = NativeEffect290Actor(graphics, operation.ModelAddress);
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect290 requires its current addressed memory.");
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect290 requires its current caster skeleton.");
        if ((uint)operation.Bone >= source.BoneCount)
        {
            throw new InvalidDataException("Effect290 target selector leaves its actual skeleton.");
        }
        NativeBattleEffectArena offsets = ReadBytes(frame, operation.OffsetRowsAddress, checked(source.BoneCount * 16));
        frame = PrepareNativeEffect290Pose(ref graphics, frame, actor, source, offsets);
        NativeBattleEffect290PointStep point = inputs.Point502170(frame, operation.ModelAddress, operation.Bone, operation.Fraction, ReadBytes(frame, address + 12, 8));
        frame = point.Frame;
        NativeBattleEffectArena packed = point.Output8;
        for (int axis = 0; axis < 3; axis++)
        {
            packed = packed.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(NativeEffect290BodyWord(packed, axis * 2) + NativeEffect290BodyWord(offsets, operation.Bone * 16 + axis * 2)))));
        }
        frame = RestoreNativeEffect290Pose(ref graphics, frame, actor, source);
        frame = NativeBattleEffect290Lower.FinishBirthTarget(frame, address, packed, operation.Scale, resources);
        if (inputs.CapturePresentation)
        {
            NativeBattleSelectedProgrammeVisuals.Allocated(frame.Effect.Pools, address + 0x14);
        }
        return frame;
    }

    private static short NativeEffect290BodyWord(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
}
