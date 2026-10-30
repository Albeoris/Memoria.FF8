using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWindowedMesh144Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context144, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleWindowedMesh144
{
    public static NativeBattleWindowedMesh144Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors colors, int tableBaseBucket = 17, byte depthShift = 2, int groupMask = (1 << 3) | (1 << 7), NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        context.Validate();
        uint flags = U(context, 0x20);
        if (U(context, 0) != mesh.Address || flags is not (0x33 or 0xF3) || depthShift > 31 || context.ExceedsExtent(144))
        {
            throw new InvalidDataException("Windowed windowed drawing requires its actual 144-byte context and selected source.");
        }
        int width = W(context, 0x1C);
        int height = W(context, 0x1E);
        if (width <= 0 || height <= 0 || mesh.Bytes.IsDefaultOrEmpty || mesh.Bytes.Length < 8 || BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan(4, 4)) <= 0)
        {
            throw new InvalidDataException("Windowed windowed drawing requires positive signed dimensions and its actual packed vertex rows.");
        }
        NativeBattleTextureWindow window = new(W(context, 0x18), W(context, 0x1A), (short)width, (short)height);
        int u = unchecked((int)U(context, 0x10)) % width;
        int v = unchecked((int)U(context, 0x14)) % height;
        context = Put(Put(Put(Put(context, 4, mesh.Address + 8), 0x80, unchecked((uint)u)), 0x84, unchecked((uint)v)), 0x88, NativeBattleEffectTextureWindows.Encode(window));
        context = Put(context, 0x8C, 0xE2000000);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Windowed windowed drawing requires its current GTE registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Windowed windowed drawing requires its current arithmetic registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Windowed windowed drawing requires its current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Windowed windowed drawing requires its current renderer process.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Windowed windowed drawing requires its current far-color registers.");
        byte[] rgb = context.Read(8, 3);
        far = far.PublishFarColor45DDA0(rgb[0], rgb[1], rgb[2]);
        uint farColor = (uint)(rgb[0] | rgb[1] << 8 | rgb[2] << 16);
        NativeBattleActionMatrix? pilotMatrix = null;
        int vertexCount = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan(4, 4));
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan(0, 4));
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        for (int group = 0; group < 8; group++)
        {
            int count = I(cursor);
            cursor += 4;
            if (count < 0 || count > (mesh.Bytes.Length - cursor) / strides[group] || count != 0 && (groupMask & (1 << group)) == 0)
            {
                throw new InvalidDataException("Windowed windowed source leaves its selected FT4 and GT4 groups.");
            }
            context = Put(context, 0x28, checked(mesh.Address + (uint)cursor));
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                bool gouraud = group is 6 or 7;
                bool triangle = group is 2 or 6;
                int colorCount = triangle ? 3 : 4;
                byte[] face = mesh.Bytes.AsSpan(cursor, strides[group]).ToArray();
                System.Numerics.Vector3[]? pilotWorld = pilotIdentity.HasValue ? new System.Numerics.Vector3[triangle ? 3 : 4] : null;
                for (int corner = 0; corner < 3; corner++)
                {
                    Load(corner, corner);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint firstFlags = registers.Colors.Flags!.Value;
                context = Put(context, 0x38, firstFlags);
                NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[4];
                for (int corner = 0; corner < 3; corner++)
                {
                    projected[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), firstFlags);
                }
                uint palette = gouraud ? 0 : U(context, 0x24);
                uint firstColor = gouraud ? BinaryPrimitives.ReadUInt32LittleEndian(face) : palette | (triangle ? 0x24000000u : 0x2C000000u);
                if ((flags & (gouraud ? 2u : 1u)) != 0)
                {
                    firstColor |= 0x02000000;
                }
                if ((flags & (gouraud ? 8u : 4u)) != 0)
                {
                    firstColor &= 0xFDFFFFFF;
                }
                if (gouraud)
                {
                    context = Put(context, 0x60, firstColor);
                    for (int color = 1; color < colorCount; color++)
                    {
                        context = Put(context, 0x60 + color * 4, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan((triangle ? 16 : 20) + color * 4)));
                    }
                }
                if ((firstFlags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    AdjustScratch(face, u, 0, width, triangle);
                    AdjustScratch(face, v, 1, height, triangle);
                    context = Put(context, 0x2C, registers.Geometry.Mac0!.Value);
                    int area = unchecked((int)registers.Geometry.Mac0.Value);
                    if (triangle && area != 0 && (area > 0 || (flags & (gouraud ? 0x20u : 0x10u)) != 0))
                    {
                        registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Windowed triangles require current ZSF3."));
                    }
                }
                NativeBattleHoly175WindowedMeshContext operands = new(flags, farColor, (flags & 0xC0) != 0 ? OptionalInt(context, 12) : null, palette, unchecked((int)U(context, 0x10)), unchecked((int)U(context, 0x14)), window, tableBaseBucket, depthShift);
                NativeBattleGfGeometryBatch leaf = triangle ? NativeBattleHoly175WindowedMeshPackets.EmitTriangle(face, Project, gouraud, operands, colors, allocated) : NativeBattleHoly175WindowedMeshPackets.EmitQuad(face, Project, gouraud, operands, colors, allocated);
                if (leaf.Attempts.Any(attempt => attempt.Primitive is not null))
                {
                    context = Put(context, 0x34, registers.Geometry.Otz!.Value);
                    if (gouraud)
                    {
                        palette = U(context, 0x24);
                        context = Put(context, 0x5C, palette);
                        for (int color = 0; color < colorCount; color++)
                        {
                            uint value = U(context, 0x60 + color * 4);
                            for (int lane = 0; lane < 3; lane++)
                            {
                                byte tinted = unchecked((byte)(((byte)(value >> (lane * 8)) * (byte)(palette >> (lane * 8))) >> 7));
                                context = context.Write(0x60 + color * 4 + lane, [tinted]);
                            }
                        }
                        uint[] values = Enumerable.Range(0, colorCount).Select(index => U(context, 0x60 + index * 4)).ToArray();
                        if ((flags & 0x80) != 0)
                        {
                            NativeBattleScriptedColorRegisters loaded = NativeBattleScriptedDpct.LoadColors45E120(registers.Colors, values[1], values[2], values[triangle ? 0 : 3]) with { Ir0 = U(context, 12) };
                            registers = registers with { Colors = NativeBattleScriptedDpct.Transform45F4C0(loaded, far, colors) };
                            for (int index = 1; index < colorCount; index++)
                            {
                                values[index] = registers.Colors.Rgb[index - 1] ?? throw new InvalidDataException("Windowed DPCT did not publish its actual color FIFO.");
                            }
                            if (triangle)
                            {
                                values[0] = registers.Colors.Rgb[2] ?? throw new InvalidDataException("Windowed GT3 DPCT did not publish its actual first color.");
                            }
                            else
                            {
                                NativeBattleScriptedDpcsResult first = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, values[0], U(context, 12), far, colors);
                                registers = registers with { Colors = first.Registers };
                                values[0] = first.PackedRgb;
                            }
                        }
                        leaf = ReplaceColors(leaf, values);
                    }
                    else if ((flags & 0x40) != 0)
                    {
                        registers = registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, firstColor, U(context, 12), far, colors).Registers };
                    }
                    int bucket = unchecked(tableBaseBucket + (unchecked((int)registers.Geometry.Otz!.Value) >> depthShift));
                    leaf = leaf with { Attempts = leaf.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = primitive with { Bucket = bucket } } : attempt).ToImmutableArray() };
                }
                if (pilotIdentity is { } identity && pilotWorld is not null)
                {
                    leaf = leaf with { Attempts = leaf.Attempts.Select(attempt =>
                    {
                        if (attempt.Primitive is not { } primitive)
                        {
                            return attempt;
                        }
                        int commandIndex = primitive.Words.Length > 2 && primitive.Words[1] >> 24 == 0xE2 ? 2 : 1;
                        if (primitive.Words.Length <= commandIndex || (primitive.Words[commandIndex] & 0xE0000000u) != 0x20000000u)
                        {
                            return attempt;
                        }
                        System.Numerics.Vector4 depths = primitive.VertexPayload4.Length switch
                        {
                            0 => System.Numerics.Vector4.Zero,
                            4 => new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]),
                            _ => throw new NotSupportedException("Windowed capture requires its actual empty or four-lane depth payload.")
                        };
                        return attempt with { Primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + cursor) }, pilotWorld[0], pilotWorld[1], pilotWorld[2], triangle ? default : pilotWorld[3], checked((byte)pilotWorld.Length)) { SourceDepths = depths, UseSourceDepths = true } } };
                    }).ToImmutableArray() };
                }
                attempts.AddRange(leaf.Attempts);
                allocated = checked(allocated + leaf.AllocationBytes);
                cursor += strides[group];
                context = Put(context, 0x28, checked(mesh.Address + (uint)cursor));

                void Load(int corner, int register)
                {
                    int offset = checked(8 + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4);
                    if (offset < 8 || (offset & 7) != 0 || offset > 8 + vertexCount * 8 - 8)
                    {
                        throw new InvalidDataException("Windowed windowed vertex index leaves its physical source.");
                    }
                    ReadOnlySpan<byte> row = mesh.Bytes.AsSpan(offset, 8);
                    NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..]));
                    registers = NativeBattleGeometry.LoadVector(registers, register, point, BinaryPrimitives.ReadInt16LittleEndian(row[6..]));
                    if (pilotWorld is not null)
                    {
                        pilotMatrix ??= NativeBattleGeometry.CaptureMatrix(registers.Geometry);
                        pilotWorld[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(pilotMatrix, point), projection);
                    }
                }

                NativeBattleModuleProjectedVertex Project(int corner)
                {
                    if (corner == 3)
                    {
                        Load(3, 0);
                        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                        projected[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                        registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Windowed windowed quads require current ZSF4."));
                    }
                    return projected[corner];
                }
            }
        }
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } };
        return new(memory, context, new(attempts.ToImmutable(), allocated));

        int I(int offset) => offset < 0 || offset > mesh.Bytes.Length - 4 ? throw new InvalidDataException("Windowed windowed directory leaves its physical source.") : BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan(offset, 4));
        void AdjustScratch(byte[] face, int delta, int axis, int dimension, bool triangle)
        {
            if (delta == 0)
            {
                return;
            }
            uint last = triangle ? BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(10)) : BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(20));
            uint[] uv = triangle ? [BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(12)), BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(16)), last] : [BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(12)), BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(16)), last, last >> 16];
            int[] values = uv.Select(value => unchecked((byte)(value >> (axis * 8)) + delta)).ToArray();
            int adjustment = values.Any(value => value >= 256) ? -dimension : values.Any(value => value < 0) ? dimension : 0;
            for (int index = 0; index < uv.Length; index++)
            {
                context = Put(context, 0x70 + index * 4, unchecked((uint)(values[index] + adjustment)));
            }
        }
    }

    private static NativeBattleGfGeometryBatch ReplaceColors(NativeBattleGfGeometryBatch batch, uint[] colors)
    {
        return batch with { Attempts = batch.Attempts.Select(attempt =>
        {
            byte[] bytes = attempt.Data.ToArray();
            for (int index = 0; index < colors.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8 + index * 12), colors[index]);
            }
            ImmutableArray<uint> words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
            return attempt with { Data = bytes.ToImmutableArray(), Primitive = attempt.Primitive is { } primitive ? primitive with { Words = words } : null };
        }).ToImmutableArray() };
    }

    private static int? OptionalInt(NativeBattleEffectArena context, int offset)
    {
        (byte[] bytes, bool[] known) = context.ReadSnapshot(offset, 4);
        return known.All(value => value) ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : null;
    }

    private static uint U(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static short W(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadInt16LittleEndian(context.Read(offset, 2));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena context, int offset, uint value) => context.Write(offset, BitConverter.GetBytes(value));
}
