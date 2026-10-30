using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect66Mesh108Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context108, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect66Mesh108
{
    public static NativeBattleEffect66Mesh108Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, int tableBaseBucket = 17, byte depthShift = 2, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        uint flags = Dword(context, 0x1C);
        if (mesh.Address != 0xDE90CC || Dword(context, 0) != mesh.Address || flags is not (0x33 or 0xF3) || depthShift > 31 || mesh.Bytes.Length != 1752 || BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)) != 70)
        {
            throw new InvalidDataException("Effect66 requires its selected addressed Gouraud mesh and caller flags.");
        }
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
            if (count != (group == 5 ? 48 : 0))
            {
                throw new InvalidDataException("Effect66 custom mesh reaches an unsupported primitive group.");
            }
            cursor += 4 + count * 24;
        }
        if (cursor != mesh.Bytes.Length)
        {
            throw new InvalidDataException("Effect66 custom mesh leaves its original selected extent.");
        }
        context = Put(Put(context, 4, mesh.Address + 8), 0x18, 0);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect66 mesh requires current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect66 mesh requires current arithmetic registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect66 mesh requires current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Effect66 mesh requires current primitive state.");
        NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Effect66 mesh requires current far-color registers.")).PublishFarColor45DDA0(context.Read(8, 1)[0], context.Read(9, 1)[0], context.Read(10, 1)[0]);
        uint tint = Dword(context, 0x20);
        uint farRgb = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(8, 3).Concat(new byte[1]).ToArray());
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan()) + 5 * 4 + 4;
        context = Put(context, 0x24, mesh.Address + (uint)cursor);
        for (int index = 0; index < 48; index++)
        {
            byte[] face = mesh.Bytes.AsSpan().Slice(cursor, 24).ToArray();
            NativeBattleActionMatrix? sourceMatrix = pilotIdentity.HasValue ? NativeBattleGeometry.CaptureMatrix(registers.Geometry) : null;
            System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[4] : null;
            for (int corner = 0; corner < 3; corner++)
            {
                (NativeBattleVector xyz, short w) = Vertex(corner);
                registers = NativeBattleGeometry.LoadVector(registers, corner, xyz, w);
            }
            registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
            uint firstFlags = registers.Colors.Flags!.Value;
            context = Put(context, 0x34, firstFlags);
            uint firstColor = BinaryPrimitives.ReadUInt32LittleEndian(face) | 0x02000000;
            context = Put(context, 0x5C, firstColor);
            for (int corner = 1; corner < 4; corner++)
            {
                context = Put(context, 0x5C + corner * 4, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(8 + corner * 4)));
            }
            NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[4];
            for (int corner = 0; corner < 3; corner++)
            {
                projected[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), firstFlags);
            }
            NativeBattleModuleProjectedVertex ReadCorner(int corner)
            {
                if (corner == 3)
                {
                    (NativeBattleVector xyz, short w) = Vertex(corner);
                    registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(registers, 0, xyz, w), projection, controls.Dqa, controls.Dqb);
                    projected[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                    registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect66 quad requires its current ZSF4."));
                }
                return projected[corner];
            }
            NativeBattleMeshPacketVariant variant = new(null, tint, true, true)
            {
                ObserveArea = _ =>
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    context = Put(context, 0x28, registers.Geometry.Mac0!.Value);
                }
            };
            int? cue = (flags & 0x80) != 0 ? unchecked((int)Dword(context, 12)) : null;
            NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt(face, new Corners(ReadCorner), 5, new(flags, farRgb, cue, null, null, 0, 0, tableBaseBucket, depthShift), constants, allocated, variant: variant);
            if (attempt.Primitive is { } primitive)
            {
                context = Put(context, 0x30, registers.Geometry.Otz!.Value);
                primitive = primitive with { Bucket = checked(tableBaseBucket + ((int)registers.Geometry.Otz.Value >> depthShift)) };
                if (pilotIdentity is { } identity)
                {
                    primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + index) }, world![0], world[1], world[2], world[3], 4) { SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true } };
                }
                attempt = attempt with { Primitive = primitive };
                context = Put(context, 0x58, tint);
                byte[] tintedFace = face.ToArray();
                for (int corner = 0; corner < 4; corner++)
                {
                    uint original = Dword(context, 0x5C + corner * 4);
                    uint color = original & 0xFF000000;
                    for (int channel = 0; channel < 3; channel++)
                    {
                        int shift = channel * 8;
                        color |= (uint)unchecked((byte)((((original >> shift) & 255) * ((tint >> shift) & 255)) >> 7)) << shift;
                    }
                    context = Put(context, 0x5C + corner * 4, color);
                    BinaryPrimitives.WriteUInt32LittleEndian(tintedFace.AsSpan(corner == 0 ? 0 : 8 + corner * 4), color);
                }
                if ((flags & 0x80) != 0)
                {
                    registers = NativeBattleGouraudMeshRegisters.Shade(registers, tintedFace, true, false, flags, unchecked((uint)cue!.Value), far, constants);
                    for (int corner = 0; corner < 4; corner++)
                    {
                        context = Put(context, 0x5C + corner * 4, primitive.Words[1 + corner * 2]);
                    }
                }
                allocated += 36;
            }
            attempts.Add(attempt);
            cursor += 24;
            context = Put(context, 0x24, mesh.Address + (uint)cursor);

            (NativeBattleVector Position, short W) Vertex(int corner)
            {
                int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
                if ((offset & 7) != 0 || offset > 69 * 8)
                {
                    throw new InvalidDataException("Effect66 custom mesh vertex leaves its current raw rows.");
                }
                ReadOnlySpan<byte> bytes = mesh.Bytes.AsSpan().Slice(8 + offset, 8);
                NativeBattleVector position = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
                short w = BinaryPrimitives.ReadInt16LittleEndian(bytes[6..]);
                if (world is not null)
                {
                    world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(sourceMatrix!, position), projection);
                }
                return (position, w);
            }
        }
        context = Put(context, 0x24, mesh.Address + (uint)mesh.Bytes.Length);
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } };
        return new(memory, context, new(attempts.ToImmutable(), allocated));
    }

    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private sealed class Corners(Func<int, NativeBattleModuleProjectedVertex> read) : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        private readonly Dictionary<int, NativeBattleModuleProjectedVertex> values = [];
        public int Count => 4;
        public NativeBattleModuleProjectedVertex this[int index] => values.TryGetValue(index, out NativeBattleModuleProjectedVertex value) ? value : values[index] = read(index);
        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator() => Enumerable.Range(0, Count).Select(index => this[index]).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
