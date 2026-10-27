using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScrollingMeshContext124Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context124, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleScrollingMeshContext124
{
    public static NativeBattleScrollingMeshContext124Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors colors, int tableBaseBucket = 17, byte depthShift = 2)
    {
        context.Validate();
        uint flags = Dword(context, 0x20);
        if (Dword(context, 0) != mesh.Address || flags is not (0x33 or 0xF3) || depthShift > 31 || context.ExceedsExtent(124))
        {
            throw new InvalidDataException("Scrolling mesh requires its actual bounded caller context.");
        }
        byte[] farBytes = context.Read(8, 3);
        if (farBytes.Any(value => value != 0))
        {
            throw new NotSupportedException("Scrolling mesh shading requires its reached zero far-color operands.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Scrolling mesh requires its current loaded geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Scrolling mesh requires its current color registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Scrolling mesh requires its current projection controls.");
        NativeBattlePrimitiveProcess primitive = memory.PrimitiveProcess ?? throw new NotSupportedException("Scrolling mesh requires its current primitive process.");
        NativeBattleModelRendererRegisters far = (primitive.ModelRendererRegisters ?? throw new NotSupportedException("Scrolling mesh requires its current far-color owner.")) with { FarRed = 0, FarGreen = 0, FarBlue = 0 };
        short width = Word(context, 0x1C);
        short height = Word(context, 0x1E);
        int u = unchecked((int)Dword(context, 0x10)) % width;
        int v = unchecked((int)Dword(context, 0x14)) % height;
        NativeBattleTextureWindow window = new(context.Read(0x18, 1)[0], context.Read(0x1A, 1)[0], width, height);
        context = Put(Put(Put(Put(context, 4, checked(mesh.Address + 8)), 0x6C, unchecked((uint)u)), 0x70, unchecked((uint)v)), 0x74, NativeBattleEffectTextureWindows.Encode(window));
        context = Put(context, 0x78, 0xE2000000);
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor, 4));
            cursor = checked(cursor + 4);
            if (group is 2 or 3 or 6 or 7)
            {
                context = Put(context, 0x24, checked(mesh.Address + (uint)cursor));
            }
            if (count == 0)
            {
                continue;
            }
            if (count < 0 || group is not (2 or 3 or 7))
            {
                throw new NotSupportedException("Scrolling mesh requires its reached textured triangle and quad groups.");
            }
            bool quad = group != 2;
            bool gouraud = group == 7;
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = mesh.Bytes.AsSpan().Slice(cursor, strides[group]).ToArray();
                for (int corner = 0; corner < 3; corner++)
                {
                    (NativeBattleVector point, short w) = Vertex(corner);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, point, w);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint firstFlags = registers.Colors.Flags!.Value;
                context = Put(context, 0x34, firstFlags);
                NativeBattleModuleProjectedVertex[] corners = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
                for (int corner = 0; corner < 3; corner++)
                {
                    corners[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), firstFlags);
                }
                if ((firstFlags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    context = ScrollScratch(context, face, quad, u, width, 0);
                    context = ScrollScratch(context, face, quad, v, height, 1);
                    context = Put(context, 0x28, registers.Geometry.Mac0!.Value);
                    int clip = unchecked((int)registers.Geometry.Mac0.Value);
                    if (!quad && clip != 0 && (clip > 0 || (flags & 0x10) != 0))
                    {
                        registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Scrolling mesh requires its current ZSF3."));
                    }
                }
                NativeBattleModuleProjectedVertex ReadCorner(int corner)
                {
                    if (corner == 3)
                    {
                        (NativeBattleVector point, short w) = Vertex(3);
                        registers = NativeBattleGeometry.LoadVector(registers, 0, point, w);
                        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                        corners[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                        registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Scrolling mesh requires its current ZSF4."));
                    }
                    return corners[corner];
                }
                (byte[] cueBytes, bool[] cueKnown) = context.ReadSnapshot(12, 4);
                int? cue = (flags & 0xC0) != 0 && cueKnown.All(value => value) ? BinaryPrimitives.ReadInt32LittleEndian(cueBytes) : null;
                NativeBattleGfGeometryBatch emitted = NativeBattleShivaScrollingPackets.EmitFace(face, ReadCorner, quad, gouraud, new(flags, cue, unchecked((int)Dword(context, 0x10)), unchecked((int)Dword(context, 0x14)), tableBaseBucket, depthShift, window), controls.Zsf3.GetValueOrDefault(), controls.Zsf4.GetValueOrDefault(), colors, allocated);
                NativeBattleGfGeometryAttempt attempt = emitted.Attempts.Single();
                if (attempt.Primitive is not null)
                {
                    uint depth = registers.Geometry.Otz!.Value;
                    context = Put(context, 0x30, depth);
                    if ((flags & (gouraud ? 0x80u : 0x40u)) != 0)
                    {
                        if (gouraud)
                        {
                            registers = NativeBattleGouraudMeshRegisters.Shade(registers, face, quad, true, flags, unchecked((uint)cue!.Value), far, colors);
                        }
                        else
                        {
                            uint material = BinaryPrimitives.ReadUInt32LittleEndian(face);
                            material = (flags & 1) != 0 ? material | 0x02000000 : material;
                            material = (flags & 4) != 0 ? material & 0xFDFFFFFF : material;
                            registers = registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, material, unchecked((uint)cue!.Value), far, colors).Registers };
                        }
                    }
                    attempt = attempt with { Primitive = attempt.Primitive with { Bucket = unchecked(tableBaseBucket + (unchecked((int)depth) >> depthShift)) } };
                    allocated = checked(allocated + emitted.AllocationBytes);
                }
                attempts.Add(attempt);
                cursor = checked(cursor + strides[group]);
                context = Put(context, 0x24, checked(mesh.Address + (uint)cursor));

                (NativeBattleVector Point, short W) Vertex(int corner)
                {
                    int offset = checked(8 + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4);
                    ReadOnlySpan<byte> bytes = mesh.Bytes.AsSpan().Slice(offset, 8);
                    return (new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..])), BinaryPrimitives.ReadInt16LittleEndian(bytes[6..]));
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = primitive with { ModelRendererRegisters = far } }, context, new(attempts.ToImmutable(), allocated));
    }

    private static NativeBattleEffectArena ScrollScratch(NativeBattleEffectArena context, byte[] face, bool quad, int delta, short dimension, int axis)
    {
        if (delta == 0)
        {
            return context;
        }
        int[] values = Enumerable.Range(0, quad ? 4 : 3).Select(corner => (int)face[(corner == 0 ? 12 : corner == 1 ? 16 : quad ? 20 + (corner - 2) * 2 : 10) + axis] + delta).ToArray();
        int correction = values.Any(value => value >= 256) ? -dimension : values.Any(value => value < 0) ? dimension : 0;
        for (int corner = 0; corner < values.Length; corner++)
        {
            context = Put(context, 0x5C + corner * 4, unchecked((uint)(values[corner] + correction)));
        }
        return context;
    }

    private static short Word(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadInt16LittleEndian(context.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena context, int offset, uint value) => context.Write(offset, BitConverter.GetBytes(value));
}
