using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect173Controller;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect173Mesh172Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context172, NativeBattleEffectArena Records, NativeBattleGfGeometryBatch Batch);

public static partial class NativeBattleEffect173Lower
{
    public static NativeBattleEffect173Frame InitializeCentroids7980(NativeBattleEffect173Frame frame, uint temporary172, NativeBattleEffect173Resources resources)
    {
        NativeBattleEffectArena context = Context(frame, temporary172, 172);
        uint source = L173U(context, 0);
        int index = checked((int)L173U(context, 0x24));
        if ((uint)index >= (uint)resources.MeshSources.Length || resources.MeshSources[index] != source)
        {
            throw new InvalidDataException("Centroid initialization requires its selected mesh and current record bank.");
        }
        NativeBattleStaticMeshResource mesh = resources.ReadMesh(source);
        int polygon = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(polygon + 24, 4));
        uint recordsAddress = resources.FaceRecordAddresses[index];
        NativeBattleEffectArena records = frame.State.ReadView(recordsAddress, checked(count * 32));
        context = L173Put(L173Put(L173Put(context, 4, source + 8), 0x28, recordsAddress), 0x2C, checked(source + (uint)polygon + 28));
        int distance = unchecked((int)frame.State.ReadDword(0x2397278));
        for (int face = 0; face < count; face++)
        {
            int cursor = polygon + 28 + face * 28;
            int[] sum = new int[3];
            for (int corner = 0; corner < 3; corner++)
            {
                int selected = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + 4 + corner * 2, 2));
                for (int axis = 0; axis < 3; axis++)
                {
                    sum[axis] += BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(8 + selected * 4 + axis * 2, 2));
                }
            }
            int random = L173Random(ref frame);
            short x = unchecked((short)(sum[0] / 3));
            short y = unchecked((short)(sum[1] / 3));
            short z = unchecked((short)(sum[2] / 3));
            short directionZ = unchecked((short)(z + distance));
            var normal = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, directionZ);
            context = L173Store(context, 0x58, new(unchecked((short)normal.X), unchecked((short)normal.Y), unchecked((short)normal.Z)));
            int length = NativeBattleRayBomb10Geometry.Sqrt56BEC0(NativeBattleSleepingGas168.NormalizeSquare(x, y, directionZ));
            int row = face * 32;
            records = L173Store(records, row, new(x, y, z));
            records = L173Word(records, row + 6, unchecked((length - distance) * 12) / 6300);
            for (int offset = 8; offset <= 14; offset += 2)
            {
                records = L173Word(records, row + offset, 0);
            }
            int speed = random % 170 + 90;
            records = L173Store(records, row + 0x10, new(unchecked((short)((normal.X * speed) >> 12)), unchecked((short)((normal.Y * speed) >> 12)), unchecked((short)((normal.Z * speed) >> 12))));
            records = L173Word(L173Word(L173Word(L173Word(records, row + 0x18, 0), row + 0x1A, (random & 7) << 4), row + 0x1C, 0), row + 0x1E, ((random >> 3) & 7) << 3);
        }
        context = L173Put(L173Put(context, 0x28, checked(recordsAddress + (uint)(count * 32))), 0x2C, checked(source + (uint)(polygon + 28 + count * 28)));
        frame = frame with { State = frame.State.WriteView(recordsAddress, records, count * 32) };
        return PublishContext(frame, temporary172, context, 172);
    }
}

public static class NativeBattleEffect173Mesh172
{
    public static NativeBattleEffect173Mesh172Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleEffectArena context172, NativeBattleEffectArena records, uint recordsAddress, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, bool moving, bool paused, int tableBaseBucket = 17, byte depthShift = 2)
    {
        int polygon = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(polygon + 24, 4));
        if (U(context172, 0) != mesh.Address || U(context172, 0x1C) != 0x30 || count < 0)
        {
            throw new InvalidDataException("The custom mesh requires its selected current context and GT3 records.");
        }
        context172 = Put(Put(Put(context172, 4, mesh.Address + 8), 0x28, recordsAddress), 0x2C, checked(mesh.Address + (uint)polygon + 28));
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Custom mesh drawing requires its current primitive owner.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Custom mesh drawing requires its current color controls.");
        byte[] rgb = context172.Read(8, 3);
        far = far with { FarRed = (uint)rgb[0] << 4, FarGreen = (uint)rgb[1] << 4, FarBlue = (uint)rgb[2] << 4 };
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Custom mesh drawing requires current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Custom mesh drawing requires current arithmetic registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Custom mesh drawing requires current projection controls.");
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int phase = unchecked((int)U(context172, 0x20));
        for (int face = 0; face < count; face++)
        {
            int record = face * 32;
            int cursor = polygon + 28 + face * 28;
            if (moving ? S(records, record + 14) < 0 : S(records, record + 6) >= phase && phase < 12)
            {
                AdvancePointers(face);
                continue;
            }
            for (int corner = 0; corner < 3; corner++)
            {
                int selected = BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + 4 + corner * 2, 2));
                context172 = context172.Write(0x40 + corner * 8, mesh.Bytes.AsSpan().Slice(8 + selected * 4, 8));
            }
            if (moving && S(records, record + 6) < phase)
            {
                context172 = NativeBattleEffectNodeContextMemory.Copy(records, record, context172, 0x64, 8);
                for (int corner = 0; corner < 3; corner++)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int offset = 0x40 + corner * 8 + axis * 2;
                        context172 = Word(context172, offset, S(context172, offset) - S(records, record + axis * 2));
                    }
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    context172 = Put(context172, 0xA0 + axis * 4, unchecked((uint)(S(records, record + axis * 2) + S(records, record + 8 + axis * 2))));
                }
                int age = S(records, record + 14);
                context172 = Put(context172, 12, unchecked((uint)(age == 0 ? -4096 : age << 8)));
                if (!paused)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        int velocity = S(records, record + 0x10 + axis * 2);
                        records = Word(records, record + 8 + axis * 2, S(records, record + 8 + axis * 2) + velocity);
                        records = Word(records, record + 0x10 + axis * 2, velocity - (velocity >> 4));
                    }
                    records = Word(Word(records, record + 0x18, S(records, record + 0x18) + S(records, record + 0x1A)), record + 0x1C, S(records, record + 0x1C) + S(records, record + 0x1E));
                    short next = unchecked((short)(age + 1));
                    records = Word(records, record + 14, next >= 16 ? -1 : next);
                }
                context172 = Word(Word(Word(context172, 0x64, S(records, record + 0x18)), 0x66, 0), 0x68, S(records, record + 0x1C));
                NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CD50(new(S(context172, 0x64), 0, S(context172, 0x68))) with { X = unchecked((int)U(context172, 0xA0)), Y = unchecked((int)U(context172, 0xA4)), Z = unchecked((int)U(context172, 0xA8)) };
                NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(Packed(context172, 0x6C).ReadMatrix(), local);
                NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(composed, null);
                context172 = NativeBattleEffectNodeContextMemory.Copy(PackedArena(packed), 0, context172, 0x8C, 32);
                registers = LoadMatrix(registers, packed);
            }
            else
            {
                context172 = Put(context172, 12, 0);
                registers = LoadMatrix(registers, Packed(context172, 0x6C));
            }
            for (int corner = 0; corner < 3; corner++)
            {
                (byte[] bytes, bool[] known) = context172.ReadSnapshot(0x40 + corner * 8, 8);
                ImmutableArray<byte?> input = bytes.Select((value, index) => known[index] ? (byte?)value : null).ToImmutableArray();
                registers = corner switch
                {
                    0 => registers with { Colors = registers.Colors with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } },
                    1 => registers with { Geometry = registers.Geometry with { V1 = input } },
                    _ => registers with { Geometry = registers.Geometry with { V2 = input } }
                };
            }
            registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
            SkinWrites packet = new(40, allocated);
            packet.Word(0, 0x09000000);
            packet.Word(4, Source(0));
            packet.Word(12, Source(12));
            packet.Word(24, Source(16));
            packet.Word(36, Source(8) >> 16);
            uint flags = registers.Colors.Flags ?? throw new NotSupportedException("Custom mesh drawing requires current projection flags.");
            context172 = Put(context172, 0x3C, flags);
            bool accepted = false;
            if ((flags & 0x60000) == 0)
            {
                registers = NativeBattleGeometry.Clip45EE10(registers);
                uint area = registers.Geometry.Mac0 ?? throw new NotSupportedException("Custom mesh drawing requires current NCLIP.");
                context172 = Put(context172, 0x30, area);
                if (area != 0)
                {
                    for (int corner = 0; corner < 3; corner++)
                    {
                        packet.Word(8 + corner * 12, registers.Geometry.Sxy[corner] ?? throw new NotSupportedException("Custom mesh drawing requires current screen coordinates."));
                    }
                    registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Custom mesh drawing requires current ZSF3."));
                    uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Custom mesh drawing requires current depth.");
                    context172 = Put(context172, 0x38, depth);
                    NativeBattleScriptedColorRegisters tinted = NativeBattleScriptedDpct.LoadColors45E120(registers.Colors, Source(20), Source(24), Source(0)) with { Ir0 = U(context172, 12) };
                    tinted = NativeBattleScriptedDpct.Transform45F4C0(tinted, far, colors);
                    registers = registers with { Colors = tinted };
                    packet.Word(16, tinted.Rgb[0] ?? throw new NotSupportedException("Custom mesh drawing requires current RGB0."));
                    packet.Word(28, tinted.Rgb[1] ?? throw new NotSupportedException("Custom mesh drawing requires current RGB1."));
                    packet.Word(4, tinted.Rgb[2] ?? throw new NotSupportedException("Custom mesh drawing requires current RGB2."));
                    ImmutableArray<int> payload = registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Custom mesh drawing requires all current depths.")))).ToImmutableArray();
                    attempts.Add(packet.Attempt(new(unchecked(tableBaseBucket + ((int)depth >> depthShift)), packet.Words(), payload)));
                    allocated += 40;
                    accepted = true;
                }
            }
            if (!accepted)
            {
                attempts.Add(packet.Attempt(null));
            }
            AdvancePointers(face);

            uint Source(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor + offset, 4));
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } }, context172, records, new(attempts.ToImmutable(), allocated));

        void AdvancePointers(int face)
        {
            context172 = Put(Put(context172, 0x28, checked(recordsAddress + (uint)((face + 1) * 32))), 0x2C, checked(mesh.Address + (uint)(polygon + 28 + (face + 1) * 28)));
        }
    }

    private static NativeBattleGeometryRegisterStep LoadMatrix(NativeBattleGeometryRegisterStep registers, NativeBattlePackedMatrix32 packed)
    {
        NativeBattleActionMatrix matrix = packed.ReadMatrix();
        return registers with { Geometry = registers.Geometry with { Rotation20 = packed.Bytes.Take(20).Select((value, index) => packed.Known[index] ? (byte?)value : null).ToImmutableArray(), Translation = [matrix.X, matrix.Y, matrix.Z] } };
    }
    private static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static short S(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
