using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect289CustomMesh108Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context108, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect289CustomMesh108
{
    public static NativeBattleEffect289CustomMesh108Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, Func<int, (NativeBattleVector Vertex, short? W)> readSelectedVertex, int tableBaseBucket = 17, byte depthShift = 2)
    {
        context.Validate();
        if (Dword(context, 0) != mesh.Address || depthShift > 31 || context.ExceedsExtent(108) || mesh.Bytes.IsDefault || BinaryPrimitives.ReadInt32LittleEndian(Read(4, 4)) != 164)
        {
            throw new InvalidDataException("Effect289 custom mesh requires its actual bounded source and 108-byte context.");
        }
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(Read(0, 4));
        int validation = cursor;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(validation, 4));
            if (count < 0 || count != 0 && group is not (3 or 7))
            {
                throw new NotSupportedException("Effect289 custom mesh requires its actual flat and Gouraud quad groups.");
            }
            validation = checked(validation + 4 + count * strides[group]);
            _ = Read(validation, 0);
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect289 custom mesh requires current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect289 custom mesh requires current color registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect289 custom mesh requires current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Effect289 custom mesh requires its current packet process.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Effect289 custom mesh requires current far-color controls.");
        byte[] rgb = context.Read(8, 3);
        far = far with { FarRed = (uint)rgb[0] << 4, FarGreen = (uint)rgb[1] << 4, FarBlue = (uint)rgb[2] << 4 };
        uint farColor = (uint)(rgb[0] | rgb[1] << 8 | rgb[2] << 16);
        var attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4));
            cursor += 4;
            context = Put(context, 0x34, checked(mesh.Address + (uint)cursor));
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                bool gouraud = group == 7;
                byte[] face = Read(cursor, strides[group]).ToArray();
                var first = Vertex(0);
                NativeBattleEffectArena firstRow = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(first.Vertex.X)).Write(2, BitConverter.GetBytes(first.Vertex.Y)).Write(4, BitConverter.GetBytes(first.Vertex.Z));
                firstRow = first.W is short w ? firstRow.Write(6, BitConverter.GetBytes(w)) : firstRow.MarkUnknown(6, 2);
                context = NativeBattleEffectNodeContextMemory.Copy(firstRow, 0, context, 0x48, 8);
                short fade = Signed(context, 0x4E);
                if (fade >= 4096)
                {
                    cursor += strides[group];
                    context = Put(context, 0x34, checked(mesh.Address + (uint)cursor));
                    continue;
                }
                registers = NativeBattleGeometry.LoadVector(registers, 0, first.Vertex, first.W);
                for (int corner = 1; corner < 3; corner++)
                {
                    var vertex = Vertex(corner);
                    registers = NativeBattleGeometry.LoadVector(registers, corner, vertex.Vertex, vertex.W);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint color = BinaryPrimitives.ReadUInt32LittleEndian(face);
                uint headerFlags = gouraud ? FlagByte() : 0;
                if (gouraud && (headerFlags & 2) != 0)
                {
                    color |= 0x02000000;
                }
                if (gouraud && (headerFlags & 8) != 0)
                {
                    color &= 0xFDFFFFFF;
                }
                int length = gouraud ? 52 : 40;
                SkinWrites packet = new(length, allocated);
                packet.Word(0, gouraud ? 0x0C000000u : 0x09000000u);
                packet.Word(4, color);
                int[] uvOffsets = gouraud ? [12, 24, 36, 48] : [12, 20, 28, 36];
                uint lastUv = BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(20));
                uint[] uv = [BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(12)), BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(16)), lastUv, lastUv >> 16];
                for (int corner = 0; corner < 4; corner++)
                {
                    packet.Word(uvOffsets[corner], uv[corner]);
                }
                uint flags = registers.Colors.Flags!.Value;
                context = Put(context, 0x44, flags);
                bool accepted = false;
                if ((flags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    int area = unchecked((int)registers.Geometry.Mac0!.Value);
                    context = Put(context, 0x38, unchecked((uint)area));
                    if (area >= 0 || (FlagByte() & (gouraud ? 0x20u : 0x10u)) != 0)
                    {
                        uint[] screens = registers.Geometry.Sxy.Select(value => value!.Value).ToArray();
                        for (int corner = 0; corner < 3; corner++)
                        {
                            packet.Word(8 + corner * (gouraud ? 12 : 8), screens[corner]);
                        }
                        var fourth = Vertex(3);
                        registers = NativeBattleGeometry.LoadVector(registers, 0, fourth.Vertex, fourth.W);
                        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                        uint fourthScreen = registers.Geometry.Sxy[2]!.Value;
                        packet.Word(gouraud ? 44 : 32, fourthScreen);
                        registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect289 custom mesh requires current ZSF4."));
                        uint[] allScreens = [screens[0], screens[1], screens[2], fourthScreen];
                        if (!allScreens.All(screen => unchecked((short)screen) is < 0 or > 2560) && !allScreens.All(screen => unchecked((short)(screen >> 16)) is < 0 or > 1728))
                        {
                            uint depth = registers.Geometry.Otz!.Value;
                            context = Put(context, 0x40, depth);
                            if (gouraud)
                            {
                                ImmutableArray<uint> shades = [color, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(24)), BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(28)), BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(32))];
                                if (fade != 0)
                                {
                                    shades = NativeBattleGteColorInterpolation.Gouraud45F4C0(shades, true, fade, farColor, constants);
                                    registers = NativeBattleGouraudMeshRegisters.Shade(registers, face, true, true, headerFlags, unchecked((uint)(int)fade), far, constants);
                                }
                                for (int corner = 0; corner < 4; corner++)
                                {
                                    packet.Word(4 + corner * 12, shades[corner]);
                                }
                            }
                            else if (fade != 0)
                            {
                                NativeBattleScriptedDpcsResult shaded = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, color, unchecked((uint)(int)fade), far, constants);
                                registers = registers with { Colors = shaded.Registers };
                                packet.Word(4, shaded.PackedRgb);
                            }
                            ushort u = Word(context, 0x20);
                            int bucket = unchecked(tableBaseBucket + (unchecked((int)depth) >> depthShift));
                            if (u != 0)
                            {
                                int[] us = uv.Select(value => (byte)value + (int)u).ToArray();
                                bool wrap = us.Aggregate(0, (value, coordinate) => value | coordinate) > 255;
                                byte width = context.Read(0x30, 1)[0];
                                for (int corner = 0; corner < 4; corner++)
                                {
                                    packet.Byte(uvOffsets[corner], unchecked((byte)(us[corner] - (wrap ? width : 0))));
                                }
                            }
                            ImmutableArray<uint> words = packet.Words();
                            uint opcode = words[1] >> 24 & 0xFC;
                            ImmutableArray<int> payload = opcode is >= 0x20 and <= 0x3C ? (opcode & 8) != 0 ? registers.Geometry.Sz.Select(value => unchecked((int)value!.Value)).ToImmutableArray() : [unchecked((int)registers.Geometry.Sz[1]!.Value), unchecked((int)registers.Geometry.Sz[2]!.Value), unchecked((int)registers.Geometry.Sz[3]!.Value), 0] : [0, 0, 0, 0];
                            NativeBattleGeometryPrimitive primitive = new(bucket, words, payload);
                            if (u == 0)
                            {
                                attempts.Add(packet.Attempt(primitive));
                                allocated += length;
                            }
                            else
                            {
                                NativeBattleGfGeometryBatch bracket = NativeBattleEffectTextureWindows.Bracket(packet.Attempt(primitive), primitive, Window(0x24), Window(0x2C));
                                attempts.AddRange(bracket.Attempts);
                                allocated += bracket.AllocationBytes;
                            }
                            accepted = true;
                        }
                    }
                }
                if (!accepted)
                {
                    attempts.Add(packet.Attempt(null));
                }
                cursor += strides[group];
                context = Put(context, 0x34, checked(mesh.Address + (uint)cursor));

                (NativeBattleVector Vertex, short? W) Vertex(int corner)
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
                    if ((offset & 7) != 0 || offset >= 164 * 8)
                    {
                        throw new InvalidDataException("Effect289 custom mesh index leaves its current selected rows.");
                    }
                    _ = Dword(context, 4);
                    return readSelectedVertex(offset);
                }
            }
        }
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } };
        return new(memory, context, new(attempts.ToImmutable(), allocated));

        uint FlagByte() => context.Read(0x1C, 1)[0];
        NativeBattleTextureWindow Window(int offset) => new(Signed(context, offset), Signed(context, offset + 2), Signed(context, offset + 4), Signed(context, offset + 6));
        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (mesh.Bytes.IsDefault || offset < 0 || length < 0 || offset > mesh.Bytes.Length - length)
            {
                throw new InvalidDataException("Effect289 custom mesh read leaves its addressed source.");
            }
            return mesh.Bytes.AsSpan().Slice(offset, length);
        }
    }

    private static uint Dword(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
    private static ushort Word(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(value.Read(offset, 2));
    private static short Signed(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena value, int offset, uint data) => value.Write(offset, BitConverter.GetBytes(data));
}
