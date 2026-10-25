using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect308Controller;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect308Frame SelectNativeEffect308Fragments(ref NativeBattleGraphicsState graphics, NativeBattleEffect308Frame frame, NativeBattleEffect308SelectFragments667DB0 operation, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        int actor = NativeEffect308Actor(graphics, operation.ModelAddress);
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect308 requires its current addressed memory.");
        NativeBattlePoseSource source = inputs.ReadPoseSource(operation.ModelAddress);
        NativeBattleEffectArena offsets = ReadBytes(frame, operation.OffsetRowsAddress, checked(source.BoneCount * 16));
        (frame, uint temporary) = Allocate(frame, 172);
        uint main = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleEffect308Profile.MainDescriptor).BankAddress;
        int scratchLength = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect308 requires its current projection capacity.")) * 8);
        uint scratch = checked(0x196B018u - (uint)scratchLength);
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattleEffectArena context = Context(frame, temporary, 172).Write(0x38, BitConverter.GetBytes(0u)).Write(0x48, BitConverter.GetBytes(main)).Write(0x24, BitConverter.GetBytes(scratch)).Write(0x2C, BitConverter.GetBytes(model.Color40 ?? throw new NotSupportedException("Effect308 requires its current caster color."))).Write(0x30, BitConverter.GetBytes((model.SequenceState ?? throw new NotSupportedException("Effect308 requires its current object mask.")).MeshMask124));
        context = context.Write(0x34, BitConverter.GetBytes((int)operation.SmokeLimit)).Write(0x3C, BitConverter.GetBytes((int)operation.NormalSpeed)).Write(0x40, BitConverter.GetBytes((int)operation.UnusedParameter)).Write(0x44, BitConverter.GetBytes((int)operation.RandomThreshold));
        frame = PublishContext(frame, temporary, context, 172);
        frame = PrepareNativeEffect308Pose(ref graphics, frame, actor, source, offsets);
        model = graphics.Models[actor];
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect308 requires its current caster world matrix.");
        NativeBattleBodyBoneMatrices publication = model.CurrentBodyBoneMatrices ?? throw new NotSupportedException("Effect308 requires its just-published offset pose.");
        var matrices = ImmutableArray.CreateBuilder<NativeBattleActionMatrix>(source.BoneCount);
        for (int bone = 0; bone < source.BoneCount; bone++)
        {
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(world, publication.Matrices[bone]);
            if (NativeEffect308BodyWord(offsets, bone * 16 + 14) != 0)
            {
                NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(NativeEffect308BodyWord(offsets, bone * 16 + 8), NativeEffect308BodyWord(offsets, bone * 16 + 10), NativeEffect308BodyWord(offsets, bone * 16 + 12)));
                NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(rotation, matrix);
                matrix = composed with { X = matrix.X, Y = matrix.Y, Z = matrix.Z };
            }
            matrices.Add(matrix with { X = unchecked(matrix.X + NativeEffect308BodyWord(offsets, bone * 16)), Y = unchecked(matrix.Y + NativeEffect308BodyWord(offsets, bone * 16 + 2)), Z = unchecked(matrix.Z + NativeEffect308BodyWord(offsets, bone * 16 + 4)) });
            frame = WriteNativeEffect308BonePadding(graphics, frame, actor, bone, 0);
        }
        graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { CurrentBodyBoneMatrices = publication with { Matrices = matrices.MoveToImmutable() } }) };
        foreach (short bone in operation.BoneSelectors)
        {
            if ((uint)bone >= source.BoneCount)
            {
                throw new InvalidDataException("Effect308 fragment selector leaves its actual skeleton.");
            }
            frame = WriteNativeEffect308BonePadding(graphics, frame, actor, bone, 1);
            frame = WriteBytes(frame, checked(operation.OffsetRowsAddress + (uint)(bone * 16 + 6)), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)2)), 2);
        }
        (uint address, Func<int, int, byte[]> read) = ReadNativeEffect308Geometry(graphics, frame, actor);
        frame = NativeBattleEffect308Fragments.Collect667F40(frame, read, address, ReadNativeEffect308PackedBones(graphics, frame, actor, source), scratch, scratchLength, temporary + 0x20, resources, inputs);
        frame = RestoreNativeEffect308Pose(ref graphics, frame, actor, source);
        return Release(frame, 172);
    }

    private static short NativeEffect308BodyWord(NativeBattleEffectArena value, int offset)
    {
        return BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
    }
}
