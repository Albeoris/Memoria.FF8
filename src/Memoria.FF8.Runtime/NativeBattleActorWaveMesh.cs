using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleActorWaveMeshStep(NativeBattleGfGeometryBatch Batch, NativeBattleEffectArena Context104, NativeBattleGeometryRegisterStep Registers, NativeBattleModelRendererRegisters FarControls);

public static class NativeBattleActorWaveMesh
{
    public const uint ContextAddress = 0x2793DF0;
    public const int ContextLength = 104;
    public const int SharedViewOffset = 76;

    public static NativeBattleEffectArena PrepareContext(NativeBattleEffectArena retained104, uint meshAddress, short sourceWord3A, short fade, uint? farColor)
    {
        ValidateContext(retained104);
        NativeBattleEffectArena context = Dword(retained104, 0, meshAddress);
        uint flags = sourceWord3A == 0 ? 0x30u : 0;
        if (fade != 0)
        {
            flags |= 0xC0;
            context = Dword(context, 8, farColor ?? throw new NotSupportedException("Native actor mesh fade requires its current node far color."));
            context = Dword(context, 12, unchecked((uint)(int)fade));
        }
        context = Dword(context, 0x14, flags);
        context = Dword(context, 0x10, 0);
        foreach (int offset in new[] { 0x18, 0x1A, 0x1C, 0x1E, 0x24, 0x26 })
        {
            context = Word(context, offset, 0);
        }
        foreach (int offset in new[] { 0x20, 0x22, 0x28, 0x2A })
        {
            context = Word(context, offset, 256);
        }
        return context;
    }

    public static NativeBattleActorWaveMeshStep EmitBatch(ImmutableArray<byte> mesh, uint meshAddress, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleEffectArena context104, NativeBattleGeometryRegisterStep registers, NativeBattleModelRendererRegisters farControls, short dqa, int dqb, short? zsf4, NativeBattleSummonColors constants, short? matrixPadding18 = null)
    {
        ValidateContext(context104);
        matrix.Validate();
        if (mesh.IsDefaultOrEmpty || mesh.Length < 8 || ReadDword(context104, 0) != meshAddress)
        {
            throw new InvalidDataException("Native actor mesh requires its actual addressed geometry and retained context.");
        }
        uint flags = ReadDword(context104, 0x14);
        if ((flags & 0x2000) != 0)
        {
            throw new NotSupportedException("Native actor mesh preserved vertex pointers require their selected original owner.");
        }
        context104 = Dword(context104, 4, checked(meshAddress + 8));
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.AsSpan()));
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, matrixPadding18) };
        (byte[] farBytes, bool[] farKnown) = context104.ReadSnapshot(8, 3);
        farControls = farControls with { FarRed = Lane(0), FarGreen = Lane(1), FarBlue = Lane(2) };
        uint? Lane(int lane) => farKnown[lane] ? (uint)farBytes[lane] << 4 : null;
        NativeBattleTextureWindow before = Window(context104, 0x1C);
        NativeBattleTextureWindow after = Window(context104, 0x24);
        int depthBias = unchecked((int)ReadDword(context104, 0x10));
        NativeBattleEsunaMeshContext packetContext = new(flags, (flags & 0x40) != 0 ? ReadDword(context104, 8) : null, (flags & 0x40) != 0 ? unchecked((int)ReadDword(context104, 12)) : null, depthBias, ReadWord(context104, 0x18), ReadWord(context104, 0x1A), before, after, 17, 2);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4));
            cursor = checked(cursor + 4);
            context104 = Dword(context104, 0x2C, checked(meshAddress + (uint)cursor));
            if (count <= 0)
            {
                continue;
            }
            if (group != 3)
            {
                throw new NotSupportedException("Native actor mesh resources require their proved FT4-only groups.");
            }
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = Read(cursor, 24).ToArray();
                NativeBattleModuleProjectedVertex[] corners = new NativeBattleModuleProjectedVertex[4];
                for (int corner = 0; corner < 3; corner++)
                {
                    (NativeBattleVector vertex, short w) = Vertex(face, corner);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, vertex, w);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, dqa, dqb);
                uint firstFlags = registers.Colors.Flags ?? throw new NotSupportedException("Native actor mesh requires the current triple projection flags.");
                context104 = Dword(context104, 0x3C, firstFlags);
                for (int corner = 0; corner < 3; corner++)
                {
                    corners[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), firstFlags);
                }
                if ((firstFlags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    context104 = Dword(context104, 0x30, registers.Geometry.Mac0!.Value);
                }
                NativeBattleModuleProjectedVertex ReadCorner(int corner)
                {
                    if (corner != 3)
                    {
                        return corners[corner];
                    }
                    (NativeBattleVector vertex, short w) = Vertex(face, 3);
                    registers = NativeBattleGeometry.LoadVector(registers, 0, vertex, w);
                    registers = NativeBattleGeometry.ProjectSingle(registers, projection, dqa, dqb);
                    corners[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                    registers = NativeBattleGeometry.Average45E610(registers, zsf4 ?? throw new NotSupportedException("Native actor mesh requires the current ZSF4 control."));
                    return corners[3];
                }
                NativeBattleGfGeometryBatch faceBatch = NativeBattleRestorationMeshPackets.EmitFt4_88ECA0(face, ReadCorner, packetContext, constants, allocated);
                NativeBattleGeometryPrimitive? accepted = faceBatch.Attempts.Select(attempt => attempt.Primitive).FirstOrDefault(primitive => primitive is not null);
                if (accepted is not null)
                {
                    int biased = Math.Max(0, unchecked((int)registers.Geometry.Otz!.Value + depthBias));
                    context104 = Dword(context104, 0x38, (uint)biased);
                    int bucket = checked(17 + (biased >> 2));
                    if ((flags & 0x40) != 0)
                    {
                        uint source = BinaryPrimitives.ReadUInt32LittleEndian(face);
                        if ((flags & 1) != 0)
                        {
                            source |= 0x02000000;
                        }
                        if ((flags & 4) != 0)
                        {
                            source &= 0xFDFFFFFF;
                        }
                        NativeBattleScriptedDpcsResult dpcs = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, source, ReadDword(context104, 12), farControls, constants);
                        registers = registers with { Colors = dpcs.Registers };
                    }
                    foreach (NativeBattleGfGeometryAttempt attempt in faceBatch.Attempts)
                    {
                        attempts.Add(attempt.Primitive is null ? attempt : attempt with { Primitive = attempt.Primitive with { Bucket = bucket } });
                    }
                }
                else
                {
                    attempts.AddRange(faceBatch.Attempts);
                }
                allocated = checked(allocated + faceBatch.AllocationBytes);
                cursor = checked(cursor + 24);
                context104 = Dword(context104, 0x2C, checked(meshAddress + (uint)cursor));
            }
        }
        return new(new(attempts.ToImmutable(), allocated), context104, registers, farControls);

        ReadOnlySpan<byte> Read(int offset, int count)
        {
            if (offset < 0 || count < 0 || offset > mesh.Length - count)
            {
                throw new InvalidDataException("Native actor mesh access leaves its current addressed bank.");
            }
            return mesh.AsSpan().Slice(offset, count);
        }
        (NativeBattleVector Vertex, short W) Vertex(byte[] face, int corner)
        {
            int offset = checked(8 + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + 2 * corner)) * 4);
            ReadOnlySpan<byte> packed = Read(offset, 8);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(packed), BinaryPrimitives.ReadInt16LittleEndian(packed[2..]), BinaryPrimitives.ReadInt16LittleEndian(packed[4..])), BinaryPrimitives.ReadInt16LittleEndian(packed[6..]));
        }
    }

    private static NativeBattleTextureWindow Window(NativeBattleEffectArena context, int offset) => new(unchecked((short)ReadWord(context, offset)), unchecked((short)ReadWord(context, offset + 2)), unchecked((short)ReadWord(context, offset + 4)), unchecked((short)ReadWord(context, offset + 6)));
    private static ushort ReadWord(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(context.Read(offset, 2));
    private static uint ReadDword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena context, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return context.Write(offset, bytes);
    }
    private static NativeBattleEffectArena Word(NativeBattleEffectArena context, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return context.Write(offset, bytes);
    }
    private static void ValidateContext(NativeBattleEffectArena context)
    {
        context.Validate();
        if (context.Regions.Any(region => region.Offset > ContextLength - region.Data.Length) || context.UnknownRegions.Any(region => region.Offset > ContextLength - region.Length))
        {
            throw new InvalidDataException("Native actor mesh context leaves its original104-byte physical owner.");
        }
    }
}
