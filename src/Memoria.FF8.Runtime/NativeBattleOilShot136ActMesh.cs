using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleOilShot136ActMesh
{
    public static NativeBattleDefaultActMeshStep Draw(NativeBattleEffectMemory memory, NativeBattleGfAnimationSource source, int geometryOffset, NativeBattleEffectArena context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<int, NativeBattleVector> readVertex, Func<int, short?> readW, int tableBaseBucket = 17, byte depthShift = 2)
    {
        context.Validate();
        if (depthShift > 31 || Dword(context, 0) != checked(source.Address + (uint)geometryOffset))
        {
            throw new InvalidDataException("Oil Shot ACT mesh requires its actual geometry and ordering shift.");
        }
        NativeBattleGeometryRegisterStep? latest = null;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int cursor = checked(geometryOffset + Integer(geometryOffset) + 12);
        context = Put(context, 0x20, checked(source.Address + (uint)cursor));
        foreach (int group in new[] { 3, 5 })
        {
            if (group == 5)
            {
                cursor = checked(cursor + 4);
            }
            int count = Integer(cursor);
            cursor = checked(cursor + 4);
            context = Put(context, 0x20, checked(source.Address + (uint)cursor));
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                NativeBattleGeometryRegisterStep registers = latest ?? new(memory.GeometryRegisters ?? throw new NotSupportedException("Oil Shot requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Oil Shot requires its current color registers."));
                NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Oil Shot requires its current projection controls.");
                byte[] face = Read(cursor, 24).ToArray();
                bool textured = group == 3;
                int length = textured ? 40 : 36;
                byte[] packet = new byte[length];
                NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
                for (int corner = 0; corner < 3; corner++)
                {
                    Load(corner, corner);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint flags = registers.Colors.Flags ?? throw new InvalidDataException("Oil Shot ACT projection lost FLAG.");
                Store(0, textured ? 0x09000000u : 0x08000000u);
                Store(4, BinaryPrimitives.ReadUInt32LittleEndian(face));
                if (textured)
                {
                    Store(12, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(12)));
                    Store(20, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(16)));
                    uint tail = BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(20));
                    Store(28, tail);
                    Store(36, tail >> 16);
                }
                context = Put(context, 0x30, flags);
                bool admitted = (flags & 0x60000) == 0;
                uint[] screens = new uint[4];
                if (admitted)
                {
                    for (int corner = 0; corner < 3; corner++)
                    {
                        screens[corner] = registers.Geometry.Sxy[corner] ?? throw new InvalidDataException("Oil Shot ACT projection lost SXY.");
                        Store(8 + corner * 8, screens[corner]);
                    }
                    Load(0, 3);
                    registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                    screens[3] = registers.Geometry.Sxy[2] ?? throw new InvalidDataException("Oil Shot ACT projection lost fourth SXY.");
                    Store(32, screens[3]);
                    registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Oil Shot ACT requires its current ZSF4."));
                    admitted = !screens.All(screen => Outside(unchecked((short)screen), 0xA00)) && !screens.All(screen => Outside(unchecked((short)(screen >> 16)), 0x6C0));
                }
                int depth = 0;
                if (admitted)
                {
                    depth = unchecked((int)(registers.Geometry.Otz ?? throw new InvalidDataException("Oil Shot ACT projection lost OTZ.")));
                    context = Put(context, 0x2C, unchecked((uint)depth));
                    uint cue = Dword(context, 12);
                    if (textured && cue != 0)
                    {
                        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Oil Shot ACT color interpolation requires its primitive process.");
                        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Oil Shot ACT color interpolation requires its far controls.");
                        byte[] rgb = context.Read(8, 3);
                        far = far with { FarRed = (uint)rgb[0] << 4, FarGreen = (uint)rgb[1] << 4, FarBlue = (uint)rgb[2] << 4 };
                        NativeBattleScriptedDpcsResult shaded = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4)), cue, far, colors);
                        registers = registers with { Colors = shaded.Registers };
                        memory = memory with { PrimitiveProcess = process with { ModelRendererRegisters = far } };
                        Store(4, shaded.PackedRgb);
                    }
                    else if (!textured)
                    {
                        Store(12, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(12)));
                        Store(20, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(16)));
                        Store(28, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(20)));
                        depth = unchecked(depth + (int)cue);
                        context = Put(context, 0x2C, unchecked((uint)depth));
                        admitted = depth >= 0;
                    }
                }
                NativeBattleGfGeometryAttempt attempt = new(allocated, packet.ToImmutableArray(), mask.ToImmutableArray(), null);
                attempts.Add(attempt);
                if (admitted)
                {
                    int bucket = unchecked(tableBaseBucket + (depth >> depthShift));
                    attempts.Add(Environment(allocated + length, 0xE1000220, bucket));
                    ImmutableArray<uint>.Builder words = ImmutableArray.CreateBuilder<uint>(length / 4);
                    for (int offset = 0; offset < length; offset += 4)
                    {
                        words.Add(BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(offset)));
                    }
                    attempts.Add(attempt with { WriteMask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, length).ToImmutableArray(), Primitive = new(bucket, words.ToImmutable(), []) });
                    if (!textured)
                    {
                        attempts.Add(Environment(allocated + length + 8, 0xE1000240, bucket));
                    }
                    allocated = checked(allocated + length + (textured ? 8 : 16));
                }
                latest = registers;
                cursor = checked(cursor + 24);

                void Load(int register, int corner)
                {
                    ushort index = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2));
                    if ((index & 1) != 0)
                    {
                        throw new InvalidDataException("Oil Shot ACT requires packed even vertex indices.");
                    }
                    _ = Dword(context, 4);
                    registers = NativeBattleGeometry.LoadVector(registers, register, readVertex(index * 4), readW(index * 4));
                }

                void Store(int offset, uint value)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(offset), value);
                    Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
                }
            }
            context = Put(context, 0x20, checked(source.Address + (uint)cursor));
        }
        if (latest is not null)
        {
            memory = memory with { GeometryRegisters = latest.Geometry, ScriptedColorRegisters = latest.Colors };
        }
        return new(memory, context, new(attempts.ToImmutable(), allocated));

        int Integer(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(offset, 4));
        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (source.Data.IsDefault || offset < 0 || length < 0 || offset > source.Data.Length - length)
            {
                throw new InvalidDataException("Oil Shot ACT mesh read leaves its source.");
            }
            return source.Data.AsSpan().Slice(offset, length);
        }
    }

    private static bool Outside(short value, int upper) => value < 0 || value > upper;
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena context, int offset, uint value) => context.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleGfGeometryAttempt Environment(int offset, uint command, int bucket)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x01000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), command);
        return new(offset, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), new(bucket, [0x01000000, command], []));
    }
}
