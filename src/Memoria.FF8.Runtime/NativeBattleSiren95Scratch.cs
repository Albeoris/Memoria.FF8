using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleSiren95Scratch
{
    internal static NativeBattleEffectNodeScratch Sprite(NativeBattleEffectNodeScratch? retained, NativeBattleModulePoolTask task, NativeBattleActionMatrix view, short scale)
    {
        NativeBattleActionMatrix matrix = NativeBattleSearchSprites.Matrix(task, view, scale);
        NativeBattleEffectNodeScratch scratch = retained ?? NativeBattleEffectNodeScratch.Unknown();
        scratch = Dword(scratch, 0, unchecked((uint)matrix.X));
        scratch = Dword(scratch, 4, unchecked((uint)matrix.Y));
        scratch = Dword(scratch, 8, unchecked((uint)matrix.Z));
        scratch = Dword(scratch, 12, 4096);
        scratch = Dword(scratch, 16, 0);
        scratch = Dword(scratch, 20, 0);
        scratch = Dword(scratch, 24, 0);
        scratch = scratch.WritePrefix(28, BitConverter.GetBytes(task.ReadWord(0x56)));
        byte[] rotation = new byte[8];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(index * 2), matrix.Rotation[index]);
        }
        return scratch.WritePrefix(32, rotation).WriteSpriteMatrix(matrix);
    }

    internal static NativeBattleEffectNodeScratch Programme(NativeBattleEffectNodeScratch? retained, ushort u, NativeBattleTextureWindow after)
    {
        NativeBattleEffectNodeScratch scratch = retained ?? NativeBattleEffectNodeScratch.Unknown();
        scratch = Dword(scratch, 0, u);
        scratch = Dword(scratch, 4, 0);
        scratch = Dword(scratch, 8, 0x1000100);
        scratch = Dword(scratch, 12, 0);
        return Dword(scratch, 16, unchecked((uint)(ushort)after.Width | (uint)(ushort)after.Height << 16));
    }

    internal static NativeBattleEffectNodeScratch Model(NativeBattleEffectNodeScratch? retained, NativeBattleModulePoolTask task, NativeBattleActionMatrix view, NativeBattleEffectOwnedModelResource resource, bool rendered, NativeBattleEffectLocalBounds bounds, NativeBattleCameraProjection projection, NativeBattleShadowSource shadowSource, bool gouraud, NativeBattleScriptedGraphicsMemory? controls, out uint? sourceCursor)
    {
        NativeBattleActionMatrix world = new(Enumerable.Range(0, 9).Select(index => unchecked((short)task.ReadWord(0x70 + index * 2))).ToImmutableArray(), unchecked((int)task.ReadDword(0x84)), unchecked((int)task.ReadDword(0x88)), unchecked((int)task.ReadDword(0x8C)), 4096);
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Compose56C2F0(view, world);
        NativeBattleEffectNodeScratch scratch = retained ?? NativeBattleEffectNodeScratch.Unknown();
        byte[] rotation = new byte[18];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(index * 2), matrix.Rotation[index]);
        }
        scratch = scratch.WritePrefix(4, rotation);
        scratch = scratch with { Prefix2793E08 = scratch.Prefix2793E08!.MarkUnknown(22, 2) };
        scratch = Dword(scratch, 24, unchecked((uint)matrix.X));
        scratch = Dword(scratch, 28, unchecked((uint)matrix.Y));
        scratch = Dword(scratch, 32, unchecked((uint)matrix.Z));
        scratch = scratch.WriteFlags(NativeBattleSiren95Model.ProjectedAddress746C10);
        uint? lastCursor = null;
        if (rendered)
        {
            int first = NativeBattleDatDirectory.Section(resource.Bytes.AsSpan(), 1);
            _ = NativeBattleRawGeometryReader.Read((offset, length) => resource.Bytes.AsSpan().Slice(first + offset, length).ToArray(), resource.PoseSource.BoneCount, (index, counts, faces, next) =>
            {
                if ((task.ReadDword(0xAC) & (1u << (index & 31))) == 0)
                {
                    return;
                }
                scratch = Dword(scratch, 36, unchecked(resource.Address + (uint)(first + faces)));
                byte[] bytes = scratch.Bytes.ToArray();
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), counts[0]);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), counts[1]);
                scratch = scratch with { Bytes = bytes.ToImmutableArray(), KnownMask = byte.MaxValue };
                lastCursor = unchecked(resource.Address + (uint)(first + next));
            });
        }
        if ((task.ReadByte(0x30) & 0x20) == 0)
        {
            NativeBattleScriptedGraphicsMemory registers = controls ?? throw new NotSupportedException("Native Siren shadow scratch requires its actual current depth-cue registers.");
            NativeBattleShadowGeometry shape = NativeBattlePoseMatrices.ShadowGeometry5088A0(world, unchecked((short)task.ReadWord(0x54)), bounds, view, shadowSource, gouraud);
            NativeBattleModuleDepthCueVertex last = NativeBattleModuleProjection.ProjectWithCue45FAA0(shape.CameraMatrix, shape.Vertices[^1], projection, registers.Dqa, registers.Dqb);
            scratch = Dword(scratch, 0, last.Cue);
        }
        sourceCursor = lastCursor;
        return scratch;
    }

    private static NativeBattleEffectNodeScratch Dword(NativeBattleEffectNodeScratch scratch, int offset, uint value) => scratch.WritePrefix(offset, BitConverter.GetBytes(value));
}
