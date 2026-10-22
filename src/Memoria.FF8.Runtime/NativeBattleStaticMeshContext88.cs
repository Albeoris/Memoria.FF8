using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStaticMeshContext88Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleStaticMeshContext88
{
    public static NativeBattleStaticMeshContext88Step Draw(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, short? matrixPadding18 = null, int tableBaseBucket = 17, byte depthShift = 2)
    {
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Masamune requires its current projection controls.");
        NativeBattlePrimitiveProcess primitive = memory.PrimitiveProcess ?? throw new NotSupportedException("Masamune requires its current far-color controls.");
        NativeBattleModelRendererRegisters far = primitive.ModelRendererRegisters ?? throw new NotSupportedException("Masamune requires its current renderer registers.");
        uint flags = Dword(context, 0x1C);
        if (Dword(context, 0) != mesh.Address || flags is not (0 or 0x30 or 0x33 or 0xC0 or 0xC3 or 0xF3 or 0xF0) || depthShift > 31)
        {
            throw new InvalidDataException("Masamune mesh requires its actual addressed caller context.");
        }
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, matrixPadding18) };
        context = Put(context, 4, checked(mesh.Address + 8));
        context = Put(context, 0x18, 0);
        byte[] rgbBytes = context.Read(8, 3);
        uint rgb = (uint)(rgbBytes[0] | rgbBytes[1] << 8 | rgbBytes[2] << 16);
        far = far with { FarRed = (rgb & 255) << 4, FarGreen = ((rgb >> 8) & 255) << 4, FarBlue = ((rgb >> 16) & 255) << 4 };
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor, 4));
            cursor = checked(cursor + 4);
            context = Put(context, 0x20, checked(mesh.Address + (uint)cursor));
            if (count <= 0)
            {
                continue;
            }
            if (flags == 0xF0 ? group != 7 : group is not (0 or 2 or 3 or 4 or 5 or 6 or 7))
            {
                throw new NotSupportedException("Masamune mesh requires an unproved primitive group.");
            }
            bool quad = (group & 1) != 0;
            bool gouraud = group >= 4;
            for (int index = 0; index < count; index++)
            {
                byte[] face = mesh.Bytes.AsSpan().Slice(cursor, lengths[group]).ToArray();
                for (int corner = 0; corner < 3; corner++)
                {
                    (NativeBattleVector position, short w) = Vertex(corner);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, position, w);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint projectedFlags = registers.Colors.Flags!.Value;
                context = Put(context, 0x30, projectedFlags);
                NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
                for (int corner = 0; corner < 3; corner++)
                {
                    projected[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), projectedFlags);
                }
                if ((projectedFlags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    context = Put(context, 0x24, registers.Geometry.Mac0!.Value);
                    if (!quad && (unchecked((int)registers.Geometry.Mac0.Value) >= 0 || (flags & (gouraud ? 0x20u : 0x10u)) != 0))
                    {
                        registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Masamune requires its actual ZSF3 control."));
                    }
                }
                NativeBattleModuleProjectedVertex ReadCorner(int corner)
                {
                    if (corner == 3)
                    {
                        (NativeBattleVector position, short w) = Vertex(corner);
                        registers = NativeBattleGeometry.LoadVector(registers, 0, position, w);
                        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                        projected[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                        registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Masamune requires its actual ZSF4 control."));
                    }
                    return projected[corner];
                }
                (byte[] cueBytes, bool[] cueKnown) = context.ReadSnapshot(12, 4);
                bool depthCue = (flags & (gouraud ? 0x80u : 0x40u)) != 0;
                int? packetCue = depthCue && cueKnown.All(value => value) ? BinaryPrimitives.ReadInt32LittleEndian(cueBytes) : null;
                NativeBattleModuleMeshContext packetContext = new(flags, rgb, packetCue, null, null, 0, tableBaseBucket, depthShift);
                NativeBattleGfGeometryAttempt attempt = NativeBattleModulePackets.EmitFace572200(face, new Corners(projected.Length, ReadCorner), group, packetContext, constants, allocated);
                if (attempt.Primitive is not null)
                {
                    uint depth = registers.Geometry.Otz!.Value;
                    context = Put(context, 0x2C, depth);
                    if (depthCue)
                    {
                        uint cue = Dword(context, 12);
                        if (gouraud)
                        {
                            registers = NativeBattleGouraudMeshRegisters.Shade(registers, face, quad, group >= 6, flags, cue, far, constants);
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
                    attempt = attempt with { Primitive = attempt.Primitive with { Bucket = unchecked(tableBaseBucket + (unchecked((int)depth) >> depthShift)) } };
                    allocated = checked(allocated + attempt.PacketLength);
                }
                attempts.Add(attempt);
                cursor = checked(cursor + lengths[group]);
                context = Put(context, 0x20, checked(mesh.Address + (uint)cursor));

                (NativeBattleVector Position, short W) Vertex(int corner)
                {
                    int offset = checked(8 + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4);
                    ReadOnlySpan<byte> bytes = mesh.Bytes.AsSpan().Slice(offset, 8);
                    return (new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..])), BinaryPrimitives.ReadInt16LittleEndian(bytes[6..]));
                }
            }
        }
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = primitive with { ModelRendererRegisters = far } };
        return new(memory, context, new(attempts.ToImmutable(), allocated));
    }

    internal static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    internal static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
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
