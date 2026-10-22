using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleProjectedSpriteHead(NativeBattleEffectMemory Memory, NativeBattleEffectArena TemporaryD8);

public sealed record NativeBattleProjectedSpriteDraw(NativeBattleEffectMemory Memory, NativeBattleEffectArena TemporaryD8, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleProjectedSpriteD8
{
    public const int TemporaryLength = 0xD8;

    public static NativeBattleProjectedSpriteHead PrepareHead(NativeBattleEffectMemory memory, NativeBattleModulePoolTask task, NativeBattlePackedMatrix32 packedCamera, NativeBattleEffectArena temporaryD8)
    {
        packedCamera.Validate();
        NativeBattleActionMatrix camera = packedCamera.ReadMatrix();
        NativeBattleGeometryRegisterStep step = new(memory.GeometryRegisters ?? throw new NotSupportedException("Projected sprite requires its actual geometry register owner."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Projected sprite requires its actual data register owner."));
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                temporaryD8 = Word(temporaryD8, 0xB8 + row * 6 + column * 2, unchecked((ushort)camera.Rotation[column * 3 + row]));
            }
        }
        NativeBattleVector position = new(Signed(task, 0x1C), Signed(task, 0x1E), Signed(task, 0x20));
        temporaryD8 = Dword(Dword(Dword(temporaryD8, 0xCC, unchecked((uint)(int)position.X)), 0xD0, unchecked((uint)(int)position.Y)), 0xD4, unchecked((uint)(int)position.Z));
        short? padding = packedCamera.Known[18] && packedCamera.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(packedCamera.Bytes.AsSpan().Slice(18, 2)) : null;
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, camera, true, false, padding) };
        for (int column = 0; column < 3; column++)
        {
            step = NativeBattleGeometry.LoadIrColumn(step, ReadWord(temporaryD8, 0xB8 + column * 2), ReadWord(temporaryD8, 0xBE + column * 2), ReadWord(temporaryD8, 0xC4 + column * 2));
            step = NativeBattleGeometry.Transform(step, false, true);
            for (int row = 0; row < 3; row++)
            {
                temporaryD8 = Word(temporaryD8, 0xB8 + row * 6 + column * 2, unchecked((ushort)(step.Colors.Ir[row] ?? throw new InvalidDataException("Projected sprite matrix product did not publish its IR output."))));
            }
        }
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, camera, false, true, null) };
        step = NativeBattleGeometry.LoadVector(step, 0, position, position.Z < 0 ? (short)-1 : (short)0);
        step = NativeBattleGeometry.Transform(step);
        for (int axis = 0; axis < 3; axis++)
        {
            temporaryD8 = Dword(temporaryD8, 0xCC + axis * 4, unchecked((uint)(step.Colors.Mac[axis] ?? throw new InvalidDataException("Projected sprite translation did not publish its MAC output."))));
        }
        NativeBattleActionMatrix matrix = ReadMatrix(temporaryD8, 0xB8);
        (byte[] paddingBytes, bool[] paddingKnown) = temporaryD8.ReadSnapshot(0xCA, 2);
        padding = paddingKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(paddingBytes) : null;
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, matrix, true, true, padding) };
        temporaryD8 = Dword(temporaryD8, 0, task.ReadDword(0x4C));
        temporaryD8 = Word(temporaryD8, 4, task.ReadWord(0x50));
        temporaryD8 = Word(temporaryD8, 0x24, 0);
        temporaryD8 = Word(temporaryD8, 0xB4, task.ReadWord(0x56));
        return new(memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors }, temporaryD8);
    }

    public static NativeBattleProjectedSpriteDraw Draw(NativeBattleProjectedSpriteHead head, Func<uint, NativeBattleModuleResource> readResource, NativeBattleCameraProjection projection, short cameraRoll, int tableBaseBucket)
    {
        return DrawCore(head, readResource, projection, cameraRoll, tableBaseBucket, true, 2);
    }

    internal static NativeBattleProjectedSpriteDraw DrawUnbiased571C80(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary180, Func<uint, NativeBattleModuleResource> readResource, NativeBattleCameraProjection projection, short cameraRoll, int tableBaseBucket, byte depthShift)
    {
        return DrawCore(new(memory, temporary180), readResource, projection, cameraRoll, tableBaseBucket, false, depthShift);
    }

    private static NativeBattleProjectedSpriteDraw DrawCore(NativeBattleProjectedSpriteHead head, Func<uint, NativeBattleModuleResource> readResource, NativeBattleCameraProjection projection, short cameraRoll, int tableBaseBucket, bool readD8DepthBias, byte depthShift)
    {
        NativeBattleEffectArena temporary = head.TemporaryD8;
        ushort callerFlags = ReadWord(temporary, 0x24);
        int supportedFlags = readD8DepthBias ? 0x100 : 0x10D;
        if ((callerFlags & ~supportedFlags) != 0)
        {
            throw new InvalidDataException("Projected sprite dispatcher requires its supported actual caller flags.");
        }
        NativeBattleModuleResource resource = readResource(unchecked((uint)ReadInt(temporary, 0)));
        ushort phase = ReadWord(temporary, 4);
        NativeBattleModuleSpriteFrame selected = resource.ProjectedFrame571C80(phase);
        int entry = BinaryPrimitives.ReadUInt16LittleEndian(resource.Bytes.AsSpan().Slice(10 + phase * 2, 2));
        ushort flags = (ushort)(callerFlags | (selected.ExtendedPacket ? 0x100 : 0));
        temporary = Word(Word(Word(temporary, 0x24, flags), 0x26, BinaryPrimitives.ReadUInt16LittleEndian(resource.Bytes.AsSpan().Slice(8, 2))), 0x28, unchecked((ushort)selected.NextOffset));
        temporary = Dword(Dword(temporary, 0x2C, checked(resource.Address + (uint)entry + 4)), 0x30, checked((uint)(selected.Quads.Length / 20)));
        foreach (int offset in new[] { 0x48, 0x50, 0x58, 0x60 })
        {
            temporary = Word(temporary, offset, 0);
        }
        NativeBattleGeometryRegisterStep step = new(head.Memory.GeometryRegisters ?? throw new NotSupportedException("Projected sprite requires its actual geometry register owner."), head.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Projected sprite requires its actual data register owner."));
        NativeBattlePackedMatrix32 captured = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(step.Geometry);
        for (int index = 0; index < 32; index++)
        {
            temporary = temporary.Write(0x84 + index, [captured.Bytes[index]]);
            if (!captured.Known[index])
            {
                temporary = temporary.MarkUnknown(0x84 + index, 1);
            }
        }
        int callerAngle = (flags & 1) != 0 ? unchecked(ReadInt(temporary, 8) - cameraRoll) : -(int)cameraRoll;
        NativeBattleActionMatrix main = NativeBattleGfMatrices.RotateZ56D090(ReadMatrix(temporary, 0x84), callerAngle);
        for (int index = 0; index < 9; index++)
        {
            temporary = Word(temporary, 0x84 + index * 2, unchecked((ushort)main.Rotation[index]));
        }
        if ((flags & 4) == 0)
        {
            temporary = Dword(temporary, 0x1C, 0x808080);
        }
        uint? tint = null;
        if (!selected.Quads.IsEmpty)
        {
            byte[] rgb = temporary.Read(0x1C, 3);
            tint = (uint)(rgb[0] | rgb[1] << 8 | rgb[2] << 16);
        }
        temporary = temporary.Write(0x20, resource.Bytes.AsSpan().Slice(4, 4));
        temporary = Dword(Dword(Dword(Dword(Dword(temporary, 0xB0, (flags & 8) != 0 ? 1u : 0u), 0xAC, 0), 0xA4, 4096), 0xA8, 0), 0x3C, uint.MaxValue);
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 255) { Resource = resource, SpriteFrame = selected, Phase = phase, SpriteFlags = flags };
        if (tint is uint actualTint)
        {
            draw = draw with { SpriteColor = actualTint };
        }
        NativeBattleModulePacketBatch encoded = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, cameraRoll, tableBaseBucket, depthShift, main);
        NativeBattleAllocatedPrimitive[] quads = encoded.Primitives.Where(packet => packet.Primitive.Words.Length == 10).ToArray();
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int accepted = 0;
        int cursor = 0;
        for (int offset = 0; offset < selected.Quads.Length; offset += 20)
        {
            ReadOnlySpan<byte> quad = selected.Quads.AsSpan().Slice(offset, 20);
            ushort quadFlags = BinaryPrimitives.ReadUInt16LittleEndian(quad.Slice(14));
            if ((quadFlags >> 12) > 3)
            {
                throw new NotSupportedException("Projected sprite requires its four original palette BYTE selectors.");
            }
            if ((quadFlags & 0xC00) != 0)
            {
                short angle = BinaryPrimitives.ReadInt16LittleEndian(quad.Slice(12));
                if (angle == 0 || ReadInt(temporary, 0xAC) != angle)
                {
                    temporary = Dword(Dword(Dword(temporary, 0xA4, unchecked((uint)NativeBattleCameraMath.Cosine(angle))), 0xA8, unchecked((uint)NativeBattleCameraMath.Sine(angle))), 0xAC, unchecked((uint)(int)angle));
                }
            }
            NativeBattleActionMatrix local = NativeBattlePoseMatrices.ModuleSpriteQuadCamera(NativeBattleCuragaMatrices.Identity8DD770(), quad);
            for (int index = 0; index < 9; index++)
            {
                temporary = Word(temporary, 0x64 + index * 2, unchecked((ushort)local.Rotation[index]));
            }
            short width = (short)(8 * quad[0]);
            short height = (short)(8 * quad[2]);
            NativeBattleVector[] corners = [new(unchecked((short)-width), unchecked((short)-height), 0), new(width, unchecked((short)-height), 0), new(unchecked((short)-width), height, 0), new(width, height, 0)];
            for (int index = 0; index < 4; index++)
            {
                temporary = Word(Word(temporary, 0x44 + index * 8, unchecked((ushort)corners[index].X)), 0x46 + index * 8, unchecked((ushort)corners[index].Y));
            }
            temporary = Dword(Dword(Dword(temporary, 0x78, unchecked((uint)(8 * quad[0] + 16 * BinaryPrimitives.ReadInt16LittleEndian(quad.Slice(8))))), 0x7C, unchecked((uint)(8 * quad[2] + 16 * BinaryPrimitives.ReadInt16LittleEndian(quad.Slice(10))))), 0x80, 0);
            step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, main, true, false, Padding(temporary, 0x96)) };
            for (int column = 0; column < 3; column++)
            {
                step = NativeBattleGeometry.LoadIrColumn(step, ReadWord(temporary, 0x64 + column * 2), ReadWord(temporary, 0x6A + column * 2), ReadWord(temporary, 0x70 + column * 2));
                step = NativeBattleGeometry.Transform(step, false, true);
                for (int row = 0; row < 3; row++)
                {
                    temporary = Word(temporary, 0x64 + row * 6 + column * 2, unchecked((ushort)step.Colors.Ir[row]!.Value));
                }
            }
            step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, main, false, true, null) };
            NativeBattleVector displacement = new(unchecked((short)ReadInt(temporary, 0x78)), unchecked((short)ReadInt(temporary, 0x7C)), 0);
            step = NativeBattleGeometry.LoadVector(step, 0, displacement, 0);
            step = NativeBattleGeometry.Transform(step);
            for (int axis = 0; axis < 3; axis++)
            {
                temporary = Dword(temporary, 0x78 + axis * 4, unchecked((uint)step.Colors.Mac[axis]!.Value));
            }
            NativeBattleActionMatrix matrix = ReadMatrix(temporary, 0x64);
            step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, matrix, true, true, Padding(temporary, 0x76)) };
            for (int index = 0; index < 3; index++)
            {
                step = NativeBattleGeometry.LoadVector(step, index, corners[index], Padding(temporary, 0x4A + index * 8));
            }
            NativeBattleScriptedGraphicsMemory controls = head.Memory.ScriptedGraphics ?? throw new NotSupportedException("Projected sprite requires its actual projection controls.");
            step = NativeBattleGeometry.ProjectTriple(step, projection, controls.Dqa, controls.Dqb);
            uint projectedFlags = step.Colors.Flags!.Value;
            temporary = Dword(temporary, 0x40, projectedFlags);
            byte[] candidate = new byte[40];
            NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 40).ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(candidate, 0x9000000);
            for (int channel = 0; channel < 3; channel++)
            {
                candidate[4 + channel] = unchecked((byte)((quad[1] * ((tint!.Value >> (channel * 8)) & 255)) >> 7));
            }
            candidate[7] = quad[3];
            quad.Slice(4, 4).CopyTo(candidate.AsSpan(12));
            int palette = unchecked((sbyte)resource.Bytes[4 + (quadFlags >> 12)]);
            BinaryPrimitives.WriteUInt16LittleEndian(candidate.AsSpan(14), unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(quad.Slice(6)) + (palette << 6))));
            candidate[20] = candidate[36] = unchecked((byte)Math.Min(quad[4] + quad[0] - ((flags & 8) != 0 ? 1 : 0), 255));
            candidate[21] = quad[5];
            candidate[28] = quad[4];
            foreach (int index in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 12, 13, 14, 15, 20, 21, 28, 36 })
            {
                mask[index] = NativeBattleGfPacketByteState.Known;
            }
            if ((projectedFlags & 0x60000) != 0)
            {
                attempts.Add(new(cursor, candidate.ToImmutableArray(), mask.ToImmutableArray(), null));
                continue;
            }
            step = NativeBattleGeometry.LoadVector(step, 0, corners[3], Padding(temporary, 0x62));
            step = NativeBattleGeometry.ProjectSingle(step, projection, controls.Dqa, controls.Dqb);
            int depth = ReadInt(temporary, 0x3C);
            bool first = depth < 0;
            if (first)
            {
                short zsf4 = controls.Zsf4 ?? throw new NotSupportedException("Projected sprite requires its reached AVSZ4 scale owner.");
                step = NativeBattleGeometry.Average45E610(step, zsf4);
                depth = unchecked((int)step.Geometry.Otz!.Value);
            }
            if (readD8DepthBias)
            {
                depth = Math.Max(0, unchecked(depth + unchecked((short)ReadWord(temporary, 0xB4))));
            }
            temporary = Dword(temporary, 0x3C, unchecked((uint)depth));
            int bucket = unchecked(tableBaseBucket + (depth >> depthShift));
            if (accepted >= quads.Length)
            {
                throw new InvalidDataException("Projected sprite GTE acceptance differs from the shared packet geometry.");
            }
            NativeBattleGeometryPrimitive primitive = quads[accepted++].Primitive with { Bucket = bucket };
            for (int index = 0; index < primitive.Words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(candidate.AsSpan(index * 4), primitive.Words[index]);
            }
            for (int index = 0; index < 40; index++)
            {
                if (index is not (30 or 31 or 38 or 39))
                {
                    mask[index] = NativeBattleGfPacketByteState.Known;
                }
            }
            attempts.Add(new(cursor, candidate.ToImmutableArray(), mask.ToImmutableArray(), null));
            bool extended = first && (flags & 0x100) != 0;
            if (extended)
            {
                byte[] environment = new byte[12];
                BinaryPrimitives.WriteUInt32LittleEndian(environment, 0x1000000);
                BinaryPrimitives.WriteUInt32LittleEndian(environment.AsSpan(4), 0xE1000220);
                NativeBattleGeometryPrimitive command = new(bucket, [0x1000000, 0xE1000220], [0, 0, 0, 0]);
                attempts.Add(new(cursor + 40, environment.ToImmutableArray(), Enumerable.Range(0, 12).Select(index => index < 8 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray(), command));
            }
            attempts.Add(new(cursor, candidate.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 40).ToImmutableArray(), primitive));
            cursor += extended ? 52 : 40;
        }
        if (accepted != quads.Length || cursor != encoded.AllocationBytes)
        {
            throw new InvalidDataException("Projected sprite packet acceptance or cursor differs from its native geometry.");
        }
        NativeBattleEffectMemory memory = head.Memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors };
        return new(memory, temporary, new(attempts.ToImmutable(), cursor));
    }

    private static short? Padding(NativeBattleEffectArena memory, int offset)
    {
        (byte[] bytes, bool[] known) = memory.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }

    private static NativeBattleActionMatrix ReadMatrix(NativeBattleEffectArena memory, int offset)
    {
        short[] rotation = Enumerable.Range(0, 9).Select(index => unchecked((short)ReadWord(memory, offset + index * 2))).ToArray();
        return new(rotation.ToImmutableArray(), ReadInt(memory, offset + 20), ReadInt(memory, offset + 24), ReadInt(memory, offset + 28), 4096);
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static ushort ReadWord(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(offset, 2));
    private static int ReadInt(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadInt32LittleEndian(memory.Read(offset, 4));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena memory, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return memory.Write(offset, bytes);
    }
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena memory, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return memory.Write(offset, bytes);
    }
}
