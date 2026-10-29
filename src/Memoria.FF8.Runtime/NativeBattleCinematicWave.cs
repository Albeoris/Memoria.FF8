using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicWaveProjection(NativeBattleWaterExecutionFrame Frame, uint Screen, int QuarterDepth)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public System.Numerics.Vector3? PilotWorld { get; init; }
}
public sealed record NativeBattleCinematicWaveServices(Func<NativeBattleWaterExecutionFrame, short, uint> ResolveSlot, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareAnchor, Func<NativeBattleWaterExecutionFrame, NativeBattleVector, NativeBattleCinematicWaveProjection> Project, Func<int, byte> ReadSector, Func<uint, int> ResolveBucket, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

/// <summary>The radial wave's current workspace, overlapping packet rows and ordered quad links.</summary>
public static class NativeBattleCinematicWave
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicWaveServices services, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        Dictionary<int, System.Numerics.Vector3>? pilotWorld = pilotIdentity is null ? null : [];
        uint state = SlotDword(0xBC);
        if (state == 0)
        {
            (frame, state) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 0xA0);
            frame = WriteSlotDword(frame, 0xBC, state);
            uint payload = SlotDword(0xB8);
            StoreWord(state + 6, 32);
            StoreWord(state + 4, Word(payload + 6));
            int page = ((Word(payload) >> 2) & 0x3F0) | (Word(payload + 2) & 0x100);
            page = (page >> 4) | (ReadSlot(frame, 0x92, 1)[0] & 0x60) | Word(payload + 4);
            frame = WriteSlotWord(frame, 0x92, unchecked((ushort)page));
        }
        uint reference = services.ResolveSlot(frame, unchecked((short)Word(state + 4)));
        uint scratch = ManagerDword(0x74);
        frame = frame.WriteTemporaryDword(0x80, scratch);
        Temporary(0x60, SignedSlot(0x90));
        Temporary(0x64, SignedWord(reference + 0x98));
        Temporary(0x68, SignedSlot(0x8C) << 8);
        Temporary(0x6C, SignedSlot(0x8E) << 4);
        Temporary(0x70, SignedWord(reference + 0x8C) << 8);
        Temporary(0x74, SignedWord(reference + 0x8E));
        Temporary(0x78, SignedWord(reference + 0x90));
        Temporary(0x84, SignedSlot(0x92));
        Temporary(0x88, SignedSlot(0x9A));
        Temporary(0x8C, unchecked((int)((SlotDword(0xCC) & 0x02FFFFFF) | 0x3C000000)));
        int radiusStep = unchecked(TemporaryInt(0x60) * TemporaryInt(0x64));
        int textureStep = unchecked(TemporaryInt(0x60) << 8);
        int radius = 0;
        int texture = 0;
        for (int index = 0; index < 32; index++)
        {
            int currentRadius = radius / 32;
            StoreWord(unchecked(state + 0x60u + (uint)(index * 2)), unchecked((ushort)(currentRadius / 256)));
            StoreScratch(index * 4, currentRadius);
            StoreScratch(0x500 + index * 4, texture / 32);
            StoreScratch(0x80 + index * 4, 0);
            StoreScratch(0x580 + index * 4, 0);
            radius = unchecked(radius + radiusStep);
            texture = unchecked(texture + textureStep);
        }
        for (int angleIndex = 1; angleIndex <= 3; angleIndex++)
        {
            int cosine = NativeBattleCameraMath.Cosine(angleIndex * 256) >> 4;
            int sine = NativeBattleCameraMath.Sine(angleIndex * 256) >> 4;
            for (int index = 0; index < 32; index++)
            {
                int radial = Scratch(index * 4);
                int uv = Scratch(0x500 + index * 4);
                StoreScratch(angleIndex * 0x100 + index * 4, unchecked(cosine * radial) >> 16);
                StoreScratch(angleIndex * 0x100 + 0x80 + index * 4, unchecked(sine * radial) >> 16);
                StoreScratch(0x500 + angleIndex * 0x100 + index * 4, unchecked(cosine * uv) >> 16);
                StoreScratch(0x580 + angleIndex * 0x100 + index * 4, unchecked(sine * uv) >> 16);
            }
        }
        for (int index = 0; index < 32; index++)
        {
            StoreScratch(0x400 + index * 4, 0);
            StoreScratch(0x900 + index * 4, 0);
            int radial = Scratch(index * 4) / 256;
            StoreScratch(index * 4, radial);
            StoreScratch(0x480 + index * 4, radial);
            int uv = Scratch(0x500 + index * 4) / 256;
            StoreScratch(0x500 + index * 4, uv);
            StoreScratch(0x980 + index * 4, uv);
        }
        int amplitude = TemporaryInt(0x68);
        int amplitudeStep = TemporaryInt(0x6C);
        int phase = TemporaryInt(0x70);
        int velocity = TemporaryInt(0x74);
        int acceleration = TemporaryInt(0x78);
        for (int index = 0; index < 32; index++)
        {
            int product = unchecked(NativeBattleCameraMath.Sine(phase >> 4) * (amplitude >> 4));
            short height = unchecked((short)(product >> 20));
            StoreWord(unchecked(state + 0x20u + (uint)(index * 2)), unchecked((ushort)height));
            StoreScratch(0xA00 + index * 4, height);
            amplitude = unchecked(amplitude + amplitudeStep);
            velocity = unchecked(velocity + acceleration);
            phase = unchecked(phase + velocity);
        }
        frame = services.PrepareAnchor(frame);
        int firstU = SignedWord(reference + 0x94);
        int firstV = SignedWord(reference + 0x96);
        uint output = ManagerDword(0x7C);
        byte[] packets = new byte[0x6E80];
        bool[] known = new bool[packets.Length];
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        uint depth = unchecked(scratch + 0xB00);
        for (int index = 0; index < 32; index++)
        {
            NativeBattleCinematicWaveProjection projected = Project(0, Scratch(0xA00 + index * 4), Scratch(index * 4));
            int point = index * 52;
            PacketDword(point + 8, projected.Screen);
            if (pilotWorld is not null && projected.PilotWorld is { } world)
            {
                pilotWorld[point + 8] = world;
                if (index != 0)
                {
                    pilotWorld[point - 32] = world;
                }
            }
            byte u = unchecked((byte)(Scratch(0x500 + index * 4) + firstU));
            byte v = unchecked((byte)(Scratch(0x580 + index * 4) + firstV));
            PacketByte(point + 12, u);
            PacketByte(point + 13, v);
            if (index != 0)
            {
                PacketDword(point - 32, projected.Screen);
                PacketByte(point - 28, u);
                PacketByte(point - 27, v);
            }
            StoreWord(depth, unchecked((ushort)projected.QuarterDepth));
            depth = unchecked(depth + 2);
        }
        int cursor = 0x680;
        frame = frame.WriteTemporaryDword(0x7C, 1);
        for (int sector = 1; sector <= 16; sector++)
        {
            byte descriptor = services.ReadSector(sector);
            int angular = (descriptor & 7) << 8;
            int quadrant = descriptor >> 6;
            uint screen = ReadPacketDword(cursor - 0x678);
            PacketDword(cursor + 8, screen);
            PacketDword(cursor - 0x660, screen);
            if (pilotWorld is not null && pilotWorld.TryGetValue(cursor - 0x678, out System.Numerics.Vector3 sourceWorld))
            {
                pilotWorld[cursor + 8] = sourceWorld;
                pilotWorld[cursor - 0x660] = sourceWorld;
            }
            ushort uv = ReadPacketWord(cursor - 0x674);
            PacketWord(cursor + 12, uv);
            PacketWord(cursor - 0x65C, uv);
            StoreWord(depth, Word(unchecked(depth - 0x20)));
            cursor += 52;
            depth = unchecked(depth + 2);
            for (int index = 1; index < 32; index++)
            {
                int radial = SignedScratch(angular + index * 4);
                int angularX = SignedScratch(angular + 0x80 + index * 4);
                NativeBattleCinematicWaveProjection projected = Project(quadrant >= 2 ? -angularX : angularX, Scratch(0xA00 + index * 4), (quadrant & 1) != 0 ? -radial : radial);
                frame = frame.WriteTemporaryDword(0xF0, projected.Screen);
                foreach (int offset in new[] { -0x688, -0x660, -0x20, 8 })
                {
                    PacketDword(cursor + offset, projected.Screen);
                    if (pilotWorld is not null && projected.PilotWorld is { } world)
                    {
                        pilotWorld[cursor + offset] = world;
                    }
                }
                int u = SignedScratch(angular + 0x500 + index * 4);
                int v = SignedScratch(angular + 0x580 + index * 4);
                ushort packedUv = unchecked((ushort)(((quadrant >= 2 ? firstV - v : firstV + v) << 8) | ((quadrant & 1) != 0 ? firstU - u : firstU + u)));
                foreach (int offset in new[] { -0x684, -0x65C, -0x1C, 12 })
                {
                    PacketWord(cursor + offset, packedUv);
                }
                StoreWord(depth, unchecked((ushort)projected.QuarterDepth));
                int packet = cursor - 0x6B4;
                PacketByte(packet + 3, 12);
                PacketWord(packet + 26, unchecked((ushort)frame.ReadTemporaryDword(0x84)));
                PacketWord(packet + 14, unchecked((ushort)frame.ReadTemporaryDword(0x88)));
                for (int corner = 0; corner < 4; corner++)
                {
                    PacketDword(packet + 4 + corner * 12, frame.ReadTemporaryDword(0x8C));
                }
                int sum = SignedWord(unchecked(depth - 0x22)) + SignedWord(unchecked(depth - 0x20)) + SignedWord(unchecked(depth - 2)) + projected.QuarterDepth;
                if (CoreDword(frame, 0x27973B8) != unchecked(frame.TemporaryFrame.Address + 0x100))
                {
                    throw new NotSupportedException("Native wave requires its current callback control binding.");
                }
                uint ordering = unchecked(frame.ReadTemporaryDword(0x14C) + (uint)(sum / 4 & ~3));
                int allocation = index == 31 ? 104 : 52;
                byte[] data = new byte[allocation];
                Array.Copy(packets, packet, data, 0, 52);
                NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[allocation];
                for (int offset = 0; offset < 52; offset++)
                {
                    mask[offset] = known[packet + offset] ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched;
                }
                ImmutableArray<uint> words = Enumerable.Range(0, 13).Select(offset => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset * 4))).ToImmutableArray();
                NativeBattleGeometryPrimitive primitive = new(services.ResolveBucket(ordering), words, [0, 0, 0, 0]);
                if (pilotIdentity is { } identity && pilotWorld is not null && pilotWorld.TryGetValue(packet + 8, out System.Numerics.Vector3 a) && pilotWorld.TryGetValue(packet + 20, out System.Numerics.Vector3 b) && pilotWorld.TryGetValue(packet + 32, out System.Numerics.Vector3 c) && pilotWorld.TryGetValue(packet + 44, out System.Numerics.Vector3 d))
                {
                    primitive = primitive with { PilotGeometry = new(identity with { Primitive = sector * 32 + index }, a, b, c, d, 4) };
                }
                attempts.Add(new(packet, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
                cursor += 52;
                depth = unchecked(depth + 2);
            }
            frame = frame.WriteTemporaryDword(0x20, 0).WriteTemporaryDword(0x30, 0).WriteTemporaryDword(0x40, 0).WriteTemporaryDword(0x50, 0).WriteTemporaryDword(0x7C, unchecked((uint)(sector + 1)));
        }
        int allocated = cursor - 0x680;
        frame = services.Append(frame, output, new(attempts.ToImmutable(), allocated));
        uint manager = CoreDword(frame, 0x27973EC);
        return WriteCoreDword(frame, unchecked(manager + 0x7C), unchecked(output + (uint)allocated));

        NativeBattleCinematicWaveProjection Project(int x, int y, int z)
        {
            NativeBattleVector vertex = new(unchecked((short)x), unchecked((short)y), unchecked((short)z));
            frame = frame.WriteTemporaryWord(0xD0, unchecked((ushort)vertex.X)).WriteTemporaryWord(0xD2, unchecked((ushort)vertex.Y)).WriteTemporaryWord(0xD4, unchecked((ushort)vertex.Z));
            NativeBattleCinematicWaveProjection projected = services.Project(frame, vertex);
            frame = projected.Frame;
            return projected;
        }

        void PacketByte(int offset, byte value)
        {
            PacketStore(offset, [value]);
        }

        void PacketWord(int offset, ushort value)
        {
            PacketStore(offset, BitConverter.GetBytes(value));
        }

        void PacketDword(int offset, uint value)
        {
            PacketStore(offset, BitConverter.GetBytes(value));
        }

        void PacketStore(int offset, byte[] value)
        {
            value.CopyTo(packets, offset);
            Array.Fill(known, true, offset, value.Length);
            int first = offset & ~3;
            int length = (offset + value.Length + 3 & ~3) - first;
            byte[] bytes = new byte[length];
            value.CopyTo(bytes, offset - first);
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset - first, value.Length);
            attempts.Add(new(first, bytes.ToImmutableArray(), mask.ToImmutableArray(), null));
        }

        ushort ReadPacketWord(int offset)
        {
            if (!known[offset] || !known[offset + 1])
            {
                throw new InvalidDataException("Native wave packet alias has no current projected WORD producer.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(packets.AsSpan(offset));
        }

        uint ReadPacketDword(int offset)
        {
            if (Enumerable.Range(offset, 4).Any(index => !known[index]))
            {
                throw new InvalidDataException("Native wave packet alias has no current projected DWORD producer.");
            }
            return BinaryPrimitives.ReadUInt32LittleEndian(packets.AsSpan(offset));
        }

        int Scratch(int offset) => unchecked((int)Dword(unchecked(scratch + (uint)offset)));
        short SignedScratch(int offset) => SignedWord(unchecked(scratch + (uint)offset));
        void StoreScratch(int offset, int value) => StoreDword(unchecked(scratch + (uint)offset), unchecked((uint)value));
        void StoreDword(uint address, uint value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(value)) };
        void StoreWord(uint address, ushort value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        short SignedWord(uint address) => unchecked((short)Word(address));
        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        short SignedSlot(int offset) => unchecked((short)SlotWord(frame, offset));
        uint ManagerDword(uint offset) => CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + offset));
        int TemporaryInt(int offset) => unchecked((int)frame.ReadTemporaryDword(offset));
        void Temporary(int offset, int value) => frame = frame.WriteTemporaryDword(offset, unchecked((uint)value));
    }
}
