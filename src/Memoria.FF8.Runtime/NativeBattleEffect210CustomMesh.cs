using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect210CustomMeshStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context152, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect210CustomMesh
{
    public static NativeBattleEffect210CustomMeshStep DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors colors, int tableBaseBucket = 17, byte depthShift = 2, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ReadOnlySpan<byte> source = mesh.Bytes.AsSpan();
        int polygon = BinaryPrimitives.ReadInt32LittleEndian(source);
        int vertices = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(4));
        if (polygon != 8 + vertices * 8 || vertices < 1 || depthShift > 31)
        {
            throw new InvalidDataException("Effect210 custom mesh requires its bounded current raw rows.");
        }
        for (int group = 0; group < 7; group++)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(source.Slice(polygon + group * 4)) != 0)
            {
                throw new NotSupportedException("Effect210 custom mesh requires its selected GT4 group.");
            }
        }
        int cursor = polygon + 28;
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(cursor));
        cursor += 4;
        if (count < 0 || (long)cursor + count * 36L > source.Length)
        {
            throw new InvalidDataException("Effect210 custom faces leave their current resource.");
        }
        context = Put(context, 0x24, checked(mesh.Address + (uint)cursor));
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect210 custom mesh requires current geometry."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect210 custom mesh requires current arithmetic."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect210 custom mesh requires current projection controls.");
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int[] rowOffsets = [0x3C, 0x44, 0x4C, 0x54];
        int[] colorOffsets = [0x5C, 0x60, 0x64, 0x68];
        for (int faceIndex = 0; faceIndex < count; faceIndex++, cursor += 36)
        {
            ReadOnlySpan<byte> face = source.Slice(cursor, 36);
            for (int corner = 0; corner < 4; corner++)
            {
                int index = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(4 + corner * 2));
                int row = checked(index * 4);
                if ((row & 7) != 0 || row > vertices * 8 - 8)
                {
                    throw new InvalidDataException("Effect210 custom vertex index leaves its current rows.");
                }
                context = context.Write(rowOffsets[corner], source.Slice(8 + row, 8));
                int factor = unchecked((short)Word(context, rowOffsets[corner] + 4) + (int)Dword(context, 0x38));
                uint color = corner == 0 ? BinaryPrimitives.ReadUInt32LittleEndian(face) : BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(0x14 + corner * 4));
                context = Put(context, colorOffsets[corner], factor <= 0 ? corner == 0 ? 0x3E000000u : 0u : color);
                if (factor > 0 && unchecked(factor << 4) < 4096)
                {
                    byte[] rgb = context.Read(colorOffsets[corner], 3);
                    registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(factor << 4)) } }, rgb[0], rgb[1], rgb[2]);
                    registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, colors);
                    context = context.Write(colorOffsets[corner], registers.Colors.Ir.Select(value => unchecked((byte)(value ?? throw new NotSupportedException("Effect210 color requires current IR.")))).ToArray());
                }
            }
            context = context.Write(0x6C, face.Slice(12, 8)).Write(0x74, face.Slice(20, 4));
            int scroll = Word(context, 0x12);
            int[] uvOffsets = [0x6C, 0x70, 0x74, 0x76];
            int[] translatedV = uvOffsets.Select(offset => context.Read(offset + 1, 1)[0] + scroll).ToArray();
            bool wraps = translatedV.Aggregate(0, (value, component) => value | component) > 255;
            for (int corner = 0; corner < 4; corner++)
            {
                int value = wraps ? translatedV[corner] - context.Read(0x20, 1)[0] : translatedV[corner];
                context = context.Write(uvOffsets[corner] + 1, [unchecked((byte)value)]);
            }
            System.Numerics.Vector3[]? world = pilotIdentity is null ? null : new System.Numerics.Vector3[4];
            if (world is not null)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    int row = rowOffsets[corner];
                    world[corner] = NativeBattleEffect210VisualCapture.World(registers.Geometry, new(unchecked((short)Word(context, row)), unchecked((short)Word(context, row + 2)), unchecked((short)Word(context, row + 4))), projection);
                }
            }
            registers = Load(registers, 0, context, 0x3C);
            registers = Load(registers, 1, context, 0x44);
            registers = Load(registers, 2, context, 0x4C);
            registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
            SkinWrites packet = new(52, allocated);
            packet.Word(0, 0x0C000000);
            packet.Word(12, Dword(context, 0x6C));
            packet.Word(24, Dword(context, 0x70));
            packet.Word(36, Dword(context, 0x74));
            packet.Word(48, Dword(context, 0x74) >> 16);
            uint flags = registers.Colors.Flags ?? throw new NotSupportedException("Effect210 custom projection requires current flags.");
            context = Put(context, 0x34, flags);
            if ((flags & 0x60000) != 0)
            {
                attempts.Add(packet.Attempt(null));
                continue;
            }
            uint[] screen = registers.Geometry.Sxy.Select(value => value ?? throw new NotSupportedException("Effect210 custom mesh requires current screen FIFO.")).ToArray();
            for (int corner = 0; corner < 3; corner++)
            {
                packet.Word(8 + corner * 12, screen[corner]);
            }
            registers = Load(registers, 0, context, 0x54);
            if (world is not null)
            {
                world[3] = NativeBattleEffect210VisualCapture.World(registers.Geometry, new(unchecked((short)Word(context, 0x54)), unchecked((short)Word(context, 0x56)), unchecked((short)Word(context, 0x58))), projection);
            }
            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
            uint fourth = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Effect210 custom mesh requires its fourth screen.");
            packet.Word(44, fourth);
            registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect210 custom mesh requires current ZSF4."));
            uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Effect210 custom mesh requires current OTZ.");
            context = Put(context, 0x30, depth);
            uint[] allScreen = [screen[0], screen[1], screen[2], fourth];
            bool outsideX = allScreen.All(value => unchecked((short)value) < 0 || unchecked((short)value) > 2560);
            bool outsideY = allScreen.All(value => unchecked((short)(value >> 16)) < 0 || unchecked((short)(value >> 16)) > 1728);
            if (outsideX || outsideY)
            {
                attempts.Add(packet.Attempt(null));
                continue;
            }
            for (int corner = 0; corner < 4; corner++)
            {
                packet.Word(4 + corner * 12, Dword(context, colorOffsets[corner]));
            }
            int bucket = unchecked(tableBaseBucket + ((int)depth >> depthShift));
            ImmutableArray<int> payload = registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Effect210 custom mesh requires all current depths.")))).ToImmutableArray();
            SkinWrites reset = Window(context, 0x14, allocated + 52);
            SkinWrites window = Window(context, 0x1C, allocated + 64);
            attempts.Add(reset.Attempt(new(bucket, reset.Words(), [0, 0, 0, 0])));
            NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), payload);
            if (pilotIdentity is { } identity && world is not null)
            {
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + faceIndex) }, world[0], world[1], world[2], world[3], 4) { SourceDepths = new(payload[0], payload[1], payload[2], payload[3]), UseSourceDepths = true } };
            }
            attempts.Add(packet.Attempt(primitive));
            attempts.Add(window.Attempt(new(bucket, window.Words(), [0, 0, 0, 0])));
            allocated += 76;
        }
        context = Put(context, 0x24, checked(mesh.Address + (uint)cursor));
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, context, new(attempts.ToImmutable(), allocated));
    }

    private static SkinWrites Window(NativeBattleEffectArena context, int offset, int allocated)
    {
        int x = Word(context, offset);
        int y = Word(context, offset + 2);
        int width = Word(context, offset + 4);
        int height = Word(context, offset + 6);
        uint word = unchecked(0xE2000000u | ((uint)(~(width - 1) >> 3) & 31) | ((uint)(~(height - 1) >> 3) & 31) << 5 | ((uint)(x >> 3) & 31) << 10 | ((uint)(y >> 3) & 31) << 15);
        SkinWrites packet = new(12, allocated);
        packet.Word(0, 0x02000000);
        packet.Word(4, word);
        packet.Word(8, 0);
        return packet;
    }

    private static NativeBattleGeometryRegisterStep Load(NativeBattleGeometryRegisterStep registers, int slot, NativeBattleEffectArena context, int offset) => NativeBattleGeometry.LoadVector(registers, slot, new(unchecked((short)Word(context, offset)), unchecked((short)Word(context, offset + 2)), unchecked((short)Word(context, offset + 4))), unchecked((short)Word(context, offset + 6)));
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
}
