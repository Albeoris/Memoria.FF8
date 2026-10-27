using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTornado146FunnelFrame(NativeBattleTornado146State State, NativeBattleModulePoolTask Task, NativeCrtRandomState Random, NativeBattleLogicalOrdering Ordering, NativeBattleFileStaging Staging, int Result);

public static class NativeBattleTornado146Funnel
{
    private const uint Rings = 0x2491CE8;

    public static NativeBattleTornado146State Initialize6152E0(NativeBattleTornado146State state)
    {
        using NativeBattleTornado146Work work = new(state);
        for (int texture = 0; texture < 2; texture++)
        {
            uint basis = texture == 0 ? 0x2495230u : 0x2497A30u;
            for (int band = 0; band < 8; band++)
            {
                byte top = unchecked((byte)(255 - band * 31));
                byte bottom = unchecked((byte)(255 - (band == 7 ? 255 : band * 31 + 31)));
                for (int segment = 0; segment < 16; segment++)
                {
                    int left = ((segment >> 1) & 1) == 0 ? 0 : 63;
                    int right = left ^ 63;
                    left += (segment & 1) * 64;
                    right += (segment & 1) * 64;
                    for (int buffer = 0; buffer < 2; buffer++)
                    {
                        uint packet = basis + (uint)((band * 32 + segment * 2 + buffer) * 40);
                        work.WriteDword(packet, 0x09000000);
                        work.WriteDword(packet + 4, 0x2E808080);
                        for (int corner = 0; corner < 4; corner++)
                        {
                            work.WriteByte(packet + 12u + (uint)corner * 8, (byte)((corner & 1) == 0 ? left : right));
                            work.WriteByte(packet + 13u + (uint)corner * 8, corner < 2 ? bottom : top);
                        }
                        work.WriteWord(packet + 14, texture == 0 ? (ushort)0x3F94 : (ushort)0x3FD4);
                        work.WriteWord(packet + 22, texture == 0 ? (ushort)0xB7 : (ushort)0xB6);
                    }
                }
            }
        }
        for (int ring = 0; ring < 17; ring++)
        {
            uint row = Rings + (uint)ring * 20;
            work.WriteWord(row, 0);
            work.WriteWord(row + 2, (ushort)(ring * 5));
            work.WriteWord(row + 4, 0);
            work.WriteDword(row + 16, 1500);
        }
        foreach (uint address in new uint[] { 0x2493708, 0x249370A, 0x249370C, 0x2491CE0, 0x2491CE2, 0x2491CE4 })
        {
            work.WriteWord(address, 0);
        }
        return work.Freeze();
    }

    public static NativeBattleTornado146FunnelFrame Advance6171F0(NativeBattleTornado146State state, NativeBattleModulePoolTask task, NativeCrtRandomState random, NativeBattleCameraProjection projection, NativeBattleLogicalOrdering ordering, uint updateFlags, NativeBattleFileStaging staging)
    {
        int age = unchecked((short)task.ReadWord(12));
        if (age >= 76)
        {
            throw new InvalidDataException("Native Tornado funnel cannot retain an active task after its executed release frame.");
        }
        if ((updateFlags & 0x201) == 0)
        {
            (state, random, staging) = Move(state, random, staging, age);
        }
        NativeBattleVector translation = new(unchecked((short)task.ReadWord(16)), unchecked((short)task.ReadWord(18)), unchecked((short)task.ReadWord(20)));
        state = state with { FunnelTranslation = translation };
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = translation.X, Y = translation.Y, Z = translation.Z };
        NativeBattleCameraProjection current = projection with { View = NativeBattleGfMatrices.Compose56C2F0(projection.View ?? throw new NotSupportedException("Native Tornado funnel requires its current camera matrix."), local) };
        (state, ordering, staging) = Draw(state, ordering, staging, current, age);
        if ((updateFlags & 0x201) != 0)
        {
            return new(state, task, random, ordering, staging, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(age + 1)));
        return new(state, task, random, ordering, staging, age + 1 >= 76 ? 2 : 0);
    }

    private static (NativeBattleTornado146State State, NativeCrtRandomState Random, NativeBattleFileStaging Staging) Move(NativeBattleTornado146State state, NativeCrtRandomState random, NativeBattleFileStaging staging, int age)
    {
        int height;
        int radius;
        int drift;
        int taper;
        int frequency;
        int phase;
        if (age < 40)
        {
            int fraction = (age << 12) / 40;
            height = 80 + (fraction * 4920 >> 12);
            radius = 1500 + (fraction * 1500 >> 12);
            drift = 100 + (fraction * 900 >> 12);
            taper = 2304 + (-fraction * 640 >> 12);
            frequency = 2048;
            phase = 3328 + (-fraction * 3328 >> 12);
        }
        else if (age < 58)
        {
            (height, radius, drift, taper, frequency, phase) = (5000, 3000, 1000, 1664, 2048, 0);
        }
        else
        {
            int fraction = ((age - 58) << 12) / 18;
            height = 5000 + (fraction * 4000 >> 12);
            radius = 3000 + (-fraction * 2600 >> 12);
            drift = 1000 + (-fraction * 200 >> 12);
            taper = 1664 + (-fraction * 1664 >> 12);
            frequency = 2048 + (-fraction * 768 >> 12);
            phase = fraction * 768 >> 12;
        }
        using NativeBattleTornado146Work work = new(state);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 40);
        Scratch scratch = new(allocation.Staging, allocation.Address, 40);
        for (int ring = 16; ring >= 0; ring--)
        {
            uint angles = Rings + (uint)ring * 20 + 8;
            for (int axis = 0; axis < 3; axis++)
            {
                uint address = angles + (uint)axis * 2;
                if (ring > 0)
                {
                    work.WriteWord(address, unchecked((ushort)(work.Word(address - 20) + (Next() & 127) - 64)));
                }
                else
                {
                    uint head = 0x2493708u + (uint)axis * 2;
                    uint walk = 0x2491CE0u + (uint)axis * 2;
                    work.WriteWord(address, work.Word(head));
                    work.WriteWord(walk, unchecked((ushort)(work.Word(walk) + (Next() & 511) - 256)));
                    work.WriteWord(head, unchecked((ushort)(work.Word(head) + (NativeBattleCameraMath.Sine(work.SignedWord(walk)) * 512 >> 12))));
                }
            }
            NativeBattleVector rotation = new((short)(NativeBattleCameraMath.Sine(work.SignedWord(angles)) * 288 >> 12), (short)(NativeBattleCameraMath.Sine(work.SignedWord(angles + 2)) * 288 >> 12), (short)(NativeBattleCameraMath.Sine(work.SignedWord(angles + 4)) * 288 >> 12));
            scratch.Vector(0, rotation);
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(rotation);
            for (int component = 0; component < 9; component++)
            {
                scratch.Word(8 + component * 2, unchecked((ushort)matrix.Rotation[component]));
            }
            int fraction = ring * 256;
            NativeBattleVector input = new(unchecked((short)(((1024 + 192 * ring) * drift) >> 12)), 0, 0);
            scratch.Vector(0, input);
            NativeBattleVector position = NativeBattleGfMatrices.RotateShort56C4F0(matrix, input);
            scratch.Vector(0, position);
            work.WriteWord(angles - 8, unchecked((ushort)position.X));
            work.WriteWord(angles - 6, unchecked((ushort)((position.Y >> 4) - (fraction * height >> 12))));
            work.WriteWord(angles - 4, unchecked((ushort)position.Z));
            int wave = NativeBattleCameraMath.Sine(NativeBattleCameraMath.Sine((fraction * frequency >> 12) + phase) >> 2);
            int width = (4096 - ((4096 - taper) * wave >> 12)) * radius >> 12;
            work.WriteDword(angles + 8, unchecked((uint)width));
        }
        staging = NativeBattleTemporaryMemory.Release5082D0(scratch.Publish(allocation.Staging), 40);
        return (work.Freeze(), random, staging);

        ushort Next()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    private static (NativeBattleTornado146State State, NativeBattleLogicalOrdering Ordering, NativeBattleFileStaging Staging) Draw(NativeBattleTornado146State state, NativeBattleLogicalOrdering ordering, NativeBattleFileStaging staging, NativeBattleCameraProjection projection, int age)
    {
        using NativeBattleTornado146Work work = new(state);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 288);
        Scratch scratch = new(allocation.Staging, allocation.Address, 288);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int brightness = age < 16 ? age * 8 : age >= 68 ? (76 - age) * 16 : 128;
        uint color = 0x2E000000u | (uint)unchecked((byte)brightness) * 0x10101;
        uint selection = work.Dword(0x24AA624);
        if (selection > 1)
        {
            throw new InvalidDataException("Native Tornado funnel lost its original two-buffer selector.");
        }
        Ring(0, 0, age * 100);
        int accepted = 0;
        for (int band = 0; band < 16; band++)
        {
            int previous = band & 1;
            int next = previous ^ 1;
            Ring(band + 1, next, age * 100 + (band + 1) * 64);
            for (int segment = 0; segment < 16; segment++)
            {
                uint address = 0x2495230u + (uint)((band * 32 + segment * 2 + selection) * 40);
                int offset = checked((int)(address - NativeBattleTornado146State.WorkAddress));
                work.WriteDword(address + 4, color);
                NativeBattleModuleProjectedVertex[] vertices = [Project(next, segment, false), Project(next, segment + 1, false), Project(previous, segment, false), default];
                uint flags = vertices[0].Flags | vertices[1].Flags | vertices[2].Flags;
                scratch.Dword(284, flags);
                bool visible = (flags & 0x60000) == 0;
                if (visible)
                {
                    vertices[3] = Project(previous, segment + 1, true);
                    for (int corner = 0; corner < 4; corner++)
                    {
                        work.WriteDword(address + 8u + (uint)corner * 8, vertices[corner].Screen);
                    }
                    int average = unchecked(vertices.Sum(vertex => (int)vertex.Depth) * 0x100) >> 12;
                    scratch.Dword(280, unchecked((uint)average));
                    uint[] words = new uint[10];
                    byte[] bytes = work.Read(address, 40);
                    for (int word = 0; word < words.Length; word++)
                    {
                        words[word] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(word * 4));
                    }
                    SkinWrites stores = Stores();
                    NativeBattleGeometryPrimitive primitive = new(17 + (average >> 2), words.ToImmutableArray(), vertices.Select(vertex => (int)vertex.Depth).ToImmutableArray());
                    attempts.Add(stores.Attempt(primitive));
                    accepted = Math.Max(accepted, offset + 40);
                }
                else
                {
                    attempts.Add(Stores().Attempt(null));
                }

                SkinWrites Stores()
                {
                    SkinWrites stores = new(40, offset);
                    stores.Word(4, color);
                    if (visible)
                    {
                        for (int corner = 0; corner < 4; corner++)
                        {
                            stores.Word(8 + corner * 8, vertices[corner].Screen);
                        }
                    }
                    return stores;
                }
            }
        }
        NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(work.Freeze().Storage, NativeBattleTornado146State.PacketBank, 0, NativeBattleTornado146State.WorkLength, ordering, new(attempts.ToImmutable(), accepted));
        staging = NativeBattleTemporaryMemory.Release5082D0(scratch.Publish(allocation.Staging), 288);
        return (state with { Storage = written.Arena }, written.Ordering, staging);

        NativeBattleModuleProjectedVertex Project(int buffer, int vertex, bool fourth)
        {
            NativeBattleVector point = scratch.Vector(buffer * 136 + 8 + vertex * 8);
            NativeBattleActionMatrix matrix = projection.View!;
            return fourth ? NativeBattleModuleProjection.Project45FAA0(matrix, point, projection, 0, 0) : NativeBattleModuleProjection.Project45FE10(matrix, point, projection);
        }

        void Ring(int ring, int buffer, int phase)
        {
            uint row = Rings + (uint)ring * 20;
            scratch.Copy(0, work.ReadSnapshot(row, 8));
            int radius = work.SignedDword(row + 16);
            for (int segment = 0; segment < 16; segment++)
            {
                int angle = phase + segment * 256;
                NativeBattleVector point = new(unchecked((short)(work.SignedWord(row) + (NativeBattleCameraMath.Sine(angle) * radius >> 12))), work.SignedWord(row + 2), unchecked((short)(work.SignedWord(row + 4) + (NativeBattleCameraMath.Cosine(angle) * radius >> 12))));
                scratch.Vector(buffer * 136 + 8 + segment * 8, point);
            }
            scratch.Copy(buffer * 136 + 136, scratch.Snapshot(buffer * 136 + 8, 8));
        }
    }

    private sealed class Scratch
    {
        private readonly uint address;
        private NativeBattleEffectArena arena;

        public Scratch(NativeBattleFileStaging staging, uint address, int length)
        {
            this.address = address;
            Length = length;
            arena = NativeBattleTemporaryMemory.ReadView(staging, address, length);
        }

        private int Length { get; }
        public void Word(int offset, ushort value) => arena = arena.Write(offset, BitConverter.GetBytes(value));
        public void Dword(int offset, uint value) => arena = arena.Write(offset, BitConverter.GetBytes(value));
        public void Vector(int offset, NativeBattleVector value)
        {
            Word(offset, unchecked((ushort)value.X));
            Word(offset + 2, unchecked((ushort)value.Y));
            Word(offset + 4, unchecked((ushort)value.Z));
        }
        public NativeBattleVector Vector(int offset)
        {
            byte[] bytes = arena.Read(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        }
        public (byte[] Data, bool[] Known) Snapshot(int offset, int length) => arena.ReadSnapshot(offset, length);
        public void Copy(int offset, (byte[] Data, bool[] Known) snapshot)
        {
            for (int first = 0; first < snapshot.Data.Length;)
            {
                int end = first + 1;
                while (end < snapshot.Data.Length && snapshot.Known[end] == snapshot.Known[first])
                {
                    end++;
                }
                arena = snapshot.Known[first] ? arena.Write(offset + first, snapshot.Data.AsSpan(first, end - first)) : arena.MarkUnknown(offset + first, end - first);
                first = end;
            }
        }
        public NativeBattleFileStaging Publish(NativeBattleFileStaging staging) => NativeBattleTemporaryMemory.PublishView(staging, address, arena, Length);
    }
}
