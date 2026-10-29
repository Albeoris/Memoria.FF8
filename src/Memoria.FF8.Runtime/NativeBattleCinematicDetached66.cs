using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;
using Memory = Memoria.FF8.Runtime.NativeBattleApocalypse221BreakupMemory;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicDetached66Geometry(NativeBattleWaterExecutionFrame Frame, uint Address, int Length);
public sealed record NativeBattleCinematicDetached66Basis(NativeBattleWaterExecutionFrame Frame, uint Address, NativeBattleActionMatrix BasisMatrix, NativeBattleActionMatrix BaselineMatrix);
public enum NativeBattleCinematicDetached66ProjectionKind { Baseline, Triangle, Quad }
public sealed record NativeBattleCinematicDetached66Projection(NativeBattleWaterExecutionFrame Frame, ImmutableArray<NativeBattleModuleProjectedVertex> Vertices, int ReturnDepth, uint Flags)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public ImmutableArray<System.Numerics.Vector3> PilotWorld { get; init; }
}
public sealed record NativeBattleCinematicDetached66Services(Func<NativeBattleWaterExecutionFrame, short, NativeBattleCinematicDetached66Geometry> ResolveGeometry, Func<NativeBattleWaterExecutionFrame, ushort, NativeBattleCinematicDetached66Basis> PrepareBasis, Func<NativeBattleWaterExecutionFrame, NativeBattleActionMatrix, ImmutableArray<NativeBattleVector>, NativeBattleCinematicDetached66ProjectionKind, NativeBattleCinematicDetached66Projection> Project, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

public static partial class NativeBattleCinematicDetached66
{
    public const int RecordSize = 48;
    private sealed record Face(uint Address, bool Quad, ImmutableArray<NativeBattleVector> Vertices, ImmutableArray<ushort> Offsets);
    private sealed record Group(uint Next, bool Quad, ImmutableArray<Face> Faces);

    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicDetached66Services services, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        Memory memory = new(frame);
        uint payload = memory.Dword(memory.Slot + 0xB8);
        uint state = memory.Dword(memory.Slot + 0xBC);
        NativeBattleCinematicDetached66Geometry geometry;
        if (state == 0)
        {
            state = memory.Allocate(16);
            memory.Dword(memory.Slot + 0xBC, state);
            memory.Dword(state, unchecked(payload + (uint)(int)memory.Signed(payload)));
            geometry = services.ResolveGeometry(memory.Frame, memory.Signed(payload + 4));
            memory.Frame = geometry.Frame;
            memory.Dword(state + 8, geometry.Address);
            uint bank = memory.Dword(memory.Manager + 0x74);
            memory.Dword(state + 4, bank);
            ImmutableArray<Group> initialized = ReadGroups(memory, geometry);
            int count = 0;
            NativeBattleVector origin = Vector(memory, payload + 8);
            foreach (Face face in initialized.SelectMany(group => group.Faces))
            {
                InitializeRecord(memory, bank + checked((uint)(count * RecordSize)), face.Vertices, origin, face.Quad);
                count++;
            }
            memory.Word(state + 0xC, count);
            uint allocated = memory.Allocate(checked((uint)(count * RecordSize)));
            if (allocated != bank)
            {
                throw new InvalidOperationException("Detached polygon records must consume the captured current arena cursor.");
            }
            for (int index = 0; index < count; index++)
            {
                memory.Byte(bank + (uint)(index * RecordSize + 0x2C), 0xFF);
            }
        }
        else
        {
            geometry = services.ResolveGeometry(memory.Frame, memory.Signed(payload + 4));
            memory.Frame = geometry.Frame;
            if (geometry.Address != memory.Dword(state + 8))
            {
                throw new NotSupportedException("Detached polygon geometry must retain its initialized owner.");
            }
        }
        ImmutableArray<Group> groups = ReadGroups(memory, geometry);
        uint records = memory.Dword(state + 4);
        int recordCount = memory.Word(state + 0xC);
        if (groups.Sum(group => group.Faces.Length) != recordCount)
        {
            throw new NotSupportedException("Detached polygon geometry no longer matches its retained record count.");
        }
        NativeBattleCinematicDetached66Basis basis = services.PrepareBasis(memory.Frame, memory.Word(memory.Slot + 0x9C));
        memory.Frame = basis.Frame.WriteTemporaryDword(0x60, basis.Address).WriteTemporaryDword(0x64, memory.Dword(state));
        PrepareScalars(memory, payload);
        memory.Frame = memory.Frame.WriteTemporaryDword(0xF0, unchecked((uint)(int)memory.Signed(memory.Slot + 0x94))).WriteTemporaryDword(0xF4, unchecked((uint)(int)memory.Signed(memory.Slot + 0x8C))).WriteTemporaryDword(0xF8, unchecked((uint)(int)memory.Signed(memory.Slot + 0x8E))).WriteTemporaryDword(0xFC, unchecked((uint)(int)memory.Signed(memory.Slot + 0x90)));
        for (int index = 0; index < recordCount; index++)
        {
            ActivateRecord(memory, records + (uint)(index * RecordSize), payload);
        }
        RequireGeometry(geometry, geometry.Address, 0x1C);
        uint vertexBank = unchecked(geometry.Address + memory.Dword(geometry.Address + 0x14));
        int vertexCount = checked((int)memory.Dword(geometry.Address + 0x18));
        RequireGeometry(geometry, vertexBank, checked(vertexCount * 8));
        ImmutableArray<NativeBattleVector> vertices = Enumerable.Range(0, vertexCount).Select(index => Vector(memory, vertexBank + (uint)(index * 8))).ToImmutableArray();
        NativeBattleCinematicDetached66Projection baseline = services.Project(memory.Frame, basis.BaselineMatrix, vertices, NativeBattleCinematicDetached66ProjectionKind.Baseline);
        memory.Frame = baseline.Frame;
        if (baseline.Vertices.Length != vertexCount)
        {
            throw new NotSupportedException("Detached polygon baseline projection requires every original vertex.");
        }
        uint scratch = memory.Dword(memory.Manager + 0x74);
        for (int index = 0; index < vertexCount; index++)
        {
            memory.Dword(scratch + (uint)(index * 8), baseline.Vertices[index].Screen);
            memory.Word(scratch + (uint)(index * 8 + 4), baseline.Vertices[index].Depth);
        }
        uint record = records;
        foreach (Group group in groups)
        {
            memory.Frame = memory.Frame.WriteTemporaryDword(0xF4, (memory.Dword(memory.Slot + 0xCC) & 0x02000000) | (group.Quad ? 0x2C000000u : 0x24000000u)).WriteTemporaryDword(0xF8, (uint)(memory.Byte(memory.Slot + 0x92) & 0x60)).WriteTemporaryDword(0xEC, vertexBank);
            uint output = memory.Dword(memory.Manager + 0x7C);
            foreach (Face face in group.Faces)
            {
                sbyte lifetime = unchecked((sbyte)memory.Byte(record + 0x2C));
                if (lifetime != 0)
                {
                    ImmutableArray<System.Numerics.Vector3> pilotWorld = default;
                    ImmutableArray<NativeBattleModuleProjectedVertex> projected;
                    int sum;
                    uint color;
                    if (lifetime < 0)
                    {
                        projected = face.Offsets.Select(offset => new NativeBattleModuleProjectedVertex(memory.Dword(scratch + offset), memory.Word(scratch + offset + 4), 0)).ToImmutableArray();
                        if (!baseline.PilotWorld.IsDefault)
                        {
                            pilotWorld = face.Offsets.Select(offset => baseline.PilotWorld[offset / 8]).ToImmutableArray();
                        }
                        sum = projected.Sum(vertex => (int)vertex.Depth);
                        color = memory.Dword(face.Address);
                    }
                    else
                    {
                        NativeBattleActionMatrix matrix = MoveRecord(memory, record, basis.BasisMatrix);
                        NativeBattleVector center = Vector(memory, record + 0x24);
                        ImmutableArray<NativeBattleVector> local = face.Vertices.Select(vertex => new NativeBattleVector(unchecked((short)(vertex.X - center.X)), unchecked((short)(vertex.Y - center.Y)), unchecked((short)(vertex.Z - center.Z)))).ToImmutableArray();
                        NativeBattleCinematicDetached66Projection active = services.Project(memory.Frame, matrix, local, face.Quad ? NativeBattleCinematicDetached66ProjectionKind.Quad : NativeBattleCinematicDetached66ProjectionKind.Triangle);
                        memory.Frame = active.Frame.WriteTemporaryDword(0xFC, active.Flags);
                        projected = active.Vertices;
                        pilotWorld = active.PilotWorld;
                        sum = face.Quad ? unchecked(active.ReturnDepth << 4) : unchecked(active.ReturnDepth * 3);
                        color = memory.Dword(unchecked(memory.Dword(state) + (uint)(lifetime * 4)));
                        memory.Byte(record + 0x2C, unchecked((byte)(lifetime - 1)));
                    }
                    color |= (memory.Dword(memory.Slot + 0xCC) & 0x02000000) | (face.Quad ? 0x2C000000u : 0x24000000u);
                    NativeBattleGfGeometryBatch packet = Packet(memory, face, projected, color);
                    if (pilotIdentity is { } identity && !pilotWorld.IsDefault && pilotWorld.Length == projected.Length)
                    {
                        NativeBattlePilotVisualGeometry metadata = new(identity with { Primitive = unchecked((int)face.Address) }, pilotWorld[0], pilotWorld[1], pilotWorld[2], face.Quad ? pilotWorld[3] : default, checked((byte)pilotWorld.Length));
                        packet = packet with { Attempts = packet.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = primitive with { PilotGeometry = metadata } } : attempt).ToImmutableArray() };
                    }
                    int offset = (sum / projected.Length >> 2) & ~3;
                    uint ordering = unchecked(memory.Frame.ReadTemporaryDword(0x14C) + (uint)offset);
                    memory.Frame = services.Append(memory.Frame, output, ordering, packet);
                    output = unchecked(output + (uint)packet.AllocationBytes);
                }
                record += RecordSize;
            }
            memory.Dword(memory.Manager + 0x7C, output);
            memory.Frame = memory.Frame.WriteTemporaryDword(0xF0, group.Next);
        }
        return memory.Frame;
    }

    private static ImmutableArray<Group> ReadGroups(Memory memory, NativeBattleCinematicDetached66Geometry geometry)
    {
        RequireGeometry(geometry, geometry.Address, 0x1C);
        uint vertexBank = unchecked(geometry.Address + memory.Dword(geometry.Address + 0x14));
        uint cursor = unchecked(geometry.Address + memory.Dword(geometry.Address + 8));
        ImmutableArray<Group>.Builder groups = ImmutableArray.CreateBuilder<Group>();
        while (true)
        {
            RequireGeometry(geometry, cursor, 4);
            int kind = memory.Signed(cursor);
            if (kind != 8 && kind != 0x12)
            {
                break;
            }
            int count = memory.Signed(cursor + 2);
            if (count < 0)
            {
                throw new NotSupportedException("Detached polygon group has a negative original face count.");
            }
            bool quad = kind == 0x12;
            int stride = quad ? 24 : 20;
            uint next = checked(cursor + 4 + (uint)(count * stride));
            RequireGeometry(geometry, cursor, checked(4 + count * stride));
            ImmutableArray<Face>.Builder faces = ImmutableArray.CreateBuilder<Face>();
            for (int index = 0; index < count; index++)
            {
                uint face = cursor + 4 + (uint)(index * stride);
                ImmutableArray<ushort> offsets = Enumerable.Range(0, quad ? 4 : 3).Select(corner => memory.Word(face + (uint)((quad ? 12 : 10) + corner * 2))).ToImmutableArray();
                foreach (ushort offset in offsets)
                {
                    RequireGeometry(geometry, vertexBank + offset, 6);
                }
                faces.Add(new(face, quad, offsets.Select(offset => Vector(memory, vertexBank + offset)).ToImmutableArray(), offsets));
            }
            groups.Add(new(next, quad, faces.ToImmutable()));
            cursor = next;
        }
        return groups.ToImmutable();
    }

    private static void RequireGeometry(NativeBattleCinematicDetached66Geometry geometry, uint address, int length)
    {
        if (length < 0 || address < geometry.Address || (ulong)address + (uint)length > (ulong)geometry.Address + (uint)geometry.Length)
        {
            throw new NotSupportedException("Detached polygon data exceeds its actual geometry owner.");
        }
    }

    private static NativeBattleVector Vector(Memory memory, uint address) => new(memory.Signed(address), memory.Signed(address + 2), memory.Signed(address + 4));
}
