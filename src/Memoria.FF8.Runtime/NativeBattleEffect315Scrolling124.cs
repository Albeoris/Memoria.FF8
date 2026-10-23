using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect315Scrolling124Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context124, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect315Scrolling124
{
    public static NativeBattleEffect315Scrolling124Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors colors, int tableBaseBucket = 17, byte depthShift = 3)
    {
        uint flags = Dword(context, 0x20);
        if (Dword(context, 0) != mesh.Address || flags is not (0x33 or 0xF3) || Dword(context, 8) != 0 || depthShift > 31 || context.Regions.Any(region => region.Offset < 0 || region.Offset > 124 - region.Data.Length) || context.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > 124 - region.Length))
        {
            throw new InvalidDataException("Effect315 requires its actual bounded scrolling mesh caller.");
        }
        int vertices = BinaryPrimitives.ReadInt32LittleEndian(Read(4, 4));
        if (vertices is < 1 or > 121)
        {
            throw new InvalidDataException("Effect315 scrolling vertices leave their selected source extent.");
        }
        NativeBattleTextureWindow window = new(Word(context, 0x18), Word(context, 0x1A), Word(context, 0x1C), Word(context, 0x1E));
        if (window.Width <= 0 || window.Height <= 0)
        {
            throw new InvalidDataException("Effect315 scrolling requires its actual nonzero window dimensions.");
        }
        int u = unchecked((int)Dword(context, 0x10)) % window.Width;
        int v = unchecked((int)Dword(context, 0x14)) % window.Height;
        context = Put(Put(Put(Put(Put(context, 4, checked(mesh.Address + 8)), 0x6C, unchecked((uint)u)), 0x70, unchecked((uint)v)), 0x74, NativeBattleEffectTextureWindows.Encode(window)), 0x78, 0xE2000000);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect315 scrolling requires its loaded geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect315 scrolling requires its current arithmetic registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect315 scrolling requires its current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Effect315 scrolling requires its current primitive process.");
        NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Effect315 scrolling requires its current far-color controls.")) with { FarRed = 0, FarGreen = 0, FarBlue = 0 };
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(Read(0, 4));
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4));
            cursor = checked(cursor + 4);
            context = Put(context, 0x24, checked(mesh.Address + (uint)cursor));
            if (count < 0 || count != 0 && group is not (2 or 3 or 6 or 7))
            {
                throw new NotSupportedException("Effect315 scrolling requires its selected textured primitive groups.");
            }
            for (int index = 0; index < count; index++)
            {
                byte[] face = Read(cursor, lengths[group]).ToArray();
                bool quad = (group & 1) != 0;
                bool gouraud = group >= 6;
                for (int corner = 0; corner < 3; corner++)
                {
                    (NativeBattleVector point, short w) = Vertex(corner);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, point, w);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint projectedFlags = registers.Colors.Flags ?? throw new NotSupportedException("Effect315 scrolling requires current projection flags.");
                context = Put(context, 0x34, projectedFlags);
                NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
                for (int corner = 0; corner < 3; corner++)
                {
                    projected[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), projectedFlags);
                }
                if ((projectedFlags & 0x60000) == 0)
                {
                    PublishUvScratch(face, quad, u, 0, window.Width);
                    PublishUvScratch(face, quad, v, 1, window.Height);
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    int area = unchecked((int)registers.Geometry.Mac0!.Value);
                    context = Put(context, 0x28, unchecked((uint)area));
                    if (!quad && area != 0 && (area > 0 || (flags & (gouraud ? 0x20u : 0x10u)) != 0))
                    {
                        registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect315 scrolling requires its current ZSF3."));
                    }
                }
                bool fourth = false;
                NativeBattleModuleProjectedVertex Project(int corner)
                {
                    if (corner == 3 && !fourth)
                    {
                        (NativeBattleVector point, short w) = Vertex(corner);
                        registers = NativeBattleGeometry.LoadVector(registers, 0, point, w);
                        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                        projected[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                        registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect315 scrolling requires its current ZSF4."));
                        fourth = true;
                    }
                    return projected[corner];
                }
                NativeBattleShivaScrollingContext operands = new(flags, (flags & (gouraud ? 0x80u : 0x40u)) != 0 ? unchecked((int)Dword(context, 12)) : null, u, v, tableBaseBucket, depthShift, window);
                NativeBattleGfGeometryBatch leaf = NativeBattleShivaScrollingPackets.EmitFace(face, Project, quad, gouraud, operands, controls.Zsf3 ?? throw new NotSupportedException("Effect315 scrolling requires its current ZSF3."), controls.Zsf4 ?? throw new NotSupportedException("Effect315 scrolling requires its current ZSF4."), colors, allocated);
                if (leaf.Attempts.Any(attempt => attempt.Primitive is not null))
                {
                    uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Effect315 scrolling requires its current average depth.");
                    context = Put(context, 0x30, depth);
                    int bucket = unchecked(tableBaseBucket + ((int)depth >> depthShift));
                    leaf = leaf with { Attempts = leaf.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = primitive with { Bucket = bucket } } : attempt).ToImmutableArray() };
                    if ((flags & (gouraud ? 0x80u : 0x40u)) != 0)
                    {
                        uint cue = Dword(context, 12);
                        uint material = BinaryPrimitives.ReadUInt32LittleEndian(face);
                        if ((flags & 1) != 0)
                        {
                            material |= 0x02000000;
                        }
                        if ((flags & 4) != 0)
                        {
                            material &= ~0x02000000u;
                        }
                        registers = gouraud ? NativeBattleGouraudMeshRegisters.Shade(registers, face, quad, true, flags, cue, far, colors) : registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, material, cue, far, colors).Registers };
                    }
                }
                attempts.AddRange(leaf.Attempts);
                allocated = checked(allocated + leaf.AllocationBytes);
                cursor = checked(cursor + lengths[group]);
                context = Put(context, 0x24, checked(mesh.Address + (uint)cursor));

                (NativeBattleVector Point, short W) Vertex(int corner)
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
                    if ((offset & 7) != 0 || offset > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect315 scrolling corner leaves its actual selected vertices.");
                    }
                    ReadOnlySpan<byte> bytes = Read(8 + offset, 8);
                    return (new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..])), BinaryPrimitives.ReadInt16LittleEndian(bytes[6..]));
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } }, context, new(attempts.ToImmutable(), allocated));

        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (mesh.Bytes.IsDefault || offset < 0 || length < 0 || offset > mesh.Bytes.Length - length)
            {
                throw new InvalidDataException("Effect315 scrolling cursor leaves its original mesh.");
            }
            return mesh.Bytes.AsSpan().Slice(offset, length);
        }
        void PublishUvScratch(byte[] face, bool quad, int delta, int axis, short dimension)
        {
            if (delta == 0)
            {
                return;
            }
            uint final = quad ? BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(20)) : BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(10));
            uint[] uv = quad ? [BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(12)), BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(16)), final, final >> 16] : [BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(12)), BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(16)), final];
            int[] values = uv.Select(value => unchecked((byte)(value >> (axis * 8)) + delta)).ToArray();
            int adjustment = values.Any(value => value >= 256) ? -dimension : values.Any(value => value < 0) ? dimension : 0;
            for (int corner = 0; corner < values.Length; corner++)
            {
                context = Put(context, 0x5C + corner * 4, unchecked((uint)(values[corner] + adjustment)));
            }
        }
    }

    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static short Word(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadInt16LittleEndian(context.Read(offset, 2));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena context, int offset, uint value) => context.Write(offset, BitConverter.GetBytes(value));
}
