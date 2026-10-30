using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect312MeshStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect312Mesh
{
    private static readonly int[] Strides = [12, 12, 20, 24, 20, 24, 28, 36];

    public static NativeBattleEffect312Frame Draw572200(NativeBattleEffect312Frame frame, uint temporary, uint meshAddress, NativeBattlePackedMatrix32 matrix, NativeBattleEffect312Resources resources, NativeBattleEffect312Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return Draw(frame, temporary, meshAddress, matrix, resources, inputs, false, pilotIdentity);
    }

    public static NativeBattleEffect312Frame DrawLocal(NativeBattleEffect312Frame frame, uint temporary, uint meshAddress, NativeBattlePackedMatrix32 matrix, NativeBattleEffect312Resources resources, NativeBattleEffect312Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return Draw(frame, temporary, meshAddress, matrix, resources, inputs, true, pilotIdentity);
    }

    private static NativeBattleEffect312Frame Draw(NativeBattleEffect312Frame frame, uint temporary, uint meshAddress, NativeBattlePackedMatrix32 matrix, NativeBattleEffect312Resources resources, NativeBattleEffect312Inputs inputs, bool local, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        frame = NativeBattleEffect312Controller.LoadMatrix(frame, matrix);
        int length = local ? 108 : 88;
        NativeBattleEffectArena context = NativeBattleEffect312Controller.ReadBytes(frame, temporary, length);
        NativeBattleEffect312MeshStep drawn = DrawLoaded(frame.Memory, resources.ReadMesh(meshAddress), inputs.ReadProjection(frame), context, inputs.Colors, local, pilotIdentity);
        frame = frame with { Memory = drawn.Memory };
        frame = NativeBattleEffect312Controller.WriteBytes(frame, temporary, drawn.Context, length);
        return inputs.Execute(frame, new NativeBattleEffect312DrawBatch(drawn.Batch));
    }

    public static NativeBattleEffect312MeshStep DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors constants, bool local, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        uint flags = U(context, 0x1C);
        if (U(context, 0) != mesh.Address || (flags & ~0x3FFFu) != 0 || mesh.Bytes.IsDefault || mesh.Bytes.Length < 8)
        {
            throw new NotSupportedException("Effect312 mesh requires its actual bounded source and context.");
        }
        if ((flags & 0x2000) != 0)
        {
            throw new NotSupportedException("Effect312 selected mesh vertices require their actual physical pointer owner.");
        }
        int vertices = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)));
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
        if (vertices <= 0 || cursor != checked(8 + vertices * 8))
        {
            throw new InvalidDataException("Effect312 mesh requires its original vertex directory.");
        }
        int sourceOffset = local ? 0x24 : 0x20;
        int flagsOffset = local ? 0x34 : 0x30;
        int areaOffset = local ? 0x28 : 0x24;
        int depthOffset = local ? 0x30 : 0x2C;
        context = Put(context, 4, checked(mesh.Address + 8));
        if ((flags & 0x1000) == 0)
        {
            context = Put(context, 0x18, 0);
        }
        context = Put(context, sourceOffset, checked(mesh.Address + (uint)cursor));
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect312 mesh requires current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect312 mesh requires current color registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect312 mesh requires current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Effect312 mesh requires its current packet process.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Effect312 mesh requires current far-color controls.");
        byte[] rgb = context.Read(8, 3);
        far = far with { FarRed = (uint)rgb[0] << 4, FarGreen = (uint)rgb[1] << 4, FarBlue = (uint)rgb[2] << 4 };
        var attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Source(cursor, 4));
            cursor += 4;
            context = Put(context, sourceOffset, checked(mesh.Address + (uint)cursor));
            if (count > 0 && (local && group is not (4 or 5) || !local && group == 1))
            {
                throw new NotSupportedException("Effect312 mesh leaves its selected primitive groups.");
            }
            bool quad = (group & 1) != 0;
            bool textured = (group & 2) != 0;
            bool gouraud = (group & 4) != 0;
            int corners = quad ? 4 : 3;
            int packetLength = 4 * (gouraud ? 1 + corners * (textured ? 3 : 2) : 2 + corners * (textured ? 2 : 1));
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = Source(cursor, Strides[group]).ToArray();
                System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[corners] : null;
                for (int corner = 0; corner < 3; corner++)
                {
                    Load(corner, corner);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                SkinWrites packet = new(packetLength, allocated);
                packet.Word(0, (uint)(packetLength / 4 - 1) << 24);
                uint command = local && !gouraud ? U(context, 0x20) | (textured ? quad ? 0x2C000000u : 0x24000000u : quad ? 0x28000000u : 0x20000000u) : BinaryPrimitives.ReadUInt32LittleEndian(face);
                if ((flags & (gouraud ? 2u : 1u)) != 0)
                {
                    command |= 0x02000000;
                }
                if ((flags & (gouraud ? 8u : 4u)) != 0)
                {
                    command &= 0xFDFFFFFF;
                }
                if (!local || !gouraud)
                {
                    packet.Word(4, command);
                }
                int colorOffset = textured ? quad ? 24 : 20 : 12;
                uint[] shades = new uint[gouraud ? corners : 1];
                shades[0] = command;
                if (gouraud)
                {
                    for (int corner = 1; corner < corners; corner++)
                    {
                        shades[corner] = BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(colorOffset + (corner - 1) * 4));
                    }
                    if (local)
                    {
                        for (int corner = 0; corner < corners; corner++)
                        {
                            context = Put(context, 0x5C + corner * 4, shades[corner]);
                        }
                    }
                }
                uint[] uv = new uint[textured ? corners : 0];
                if (textured)
                {
                    uint offset = U(context, 0x18);
                    uv[0] = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(12)) + offset);
                    uv[1] = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(16)) + offset);
                    uv[2] = quad ? unchecked(BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(20)) + offset + (offset << 16)) : unchecked(BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(10)) + offset);
                    if (quad)
                    {
                        uv[3] = uv[2] >> 16;
                    }
                    for (int corner = 0; corner < corners; corner++)
                    {
                        packet.Word(Screen(corner) + 4, uv[corner]);
                    }
                }
                uint projectionFlags = registers.Colors.Flags ?? throw new NotSupportedException("Effect312 mesh requires actual projection flags.");
                context = Put(context, flagsOffset, projectionFlags);
                bool accepted = false;
                if ((projectionFlags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    if (textured)
                    {
                        if ((flags & 0x500) != 0)
                        {
                            uv[1] = Material(uv[1], Word(context, 0x10), (flags & 0x400) != 0);
                            packet.Word(Screen(1) + 4, uv[1]);
                        }
                        if ((flags & 0xA00) != 0)
                        {
                            uv[0] = Material(uv[0], Word(context, 0x14), (flags & 0x800) != 0);
                            packet.Word(Screen(0) + 4, uv[0]);
                        }
                    }
                    int area = unchecked((int)(registers.Geometry.Mac0 ?? throw new NotSupportedException("Effect312 mesh requires actual NCLIP output.")));
                    context = Put(context, areaOffset, unchecked((uint)area));
                    if ((!local || area != 0) && (area >= 0 || (flags & (gouraud ? 0x20u : 0x10u)) != 0))
                    {
                        uint[] screens = new uint[corners];
                        for (int corner = 0; corner < 3; corner++)
                        {
                            screens[corner] = registers.Geometry.Sxy[corner] ?? throw new NotSupportedException("Effect312 mesh requires current SXY.");
                            packet.Word(Screen(corner), screens[corner]);
                        }
                        if (quad)
                        {
                            Load(3, 0);
                            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                            screens[3] = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Effect312 mesh requires its fourth SXY.");
                            packet.Word(Screen(3), screens[3]);
                            registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect312 mesh requires current ZSF4."));
                        }
                        else
                        {
                            registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect312 mesh requires current ZSF3."));
                        }
                        if (!screens.All(screen => unchecked((short)screen) is < 0 or > 2560) && !screens.All(screen => unchecked((short)(screen >> 16)) is < 0 or > 1728))
                        {
                            uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Effect312 mesh requires its current OTZ.");
                            context = Put(context, depthOffset, depth);
                            if (local && gouraud)
                            {
                                uint tint = U(context, 0x20);
                                context = Put(context, 0x58, tint);
                                for (int corner = 0; corner < corners; corner++)
                                {
                                    shades[corner] = Tint(shades[corner], tint);
                                    context = Put(context, 0x5C + corner * 4, shades[corner]);
                                    BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(corner == 0 ? 0 : colorOffset + (corner - 1) * 4), shades[corner]);
                                }
                            }
                            if ((flags & (gouraud ? 0x80u : 0x40u)) != 0)
                            {
                                uint cue = U(context, 12);
                                if (gouraud)
                                {
                                    registers = NativeBattleGouraudMeshRegisters.Shade(registers, face, quad, textured, flags, cue, far, constants);
                                    uint farColor = (uint)(rgb[0] | rgb[1] << 8 | rgb[2] << 16);
                                    shades = NativeBattleGteColorInterpolation.Gouraud45F4C0(shades.ToImmutableArray(), quad, unchecked((int)cue), farColor, constants).ToArray();
                                }
                                else
                                {
                                    NativeBattleScriptedDpcsResult shaded = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, command, cue, far, constants);
                                    registers = registers with { Colors = shaded.Registers };
                                    shades[0] = shaded.PackedRgb;
                                }
                            }
                            for (int corner = 0; corner < shades.Length; corner++)
                            {
                                packet.Word(gouraud ? Screen(corner) - 4 : 4, shades[corner]);
                            }
                            ImmutableArray<int> payload = registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Effect312 mesh requires current SZ sidecars.")))).ToImmutableArray();
                            NativeBattleGeometryPrimitive primitive = new(17 + (unchecked((int)depth) >> 2), packet.Words(), payload);
                            if (pilotIdentity is { } identity && world is not null)
                            {
                                primitive = NativeBattleEffect312Presentation.Attach(primitive, identity with { Primitive = unchecked(identity.Primitive * 397 + cursor) }, world);
                            }
                            attempts.Add(packet.Attempt(primitive));
                            allocated += packetLength;
                            accepted = true;
                        }
                    }
                }
                if (!accepted)
                {
                    attempts.Add(packet.Attempt(null));
                }
                cursor += Strides[group];
                context = Put(context, sourceOffset, checked(mesh.Address + (uint)cursor));

                int Screen(int corner) => 8 + corner * (gouraud ? textured ? 12 : 8 : textured ? 8 : 4);

                void Load(int corner, int slot)
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
                    if ((offset & 7) != 0 || offset > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Effect312 mesh index leaves its actual vertex rows.");
                    }
                    ReadOnlySpan<byte> row = Source(8 + offset, 8);
                    NativeBattleVector vertex = new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..]));
                    registers = NativeBattleGeometry.LoadVector(registers, slot, vertex, BinaryPrimitives.ReadInt16LittleEndian(row[6..]));
                    if (world is not null)
                    {
                        world[corner] = NativeBattleEffect312Presentation.Loaded(registers, slot, projection);
                    }
                }
            }
        }
        if (cursor != mesh.Bytes.Length)
        {
            throw new InvalidDataException("Effect312 mesh has unconsumed primitive bytes.");
        }
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } };
        return new(memory, context, new(attempts.ToImmutable(), allocated));

        ReadOnlySpan<byte> Source(int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > mesh.Bytes.Length - length)
            {
                throw new InvalidDataException("Effect312 mesh read leaves its original source.");
            }
            return mesh.Bytes.AsSpan().Slice(offset, length);
        }
    }

    public static uint Tint(uint color, uint tint)
    {
        uint result = color & 0xFF000000;
        for (int channel = 0; channel < 3; channel++)
        {
            int shift = channel * 8;
            uint product = ((color >> shift) & 255) * ((tint >> shift) & 255);
            result |= (uint)unchecked((byte)(product >> 7)) << shift;
        }
        return result;
    }

    private static uint Material(uint original, ushort value, bool add)
    {
        ushort high = add ? unchecked((ushort)((original >> 16) + value)) : value;
        return (original & 0xFFFF) | (uint)high << 16;
    }

    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
}
