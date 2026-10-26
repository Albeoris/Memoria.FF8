using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    private static NativeBattleEffect326Frame DrawSplitModel(NativeBattleEffect326Frame frame, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs, uint taskAddress, int localAge, bool animated)
    {
        const uint modelAddress = 0x24F1268;
        (frame, uint temporary) = Allocate(frame, 464);
        NativeBattleEffectArena context = Context(frame, temporary, 464);
        NativeBattleEffect326PointStep point = ReadOwnedBone631050(frame, resources, modelAddress, 33, 2048, SliceArena(context, 0x40, 8));
        frame = point.Frame;
        context = CopyBytes(point.Point8, 0, context, 0x40, 8);
        short anchorX = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(context.Read(0x40, 2)) + 13));
        context = context.Write(0x40, BitConverter.GetBytes(anchorX)).Write(0xA0, BitConverter.GetBytes((short)-4096)).Write(0xA2, new byte[4]);
        int dot = unchecked(anchorX * -4096);
        context = context.Write(0xA6, BitConverter.GetBytes(unchecked((short)-(dot >> 12))));
        NativeBattleActionMatrix world = ReadPacked(frame, modelAddress + 0x40).ReadMatrix();
        NativeBattleActionMatrix first;
        NativeBattleActionMatrix second;
        if (animated)
        {
            int angle = Signed(Read(frame, taskAddress), 0x20);
            first = NativeBattleGfMatrices.Compose56C2F0(world with { X = 800 }, NativeBattleGfMatrices.Euler56CE30(new(0, 0, unchecked((short)angle))));
            second = NativeBattleGfMatrices.Compose56C2F0(world with { X = -800 }, NativeBattleGfMatrices.Euler56CE30(new(0, 0, unchecked((short)-angle))));
            if (localAge >= 46)
            {
                int fade = 4096 - NativeBattleCameraMath.Sine((localAge - 46) * 1024 / 12);
                NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect326 split requires its current primitive controls.");
                NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Effect326 split requires its far-color controls.")) with { FarRed = 128u << 4, FarGreen = 128u << 4, FarBlue = 128u << 4 };
                NativeBattleScriptedDpcsResult faded = NativeBattleScriptedDpcs.Transform45F270(frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect326 split requires its current arithmetic registers."), 0, unchecked((uint)fade), far, inputs.Colors);
                frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = faded.Registers, PrimitiveProcess = process with { ModelRendererRegisters = far } } };
                frame = WriteBytes(frame, modelAddress + 0x28, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((faded.PackedRgb & 0xFFFFFF) | 0x02000000)), 4);
            }
        }
        else
        {
            int distance = unchecked(localAge * 8 - 8);
            first = world with { X = unchecked(world.X + distance) };
            second = world with { X = unchecked(world.X - distance) };
        }
        frame = PublishContext(frame, temporary, context, 464);
        frame = DrawOwnedSplit62D610(frame, resources, inputs, temporary, modelAddress, first, second);
        return Release(frame, 464);
    }

    private static NativeBattleEffect326Frame DrawOwnedSplit62D610(NativeBattleEffect326Frame frame, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs, uint temporary, uint modelAddress, NativeBattleActionMatrix first, NativeBattleActionMatrix second)
    {
        NativeBattleEffectOwnedModelResource resource = ModelResource(resources, modelAddress);
        NativeBattleCactuarRawModelState model = RequireModel(frame, modelAddress);
        NativeBattleCameraProjection projection = inputs.ReadProjection(frame);
        if ((model.Model[0] & 0x20) == 0)
        {
            NativeBattleVector minimum = ReadSplitVector(frame, modelAddress + 0x34);
            NativeBattleVector maximum = ReadSplitVector(frame, modelAddress + 0x3A);
            NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(RequireStaging(frame), Registers(frame), ReadPacked(frame, modelAddress + 0x40).ReadMatrix(), unchecked((short)BinaryPrimitives.ReadUInt16LittleEndian(model.Model.AsSpan().Slice(0x24, 2))), new(minimum, maximum), BinaryPrimitives.ReadUInt32LittleEndian(model.Model.AsSpan().Slice(0x2C, 4)), projection, frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect326 shadow requires its current depth controls."), inputs.ShadowSource ?? throw new NotSupportedException("Effect326 shadow requires its actual template."), inputs.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Effect326 shadow requires its current primitive capabilities."), 4121, 16);
            frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 120), Memory = frame.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors } };
            frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(shadow.Batch, 0x1D8E054));
            frame = Release(frame, 120);
        }
        NativeBattleEffectArena context = Context(frame, temporary, 464);
        NativeBattleActionMatrix view = ReadPacked(frame, NativeBattleEffect326Profile.ViewMatrix).ReadMatrix();
        context = WriteSplitMatrix(context, 0x190, NativeBattleGfMatrices.Compose56C2F0(view, first));
        context = WriteSplitMatrix(context, 0x1B0, NativeBattleGfMatrices.Compose56C2F0(view, second));
        uint scratch = frame.State.ReadDword(0xE196B0);
        context = context.Write(4, BitConverter.GetBytes(scratch)).Write(0x10, ReadBytes(frame, modelAddress + 0x7C, 4).Read(0, 4)).Write(0x18, ReadBytes(frame, modelAddress + 0x28, 4).Read(0, 4));
        uint working = model.WorkingBase ?? throw new NotSupportedException("Effect326 split requires its current owned model source.");
        uint geometry = checked(working + (uint)resource.Section(1));
        uint skeleton = checked(working + (uint)resource.Section(0));
        int scratchLength = checked(resource.Geometry.Max(mesh => mesh.Vertices.Length) * 8);
        ImmutableArray<NativeBattlePackedMatrix32> bones = Enumerable.Range(0, resource.PoseSource.BoneCount).Select(index => ReadPacked(frame, checked(skeleton + 32u + (uint)(index * 48)))).ToImmutableArray();
        int cursor = frame.Memory.PrimitiveProcess?.ByteCursor ?? throw new NotSupportedException("Effect326 split requires its current COMMON cursor.");
        NativeBattleEffect326Frame current = frame;
        NativeBattleEffect167BodyStep drawn = NativeBattleEffect167BodyDrawing.Draw(frame.Memory, context, temporary, (address, length) => ReadBytes(current, address, length), geometry, bones, ReadBytes(frame, scratch, scratchLength), scratchLength, projection, Enumerable.Range(0, 56).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(0xE19988 + (uint)(index * 4), 4))).ToImmutableArray(), cursor, 17, 2);
        frame = frame with { Memory = drawn.Memory };
        frame = WriteBytes(frame, scratch, drawn.Scratch, scratchLength);
        frame = PublishContext(frame, temporary, drawn.Temporary464, 464);
        frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(drawn.Batch, 0x1D8E054));
        return PublishModelPose(frame, resources, modelAddress);
    }

    private static NativeBattleEffectArena SliceArena(NativeBattleEffectArena source, int offset, int length) => CopyBytes(source, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);

    private static NativeBattleVector ReadSplitVector(NativeBattleEffect326Frame frame, uint address)
    {
        byte[] bytes = ReadBytes(frame, address, 6).Read(0, 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
    }

    private static NativeBattleEffectArena WriteSplitMatrix(NativeBattleEffectArena context, int offset, NativeBattleActionMatrix matrix)
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, null);
        return context.Write(offset, packed.Bytes.AsSpan().Slice(0, 18)).Write(offset + 20, packed.Bytes.AsSpan().Slice(20, 12));
    }
}
