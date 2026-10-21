using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAquaSpriteComposition(NativeBattleActionMatrix Billboard, NativeBattleActionMatrix Main, NativeBattleActionMatrix Quad, NativeBattleVector[] Corners, ImmutableArray<byte> Resource, ushort Phase, ushort FrameOffset, NativeBattleGfGeometryBatch Batch);

public static partial class NativeBattleAquaRendering
{
    public static NativeBattleActionMatrix ComposeWave(NativeBattleModulePoolTask task, NativeBattleActionMatrix camera)
    {
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(matrix, Word(task, 0x46));
        matrix = NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, Word(task, 0x44));
        matrix = NativeBattleCuragaMatrices.RotateZ8DD960(matrix, Word(task, 0x48));
        matrix = matrix with { X = Word(task, 0x1C), Y = Word(task, 0x1E), Z = Word(task, 0x20) };
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38)));
        return NativeBattleGfMatrices.Compose56C2F0(camera, matrix);
    }

    public static NativeBattleEsunaMeshContext WaveContext(NativeBattleModulePoolTask task)
    {
        return new(0xF0, task.ReadDword(0x40), Word(task, 0x50), Word(task, 0x52), task.ReadWord(0x5E), 0, new(0, 0, 256, 256), new(0, 0, 128, 256), 17, 2);
    }

    public static NativeBattleActionMatrix ComposeBillboard(NativeBattleActionMatrix camera, NativeBattleVector position)
    {
        camera.Validate();
        ImmutableArray<short> r = camera.Rotation;
        NativeBattleActionMatrix transpose = new([r[0], r[3], r[6], r[1], r[4], r[7], r[2], r[5], r[8]], camera.X, camera.Y, camera.Z, 4096);
        NativeBattleActionMatrix billboard = NativeBattlePoseMatrices.MultiplyGteRotation45E470(camera, transpose);
        NativeBattleFullMacVertex point = NativeBattleGfMatrices.TransformMac460840(camera, position);
        return billboard with { X = point.X, Y = point.Y, Z = point.Z };
    }

    public static NativeBattleAquaSpriteComposition ComposeSprite(NativeBattleModulePoolTask task, NativeBattleAquaResources resources, NativeBattleActionMatrix camera, NativeBattleCameraProjection projection, short cameraRoll)
    {
        uint address = task.ReadDword(0x4C);
        if (!resources.Sprites.TryGetValue(address, out ImmutableArray<byte> resource))
        {
            throw new NotSupportedException("The Aqua sprite pointer leaves its authenticated physical source directory.");
        }
        ushort phase = task.ReadWord(0x50);
        if (resource.IsDefaultOrEmpty || resource.Length < 12 || phase >= BinaryPrimitives.ReadUInt16LittleEndian(resource.AsSpan().Slice(8)))
        {
            throw new InvalidDataException("The Aqua sprite frame leaves its actual directory.");
        }
        ushort frame = BinaryPrimitives.ReadUInt16LittleEndian(resource.AsSpan().Slice(10 + phase * 2));
        if (frame > resource.Length - 24 || BinaryPrimitives.ReadUInt32LittleEndian(resource.AsSpan().Slice(frame)) != 1)
        {
            throw new NotSupportedException("The Aqua sprite requires its original single nonextended quad frame.");
        }
        ReadOnlySpan<byte> quad = resource.AsSpan().Slice(frame + 4, 20);
        if (BinaryPrimitives.ReadInt16LittleEndian(quad[12..]) != 0)
        {
            throw new NotSupportedException("The Aqua sprite requires its authenticated zero-angle quad operands.");
        }
        NativeBattleActionMatrix billboard = ComposeBillboard(camera, new(Word(task, 0x1C), Word(task, 0x1E), Word(task, 0x20)));
        NativeBattleActionMatrix? main = null;
        NativeBattleActionMatrix? selected = null;
        NativeBattleGfGeometryBatch batch = NativeBattleCuragaProjectedSprites.EmitBatch8DD260(resource, phase, billboard, projection, cameraRoll, 17, 2, Word(task, 0x56), value => main = value, value => selected = value);
        short width = unchecked((short)(8 * quad[0]));
        short height = unchecked((short)(8 * quad[2]));
        NativeBattleVector[] corners = [new(unchecked((short)-width), unchecked((short)-height), 0), new(width, unchecked((short)-height), 0), new(unchecked((short)-width), height, 0), new(width, height, 0)];
        return new(billboard, main ?? throw new InvalidDataException("The Aqua sprite preparation did not produce its main matrix."), selected ?? throw new InvalidDataException("The Aqua sprite preparation did not produce its selected quad matrix."), corners, resource, phase, frame, batch);
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
