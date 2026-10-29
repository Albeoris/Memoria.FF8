using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDefaultActMeshStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context88, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleDefaultActMesh
{
    public static NativeBattleDefaultActMeshStep Draw(NativeBattleEffectMemory memory, NativeBattleGfAnimationSource source, int geometryOffset, NativeBattleEffectArena context88, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, Func<int, NativeBattleVector>? readVertex = null, Func<int, short?>? readW = null, int tableBaseBucket = 17, byte depthShift = 2, Func<int, NativeBattleEffectArena>? readPackedVertex = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return Draw(memory, source.Data, source.Address, geometryOffset, context88, projection, constants, readVertex, readW, tableBaseBucket, depthShift, readPackedVertex, pilotIdentity);
    }

    public static NativeBattleDefaultActMeshStep Draw(NativeBattleEffectMemory memory, ImmutableArray<byte> source, uint sourceAddress, int geometryOffset, NativeBattleEffectArena context88, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, Func<int, NativeBattleVector>? readVertex = null, Func<int, short?>? readW = null, int tableBaseBucket = 17, byte depthShift = 2, Func<int, NativeBattleEffectArena>? readPackedVertex = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        context88.Validate();
        if (context88.ExceedsExtent(88) || depthShift > 31)
        {
            throw new InvalidDataException("Default ACT mesh requires its bounded context and ordering shift.");
        }
        uint geometryAddress = checked(sourceAddress + (uint)geometryOffset);
        if (Dword(context88, 0) != geometryAddress)
        {
            throw new InvalidDataException("Default ACT mesh context does not identify its current geometry.");
        }
        uint flags = Dword(context88, 0x1C);
        bool selected = (flags & 0x2000) != 0;
        if (!selected)
        {
            context88 = Put(context88, 4, checked(geometryAddress + 8));
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Default ACT mesh requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Default ACT mesh requires its current color registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Default ACT mesh requires its current projection controls.");
        NativeBattlePrimitiveProcess primitive = memory.PrimitiveProcess ?? throw new NotSupportedException("Default ACT mesh requires its primitive process.");
        NativeBattleModelRendererRegisters far = primitive.ModelRendererRegisters ?? throw new NotSupportedException("Default ACT mesh requires its current far controls.");
        (byte[] rgb, bool[] rgbKnown) = context88.ReadSnapshot(8, 3);
        far = far with { FarRed = Lane(0), FarGreen = Lane(1), FarBlue = Lane(2) };
        uint? Lane(int index) => rgbKnown[index] ? (uint)rgb[index] << 4 : null;
        uint? farColor = rgbKnown.All(value => value) ? (uint)(rgb[0] | rgb[1] << 8 | rgb[2] << 16) : null;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int cursor = checked(geometryOffset + BinaryPrimitives.ReadInt32LittleEndian(Read(geometryOffset, 4)));
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4));
            cursor = checked(cursor + 4);
            context88 = Put(context88, 0x20, checked(sourceAddress + (uint)cursor));
            if (count <= 0)
            {
                continue;
            }
            bool quad = (group & 1) != 0;
            bool gouraud = group >= 4;
            bool textured = group is 2 or 3 or 6 or 7;
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                NativeBattleActionMatrix? sourceMatrix = pilotIdentity.HasValue ? NativeBattleGeometry.CaptureMatrix(registers.Geometry) : null;
                System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[quad ? 4 : 3] : null;
                byte[] face = Read(cursor, lengths[group]).ToArray();
                for (int corner = 0; corner < 3; corner++)
                {
                    LoadCorner(corner, corner);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint firstFlags = registers.Colors.Flags!.Value;
                NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
                for (int corner = 0; corner < 3; corner++)
                {
                    projected[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), firstFlags);
                }
                NativeBattleModuleProjectedVertex ReadCorner(int corner)
                {
                    if (corner == 3)
                    {
                        LoadCorner(corner, 0);
                        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                        projected[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                        registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Default ACT quad requires its current ZSF4 control."));
                    }
                    return projected[corner];
                }
                NativeBattleMeshPacketVariant variant = new(null, null, false, false)
                {
                    ObserveFlags = value => context88 = Put(context88, 0x30, value),
                    ObserveArea = _ =>
                    {
                        registers = NativeBattleGeometry.Clip45EE10(registers);
                        uint area = registers.Geometry.Mac0!.Value;
                        context88 = Put(context88, 0x24, area);
                        if (!quad && (unchecked((int)area) >= 0 || (flags & (gouraud ? 0x20u : 0x10u)) != 0))
                        {
                            registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Default ACT triangle requires its current ZSF3 control."));
                        }
                    }
                };
                NativeBattleMeshPacketContext packetContext = new(flags, farColor, OptionalInt(context88, 12), textured ? OptionalWord(context88, 0x10) : null, textured ? OptionalWord(context88, 0x14) : null, 0, 0, tableBaseBucket, depthShift);
                NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(face, new Corners(projected.Length, ReadCorner), group, packetContext, constants, allocated, variant: variant);
                if (attempt.Primitive is { } polygon)
                {
                    uint depth = registers.Geometry.Otz!.Value;
                    context88 = Put(context88, 0x2C, depth);
                    if ((flags & (gouraud ? 0x80u : 0x40u)) != 0)
                    {
                        uint cue = Dword(context88, 12);
                        if (gouraud)
                        {
                            registers = NativeBattleGouraudMeshRegisters.Shade(registers, face, quad, textured, flags, cue, far, constants);
                        }
                        else
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
                            registers = registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, material, cue, far, constants).Registers };
                        }
                    }
                    int biased = Math.Max(0, unchecked((int)depth + (int)Dword(context88, 0x18)));
                    context88 = Put(context88, 0x2C, unchecked((uint)biased));
                    attempt = attempt with { Primitive = polygon with { Bucket = unchecked(tableBaseBucket + (biased >> depthShift)) } };
                    if (pilotIdentity is { } identity && world is not null)
                    {
                        attempt = attempt with { Primitive = attempt.Primitive! with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + cursor) }, world[0], world[1], world[2], quad ? world[3] : default, checked((byte)world.Length)) } };
                    }
                    allocated = checked(allocated + attempt.PacketLength);
                }
                attempts.Add(attempt);
                cursor = checked(cursor + lengths[group]);
                context88 = Put(context88, 0x20, checked(sourceAddress + (uint)cursor));

                void LoadCorner(int corner, int slot)
                {
                    if (selected && readPackedVertex is not null)
                    {
                        int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
                        _ = Dword(context88, 4);
                        NativeBattleEffectArena row = readPackedVertex(offset);
                        (byte[] bytes, bool[] known) = row.ReadSnapshot(0, 8);
                        NativeBattleVector vertex = new(BinaryPrimitives.ReadInt16LittleEndian(row.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(row.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(row.Read(4, 2)));
                        short? w = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : null;
                        registers = NativeBattleGeometry.LoadVector(registers, slot, vertex, w);
                        registers = registers with { Colors = registers.Colors with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } };
                        Capture(corner, vertex);
                        if (corner < 3)
                        {
                            Capture(0, vertex);
                        }
                    }
                    else
                    {
                        (NativeBattleVector vertex, short? w) = Vertex(corner);
                        registers = NativeBattleGeometry.LoadVector(registers, slot, vertex, w);
                        Capture(corner, vertex);
                    }
                }

                void Capture(int corner, NativeBattleVector vertex)
                {
                    if (world is not null)
                    {
                        world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(sourceMatrix!, vertex), projection);
                    }
                }

                (NativeBattleVector Vertex, short? W) Vertex(int corner)
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
                    if (selected)
                    {
                        _ = Dword(context88, 4);
                        if (readVertex is null || readW is null)
                        {
                            throw new NotSupportedException("Default ACT mesh requires its selected packed vertex owner.");
                        }
                        return (readVertex(offset), readW(offset));
                    }
                    ReadOnlySpan<byte> packed = Read(checked(geometryOffset + 8 + offset), 8);
                    return (new(BinaryPrimitives.ReadInt16LittleEndian(packed), BinaryPrimitives.ReadInt16LittleEndian(packed[2..]), BinaryPrimitives.ReadInt16LittleEndian(packed[4..])), BinaryPrimitives.ReadInt16LittleEndian(packed[6..]));
                }
            }
        }
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = primitive with { ModelRendererRegisters = far } };
        return new(memory, context88, new(attempts.ToImmutable(), allocated));

        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (source.IsDefault || offset < 0 || length < 0 || offset > source.Length - length)
            {
                throw new InvalidDataException("Default ACT mesh access leaves its current source.");
            }
            return source.AsSpan().Slice(offset, length);
        }
    }

    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena context, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return context.Write(offset, bytes);
    }
    private static int? OptionalInt(NativeBattleEffectArena context, int offset)
    {
        (byte[] bytes, bool[] known) = context.ReadSnapshot(offset, 4);
        return known.All(value => value) ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : null;
    }
    private static ushort? OptionalWord(NativeBattleEffectArena context, int offset)
    {
        (byte[] bytes, bool[] known) = context.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : null;
    }
    private sealed class Corners(int count, Func<int, NativeBattleModuleProjectedVertex> read) : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        private readonly Dictionary<int, NativeBattleModuleProjectedVertex> values = [];
        public int Count => count;
        public NativeBattleModuleProjectedVertex this[int index]
        {
            get
            {
                if (!values.TryGetValue(index, out NativeBattleModuleProjectedVertex value))
                {
                    value = read(index);
                    values.Add(index, value);
                }
                return value;
            }
        }
        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator() => Enumerable.Range(0, Count).Select(index => this[index]).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
