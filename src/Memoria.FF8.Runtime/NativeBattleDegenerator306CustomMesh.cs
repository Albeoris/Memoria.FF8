using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDegenerator306CustomMeshStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context108, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleDegenerator306CustomMesh
{
    public static NativeBattleDegenerator306CustomMeshStep Draw5A3AC0(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleEffectArena context108, NativeBattleCameraProjection projection, NativeBattleSummonColors constants, int tableBaseBucket = 17, byte depthShift = 2, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        context108.Validate();
        if (mesh.Bytes.IsDefault || mesh.Bytes.Length < 8 || Dword(0) != mesh.Address || depthShift > 31)
        {
            throw new InvalidDataException("Degenerator custom mesh requires its addressed source and physical108-byte context.");
        }
        uint flags = Dword(0x1C);
        if ((flags & 0x2000) == 0)
        {
            Put(4, checked(mesh.Address + 8));
        }
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
        Put(0x24, checked(mesh.Address + (uint)cursor));
        if ((flags & 0x1000) == 0)
        {
            Put(0x18, 0);
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Degenerator custom mesh requires current projection controls.");
        NativeBattlePrimitiveProcess primitive = memory.PrimitiveProcess ?? throw new NotSupportedException("Degenerator custom mesh requires current primitive controls.");
        NativeBattleModelRendererRegisters far = primitive.ModelRendererRegisters ?? throw new NotSupportedException("Degenerator custom mesh requires its current far-color owner.");
        uint rgb = Dword(8);
        far = far with { FarRed = (rgb & 255) << 4, FarGreen = ((rgb >> 8) & 255) << 4, FarBlue = ((rgb >> 16) & 255) << 4 };
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        NativeBattleActionMatrix? pilotMatrix = pilotIdentity.HasValue ? NativeBattleGeometry.CaptureMatrix(registers.Geometry) : null;
        int allocated = 0;
        cursor = checked(cursor + 8);
        Put(0x24, checked(mesh.Address + (uint)cursor));
        DrawGroup(2);
        DrawGroup(3);
        cursor = checked(cursor + 8);
        Put(0x24, checked(mesh.Address + (uint)cursor));
        SkipUnreachedGroup();
        SkipUnreachedGroup();
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = primitive with { ModelRendererRegisters = far } };
        return new(memory, context108, new(attempts.ToImmutable(), allocated));

        void SkipUnreachedGroup()
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4)) != 0)
            {
                throw new NotSupportedException("Degenerator custom mesh requires an unsupported Gouraud leaf.");
            }
            cursor = checked(cursor + 4);
            Put(0x24, checked(mesh.Address + (uint)cursor));
        }

        void DrawGroup(int group)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4));
            cursor = checked(cursor + 4);
            Put(0x24, checked(mesh.Address + (uint)cursor));
            bool quad = group == 3;
            int stride = quad ? 24 : 20;
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = Read(cursor, stride).ToArray();
                System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[quad ? 4 : 3] : null;
                for (int corner = 0; corner < 3; corner++)
                {
                    var vertex = Vertex(corner);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, vertex.Position, vertex.W);
                    Capture(corner, vertex.Position);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint firstFlags = registers.Colors.Flags ?? throw new NotSupportedException("Degenerator custom mesh requires its RTPT flags.");
                NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
                for (int corner = 0; corner < 3; corner++)
                {
                    projected[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), firstFlags);
                }
                NativeBattleMeshPacketContext packetContext = new(flags, rgb, (flags & 0x40) != 0 && KnownDword(12) is uint fade ? unchecked((int)fade) : null, (flags & 0x500) != 0 ? KnownWord(0x10) : null, (flags & 0xA00) != 0 ? KnownWord(0x14) : null, Dword(0x18), 0, tableBaseBucket, depthShift);
                NativeBattleMeshPacketVariant variant = new(Dword(0x20), null, false, true)
                {
                    ObserveFlags = value => Put(0x34, value),
                    ObserveArea = value =>
                    {
                        registers = NativeBattleGeometry.Clip45EE10(registers);
                        Put(0x28, registers.Geometry.Mac0!.Value);
                        if (!quad && value != 0 && (value >= 0 || (flags & 0x10) != 0))
                        {
                            registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Degenerator custom mesh requires its ZSF3 owner."));
                        }
                    }
                };
                NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(face, new Corners(projected.Length, ReadCorner), group, packetContext, constants, allocated, variant: variant);
                if (attempt.Primitive is not null)
                {
                    uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Degenerator custom mesh requires its averaged depth.");
                    Put(0x30, depth);
                    if ((flags & 0x40) != 0)
                    {
                        registers = registers with { Colors = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, (Dword(0x20) | (quad ? 0x2C000000u : 0x24000000u) | ((flags & 1) != 0 ? 0x02000000u : 0)) & ~((flags & 4) != 0 ? 0x02000000u : 0), Dword(12), far, constants).Registers };
                    }
                    attempt = attempt with { Primitive = attempt.Primitive with { Bucket = checked(tableBaseBucket + (unchecked((int)depth) >> depthShift)) } };
                    if (pilotIdentity is { } identity && world is not null)
                    {
                        attempt = attempt with { Primitive = attempt.Primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + cursor) }, world[0], world[1], world[2], quad ? world[3] : default, checked((byte)world.Length)) } };
                    }
                    if (pilotIdentity?.Effect == 174)
                    {
                        attempt = attempt with { Primitive = NativeBattleEffect174Presentation.PreserveNativeDepths(attempt.Primitive!) };
                    }
                    allocated = checked(allocated + attempt.PacketLength);
                }
                attempts.Add(attempt);
                cursor = checked(cursor + stride);
                Put(0x24, checked(mesh.Address + (uint)cursor));

                NativeBattleModuleProjectedVertex ReadCorner(int corner)
                {
                    if (corner == 3)
                    {
                        var vertex = Vertex(corner);
                        registers = NativeBattleGeometry.LoadVector(registers, 0, vertex.Position, vertex.W);
                        Capture(corner, vertex.Position);
                        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                        projected[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                        registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Degenerator custom mesh requires its ZSF4 owner."));
                    }
                    return projected[corner];
                }

                void Capture(int corner, NativeBattleVector point)
                {
                    if (world is not null && pilotMatrix is not null)
                    {
                        world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(pilotMatrix, point), projection);
                    }
                }

                (NativeBattleVector Position, short W) Vertex(int corner)
                {
                    int offset = checked((int)(Dword(4) - mesh.Address) + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4);
                    ReadOnlySpan<byte> bytes = Read(offset, 8);
                    return (new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..])), BinaryPrimitives.ReadInt16LittleEndian(bytes[6..]));
                }
            }
        }

        ReadOnlySpan<byte> Read(int offset, int count) => mesh.Bytes.AsSpan().Slice(offset, count);
        uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context108.Read(offset, 4));
        uint? KnownDword(int offset)
        {
            (byte[] bytes, bool[] known) = context108.ReadSnapshot(offset, 4);
            return known.All(value => value) ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : null;
        }
        ushort? KnownWord(int offset)
        {
            (byte[] bytes, bool[] known) = context108.ReadSnapshot(offset, 2);
            return known.All(value => value) ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : null;
        }
        void Put(int offset, uint value)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            context108 = context108.Write(offset, bytes);
        }
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
