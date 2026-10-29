using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicGroundProjection18(NativeBattleWaterExecutionFrame Frame, uint Screen, int Depth, uint Flags)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public System.Numerics.Vector3? PilotWorld { get; init; }
}

public sealed record NativeBattleCinematicGroundProfile18Services(Func<NativeBattleWaterExecutionFrame, short, uint> ResolveReference, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareBasis, Func<NativeBattleWaterExecutionFrame, NativeBattleCinematicGroundProjection18> Project, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

/// <summary>Retained radial height profile and its ordered, partially written ground packets.</summary>
public static class NativeBattleCinematicGroundProfile18
{
    public const int AllocationBytes = 0x6800;
    public const int PhysicalWriteEnd = 0x6E5A;
    private static readonly byte[] Sectors = [1, 2, 3, 4, 0x43, 0x42, 0x41, 0x40, 0xC1, 0xC2, 0xC3, 0x84, 0x83, 0x82, 0x81, 0];

    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicGroundProfile18Services services, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        Dictionary<int, System.Numerics.Vector3>? pilotWorld = pilotIdentity is null ? null : [];
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(services.ResolveReference);
        ArgumentNullException.ThrowIfNull(services.PrepareBasis);
        ArgumentNullException.ThrowIfNull(services.Project);
        ArgumentNullException.ThrowIfNull(services.Append);
        uint profile = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        if (profile == 0)
        {
            (frame, profile) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 0xA0);
            frame = WriteSlotDword(frame, 0xBC, profile);
            uint parameters = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
            StoreWord(profile + 4, Word(parameters + 6));
            StoreWord(profile + 6, 32);
            int page = ((SignedWord(parameters) >> 2) & 0x3F0) | (Word(parameters + 2) & 0x100);
            page = unchecked((short)page) >> 4;
            page |= ReadSlot(frame, 0x92, 1)[0] & 0x60;
            page |= Word(parameters + 4);
            frame = WriteSlotWord(frame, 0x92, unchecked((ushort)page));
        }
        uint reference = services.ResolveReference(frame, SignedWord(profile + 4));
        uint manager = CoreDword(frame, 0x27973EC);
        uint work = CoreDword(frame, manager + 0x74);
        frame = frame.WriteTemporaryDword(0x80, work);
        int radiusStep = unchecked(SignedWord(reference + 0x98) * (short)SlotWord(frame, 0x90));
        int uvStep = unchecked((short)SlotWord(frame, 0x90) << 8);
        int radial = 0;
        int uv = 0;
        for (int index = 0; index < 32; index++)
        {
            int distance = radial / 32;
            int texture = uv / 32;
            StoreDword(work + (uint)(4 * index), distance);
            StoreWord(profile + (uint)(0x60 + 2 * index), unchecked((ushort)(distance / 256)));
            StoreDword(work + (uint)(0x500 + 4 * index), texture);
            StoreDword(work + (uint)(0x80 + 4 * index), 0);
            StoreDword(work + (uint)(0x580 + 4 * index), 0);
            for (int angleIndex = 1; angleIndex <= 3; angleIndex++)
            {
                int cosine = NativeBattleCameraMath.Cosine(angleIndex << 8) >> 4;
                int sine = NativeBattleCameraMath.Sine(angleIndex << 8) >> 4;
                StoreDword(work + (uint)(0x100 * angleIndex + 4 * index), unchecked(cosine * distance) >> 16);
                StoreDword(work + (uint)(0x100 * angleIndex + 0x80 + 4 * index), unchecked(sine * distance) >> 16);
                StoreDword(work + (uint)(0x500 + 0x100 * angleIndex + 4 * index), unchecked(cosine * texture) >> 16);
                StoreDword(work + (uint)(0x580 + 0x100 * angleIndex + 4 * index), unchecked(sine * texture) >> 16);
            }
            StoreDword(work + (uint)(4 * index), distance / 256);
            StoreDword(work + (uint)(0x500 + 4 * index), texture / 256);
            StoreDword(work + (uint)(0x400 + 4 * index), 0);
            StoreDword(work + (uint)(0x900 + 4 * index), 0);
            StoreDword(work + (uint)(0x480 + 4 * index), distance / 256);
            StoreDword(work + (uint)(0x980 + 4 * index), texture / 256);
            radial = unchecked(radial + radiusStep);
            uv = unchecked(uv + uvStep);
        }
        int amplitude = unchecked((short)SlotWord(frame, 0x8C) << 8);
        int amplitudeStep = unchecked((short)SlotWord(frame, 0x8E) << 4);
        int phase = unchecked(SignedWord(reference + 0x8C) << 8);
        int increment = SignedWord(reference + 0x8E);
        int incrementStep = SignedWord(reference + 0x90);
        for (int index = 0; index < 32; index++)
        {
            short height = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(phase >> 4) * (amplitude >> 4)) >> 20));
            StoreWord(profile + (uint)(0x20 + 2 * index), unchecked((ushort)height));
            StoreDword(work + (uint)(0xA00 + 4 * index), height);
            amplitude = unchecked(amplitude + amplitudeStep);
            increment = unchecked(increment + incrementStep);
            phase = unchecked(phase + increment);
        }
        frame = services.PrepareBasis(frame);
        int baseU = SignedWord(reference + 0x94);
        int baseV = SignedWord(reference + 0x96);
        uint output = CoreDword(frame, manager + 0x7C);
        ushort pageWord = SlotWord(frame, 0x92);
        ushort clut = SlotWord(frame, 0x9A);
        uint color = (BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xCC, 4)) & 0x02FFFFFFu) | 0x3C000000u;
        frame = frame.WriteTemporaryWord(0x84, pageWord).WriteTemporaryWord(0x88, clut).WriteTemporaryDword(0x8C, color);
        PacketWrites packets = new();
        int depthOffset = 0xB00;
        for (int index = 0; index < 32; index++)
        {
            NativeBattleCinematicGroundProjection18 projection = Project(0, SignedWord(work + (uint)(0xA00 + 4 * index)), SignedWord(work + (uint)(4 * index)), false);
            packets.Dword(52 * index + 8, projection.Screen);
            if (pilotWorld is not null && projection.PilotWorld is { } world)
            {
                pilotWorld[52 * index + 8] = world;
                if (index != 0)
                {
                    pilotWorld[52 * index - 32] = world;
                }
            }
            if (index != 0)
            {
                packets.Dword(52 * index - 32, projection.Screen);
            }
            byte u = unchecked((byte)(Read(work + (uint)(0x500 + 4 * index), 1)[0] + baseU));
            byte v = unchecked((byte)(Read(work + (uint)(0x580 + 4 * index), 1)[0] + baseV));
            packets.Byte(52 * index + 12, u);
            packets.Byte(52 * index + 13, v);
            if (index != 0)
            {
                packets.Byte(52 * index - 28, u);
                packets.Byte(52 * index - 27, v);
            }
            StoreWord(work + (uint)depthOffset, unchecked((ushort)projection.Depth));
            depthOffset += 2;
        }
        int cursor = 0x680;
        for (int sector = 0; sector < 16; sector++)
        {
            frame = frame.WriteTemporaryDword(0x7C, (uint)(sector + 1));
            byte code = Sectors[sector];
            int array = (code & 7) << 8;
            int orientation = (code >> 6) & 3;
            uint firstScreen = packets.ReadDword(cursor - 0x678);
            ushort firstUv = packets.ReadWord(cursor - 0x674);
            packets.Dword(cursor + 8, firstScreen);
            packets.Dword(cursor - 0x660, firstScreen);
            if (pilotWorld is not null && pilotWorld.TryGetValue(cursor - 0x678, out System.Numerics.Vector3 sourceWorld))
            {
                pilotWorld[cursor + 8] = sourceWorld;
                pilotWorld[cursor - 0x660] = sourceWorld;
            }
            packets.Word(cursor + 12, firstUv);
            packets.Word(cursor - 0x65C, firstUv);
            StoreWord(work + (uint)depthOffset, Word(work + (uint)(depthOffset - 0x20)));
            cursor += 52;
            depthOffset += 2;
            for (int index = 1; index < 32; index++)
            {
                int a = SignedWord(work + (uint)(array + 4 * index));
                int b = SignedWord(work + (uint)(array + 0x80 + 4 * index));
                int x = orientation >= 2 ? unchecked((short)-b) : b;
                int z = (orientation & 1) != 0 ? unchecked((short)-a) : a;
                NativeBattleCinematicGroundProjection18 projection = Project(x, SignedWord(work + (uint)(0xA00 + 4 * index)), z, true);
                packets.Dword(cursor - 0x688, projection.Screen);
                packets.Dword(cursor - 0x660, projection.Screen);
                packets.Dword(cursor - 0x20, projection.Screen);
                packets.Dword(cursor + 8, projection.Screen);
                if (pilotWorld is not null && projection.PilotWorld is { } world)
                {
                    foreach (int xyOffset in new[] { -0x688, -0x660, -0x20, 8 })
                    {
                        pilotWorld[cursor + xyOffset] = world;
                    }
                }
                int u = SignedWord(work + (uint)(array + 0x500 + 4 * index));
                int v = SignedWord(work + (uint)(array + 0x580 + 4 * index));
                u = unchecked(baseU + ((orientation & 1) != 0 ? -u : u));
                v = unchecked(baseV + (orientation >= 2 ? -v : v));
                ushort packedUv = unchecked((ushort)((v << 8) | u));
                packets.Word(cursor - 0x684, packedUv);
                packets.Word(cursor - 0x65C, packedUv);
                packets.Word(cursor - 0x1C, packedUv);
                packets.Word(cursor + 12, packedUv);
                StoreWord(work + (uint)depthOffset, unchecked((ushort)projection.Depth));
                int packet = cursor - 0x6B4;
                packets.Byte(packet + 3, 12);
                packets.Word(packet + 0x1A, pageWord);
                packets.Word(packet + 0xE, clut);
                packets.Dword(packet + 4, color);
                packets.Dword(packet + 0x10, color);
                packets.Dword(packet + 0x1C, color);
                packets.Dword(packet + 0x28, color);
                int sum = unchecked(SignedWord(work + (uint)(depthOffset - 0x22)) + SignedWord(work + (uint)(depthOffset - 0x20)) + SignedWord(work + (uint)(depthOffset - 2)) + projection.Depth);
                int offset = (sum / 4) & ~3;
                NativeBattlePilotVisualGeometry? geometryMetadata = null;
                if (pilotIdentity is { } identity && pilotWorld is not null && pilotWorld.TryGetValue(packet + 8, out System.Numerics.Vector3 first) && pilotWorld.TryGetValue(packet + 20, out System.Numerics.Vector3 second) && pilotWorld.TryGetValue(packet + 32, out System.Numerics.Vector3 third) && pilotWorld.TryGetValue(packet + 44, out System.Numerics.Vector3 fourth))
                {
                    geometryMetadata = new(identity with { Primitive = sector * 32 + index }, first, second, third, fourth, 4) { ScreenScale = 0.125f };
                }
                packets.Insert(packet, offset / 4, geometryMetadata);
                cursor += 52;
                depthOffset += 2;
            }
            frame = frame.WriteTemporaryDword(0x20, 0).WriteTemporaryDword(0x30, 0).WriteTemporaryDword(0x40, 0).WriteTemporaryDword(0x50, 0);
        }
        frame = frame.WriteTemporaryDword(0x7C, 17);
        frame = services.Append(frame, output, frame.ReadTemporaryDword(0x14C), new(packets.Attempts.ToImmutable(), AllocationBytes) { NativeUnlinkedAllocation = true });
        return WriteCoreDword(frame, manager + 0x7C, unchecked(output + AllocationBytes));

        NativeBattleCinematicGroundProjection18 Project(int x, int y, int z, bool temporaryScreen)
        {
            frame = frame.WriteTemporaryWord(0xD0, unchecked((ushort)x)).WriteTemporaryWord(0xD2, unchecked((ushort)y)).WriteTemporaryWord(0xD4, unchecked((ushort)z));
            NativeBattleCinematicGroundProjection18 result = services.Project(frame);
            frame = result.Frame.WriteTemporaryDword(0xFC, result.Flags);
            if (temporaryScreen)
            {
                frame = frame.WriteTemporaryDword(0xF0, result.Screen);
            }
            return result;
        }

        byte[] Read(uint address, int length) => frame.Arena.Read(ArenaOffset(address, length), length);
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
        short SignedWord(uint address) => unchecked((short)Word(address));

        void StoreWord(uint address, ushort value)
        {
            byte[] data = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(data, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), data) };
        }

        void StoreDword(uint address, int value)
        {
            byte[] data = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(data, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), data) };
        }
    }

    private sealed class PacketWrites
    {
        private readonly byte[] data = new byte[0x6E5C];
        private readonly NativeBattleGfPacketByteState[] known = new NativeBattleGfPacketByteState[0x6E5C];
        public ImmutableArray<NativeBattleGfGeometryAttempt>.Builder Attempts { get; } = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();

        public uint ReadDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset));
        public ushort ReadWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));
        public void Byte(int offset, byte value) => Store(offset, [value]);

        public void Word(int offset, ushort value)
        {
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            Store(offset, bytes);
        }

        public void Dword(int offset, uint value)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            Store(offset, bytes);
        }

        public void Insert(int offset, int bucket, NativeBattlePilotVisualGeometry? pilotGeometry = null)
        {
            uint[] words = new uint[13];
            for (int index = 0; index < words.Length; index++)
            {
                words[index] = ReadDword(offset + 4 * index);
            }
            NativeBattleGeometryPrimitive primitive = new(bucket, words.ToImmutableArray(), []) { PhysicalWriteMask = known.AsSpan(offset, 52).ToArray().ToImmutableArray(), PilotGeometry = pilotGeometry };
            Attempts.Add(new(offset, data.AsSpan(offset, 52).ToArray().ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 52).ToImmutableArray(), primitive));
        }

        private void Store(int offset, ReadOnlySpan<byte> bytes)
        {
            bytes.CopyTo(data.AsSpan(offset));
            Array.Fill(known, NativeBattleGfPacketByteState.Known, offset, bytes.Length);
            int start = offset & ~3;
            int length = (offset + bytes.Length + 3 & ~3) - start;
            byte[] written = new byte[length];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
            bytes.CopyTo(written.AsSpan(offset - start));
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset - start, bytes.Length);
            Attempts.Add(new(start, written.ToImmutableArray(), mask.ToImmutableArray(), null));
        }
    }
}
