using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleControl18Face(uint Address, bool Quad, ImmutableArray<NativeBattleVector> Vertices);
public sealed record NativeBattleControl18SpawnStep(ImmutableArray<byte> Records, ImmutableArray<byte> PolygonStates, ushort LiveCount, NativeCrtRandomState Random, int? LastDistance, ImmutableArray<NativeBattleVector> LastVertices, int? FallbackCountdown = null)
{
    public ImmutableDictionary<int, NativeBattleResourcePointer> ResourcePointers { get; init; } = ImmutableDictionary<int, NativeBattleResourcePointer>.Empty;
}
public sealed record NativeBattleControl18SpawnServices(int Interval, Func<NativeCrtRandomState, int, int, int, int, NativeCrtRandomState> EmitAuxiliary)
{
    public ImmutableArray<byte> Mode5Anchors { get; init; }
    public Func<NativeCrtRandomState, int, int, int, NativeCrtRandomState>? EmitFragmentHook { get; init; }
}

public static class NativeBattleControl18Spawn
{
    public static NativeBattleControl18SpawnStep Advance(ReadOnlySpan<byte> source, ReadOnlySpan<byte> polygonStates, ushort liveCount, ReadOnlySpan<byte> payload, IReadOnlyList<NativeBattleControl18Face> faces, short threshold, NativeCrtRandomState random, NativeBattleControl18SpawnServices? services = null)
    {
        return AdvanceCore(source, polygonStates, liveCount, payload, faces, face => new(face.Address, null, face.Quad, face.Vertices), threshold, random, services, null);
    }

    internal static NativeBattleControl18SpawnStep Advance(ReadOnlySpan<byte> source, ReadOnlySpan<byte> polygonStates, ushort liveCount, ReadOnlySpan<byte> payload, IReadOnlyList<NativeBattleControl18ResourceFace> faces, short threshold, NativeCrtRandomState random, NativeBattleControl18SpawnServices? services, ImmutableDictionary<int, NativeBattleResourcePointer> pointers)
    {
        return AdvanceCore(source, polygonStates, liveCount, payload, faces, face => new(face.Source.Cpu?.TryPhysicalAddress() ?? face.Source.StagingAddress, face.Source, face.Quad, face.Vertices), threshold, random, services, pointers);
    }

    private readonly record struct Face(uint? Address, NativeBattleResourcePointer? Source, bool Quad, ImmutableArray<NativeBattleVector> Vertices);

    private static NativeBattleControl18SpawnStep AdvanceCore<TFace>(ReadOnlySpan<byte> source, ReadOnlySpan<byte> polygonStates, ushort liveCount, ReadOnlySpan<byte> payload, IReadOnlyList<TFace> faces, Func<TFace, Face> selectFace, short threshold, NativeCrtRandomState random, NativeBattleControl18SpawnServices? services, ImmutableDictionary<int, NativeBattleResourcePointer>? pointers)
    {
        if (source.Length % 68 != 0 || payload.Length < 0x32 || faces.Count > polygonStates.Length)
        {
            throw new InvalidDataException("Native breakup spawning requires complete records, controls and polygon states.");
        }
        ImmutableDictionary<int, NativeBattleResourcePointer> owners = NativeBattleControl18ResourceRecords.Require(pointers, source.Length);
        byte[] records = source.ToArray();
        byte[] states = polygonStates.ToArray();
        byte[] controls = payload.ToArray();
        ushort mode = U16(8);
        if (mode is not (1 or 2 or 5 or 6 or 7) || (mode is 5 or 7 || U16(0x2E) != 0 || U16(0x30) != 0 || U16(0xE) != 0) && services is null)
        {
            throw new NotSupportedException("Native breakup spawning requires its selected threshold mode and auxiliary services.");
        }
        if (U16(0xE) != 0 && services!.EmitFragmentHook is null)
        {
            throw new NotSupportedException("Native breakup spawning requires its selected fragment hook service.");
        }
        if (mode == 5 && (services!.Mode5Anchors.IsDefault || services.Mode5Anchors.Length != checked(faces.Count * 4)))
        {
            throw new NotSupportedException("Native radial breakup spawning requires its retained per-polygon anchor records.");
        }
        int countdown = services?.Interval ?? 0;
        int? lastDistance = null;
        ImmutableArray<NativeBattleVector> lastVertices = default;
        int cursor = 0;
        for (int polygon = 0; polygon < faces.Count; polygon++)
        {
            if (states[polygon] != 0)
            {
                continue;
            }
            if (services is not null && liveCount >= records.Length / 68)
            {
                break;
            }
            Face face = selectFace(faces[polygon]);
            int count = face.Quad ? 4 : 3;
            if (face.Vertices.Length != count)
            {
                throw new InvalidDataException("Native breakup polygon has an incomplete corner directory.");
            }
            bool fallback = false;
            bool selected;
            if (mode == 5)
            {
                selected = BinaryPrimitives.ReadInt16LittleEndian(services!.Mode5Anchors.AsSpan().Slice(polygon * 4 + 2, 2)) > threshold;
            }
            else
            {
                int y = face.Vertices.Sum(value => value.Y);
                selected = mode is 1 or 2 or 6 ? y < threshold * count : y > threshold * count;
            }
            if (!selected)
            {
                if (U16(0x2E) == 0)
                {
                    continue;
                }
                countdown = unchecked(countdown - 1);
                if (countdown > 0)
                {
                    continue;
                }
                countdown = services!.Interval;
                fallback = true;
            }
            if (liveCount >= records.Length / 68)
            {
                break;
            }
            while (cursor < records.Length && NativeBattleControl18ResourceRecords.Active(records, cursor, owners))
            {
                cursor += 68;
            }
            if (cursor >= records.Length)
            {
                throw new InvalidDataException("Native breakup live count disagrees with its physical free-record scan.");
            }
            if (face.Source is { } pointer)
            {
                if (!pointer.HasValidOwner() || pointer.CpuRow is not null || pointer.Cpu is { Bank: < 3 } && pointer.CpuPublication is null)
                {
                    throw new InvalidDataException("Native breakup spawning requires its selected polygon source.");
                }
                owners = owners.SetItem(cursor, pointer);
            }
            if (face.Address is uint address)
            {
                if (address == 0)
                {
                    throw new InvalidDataException("Native breakup spawning cannot activate a null polygon address.");
                }
                BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(cursor), address);
            }
            else if (face.Source is null)
            {
                throw new InvalidDataException("Native breakup spawning requires an addressed or typed polygon source.");
            }
            records[cursor + 0x26] = face.Quad ? (byte)1 : (byte)0;
            records[cursor + 0x2E] = controls[0xC];
            Write16(0x30, polygon);
            states[polygon] = 1;
            int[] destinations = [0x3C, 0x3E, 0x16, 0x1E, 0x40, 0x42];
            for (int lane = 0; lane < destinations.Length; lane++)
            {
                NativeBattleWaterVectorRandom sample = NativeBattleWaterVectorCommands.RandomB65740(S16(0x14 + lane * 4), random);
                random = sample.Random;
                Write16(destinations[lane], unchecked(S16(0x12 + lane * 4) + sample.Value));
            }
            int[] center = new int[3];
            for (int axis = 0; axis < 3; axis++)
            {
                int sum = face.Vertices.Sum(value => axis == 0 ? value.X : axis == 1 ? value.Y : value.Z);
                center[axis] = face.Quad ? sum >> 2 : sum / 3;
                BinaryPrimitives.WriteInt32LittleEndian(records.AsSpan(cursor + 4 + axis * 4), unchecked(center[axis] << 8));
                for (int vertex = 0; vertex < count; vertex++)
                {
                    NativeBattleVector point = face.Vertices[vertex];
                    int value = axis == 0 ? point.X : axis == 1 ? point.Y : point.Z;
                    Write16(0x10 + vertex * 8 + axis * 2, value - center[axis]);
                }
            }
            Write16(0x32, NativeWorldScriptedPathMath.NativeAngle(center[0], center[2]) & 0xFFF);
            lastDistance = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(center[0] * center[0] + center[2] * center[2]));
            Write16(0x36, lastDistance.Value);
            NativeBattleWaterVectorRandom spin = NativeBattleWaterVectorCommands.RandomB65740(S16(0x2C), random);
            random = spin.Random;
            Write16(0x34, unchecked(S16(0x2A) + spin.Value));
            if (services is not null)
            {
                int emissions = U16(0x30);
                if (emissions > 0)
                {
                    random = services.EmitAuxiliary(random, unchecked(center[0] << 8), unchecked(center[1] << 8), unchecked(center[2] << 8), emissions);
                }
                if (U16(0xE) != 0)
                {
                    random = services.EmitFragmentHook!(random, unchecked(center[0] << 8), unchecked(center[1] << 8), unchecked(center[2] << 8));
                }
                if (fallback && emissions > 0)
                {
                    random = services.EmitAuxiliary(random, unchecked(center[0] << 8), unchecked(center[1] << 8), unchecked(center[2] << 8), emissions);
                }
            }
            lastVertices = face.Vertices;
            liveCount = unchecked((ushort)(liveCount + 1));
            cursor += 68;
        }
        return new(records.ToImmutableArray(), states.ToImmutableArray(), liveCount, random, lastDistance, lastVertices, services is not null ? countdown : null) { ResourcePointers = owners };

        ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(controls.AsSpan(offset));
        short S16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(controls.AsSpan(offset));
        void Write16(int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(cursor + offset), unchecked((ushort)value));
    }
}
