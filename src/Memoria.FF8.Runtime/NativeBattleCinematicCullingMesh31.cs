using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicCullingColor(NativeBattleWaterExecutionFrame Frame, uint Color);
public sealed record NativeBattleCinematicCullingMesh31Services(Func<NativeBattleWaterExecutionFrame, short, uint> ResolveShape, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareBasis, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleWaterExecutionFrame> Project, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> PrepareMaterial, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleCinematicCullingColor> Shade, Func<int, byte> PrimitiveSize, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append)
{
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
    public IReadOnlyDictionary<uint, System.Numerics.Vector3>? PilotWorld { get; init; }
}

/// <summary>A mesh revealed by its local depth, with independently linked Gouraud quadrilaterals.</summary>
public static class NativeBattleCinematicCullingMesh31
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicCullingMesh31Services services)
    {
        if (SlotDword(0xBC) == 0)
        {
            uint payload = SlotDword(0xB8);
            short selector = SignedWord(unchecked(payload + 2));
            frame = WriteSlotDword(frame, 0xBC, uint.MaxValue);
            frame = WriteSlotDword(frame, 0xD8, services.ResolveShape(frame, selector));
            frame = WriteSlotDword(frame, 0xCC, SlotDword(0xCC) & 0x02FFFFFF);
        }
        uint manager = CoreDword(frame, 0x27973EC);
        uint shape = SlotDword(0xD8);
        uint groups = unchecked(shape + Dword(shape + 8));
        uint vertices = unchecked(shape + Dword(shape + 0x14));
        int count = unchecked((int)Dword(shape + 0x18));
        uint work = CoreDword(frame, unchecked(manager + 0x74));
        uint normalWork = unchecked(work + (uint)(count * 8));
        uint masks = unchecked(normalWork + Dword(shape + 0x20) * 8);
        uint output = CoreDword(frame, unchecked(manager + 0x7C));
        frame = frame.WriteTemporaryDword(0x60, output).WriteTemporaryDword(0x68, shape).WriteTemporaryDword(0x6C, groups).WriteTemporaryDword(0x74, vertices).WriteTemporaryDword(0x78, unchecked(shape + Dword(shape + 0x1C))).WriteTemporaryDword(0x7C, work).WriteTemporaryDword(0x80, normalWork).WriteTemporaryDword(0x40, masks).WriteTemporaryDword(0x48, unchecked((uint)count));
        int maximum = 0;
        for (int index = 0; index < count; index++)
        {
            maximum = Math.Max(maximum, SignedWord(unchecked(vertices + (uint)(index * 8 + 4))));
        }
        frame = frame.WriteTemporaryDword(0x44, unchecked((uint)maximum));
        int threshold = unchecked(maximum * (short)SlotWord(frame, 0x98)) / 4096;
        for (int index = 0; index < count; index++)
        {
            byte mask = SignedWord(unchecked(vertices + (uint)(index * 8 + 4))) > threshold ? (byte)0 : byte.MaxValue;
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(masks + (uint)index), 1), [mask]) };
        }
        frame = services.PrepareBasis(frame);
        for (int index = 0; index < count; index++)
        {
            if (Read(unchecked(masks + (uint)index), 1)[0] == 0)
            {
                frame = services.Project(frame, unchecked(vertices + (uint)(index * 8)), unchecked(work + (uint)(index * 8)));
            }
        }
        while (true)
        {
            int faces = SignedWord(unchecked(groups + 2));
            if (faces == -1)
            {
                break;
            }
            int kind = SignedWord(groups);
            uint face = unchecked(groups + 4);
            uint next = unchecked(face + (uint)(faces * services.PrimitiveSize(kind)));
            frame = frame.WriteTemporaryDword(0x70, unchecked((uint)faces)).WriteTemporaryDword(0x6C, face).WriteTemporaryDword(0x58, next);
            uint material = SlotDword(0xCC);
            frame = services.PrepareMaterial(frame, material);
            for (int index = 0; index < faces; index++, face = unchecked(face + 24))
            {
                ushort[] offsets = Enumerable.Range(0, 4).Select(vertex => Word(unchecked(face + 0x10u + (uint)(vertex * 2)))).ToArray();
                int visibility = 0;
                foreach (ushort offset in offsets)
                {
                    visibility |= Read(unchecked(masks + (uint)(offset >> 3)), 1)[0];
                }
                if (visibility != 0)
                {
                    continue;
                }
                byte[] packet = new byte[36];
                packet[3] = 8;
                int depth = 0;
                for (int vertex = 0; vertex < 4; vertex++)
                {
                    uint projected = unchecked(work + offsets[vertex]);
                    Read(projected, 4).CopyTo(packet, 8 + vertex * 8);
                    depth = unchecked(depth + SignedWord(unchecked(projected + 4)));
                }
                for (int vertex = 0; vertex < 4; vertex++)
                {
                    NativeBattleCinematicCullingColor shaded = services.Shade(frame, Dword(unchecked(face + (uint)(vertex * 4))));
                    frame = shaded.Frame;
                    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4 + vertex * 8), shaded.Color);
                }
                packet[7] = (byte)((material | 0x38000000) >> 24);
                uint control = unchecked(CoreDword(frame, 0x27973B8) + 0x4C);
                uint relative = unchecked(control - frame.TemporaryFrame.Address);
                uint orderingBase = relative <= 0x17C ? frame.ReadTemporaryDword((int)relative) : CoreDword(frame, control);
                uint ordering = unchecked(orderingBase + (uint)((depth / 4) & ~3));
                NativeBattleGfGeometryBatch batch = Batch(packet);
                if (services.PilotIdentity is { } identity && services.PilotWorld is { } world && offsets.All(offset => world.ContainsKey(unchecked(work + offset))))
                {
                    NativeBattlePilotVisualGeometry geometry = new(identity with { Primitive = unchecked((int)face) }, world[work + offsets[0]], world[work + offsets[1]], world[work + offsets[2]], world[work + offsets[3]], 4);
                    batch = batch with { Attempts = batch.Attempts.Select(attempt => attempt with { Primitive = attempt.Primitive! with { PilotGeometry = geometry } }).ToImmutableArray() };
                }
                frame = services.Append(frame, output, ordering, batch);
                output = unchecked(output + 36);
                byte[] mode = new byte[12];
                mode[3] = 2;
                BinaryPrimitives.WriteUInt32LittleEndian(mode.AsSpan(4), 0xE1000000u | (ReadSlot(frame, 0x92, 1)[0] & 0x60u));
                frame = services.Append(frame, output, ordering, Batch(mode));
                output = unchecked(output + 12);
            }
            frame = frame.WriteTemporaryDword(0x60, output).WriteTemporaryDword(0x6C, next);
            groups = next;
        }
        return WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), output);

        byte[] Read(uint address, int length) => frame.Arena.Read(ArenaOffset(address, length), length);
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
        short SignedWord(uint address) => unchecked((short)Word(address));
        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
    }

    private static NativeBattleGfGeometryBatch Batch(byte[] data)
    {
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, data.Length).Select(index => index >= 3 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
        ImmutableArray<uint> words = Enumerable.Range(0, data.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(0, words, []) { PhysicalWriteMask = mask };
        return new([new(0, data.ToImmutableArray(), mask, primitive)], data.Length);
    }
}
