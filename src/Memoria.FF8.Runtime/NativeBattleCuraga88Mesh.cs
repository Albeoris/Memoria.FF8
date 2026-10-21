using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuraga88MeshStep(NativeBattleGfGeometryBatch Batch, NativeBattleEffectArena Context88, NativeBattleGeometryRegisterStep Registers, NativeBattleModelRendererRegisters FarControls);

public static class NativeBattleCuraga88Mesh
{
    public const uint ContextAddress = 0x2793E00;
    public const int ContextLength = 88;
    public const int SharedViewOffset = 92;

    public static NativeBattleEffectArena PrepareContext(NativeBattleEffectArena retained88, uint meshAddress, short sourceWord3A, short fade, uint? farColor)
    {
        ValidateContext(retained88);
        NativeBattleEffectArena context = Dword(retained88, 0, meshAddress);
        uint flags = sourceWord3A == 0 ? 0x30u : 0;
        if (fade != 0)
        {
            flags |= 0xC0;
            context = Dword(context, 8, farColor ?? throw new NotSupportedException("Native Curaga mesh fade requires its current node far color."));
            context = Dword(context, 12, unchecked((uint)(int)fade));
        }
        context = Dword(context, 0x1C, flags);
        return context;
    }

    public static NativeBattleCuraga88MeshStep EmitBatch(ImmutableArray<byte> mesh, uint meshAddress, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleEffectArena context88, NativeBattleGeometryRegisterStep registers, NativeBattleModelRendererRegisters farControls, short dqa, int dqb, short? zsf4, NativeBattleSummonColors constants, short? matrixPadding18 = null, short? zsf3 = null, Func<int, short?>? readVertexW = null)
    {
        ValidateContext(context88);
        matrix.Validate();
        if (mesh.IsDefaultOrEmpty || mesh.Length < 8 || ReadDword(context88, 0) != meshAddress)
        {
            throw new InvalidDataException("Native Curaga mesh requires its actual addressed geometry and retained context.");
        }
        uint flags = ReadDword(context88, 0x1C);
        if ((flags & 0x2000) != 0)
        {
            throw new NotSupportedException("Native Curaga mesh preserved vertex pointers require their selected original owner.");
        }
        context88 = Dword(context88, 4, checked(meshAddress + 8));
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.AsSpan()));
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, matrixPadding18) };
        (byte[] farBytes, bool[] farKnown) = context88.ReadSnapshot(8, 3);
        farControls = farControls with { FarRed = Lane(0), FarGreen = Lane(1), FarBlue = Lane(2) };
        uint? Lane(int lane) => farKnown[lane] ? (uint)farBytes[lane] << 4 : null;
        if ((flags & 0x1000) == 0)
        {
            context88 = Dword(context88, 0x18, 0);
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4));
            cursor = checked(cursor + 4);
            context88 = Dword(context88, 0x20, checked(meshAddress + (uint)cursor));
            if (count <= 0)
            {
                continue;
            }
            if (group is not (3 or 4 or 5 or 7))
            {
                throw new NotSupportedException("Native Curaga mesh resources require their proved FT4, G3, G4 or GT4 groups.");
            }
            bool quad = group != 4;
            uint fadeFlag = group == 3 ? 0x40u : 0x80u;
            bool textured = group is 3 or 7;
            NativeBattleCuragaMeshContext packetContext = new(flags, (flags & fadeFlag) != 0 ? ReadDword(context88, 8) : null, (flags & fadeFlag) != 0 ? unchecked((int)ReadDword(context88, 12)) : null, textured && (flags & 0x500) != 0 ? ReadWord(context88, 0x10) : null, textured && (flags & 0xA00) != 0 ? ReadWord(context88, 0x14) : null, textured && (flags & 0x1000) != 0 ? ReadDword(context88, 0x18) : null, 17, 2);
            int faceBytes = group switch { 4 => 20, 7 => 36, _ => 24 };
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = Read(cursor, faceBytes).ToArray();
                NativeBattleModuleProjectedVertex[] corners = new NativeBattleModuleProjectedVertex[4];
                for (int corner = 0; corner < 3; corner++)
                {
                    (NativeBattleVector vertex, short? w) = Vertex(face, corner);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, vertex, w);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, dqa, dqb);
                uint firstFlags = registers.Colors.Flags ?? throw new NotSupportedException("Native Curaga mesh requires the current triple projection flags.");
                context88 = Dword(context88, 0x30, firstFlags);
                for (int corner = 0; corner < 3; corner++)
                {
                    corners[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), firstFlags);
                }
                if ((firstFlags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    context88 = Dword(context88, 0x24, registers.Geometry.Mac0!.Value);
                    if (!quad && (unchecked((int)registers.Geometry.Mac0.Value) >= 0 || (flags & 0x20) != 0))
                    {
                        registers = NativeBattleGeometry.Average45E5C0(registers, zsf3 ?? throw new NotSupportedException("Native Curaga G3 mesh requires the current ZSF3 control."));
                    }
                }
                NativeBattleModuleProjectedVertex ReadCorner(int corner)
                {
                    if (corner != 3)
                    {
                        return corners[corner];
                    }
                    (NativeBattleVector vertex, short? w) = Vertex(face, 3);
                    registers = NativeBattleGeometry.LoadVector(registers, 0, vertex, w);
                    registers = NativeBattleGeometry.ProjectSingle(registers, projection, dqa, dqb);
                    corners[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                    registers = NativeBattleGeometry.Average45E610(registers, zsf4 ?? throw new NotSupportedException("Native Curaga mesh requires the current ZSF4 control."));
                    return corners[3];
                }
                NativeBattleGfGeometryBatch faceBatch = group switch
                {
                    3 => NativeBattleCuragaMeshPackets.EmitFt4_87F6C0(face, ReadCorner, packetContext, constants, allocated),
                    4 => NativeBattleCuragaMeshPackets.EmitG3_87F960(face, ReadCorner, packetContext, constants, allocated),
                    5 => NativeBattleCuragaMeshPackets.EmitG4_87FB90(face, ReadCorner, packetContext, constants, allocated),
                    7 => NativeBattleCuragaMeshPackets.EmitGt4_8800B0(face, ReadCorner, packetContext, constants, allocated),
                    _ => throw new InvalidDataException("The actor mesh group was not admitted.")
                };
                NativeBattleGeometryPrimitive? accepted = faceBatch.Attempts.Select(attempt => attempt.Primitive).FirstOrDefault(primitive => primitive is not null);
                if (accepted is not null)
                {
                    uint depth = registers.Geometry.Otz!.Value;
                    context88 = Dword(context88, 0x2C, depth);
                    int bucket = checked(17 + (unchecked((int)depth) >> 2));
                    if ((flags & fadeFlag) != 0)
                    {
                        if (group == 3)
                        {
                            uint material = BinaryPrimitives.ReadUInt32LittleEndian(face);
                            if ((flags & 1) != 0)
                            {
                                material |= 0x02000000;
                            }
                            if ((flags & 4) != 0)
                            {
                                material &= 0xFDFFFFFF;
                            }
                            registers = registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, material, ReadDword(context88, 12), farControls, constants).Registers };
                        }
                        else
                        {
                            registers = NativeBattleGouraudMeshRegisters.Shade(registers, face, quad, group == 7, flags, ReadDword(context88, 12), farControls, constants);
                        }
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
                cursor = checked(cursor + faceBytes);
                context88 = Dword(context88, 0x20, checked(meshAddress + (uint)cursor));
            }
        }
        return new(new(attempts.ToImmutable(), allocated), context88, registers, farControls);

        ReadOnlySpan<byte> Read(int offset, int count)
        {
            if (offset < 0 || count < 0 || offset > mesh.Length - count)
            {
                throw new InvalidDataException("Native Curaga mesh access leaves its current addressed bank.");
            }
            return mesh.AsSpan().Slice(offset, count);
        }
        (NativeBattleVector Vertex, short? W) Vertex(byte[] face, int corner)
        {
            int offset = checked(8 + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + 2 * corner)) * 4);
            ReadOnlySpan<byte> packed = Read(offset, 8);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(packed), BinaryPrimitives.ReadInt16LittleEndian(packed[2..]), BinaryPrimitives.ReadInt16LittleEndian(packed[4..])), readVertexW is null ? BinaryPrimitives.ReadInt16LittleEndian(packed[6..]) : readVertexW(offset));
        }
    }

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
            throw new InvalidDataException("Native Curaga mesh context leaves its original88-byte physical owner.");
        }
    }
}

internal static class NativeBattleGouraudMeshRegisters
{
    public static NativeBattleGeometryRegisterStep Shade(NativeBattleGeometryRegisterStep registers, ReadOnlySpan<byte> face, bool quad, bool textured, uint flags, uint cue, NativeBattleModelRendererRegisters farControls, NativeBattleSummonColors constants)
    {
        uint source = BinaryPrimitives.ReadUInt32LittleEndian(face);
        if ((flags & 2) != 0)
        {
            source |= 0x02000000;
        }
        if ((flags & 8) != 0)
        {
            source &= 0xFDFFFFFF;
        }
        int colorOffset = textured ? 24 : 12;
        uint first = BinaryPrimitives.ReadUInt32LittleEndian(face[colorOffset..]);
        uint second = BinaryPrimitives.ReadUInt32LittleEndian(face[(colorOffset + 4)..]);
        uint third = quad ? BinaryPrimitives.ReadUInt32LittleEndian(face[(colorOffset + 8)..]) : source;
        NativeBattleScriptedColorRegisters colors = NativeBattleScriptedDpct.LoadColors45E120(registers.Colors, first, second, third) with { Ir0 = cue };
        registers = registers with { Colors = NativeBattleScriptedDpct.Transform45F4C0(colors, farControls, constants) };
        if (quad)
        {
            registers = registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, source, cue, farControls, constants).Registers };
        }
        return registers;
    }
}
