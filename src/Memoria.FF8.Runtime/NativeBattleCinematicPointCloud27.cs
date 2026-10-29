using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicPointProjection(NativeBattleWaterExecutionFrame Frame, uint Screen, int QuarterDepth)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public System.Numerics.Vector3? PilotWorld { get; init; }
}
public sealed record NativeBattleCinematicPointCloud27Services(Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareAnchor, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleCinematicPointProjection> ProjectPoint, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

/// <summary>The retained cinematic point cloud and its independent blink counters.</summary>
public static class NativeBattleCinematicPointCloud27
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicPointCloud27Services services, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4)) == 0)
        {
            uint payload = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
            int count = SignedWord(payload);
            uint manager = CoreDword(frame, 0x27973EC);
            uint storage = CoreDword(frame, unchecked(manager + 0x74));
            uint allocation = unchecked((uint)(count * 16 + 4));
            uint aligned = unchecked(allocation + 3) & ~3u;
            frame = WriteCoreDword(frame, unchecked(manager + 0x74), unchecked(storage + aligned));
            frame = WriteSlotDword(frame, 0xBC, storage);
            StoreWord(unchecked(storage + 2), unchecked((ushort)count));
            int colorWidth = SignedWord(unchecked(payload + 8));
            uint color = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4));
            int xWidth = SignedWord(unchecked(payload + 2));
            int yWidth = SignedWord(unchecked(payload + 4));
            int zWidth = SignedWord(unchecked(payload + 6));
            frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)(int)SignedWord(unchecked(payload + 10))));
            uint record = unchecked(storage + 4);
            for (int index = 0; index < count; index++)
            {
                StoreWord(unchecked(record + 6), 0);
                StoreWord(unchecked(record + 12), 0);
                StoreWord(record, unchecked((ushort)SignedRandom(xWidth)));
                StoreWord(unchecked(record + 2), unchecked((ushort)SignedRandom(yWidth)));
                StoreWord(unchecked(record + 4), unchecked((ushort)SignedRandom(zWidth)));
                int red = unchecked(SignedRandom(colorWidth) + (byte)color);
                int green = unchecked(SignedRandom(colorWidth) + (byte)(color >> 8));
                int blue = unchecked(SignedRandom(colorWidth) + (byte)(color >> 16));
                StoreDword(unchecked(record + 8), unchecked((uint)(red | (green << 8) | (blue << 16) | 0x68000000)));
                if (PositiveRandom(256) < unchecked((int)frame.ReadTemporaryDword(0xF0)))
                {
                    StoreByte(unchecked(record + 6), (byte)(Read(record + 6, 1)[0] | 1));
                    int duration = SignedRandom(64);
                    if (duration > 0)
                    {
                        StoreByte(unchecked(record + 12), unchecked((byte)duration));
                    }
                    else
                    {
                        StoreByte(unchecked(record + 13), unchecked((byte)~duration));
                    }
                }
                record = unchecked(record + 16);
            }
        }
        uint state = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        uint points = unchecked(state + 4);
        frame = frame.WriteTemporaryDword(0x60, points);
        StoreWord(state, unchecked((ushort)(UnsignedWord(state) + 1)));
        ushort fixedDepth = SlotWord(frame, 0xC0);
        frame = frame.WriteTemporaryDword(0x64, fixedDepth == 0 ? 0 : unchecked(frame.ReadTemporaryDword(0x14C) + (uint)(fixedDepth & 0x3FFC)));
        frame = services.PrepareAnchor(frame);
        int projectionCount = SignedWord(unchecked(state + 2));
        uint scratch = CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x74));
        Dictionary<int, System.Numerics.Vector3>? pilotWorld = pilotIdentity is null ? null : [];
        for (int index = 0; index < projectionCount; index++)
        {
            NativeBattleCinematicPointProjection projected = services.ProjectPoint(frame, unchecked(points + (uint)(index * 16)));
            frame = projected.Frame;
            if (pilotWorld is not null && projected.PilotWorld is { } world)
            {
                pilotWorld[index] = world;
            }
            StoreDword(unchecked(scratch + (uint)(index * 8)), projected.Screen);
            StoreDword(unchecked(scratch + (uint)(index * 8 + 4)), unchecked((uint)projected.QuarterDepth));
        }
        uint currentManager = CoreDword(frame, 0x27973EC);
        scratch = CoreDword(frame, unchecked(currentManager + 0x74));
        uint output = CoreDword(frame, unchecked(currentManager + 0x7C));
        points = frame.ReadTemporaryDword(0x60);
        int drawCount = SignedWord(unchecked(state + 2));
        for (int index = 0; index < drawCount; index++)
        {
            uint record = unchecked(points + (uint)(index * 16));
            bool emit = true;
            if ((Read(unchecked(record + 6), 1)[0] & 1) != 0)
            {
                byte visible = Read(unchecked(record + 12), 1)[0];
                if (visible != 0)
                {
                    visible--;
                    StoreByte(unchecked(record + 12), visible);
                    if (visible == 0)
                    {
                        int hidden = PositiveRandom(64);
                        StoreByte(unchecked(record + 12), 0);
                        StoreByte(unchecked(record + 13), unchecked((byte)hidden));
                    }
                }
                else
                {
                    byte hidden = unchecked((byte)(Read(unchecked(record + 13), 1)[0] - 1));
                    StoreByte(unchecked(record + 13), hidden);
                    if (hidden == 0)
                    {
                        int duration = PositiveRandom(64);
                        StoreByte(unchecked(record + 13), 0);
                        StoreByte(unchecked(record + 12), unchecked((byte)duration));
                    }
                    emit = false;
                }
            }
            if (!emit)
            {
                continue;
            }
            byte[] data = new byte[12];
            data[3] = 2;
            Read(unchecked(record + 8), 4).CopyTo(data, 4);
            uint projectedAddress = unchecked(scratch + (uint)(index * 8));
            Read(projectedAddress, 4).CopyTo(data, 8);
            uint ordering = frame.ReadTemporaryDword(0x64);
            if (ordering == 0)
            {
                int cueOffset = SignedWord(unchecked(projectedAddress + 4)) & ~3;
                ordering = unchecked(frame.ReadTemporaryDword(0x14C) + (uint)cueOffset);
            }
            ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, 12).Select(offset => offset >= 3 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
            ImmutableArray<uint> words = [BinaryPrimitives.ReadUInt32LittleEndian(data), BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4)), BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8))];
            NativeBattleGeometryPrimitive primitive = new(0, words, []) { PhysicalWriteMask = mask };
            if (pilotIdentity is { } identity && pilotWorld is not null && pilotWorld.TryGetValue(index, out System.Numerics.Vector3 anchor))
            {
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = index }, anchor, anchor, anchor, anchor, 4) { ScreenOffsets = [new(0, 0), new(8, 0), new(0, 8), new(8, 8)] } };
            }
            frame = services.Append(frame, output, ordering, new([new(0, data.ToImmutableArray(), mask, primitive)], 12));
            output = unchecked(output + 12);
        }
        currentManager = CoreDword(frame, 0x27973EC);
        return WriteCoreDword(frame, unchecked(currentManager + 0x7C), output);

        byte[] Read(uint address, int length) => frame.Arena.Read(ArenaOffset(address, length), length);
        short SignedWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(Read(address, 2));
        ushort UnsignedWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));

        void StoreByte(uint address, byte value)
        {
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 1), [value]) };
        }

        void StoreWord(uint address, ushort value)
        {
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), bytes) };
        }

        void StoreDword(uint address, uint value)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), bytes) };
        }

        int SignedRandom(int operand)
        {
            NativeBattleWaterVectorRandom result = NativeBattleWaterVectorCommands.RandomB65740(unchecked((short)operand), frame.Random);
            frame = frame with { Random = result.Random };
            return result.Value;
        }

        int PositiveRandom(int operand)
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            return unchecked((int)((uint)(value * operand) >> 15));
        }
    }
}
