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

    public static NativeBattleActorWaveMeshStep EmitBatch(ImmutableArray<byte> mesh, uint meshAddress, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleEffectArena context104, NativeBattleGeometryRegisterStep registers, NativeBattleModelRendererRegisters farControls, short dqa, int dqb, short? zsf4, NativeBattleSummonColors constants, short? matrixPadding18 = null, short? zsf3 = null, Func<int, short?>? readVertexW = null, uint? selectedVerticesAddress = null, Func<int, (NativeBattleVector Vertex, short? W)>? readSelectedVertex = null)
    {
        using NativeBattleGfSourceReader reader = new(mesh);
        return EmitBatch(reader, meshAddress, matrix, projection, context104, registers, farControls, dqa, dqb, zsf4, constants, matrixPadding18, zsf3, readVertexW, selectedVerticesAddress, readSelectedVertex);
    }

    internal static NativeBattleActorWaveMeshStep EmitBatch(NativeBattleGfSourceReader mesh, uint meshAddress, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleEffectArena context104, NativeBattleGeometryRegisterStep registers, NativeBattleModelRendererRegisters farControls, short dqa, int dqb, short? zsf4, NativeBattleSummonColors constants, short? matrixPadding18 = null, short? zsf3 = null, Func<int, short?>? readVertexW = null, uint? selectedVerticesAddress = null, Func<int, (NativeBattleVector Vertex, short? W)>? readSelectedVertex = null)
    {
        ValidateContext(context104);
        matrix.Validate();
        if (mesh.IsDefault || mesh.Length == 0 || mesh.Length < 8 || ReadDword(context104, 0) != meshAddress)
        {
            throw new InvalidDataException("Native actor mesh requires its actual addressed geometry and retained context.");
        }
        uint flags = ReadDword(context104, 0x14);
        bool preservedVertices = (flags & 0x2000) != 0;
        if (preservedVertices && (selectedVerticesAddress is null || readSelectedVertex is null))
        {
            throw new NotSupportedException("Native actor mesh preserved vertex pointers require their selected original owner.");
        }
        context104 = Dword(context104, 4, preservedVertices ? selectedVerticesAddress!.Value : checked(meshAddress + 8));
        int cursor = checked((int)mesh.UInt(0));
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, matrixPadding18) };
        (byte[] farBytes, bool[] farKnown) = context104.ReadSnapshot(8, 3);
        farControls = farControls with { FarRed = Lane(0), FarGreen = Lane(1), FarBlue = Lane(2) };
        uint? Lane(int lane) => farKnown[lane] ? (uint)farBytes[lane] << 4 : null;
        NativeBattleTextureWindow before = Window(context104, 0x1C);
        NativeBattleTextureWindow after = Window(context104, 0x24);
        int depthBias = unchecked((int)ReadDword(context104, 0x10));
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
            if (group is not (0 or 2 or 3 or 4 or 5 or 7))
            {
                throw new NotSupportedException("Native actor mesh resources require their proved F3, FT3, FT4, G3, G4 or GT4 groups.");
            }
            bool quad = group is 3 or 5 or 7;
            bool gouraud = group is 4 or 5 or 7;
            uint fadeFlag = gouraud ? 0x80u : 0x40u;
            NativeBattleEsunaMeshContext packetContext = new(flags, (flags & fadeFlag) != 0 ? ReadDword(context104, 8) : null, (flags & fadeFlag) != 0 ? unchecked((int)ReadDword(context104, 12)) : null, depthBias, ReadWord(context104, 0x18), ReadWord(context104, 0x1A), before, after, 17, 2);
            int faceBytes = group switch { 0 => 12, 2 or 4 => 20, 7 => 36, _ => 24 };
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
                    if (!quad && (unchecked((int)registers.Geometry.Mac0.Value) >= 0 || (flags & (gouraud ? 0x20u : 0x10u)) != 0))
                    {
                        registers = NativeBattleGeometry.Average45E5C0(registers, zsf3 ?? throw new NotSupportedException("Native actor triangle mesh requires the current ZSF3 control."));
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
                    registers = NativeBattleGeometry.Average45E610(registers, zsf4 ?? throw new NotSupportedException("Native actor mesh requires the current ZSF4 control."));
                    return corners[3];
                }
                NativeBattleGfGeometryBatch faceBatch = group switch
                {
                    0 => NativeBattleRestorationMeshPackets.EmitF3_88E370(face, ReadCorner, packetContext, constants, allocated),
                    2 => NativeBattleRestorationMeshPackets.EmitFt3_88E820(face, ReadCorner, packetContext, constants, allocated),
                    3 => NativeBattleRestorationMeshPackets.EmitFt4_88ECA0(face, ReadCorner, packetContext, constants, allocated),
                    4 => NativeBattleRestorationMeshPackets.EmitG3_88F220(face, ReadCorner, packetContext, constants, allocated),
                    5 => NativeBattleRestorationMeshPackets.EmitG4_88F470(face, ReadCorner, packetContext, constants, allocated),
                    7 => NativeBattleRestorationMeshPackets.EmitGt4_88FC50(face, ReadCorner, packetContext, constants, allocated),
                    _ => throw new InvalidDataException("The actor mesh group was not admitted.")
                };
                NativeBattleGeometryPrimitive? accepted = faceBatch.Attempts.Select(attempt => attempt.Primitive).FirstOrDefault(primitive => primitive is not null);
                if (accepted is not null)
                {
                    int biased = Math.Max(0, unchecked((int)registers.Geometry.Otz!.Value + depthBias));
                    context104 = Dword(context104, 0x38, (uint)biased);
                    int bucket = checked(17 + (biased >> 2));
                    if ((flags & fadeFlag) != 0)
                    {
                        uint cue = ReadDword(context104, 12);
                        if (gouraud)
                        {
                            registers = NativeBattleGouraudMeshRegisters.Shade(registers, face, quad, group == 7, flags, cue, farControls, constants);
                        }
                        else
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
                            registers = registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, source, cue, farControls, constants).Registers };
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
            return mesh.Read(offset, count);
        }
        (NativeBattleVector Vertex, short? W) Vertex(byte[] face, int corner)
        {
            int sourceOffset = checked(BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + 2 * corner)) * 4);
            if (preservedVertices)
            {
                return readSelectedVertex!(sourceOffset);
            }
            int offset = checked(8 + sourceOffset);
            ReadOnlySpan<byte> packed = Read(offset, 8);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(packed), BinaryPrimitives.ReadInt16LittleEndian(packed[2..]), BinaryPrimitives.ReadInt16LittleEndian(packed[4..])), readVertexW is null ? BinaryPrimitives.ReadInt16LittleEndian(packed[6..]) : readVertexW(offset));
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
        context.ValidateExtent(ContextLength);
    }
}
