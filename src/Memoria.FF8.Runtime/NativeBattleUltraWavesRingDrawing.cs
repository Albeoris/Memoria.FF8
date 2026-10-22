using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleUltraWavesRingDraw(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary108, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleUltraWavesRingDrawing
{
    public static NativeBattleUltraWavesRingDraw Draw(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary108, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleSummonColors constants)
    {
        if (U32(temporary108, 0) != mesh.Address || U32(temporary108, 0x1C) is not (0x33 or 0xF3))
        {
            throw new InvalidDataException("Ultra Waves ring requires its actual physical caller context.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Ultra Waves ring requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Ultra Waves ring requires its current arithmetic registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Ultra Waves ring requires its current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Ultra Waves ring requires its current primitive process.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Ultra Waves ring requires its current FAR controls.");
        uint flags = U32(temporary108, 0x1C);
        uint rgb = U32(temporary108, 8);
        far = far with { FarRed = (rgb & 255) << 4, FarGreen = ((rgb >> 8) & 255) << 4, FarBlue = ((rgb >> 16) & 255) << 4 };
        temporary108 = Put(temporary108, 4, checked(mesh.Address + 8));
        temporary108 = Put(temporary108, 0x18, 0);
        int directory = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(directory, 4));
            directory = checked(directory + 4);
            temporary108 = Put(temporary108, 0x24, checked(mesh.Address + (uint)directory));
            if (count < 0 || count != 0 && group != 3)
            {
                throw new NotSupportedException("Ultra Waves ring requires its proved original FT4-only geometry.");
            }
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = mesh.Bytes.AsSpan().Slice(directory, strides[group]).ToArray();
                for (int corner = 0; corner < 3; corner++)
                {
                    (NativeBattleVector vertex, short w) = Vertex(corner);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, vertex, w);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint projectedFlags = registers.Colors.Flags ?? throw new NotSupportedException("Ultra Waves ring requires its reached RTPT flags.");
                NativeBattleModuleProjectedVertex[] corners = new NativeBattleModuleProjectedVertex[4];
                for (int corner = 0; corner < 3; corner++)
                {
                    corners[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), projectedFlags);
                }
                uint color = U32(temporary108, 0x20);
                (byte[] cueBytes, bool[] cueKnown) = temporary108.ReadSnapshot(12, 4);
                int? cue = cueKnown.All(value => value) ? BinaryPrimitives.ReadInt32LittleEndian(cueBytes) : null;
                NativeBattleMeshPacketVariant variant = new(color, null, false, true)
                {
                    ObserveFlags = value => temporary108 = Put(temporary108, 0x34, value),
                    ObserveArea = _ =>
                    {
                        registers = NativeBattleGeometry.Clip45EE10(registers);
                        temporary108 = Put(temporary108, 0x28, registers.Geometry.Mac0!.Value);
                    },
                    ObserveAverageDepth = _ => temporary108 = Put(temporary108, 0x30, registers.Geometry.Otz ?? throw new NotSupportedException("Ultra Waves ring requires its reached AVSZ4 result."))
                };
                NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(face, new Corners(corners, ReadCorner), 3, new NativeBattleMeshPacketContext(flags, rgb, cue, null, null, 0, 0, 17, 2), constants, allocated, variant: variant);
                if (attempt.Primitive is not null)
                {
                    uint depth = registers.Geometry.Otz!.Value;
                    if ((flags & 0x40) != 0)
                    {
                        uint material = color | 0x2C000000;
                        if ((flags & 1) != 0)
                        {
                            material |= 0x02000000;
                        }
                        if ((flags & 4) != 0)
                        {
                            material &= 0xFDFFFFFF;
                        }
                        registers = registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, material, U32(temporary108, 12), far, constants).Registers };
                    }
                    attempt = attempt with { Primitive = attempt.Primitive with { Bucket = unchecked(17 + (unchecked((int)depth) >> 2)) } };
                    allocated = checked(allocated + attempt.PacketLength);
                }
                attempts.Add(attempt);
                directory = checked(directory + strides[group]);
                temporary108 = Put(temporary108, 0x24, checked(mesh.Address + (uint)directory));

                NativeBattleModuleProjectedVertex ReadCorner(int corner)
                {
                    if (corner == 3)
                    {
                        (NativeBattleVector vertex, short w) = Vertex(3);
                        registers = NativeBattleGeometry.LoadVector(registers, 0, vertex, w);
                        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                        corners[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                        registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Ultra Waves ring requires its actual ZSF4."));
                    }
                    return corners[corner];
                }
                (NativeBattleVector Vertex, short W) Vertex(int corner)
                {
                    int at = checked(8 + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4);
                    ReadOnlySpan<byte> vertex = mesh.Bytes.AsSpan().Slice(at, 8);
                    return (new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex[2..]), BinaryPrimitives.ReadInt16LittleEndian(vertex[4..])), BinaryPrimitives.ReadInt16LittleEndian(vertex[6..]));
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } }, temporary108, new(attempts.ToImmutable(), allocated));
    }

    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private sealed class Corners(NativeBattleModuleProjectedVertex[] vertices, Func<int, NativeBattleModuleProjectedVertex> read) : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        private readonly bool[] fetched = new bool[4];
        public int Count => 4;
        public NativeBattleModuleProjectedVertex this[int index]
        {
            get
            {
                if (!fetched[index])
                {
                    vertices[index] = read(index);
                    fetched[index] = true;
                }
                return vertices[index];
            }
        }
        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator() => Enumerable.Range(0, 4).Select(index => this[index]).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
