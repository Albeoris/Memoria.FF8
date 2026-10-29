using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlizzagaRibbonNodes(int Count, ImmutableArray<byte> Nodes);
public sealed record NativeBattleBlizzagaRibbonResult(int Count, ImmutableArray<byte> Nodes, NativeBattleGfGeometryBatch Packets);

/// <summary>Retained36-byte ribbon nodes and6D9A40's GT4 submissions.</summary>
public static class NativeBattleBlizzagaRibbon
{
    public static NativeBattleBlizzagaRibbonNodes Insert6D99D0(int count, ImmutableArray<byte> nodes, ImmutableArray<byte> firstPoint, ImmutableArray<byte> secondPoint, byte brightness, sbyte velocity, byte uBasis)
    {
        Validate(count, nodes);
        if (firstPoint.IsDefault || firstPoint.Length != 8 || secondPoint.IsDefault || secondPoint.Length != 8)
        {
            throw new InvalidDataException("Native ribbon insertion requires both complete8-byte source points.");
        }
        byte[] next = nodes.ToArray();
        if (count < 30)
        {
            next.AsSpan(0, count * 36).CopyTo(next.AsSpan(36));
            count++;
        }
        firstPoint.AsSpan().CopyTo(next);
        secondPoint.AsSpan().CopyTo(next.AsSpan(8));
        next[0x20] = brightness;
        next[0x21] = unchecked((byte)velocity);
        next[0x22] = uBasis;
        return new(count, next.ToImmutableArray());
    }

    public static ImmutableArray<byte> Point6D9CF0(ImmutableArray<byte> initialPoint, int angle, int height, int radius)
    {
        if (initialPoint.IsDefault || initialPoint.Length != 8)
        {
            throw new InvalidDataException("Native ribbon point construction requires its retained fourth WORD.");
        }
        byte[] point = initialPoint.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(point, unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 14)));
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(2), unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 14)));
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(4), unchecked((short)((height >> 2) - height)));
        return point.ToImmutableArray();
    }

    public static NativeBattleBlizzagaRibbonResult Emit6D9A40(int count, ImmutableArray<byte> nodes, sbyte velocityDelta, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattlePilotVisualIdentity? pilotIdentity = null, ImmutableArray<int> nodeBirths = default)
    {
        return Emit(count, nodes, velocityDelta, matrix, new(0x3E14, 0xB8, 0, 31, 0x4000, 0x7F00, false, false), false, 17, 4, point => NativeBattleModuleProjection.Project45FE10(matrix, point, projection), null, projection, pilotIdentity, nodeBirths);
    }

    public static NativeBattleBlizzagaRibbonResult Emit6F1290(int count, ImmutableArray<byte> nodes, sbyte velocityDelta, NativeBattleActionMatrix matrix, bool paused, int tableBaseBucket, byte depthShift, Func<NativeBattleVector, NativeBattleModuleProjectedVertex> project, Action<int>? observeInput = null, NativeBattleCameraProjection? projection = null, NativeBattlePilotVisualIdentity? pilotIdentity = null, ImmutableArray<int> nodeBirths = default)
    {
        return Emit(count, nodes, velocityDelta, matrix, new(0x3C94, 0xB6, 128, 31, 0, 127, true, true), paused, tableBaseBucket, unchecked(depthShift + 2), project, observeInput, projection, pilotIdentity, nodeBirths);
    }

    private sealed record RibbonProfile(ushort Clut, ushort TexturePage, uint AxisOrigin, uint AxisSpan, uint OtherFirst, uint OtherSecond, bool Vertical, bool SharedDepthMetadata);

    private static NativeBattleBlizzagaRibbonResult Emit(int count, ImmutableArray<byte> nodes, sbyte velocityDelta, NativeBattleActionMatrix matrix, RibbonProfile profile, bool paused, int tableBaseBucket, int depthShift, Func<NativeBattleVector, NativeBattleModuleProjectedVertex> project, Action<int>? observeInput, NativeBattleCameraProjection? projection = null, NativeBattlePilotVisualIdentity? pilotIdentity = null, ImmutableArray<int> nodeBirths = default)
    {
        Validate(count, nodes);
        matrix.Validate();
        byte[] next = nodes.ToArray();
        if (!paused)
        {
            for (int index = 0; index < count; index++)
            {
                int offset = index * 36;
                sbyte velocity = unchecked((sbyte)(next[offset + 0x21] + velocityDelta));
                next[offset + 0x21] = unchecked((byte)velocity);
                int brightness = next[offset + 0x20] + (velocity >> 3);
                next[offset + 0x20] = brightness < 0 ? (byte)0 : unchecked((byte)brightness);
            }
        }
        if (count >= 2 && next[(count - 1) * 36 + 0x20] == 0 && next[(count - 2) * 36 + 0x20] == 0)
        {
            count--;
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        if (count >= 2)
        {
            for (int index = 0; index < count; index++)
            {
                Project(index * 36, 0, 0x10);
                Project(index * 36, 8, 0x18);
            }
            ImmutableArray<int> sharedDepth = [I16((count - 2) * 36 + 0x14) & 0xFFFF, I16((count - 2) * 36 + 0x1C) & 0xFFFF, I16((count - 1) * 36 + 0x14) & 0xFFFF, I16((count - 1) * 36 + 0x1C) & 0xFFFF];
            for (int index = 0; index < count - 1; index++)
            {
                int a = index * 36;
                int b = a + 36;
                int[] depths = [I16(a + 0x14), I16(a + 0x1C), I16(b + 0x14), I16(b + 0x1C)];
                if (depths.Any(depth => depth <= 0))
                {
                    continue;
                }
                uint[] screens = [U32(a + 0x10), U32(a + 0x18), U32(b + 0x10), U32(b + 0x18)];
                bool swap = (NativeBattleModulePackets.Nclip(screens[0], screens[1], screens[2]) > 0) != (NativeBattleModulePackets.Nclip(screens[1], screens[3], screens[2]) > 0);
                if (swap)
                {
                    (screens[2], screens[3]) = (screens[3], screens[2]);
                }
                uint colorA = (uint)next[a + 0x20] * 0x10101;
                uint colorB = (uint)next[b + 0x20] * 0x10101;
                uint u = next[a + 0x22];
                uint firstAxis = unchecked(u + profile.AxisOrigin);
                uint lastAxis = unchecked(firstAxis + profile.AxisSpan);
                if (profile.Vertical)
                {
                    firstAxis <<= 8;
                    lastAxis <<= 8;
                }
                ImmutableArray<uint> words = [0x0C000000, 0x3E000000 | colorA, screens[0], ((uint)profile.Clut << 16) | profile.OtherFirst | firstAxis, colorA, screens[1], ((uint)profile.TexturePage << 16) | profile.OtherSecond | firstAxis, colorB, screens[2], (swap ? profile.OtherSecond : profile.OtherFirst) | lastAxis, colorB, screens[3], (swap ? profile.OtherFirst : profile.OtherSecond) | lastAxis];
                NativeBattleGeometryPrimitive primitive = new(tableBaseBucket + (depths.Sum() >> (depthShift & 31)), words, profile.SharedDepthMetadata ? sharedDepth : depths.Select(depth => tableBaseBucket * 4 + depth * 4).ToImmutableArray());
                if (pilotIdentity is { } identity && projection is not null && !nodeBirths.IsDefault)
                {
                    System.Numerics.Vector3 World(int node, int source)
                    {
                        NativeBattleVector point = new(I16(node + source), I16(node + source + 2), I16(node + source + 4));
                        return NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, point), projection);
                    }
                    primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + nodeBirths[index]) }, World(a, 0), World(a, 8), World(b, swap ? 8 : 0), World(b, swap ? 0 : 8), 4) { DepthQuantum = profile.SharedDepthMetadata ? 1 : 4, DepthOffset = profile.SharedDepthMetadata ? 0 : tableBaseBucket * 4, SourceDepths = profile.SharedDepthMetadata ? new(sharedDepth[0], sharedDepth[1], sharedDepth[2], sharedDepth[3]) : null, UseSourceDepths = profile.SharedDepthMetadata } };
                }
                byte[] data = new byte[52];
                for (int word = 0; word < words.Length; word++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(word * 4), words[word]);
                }
                attempts.Add(new(allocated, data.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 52).ToImmutableArray(), primitive));
                allocated += 52;
            }
        }
        return new(count, next.ToImmutableArray(), new(attempts.ToImmutable(), allocated));

        short I16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(next.AsSpan(offset));
        uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(next.AsSpan(offset));
        void Project(int node, int source, int destination)
        {
            observeInput?.Invoke(node + source);
            NativeBattleModuleProjectedVertex vertex = project(new(I16(node + source), I16(node + source + 2), I16(node + source + 4)));
            BinaryPrimitives.WriteUInt32LittleEndian(next.AsSpan(node + destination), vertex.Screen);
            BinaryPrimitives.WriteUInt32LittleEndian(next.AsSpan(node + destination + 4), vertex.ReadDepth45E250());
        }
    }

    private static readonly ConditionalWeakTable<NativeBattleTaskBankOwner, Dictionary<(int Slot, int Offset), NodeLifetimes>> lifetimes = new();

    private sealed class NodeLifetimes(long generation)
    {
        internal long Generation { get; } = generation;
        internal int Serial;
        internal List<int> Births { get; } = [];
    }

    internal static void RecordInsertion(NativeBattleModulePoolTask task, int nodeOffset, int count)
    {
        NodeLifetimes current = Lifetime(task, nodeOffset, count);
        int birth = checked(++current.Serial);
        if (count < 30)
        {
            current.Births.Insert(0, birth);
        }
        else
        {
            current.Births[0] = birth;
        }
    }

    internal static ImmutableArray<int> NodeBirths(NativeBattleModulePoolTask task, int nodeOffset, int count) => Lifetime(task, nodeOffset, count).Births.ToImmutableArray();

    private static NodeLifetimes Lifetime(NativeBattleModulePoolTask task, int nodeOffset, int count)
    {
        Dictionary<(int Slot, int Offset), NodeLifetimes> owners = lifetimes.GetValue(task.RequireOwner(), _ => []);
        var key = (task.PilotSlot, nodeOffset);
        long generation = NativeBattlePilotVisualLifetimes.Identity(104, task).Generation;
        if (!owners.TryGetValue(key, out NodeLifetimes? current) || current.Generation != generation)
        {
            current = new(generation);
            owners[key] = current;
        }
        if (current.Births.Count > count)
        {
            current.Births.RemoveRange(count, current.Births.Count - count);
        }
        while (current.Births.Count < count)
        {
            current.Births.Add(checked(++current.Serial));
        }
        return current;
    }

    private static void Validate(int count, ImmutableArray<byte> nodes)
    {
        if ((uint)count > 30 || nodes.IsDefault || nodes.Length != 30 * 36)
        {
            throw new InvalidDataException("Native ribbon drawing requires its complete30-node retained owner and current bounded count.");
        }
    }
}
