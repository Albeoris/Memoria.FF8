using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect166Mesh108Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context108, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect166Mesh108
{
    public static NativeBattleEffect166Mesh108Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context108, NativeBattleSummonColors colors, int tableBaseBucket = 17, byte depthShift = 2, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        uint flags = U(context108, 0x1C);
        if (U(context108, 0) != mesh.Address || flags is not (0x33 or 0xF3) || depthShift > 31)
        {
            throw new NotSupportedException("Effect166 custom mesh requires its selected Gouraud context.");
        }
        int vertices = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan());
        if (vertices <= 0 || cursor != 8 + vertices * 8)
        {
            throw new InvalidDataException("Custom mesh requires its actual packed source rows.");
        }
        context108 = Put(Put(Put(context108, 4, mesh.Address + 8), 0x18, 0), 0x24, checked(mesh.Address + (uint)cursor));
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Custom mesh requires current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Custom mesh requires current color registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Custom mesh requires current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Custom mesh requires its current primitive owner.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Custom mesh requires current renderer controls.");
        byte[] rgb = context108.Read(8, 3);
        uint farColor = (uint)(rgb[0] | rgb[1] << 8 | rgb[2] << 16);
        far = far with { FarRed = (uint)rgb[0] << 4, FarGreen = (uint)rgb[1] << 4, FarBlue = (uint)rgb[2] << 4 };
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
            cursor += 4;
            context108 = Put(context108, 0x24, checked(mesh.Address + (uint)cursor));
            if (count < 0 || count != 0 && group is not (4 or 5))
            {
                throw new NotSupportedException("Custom mesh leaves its selected Gouraud primitive groups.");
            }
            bool quad = group == 5;
            int corners = quad ? 4 : 3;
            int faceLength = quad ? 24 : 20;
            int packetLength = quad ? 36 : 28;
            for (int index = 0; index < count; index++)
            {
                byte[] face = mesh.Bytes.AsSpan().Slice(cursor, faceLength).ToArray();
                NativeBattleActionMatrix? pilotMatrix = pilotIdentity.HasValue ? NativeBattleGeometry.CaptureMatrix(registers.Geometry) : null;
                System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[4] : null;
                for (int corner = 0; corner < 3; corner++)
                {
                    Load(corner, corner);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                SkinWrites packet = new(packetLength, allocated);
                packet.Word(0, quad ? 0x08000000u : 0x06000000u);
                uint firstColor = BinaryPrimitives.ReadUInt32LittleEndian(face) | 0x02000000;
                context108 = Put(context108, 0x5C, firstColor);
                for (int corner = 1; corner < corners; corner++)
                {
                    context108 = Put(context108, 0x5C + corner * 4, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(8 + corner * 4)));
                }
                uint projectionFlags = registers.Colors.Flags!.Value;
                context108 = Put(context108, 0x34, projectionFlags);
                bool accepted = false;
                if ((projectionFlags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    int area = unchecked((int)registers.Geometry.Mac0!.Value);
                    context108 = Put(context108, 0x28, unchecked((uint)area));
                    if (area != 0)
                    {
                        uint[] screens = new uint[corners];
                        for (int corner = 0; corner < 3; corner++)
                        {
                            screens[corner] = registers.Geometry.Sxy[corner]!.Value;
                            packet.Word(8 + corner * 8, screens[corner]);
                        }
                        if (quad)
                        {
                            Load(3, 0);
                            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                            screens[3] = registers.Geometry.Sxy[2]!.Value;
                        }
                        else
                        {
                            registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Custom mesh requires current ZSF3."));
                        }
                        if (!screens.All(value => unchecked((short)value) is < 0 or > 2560) && !screens.All(value => unchecked((short)(value >> 16)) is < 0 or > 1728))
                        {
                            if (quad)
                            {
                                packet.Word(32, screens[3]);
                                registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Custom mesh requires current ZSF4."));
                            }
                            uint depth = registers.Geometry.Otz!.Value;
                            context108 = Put(context108, 0x30, depth);
                            uint tint = U(context108, 0x20);
                            context108 = Put(context108, 0x58, tint);
                            ImmutableArray<uint>.Builder faceColors = ImmutableArray.CreateBuilder<uint>(corners);
                            for (int corner = 0; corner < corners; corner++)
                            {
                                uint original = U(context108, 0x5C + corner * 4);
                                uint scaled = original & 0xFF000000;
                                for (int channel = 0; channel < 3; channel++)
                                {
                                    uint value = (((original >> (channel * 8)) & 255) * ((tint >> (channel * 8)) & 255)) >> 7;
                                    scaled |= (value & 255) << (channel * 8);
                                }
                                context108 = Put(context108, 0x5C + corner * 4, scaled);
                                faceColors.Add(scaled);
                                BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(corner == 0 ? 0 : 8 + corner * 4), scaled);
                            }
                            ImmutableArray<uint> resultColors = faceColors.ToImmutable();
                            if ((flags & 0x80) != 0)
                            {
                                uint cue = U(context108, 12);
                                resultColors = NativeBattleGteColorInterpolation.Gouraud45F4C0(resultColors, quad, unchecked((int)cue), farColor, colors);
                                registers = NativeBattleGouraudMeshRegisters.Shade(registers, face, quad, false, flags, cue, far, colors);
                            }
                            for (int corner = 0; corner < corners; corner++)
                            {
                                packet.Word(4 + corner * 8, resultColors[corner]);
                            }
                            attempts.Add(packet.Attempt(new(tableBaseBucket + (unchecked((int)depth) >> depthShift), packet.Words(), registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Custom mesh requires all four current SZ sidecars.")))).ToImmutableArray()) { PilotGeometry = pilotIdentity is { } identity && world is not null ? new(identity with { Primitive = unchecked(identity.Primitive * 65537 + cursor) }, world[0], world[1], world[2], quad ? world[3] : default, (byte)corners) { UseSourceDepths = true, SourceDepths = new(registers.Geometry.Sz[0]!.Value, registers.Geometry.Sz[1]!.Value, registers.Geometry.Sz[2]!.Value, registers.Geometry.Sz[3]!.Value) } : null }));
                            allocated += packetLength;
                            accepted = true;
                        }
                    }
                }
                if (!accepted)
                {
                    attempts.Add(packet.Attempt(null));
                }
                cursor += faceLength;
                context108 = Put(context108, 0x24, checked(mesh.Address + (uint)cursor));

                void Load(int corner, int register)
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
                    if ((offset & 7) != 0 || offset >= vertices * 8)
                    {
                        throw new InvalidDataException("Custom mesh selected an invalid current vertex row.");
                    }
                    ReadOnlySpan<byte> vertex = mesh.Bytes.AsSpan().Slice(8 + offset, 6);
                    NativeBattleVector position = new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex[2..]), BinaryPrimitives.ReadInt16LittleEndian(vertex[4..]));
                    if (world is not null)
                    {
                        world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(pilotMatrix!, position), projection);
                    }
                    registers = NativeBattleGeometry.LoadVector(registers, register, position, null);
                }
            }
        }
        if (cursor != mesh.Bytes.Length)
        {
            throw new InvalidDataException("Custom mesh leaves its current source extent.");
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } }, context108, new(attempts.ToImmutable(), allocated));
    }

    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
}
