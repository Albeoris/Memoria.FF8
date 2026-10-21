using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModuleSpriteTemporaryResult(NativeBattleFileStaging Staging, NativeBattleModulePacketBatch Batch);

/// <summary>The retained180-byte571C80/571DC0 call frame in5082B0 memory.</summary>
public static class NativeBattleModuleSpriteTemporary
{
    public static NativeBattleFileStaging Begin571C80(NativeBattleFileStaging staging, uint address, uint resourceAddress, ushort flags)
    {
        return Word(Dword(staging, address, resourceAddress), address + 36, flags);
    }

    public static NativeBattleModuleSpriteTemporaryResult Emit571C80(NativeBattleFileStaging staging, uint address, NativeBattleModuleDraw draw, NativeBattleCameraProjection projection, short cameraRoll, int tableBaseBucket, byte depthShift, NativeBattlePackedMatrix32? capturedMatrix = null)
    {
        NativeBattleModuleResource resource = draw.Resource ?? throw new InvalidDataException("Native571C80 requires its actual sprite resource.");
        NativeBattleModuleSpriteFrame selected = draw.SpriteFrame ?? throw new InvalidDataException("Native571C80 requires its actual selected frame.");
        if (ReadDword(staging, address) != resource.Address)
        {
            throw new InvalidDataException("Native571C80 resource differs from its physical call frame.");
        }
        ushort flags = ReadWord(staging, address + 36);
        if ((flags & ~0x100) != (draw.SpriteFlags & ~0x100))
        {
            throw new InvalidDataException("Native571C80 flags differ from its physical call frame.");
        }
        int directory = 10 + draw.Phase * 2;
        ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(resource.Bytes.AsSpan().Slice(directory, 2));
        ushort count = BinaryPrimitives.ReadUInt16LittleEndian(resource.Bytes.AsSpan().Slice(8, 2));
        if (selected.ExtendedPacket)
        {
            flags |= 0x100;
        }
        draw = draw with { SpriteFlags = flags };
        staging = Word(Word(Word(Word(staging, address + 4, draw.Phase), address + 36, flags), address + 38, count), address + 40, unchecked((ushort)selected.NextOffset));
        staging = Dword(Dword(staging, address + 44, checked(resource.Address + offset + 4)), address + 48, checked((uint)(selected.Quads.Length / 20)));
        foreach (uint displacement in new uint[] { 72, 80, 88, 96 })
        {
            staging = Word(staging, address + displacement, 0);
        }
        NativeBattleActionMatrix main = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, projection.View ?? throw new NotSupportedException("Native571C80 requires its loaded camera."), cameraRoll);
        staging = Matrix(staging, address + 132, main);
        if (capturedMatrix is not null)
        {
            capturedMatrix.Validate();
            for (int index = 18; index < 20; index++)
            {
                staging = staging.Write(address + 132 + (uint)index, [capturedMatrix.Bytes[index]]);
                if (!capturedMatrix.Known[index])
                {
                    staging = staging.MarkUnknown(address + 132 + (uint)index, 1);
                }
            }
        }
        if ((flags & 2) != 0)
        {
            if (draw.Direction is not (4096 or -4096))
            {
                throw new NotSupportedException("Native571C80 requires its actual mirrored scale operand.");
            }
            staging = Dword(staging, address + 20, 4096);
        }
        if ((flags & 4) == 0)
        {
            staging = Dword(staging, address + 28, 0x808080);
        }
        else if (ReadRgb(staging, address + 28) != (draw.SpriteColor & 0xFFFFFF))
        {
            throw new InvalidDataException("Native571C80 color differs from its retained physical operand.");
        }
        for (int index = 0; index < 4; index++)
        {
            if ((flags & (0x10 << index)) == 0)
            {
                staging = staging.Write(address + 32 + (uint)index, [resource.Bytes[4 + index]]);
            }
            else if (draw.SpritePaletteOffsets.Length != 4 || staging.Read(address + 32 + (uint)index, 1)[0] != draw.SpritePaletteOffsets[index])
            {
                throw new InvalidDataException("Native571C80 palette differs from its retained physical BYTE.");
            }
        }
        staging = Dword(Dword(Dword(Dword(Dword(staging, address + 176, (flags & 8) != 0 ? 1u : 0u), address + 172, 0), address + 164, 4096), address + 168, 0), address + 60, uint.MaxValue);
        bool accepted = false;
        for (int offsetInFrame = 0; offsetInFrame < selected.Quads.Length; offsetInFrame += 20)
        {
            ReadOnlySpan<byte> quad = selected.Quads.AsSpan().Slice(offsetInFrame, 20);
            ushort quadFlags = BinaryPrimitives.ReadUInt16LittleEndian(quad.Slice(14));
            if ((quadFlags & 0xC00) != 0)
            {
                short angle = BinaryPrimitives.ReadInt16LittleEndian(quad.Slice(12));
                if (angle == 0)
                {
                    staging = Dword(Dword(Dword(staging, address + 164, 4096), address + 168, 0), address + 172, 0);
                }
                else if (ReadDword(staging, address + 172) != unchecked((uint)angle))
                {
                    staging = Dword(Dword(Dword(staging, address + 164, unchecked((uint)NativeBattleCameraMath.Cosine(angle))), address + 168, unchecked((uint)NativeBattleCameraMath.Sine(angle))), address + 172, unchecked((uint)angle));
                }
            }
            int width = 8 * quad[0];
            int height = 8 * quad[2];
            staging = Point(staging, address + 68, -width, -height);
            staging = Point(staging, address + 76, width, -height);
            staging = Point(staging, address + 84, -width, height);
            staging = Point(staging, address + 92, width, height);
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.ModuleSpriteQuadCamera(main, quad);
            for (int component = 0; component < 9; component++)
            {
                staging = Word(staging, address + 100 + (uint)component * 2, unchecked((ushort)matrix.Rotation[component]));
            }
            staging = Dword(Dword(Dword(staging, address + 120, unchecked((uint)matrix.X)), address + 124, unchecked((uint)matrix.Y)), address + 128, unchecked((uint)matrix.Z));
            NativeBattleModuleProjectedVertex a = NativeBattleModuleProjection.Project45FE10(matrix, new(unchecked((short)-width), unchecked((short)-height), 0), projection);
            NativeBattleModuleProjectedVertex b = NativeBattleModuleProjection.Project45FE10(matrix, new(unchecked((short)width), unchecked((short)-height), 0), projection);
            NativeBattleModuleProjectedVertex c = NativeBattleModuleProjection.Project45FE10(matrix, new(unchecked((short)-width), unchecked((short)height), 0), projection);
            uint gteFlags = a.Flags | b.Flags | c.Flags;
            staging = Dword(staging, address + 64, gteFlags);
            if ((gteFlags & 0x60000) != 0)
            {
                continue;
            }
            NativeBattleModuleProjectedVertex d = NativeBattleModuleProjection.Project45FAA0(matrix, new(unchecked((short)width), unchecked((short)height), 0), projection, 0, 0);
            if (!accepted)
            {
                int average = (a.Depth + b.Depth + c.Depth + d.Depth) >> 4;
                staging = Dword(staging, address + 60, unchecked((uint)average));
            }
            accepted = true;
        }
        NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, projection, cameraRoll, tableBaseBucket, depthShift);
        return new(staging, batch with { CapturedWrites = NativeBattleModulePackets.CaptureSprite571DC0(batch) });
    }

    private static NativeBattleFileStaging Matrix(NativeBattleFileStaging staging, uint address, NativeBattleActionMatrix matrix)
    {
        for (int component = 0; component < 9; component++)
        {
            staging = Word(staging, address + (uint)component * 2, unchecked((ushort)matrix.Rotation[component]));
        }
        staging = staging.MarkUnknown(address + 18, 2);
        return Dword(Dword(Dword(staging, address + 20, unchecked((uint)matrix.X)), address + 24, unchecked((uint)matrix.Y)), address + 28, unchecked((uint)matrix.Z));
    }

    private static NativeBattleFileStaging Point(NativeBattleFileStaging staging, uint address, int x, int y) => Word(Word(staging, address, unchecked((ushort)x)), address + 2, unchecked((ushort)y));
    private static ushort ReadWord(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(staging.Read(address, 2));
    private static uint ReadDword(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));

    private static uint ReadRgb(NativeBattleFileStaging staging, uint address)
    {
        byte[] bytes = staging.Read(address, 3);
        return (uint)(bytes[0] | bytes[1] << 8 | bytes[2] << 16);
    }

    private static NativeBattleFileStaging Word(NativeBattleFileStaging staging, uint address, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }

    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }
}
