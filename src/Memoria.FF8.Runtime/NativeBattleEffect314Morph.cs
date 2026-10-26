using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect314Morph
{
    public static NativeBattleEffect314Frame Draw663850(NativeBattleEffect314Frame frame, NativeBattleEffect314Resources resources, NativeBattleEffect314Inputs inputs, uint modelAddress, uint offsetAddress, int texturePageAddition, int clutAddition)
    {
        NativeBattleEffect314AddressStep allocation = inputs.AllocateTemporary(frame, 156);
        frame = allocation.Frame;
        uint temporaryAddress = allocation.Address;
        frame = inputs.Execute(frame, new NativeBattleEffect314Shadow5088A0(modelAddress, 4112, 16));
        frame = inputs.Execute(frame, new NativeBattleEffect314Prepare662C00(modelAddress, offsetAddress));
        NativeBattleGraphicsModel model = inputs.ReadModel(frame, modelAddress);
        NativeBattleEffect298BodySource source = inputs.ReadBody(frame, modelAddress);
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect314 morph requires its actual model world matrix.");
        NativeBattleEffectArena offsets = inputs.ReadArena(frame, offsetAddress, checked(source.CurrentBones.Length * 16));
        ImmutableArray<NativeBattlePackedMatrix32>.Builder bones = ImmutableArray.CreateBuilder<NativeBattlePackedMatrix32>(source.CurrentBones.Length);
        for (int bone = 0; bone < source.CurrentBones.Length; bone++)
        {
            NativeBattleActionMatrix transformed = NativeBattleGfMatrices.Compose56C2F0(world, source.CurrentBones[bone].ReadMatrix());
            transformed = transformed with { X = unchecked(transformed.X + Word(offsets, bone * 16)), Y = unchecked(transformed.Y + Word(offsets, bone * 16 + 2)), Z = unchecked(transformed.Z + Word(offsets, bone * 16 + 4)) };
            transformed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), transformed);
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(transformed, Padding(source.CurrentBones[bone]) is short padding ? unchecked((ushort)padding) : null);
            bones.Add(packed);
            frame = inputs.Execute(frame, new NativeBattleEffect314WriteArena(inputs.ResolveBoneRecord(frame, modelAddress, bone), NativeBattleEffect314Controller.PackedArena(packed), 32));
        }
        int scratchLength = source.VertexScratchLength ?? throw new NotSupportedException("Effect314 morph requires its loaded projection capacity.");
        uint scratchAddress = source.VertexScratchAddress ?? throw new NotSupportedException("Effect314 morph requires its physical projection workspace.");
        NativeBattleEffectArena temporary = inputs.ReadArena(frame, temporaryAddress, 156);
        temporary = temporary.Write(0x18, BitConverter.GetBytes(unchecked((short)texturePageAddition))).Write(0x1A, BitConverter.GetBytes(unchecked((short)clutAddition)));
        temporary = temporary.Write(0x50, BitConverter.GetBytes(offsetAddress)).Write(4, BitConverter.GetBytes(scratchAddress));
        temporary = temporary.Write(0xC, new byte[4]).Write(0x10, BitConverter.GetBytes((short)320)).Write(0x12, BitConverter.GetBytes((short)216));
        temporary = temporary.Write(0x14, BitConverter.GetBytes(model.Color40 ?? throw new NotSupportedException("Effect314 morph requires its current model color.")));
        temporary = temporary.Write(0x1C, BitConverter.GetBytes((model.SequenceState ?? throw new NotSupportedException("Effect314 morph requires its current mesh mask.")).MeshMask124));
        NativeBattleEffect314MorphBodyStep drawn = DrawBody(frame.Memory, temporary, inputs.ReadArena(frame, scratchAddress, scratchLength), source.ReadGeometry, source.GeometryAddress, bones.MoveToImmutable(), offsets, inputs.ReadProjection(frame), frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect314 morph requires current projection controls."), scratchAddress, scratchLength, 17, 0);
        frame = frame with { Memory = drawn.Memory };
        frame = inputs.Execute(frame, new NativeBattleEffect314WriteArena(temporaryAddress, drawn.Temporary156, 156));
        frame = inputs.Execute(frame, new NativeBattleEffect314WriteArena(scratchAddress, drawn.VertexScratch, scratchLength));
        frame = inputs.Execute(frame, new NativeBattleEffect314DrawBatch(drawn.Batch, 0x1D8E054));
        frame = inputs.Execute(frame, new NativeBattleEffect314Prepare662C00(modelAddress, offsetAddress));
        return inputs.ReleaseTemporary(frame, 156);
    }
}
