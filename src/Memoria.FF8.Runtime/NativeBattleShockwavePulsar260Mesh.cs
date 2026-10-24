using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShockwavePulsar260MeshStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context88, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleShockwavePulsar260Mesh
{
    public static NativeBattleShockwavePulsar260MeshStep Draw(NativeBattleEffectMemory memory, NativeBattleGfAnimationSource source, int geometryOffset, NativeBattleEffectArena context88, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, Func<int, NativeBattleVector>? readVertex = null, Func<int, short?>? readW = null, int tableBaseBucket = 17, byte depthShift = 2)
    {
        context88.Validate();
        if (context88.Regions.Any(region => region.Offset > 88 - region.Data.Length) || context88.UnknownRegions.Any(region => region.Offset > 88 - region.Length) || depthShift > 31)
        {
            throw new InvalidDataException("Shockwave Pulsar requires its actual physical88-byte context and ordering shift.");
        }
        uint geometryAddress = checked(source.Address + (uint)geometryOffset);
        uint flags = U32(context88, 0x1C);
        if (U32(context88, 0) != geometryAddress || flags is not (0x10 or 0x30 or 0x2000 or 0x2030 or 0x20C0 or 0x20F0))
        {
            throw new NotSupportedException("Shockwave Pulsar requires its selected original ACT geometry and addressed vertex owner.");
        }
        bool selected = (flags & 0x2000) != 0;
        if (selected)
        {
            _ = U32(context88, 4);
        }
        else
        {
            context88 = Put(context88, 4, checked(geometryAddress + 8));
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Shockwave Pulsar requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Shockwave Pulsar requires its current color registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Shockwave Pulsar requires its current projection controls.");
        NativeBattlePrimitiveProcess primitive = memory.PrimitiveProcess ?? throw new NotSupportedException("Shockwave Pulsar requires its primitive process.");
        NativeBattleModelRendererRegisters far = primitive.ModelRendererRegisters ?? throw new NotSupportedException("Shockwave Pulsar requires its current far controls.");
        (byte[] rgb, bool[] knownRgb) = context88.ReadSnapshot(8, 3);
        far = far with { FarRed = Lane(0), FarGreen = Lane(1), FarBlue = Lane(2) };
        uint? Lane(int index) => knownRgb[index] ? (uint)rgb[index] << 4 : null;
        uint? farColor = knownRgb.All(value => value) ? (uint)(rgb[0] | rgb[1] << 8 | rgb[2] << 16) : null;
        (byte[] cue, bool[] knownCue) = context88.ReadSnapshot(12, 4);
        int? depthCue = knownCue.All(value => value) ? BinaryPrimitives.ReadInt32LittleEndian(cue) : null;
        int cursor = checked(geometryOffset + BinaryPrimitives.ReadInt32LittleEndian(Read(geometryOffset, 4)));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4));
            cursor = checked(cursor + 4);
            context88 = Put(context88, 0x20, checked(source.Address + (uint)cursor));
            if (count <= 0)
            {
                continue;
            }
            if (group is not (1 or 2 or 3 or 5 or 7))
            {
                throw new NotSupportedException("Shockwave Pulsar requires an unreached custom polygon leaf.");
            }
            bool quad = (group & 1) != 0;
            bool gouraud = group >= 4;
            bool shaded = (flags & (gouraud ? 0x80u : 0x40u)) != 0;
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = Read(cursor, lengths[group]).ToArray();
                for (int corner = 0; corner < 3; corner++)
                {
                    int offset = VertexOffset(corner);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, Vertex(offset), VertexW(offset));
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint projectedFlags = registers.Colors.Flags!.Value;
                context88 = Put(context88, 0x30, projectedFlags);
                NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
                for (int corner = 0; corner < 3; corner++)
                {
                    projected[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), projectedFlags);
                }
                if ((projectedFlags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    context88 = Put(context88, 0x24, registers.Geometry.Mac0!.Value);
                    if (!quad && (unchecked((int)registers.Geometry.Mac0.Value) >= 0 || (flags & 0x10) != 0))
                    {
                        registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Shockwave Pulsar requires its current ZSF3 control."));
                    }
                }
                NativeBattleModuleProjectedVertex ReadCorner(int corner)
                {
                    if (corner == 3)
                    {
                        int offset = VertexOffset(corner);
                        registers = NativeBattleGeometry.LoadVector(registers, 0, Vertex(offset), VertexW(offset));
                        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                        projected[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                        registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Shockwave Pulsar requires its current ZSF4 control."));
                    }
                    return projected[corner];
                }
                NativeBattleMeshPacketContext packetContext = new(flags, farColor, depthCue, null, null, 0, 0, tableBaseBucket, depthShift);
                NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(face, new Corners(projected.Length, ReadCorner), group, packetContext, constants, allocated);
                if (attempt.Primitive is { } polygon)
                {
                    uint rawDepth = registers.Geometry.Otz!.Value;
                    context88 = Put(context88, 0x2C, rawDepth);
                    if (shaded)
                    {
                        uint actualCue = U32(context88, 12);
                        if (gouraud)
                        {
                            registers = NativeBattleGouraudMeshRegisters.Shade(registers, face, quad, group == 7, flags, actualCue, far, constants);
                        }
                        else
                        {
                            uint material = BinaryPrimitives.ReadUInt32LittleEndian(face);
                            registers = registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, material, actualCue, far, constants).Registers };
                        }
                    }
                    int biased = Math.Max(0, unchecked((int)rawDepth + (int)U32(context88, 0x18)));
                    context88 = Put(context88, 0x2C, unchecked((uint)biased));
                    int bucket = unchecked(tableBaseBucket + (biased >> depthShift));
                    polygon = polygon with { Bucket = bucket };
                    attempts.Add(attempt with { Primitive = null });
                    attempts.Add(Mode(allocated + attempt.PacketLength, 0xE1000220, bucket));
                    attempts.Add(attempt with { WriteMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, attempt.PacketLength).ToImmutableArray(), Primitive = polygon });
                    int modes = 8;
                    if (group is 1 or 5)
                    {
                        attempts.Add(Mode(allocated + attempt.PacketLength + 8, 0xE1000240, bucket));
                        modes = 16;
                    }
                    allocated = checked(allocated + attempt.PacketLength + modes);
                }
                else
                {
                    attempts.Add(attempt);
                }
                cursor = checked(cursor + lengths[group]);
                context88 = Put(context88, 0x20, checked(source.Address + (uint)cursor));

                int VertexOffset(int corner) => BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
            }
        }
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = primitive with { ModelRendererRegisters = far } };
        return new(memory, context88, new(attempts.ToImmutable(), allocated));

        NativeBattleVector Vertex(int offset)
        {
            if (selected)
            {
                return (readVertex ?? throw new NotSupportedException("Shockwave Pulsar requires its selected vertex owner."))(offset);
            }
            ReadOnlySpan<byte> bytes = Read(checked(geometryOffset + 8 + offset), 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
        }
        short? VertexW(int offset)
        {
            if (selected)
            {
                return (readW ?? throw new NotSupportedException("Shockwave Pulsar requires its selected packed vertex W."))(offset);
            }
            return BinaryPrimitives.ReadInt16LittleEndian(Read(checked(geometryOffset + 14 + offset), 2));
        }

        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > source.Data.Length - length)
            {
                throw new InvalidDataException("Shockwave Pulsar geometry leaves its original ACT resource.");
            }
            return source.Data.AsSpan().Slice(offset, length);
        }
    }

    private static uint U32(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));

    private static NativeBattleEffectArena Put(NativeBattleEffectArena context, int offset, uint value) => context.Write(offset, BitConverter.GetBytes(value));

    private static NativeBattleGfGeometryAttempt Mode(int offset, uint command, int bucket)
    {
        uint[] words = [0x01000000, command];
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, words[0]);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), words[1]);
        return new(offset, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), new(bucket, words.ToImmutableArray(), [0, 0, 0, 0]));
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
