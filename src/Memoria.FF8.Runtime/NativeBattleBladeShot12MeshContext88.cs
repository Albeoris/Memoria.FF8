using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBladeShot12MeshContext88Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context88, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleBladeShot12MeshContext88
{
    public static NativeBattleBladeShot12MeshContext88Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context, NativeBattleSummonColors colors, int tableBaseBucket = 17, byte depthShift = 2)
    {
        if (Dword(context, 0x1C) != 0xC0 || mesh.Address != 0x1364F14 || Dword(context, 0) != mesh.Address || depthShift > 31)
        {
            throw new InvalidDataException("Blade Shot custom mesh requires its actual G3/G4 caller context.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Blade Shot custom mesh requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Blade Shot custom mesh requires its current arithmetic registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Blade Shot custom mesh requires its current projection controls.");
        int vertices = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int cursor = checked(BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan()) + 16);
        context = Put(context, 4, checked(mesh.Address + 8));
        context = Put(context, 0x20, checked(mesh.Address + (uint)cursor));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        bool unlinked = false;
        for (int group = 4; group <= 5; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
            cursor = checked(cursor + 4);
            context = Put(context, 0x20, checked(mesh.Address + (uint)cursor));
            bool quad = group == 5;
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = mesh.Bytes.AsSpan().Slice(cursor, quad ? 24 : 20).ToArray();
                NativeBattleModuleProjectedVertex[] projected = new NativeBattleModuleProjectedVertex[quad ? 4 : 3];
                for (int corner = 0; corner < 3; corner++)
                {
                    registers = Load(corner, corner);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                uint flags = registers.Colors.Flags ?? throw new NotSupportedException("Blade Shot requires its current RTPT flag.");
                context = Put(context, 0x30, flags);
                for (int corner = 0; corner < 3; corner++)
                {
                    projected[corner] = new(registers.Geometry.Sxy[corner]!.Value, unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value), flags);
                }
                byte[] data = new byte[quad ? 36 : 28];
                NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[data.Length];
                Store(0, quad ? 0x08000000u : 0x06000000u);
                Store(4, BinaryPrimitives.ReadUInt32LittleEndian(face));
                NativeBattleGeometryPrimitive? primitive = null;
                int reserve = 0;
                if ((flags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    int area = unchecked((int)registers.Geometry.Mac0!.Value);
                    context = Put(context, 0x24, unchecked((uint)area));
                    if (area >= 0)
                    {
                        for (int corner = 0; corner < 3; corner++)
                        {
                            Store(8 + corner * 8, projected[corner].Screen);
                        }
                        if (quad)
                        {
                            registers = Load(0, 3);
                            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                            projected[3] = new(registers.Geometry.Sxy[2]!.Value, unchecked((ushort)registers.Geometry.Sz[3]!.Value), registers.Colors.Flags!.Value);
                            Store(32, projected[3].Screen);
                            registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Blade Shot requires its actual ZSF4."));
                        }
                        else
                        {
                            registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Blade Shot requires its actual ZSF3."));
                        }
                        uint rawDepth = registers.Geometry.Otz!.Value;
                        bool outside = projected.All(vertex => unchecked((ushort)vertex.Screen) > 2560) || projected.All(vertex => (vertex.Screen >> 16) > 1728);
                        if (!quad || !outside)
                        {
                            context = Put(context, 0x2C, rawDepth);
                        }
                        if (!outside)
                        {
                            int adjusted = unchecked((int)rawDepth - 8);
                            context = Put(context, 0x2C, unchecked((uint)adjusted));
                            if (adjusted > 0)
                            {
                                NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Blade Shot shading requires its current FAR owner.");
                                NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Blade Shot shading requires its current FAR registers.");
                                if (!quad)
                                {
                                    far = far with { FarRed = (uint)context.Read(8, 1)[0] << 4, FarGreen = (uint)context.Read(9, 1)[0] << 4, FarBlue = (uint)context.Read(10, 1)[0] << 4 };
                                    memory = memory with { PrimitiveProcess = process with { ModelRendererRegisters = far } };
                                }
                                uint cue = Dword(context, 12);
                                uint first = BinaryPrimitives.ReadUInt32LittleEndian(face);
                                uint second = BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(12));
                                uint third = BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(16));
                                uint fourth = quad ? BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(20)) : first;
                                NativeBattleScriptedColorRegisters shaded = NativeBattleScriptedDpct.LoadColors45E120(registers.Colors, second, third, fourth) with { Ir0 = cue };
                                shaded = NativeBattleScriptedDpct.Transform45F4C0(shaded, far, colors);
                                uint[] rgb = shaded.Rgb.Select(value => value ?? throw new NotSupportedException("Blade Shot requires its produced RGB FIFO.")).ToArray();
                                byte[] finalFace = face.ToArray();
                                if (quad)
                                {
                                    NativeBattleScriptedDpcsResult single = NativeBattleScriptedDpcs.Transform45F270(shaded, first, cue, far, colors);
                                    shaded = single.Registers;
                                    BinaryPrimitives.WriteUInt32LittleEndian(finalFace, single.PackedRgb);
                                    BinaryPrimitives.WriteUInt32LittleEndian(finalFace.AsSpan(20), rgb[2]);
                                }
                                else
                                {
                                    BinaryPrimitives.WriteUInt32LittleEndian(finalFace, rgb[2]);
                                }
                                BinaryPrimitives.WriteUInt32LittleEndian(finalFace.AsSpan(12), rgb[0]);
                                BinaryPrimitives.WriteUInt32LittleEndian(finalFace.AsSpan(16), rgb[1]);
                                registers = registers with { Colors = shaded };
                                NativeBattleGfGeometryAttempt encoded = NativeBattleGfGeometryPackets.EmitAttempt(finalFace, projected, group, new(0, null, null, null, null, 0, 0, tableBaseBucket, depthShift), null, allocated);
                                primitive = (encoded.Primitive ?? throw new InvalidDataException("Blade Shot admitted face differs from its canonical packet encoder.")) with { Bucket = unchecked(tableBaseBucket + (adjusted >> depthShift)) };
                                data = encoded.Data.ToArray();
                                mask = encoded.WriteMask.ToArray();
                                reserve = data.Length;
                            }
                            else if (quad)
                            {
                                reserve = 36;
                                unlinked = true;
                            }
                        }
                    }
                }
                attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
                allocated = checked(allocated + reserve);
                cursor = checked(cursor + (quad ? 24 : 20));
                context = Put(context, 0x20, checked(mesh.Address + (uint)cursor));

                NativeBattleGeometryRegisterStep Load(int slot, int corner)
                {
                    int offset = checked(BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4);
                    if ((offset & 7) != 0 || offset > checked(vertices * 8) - 8)
                    {
                        throw new InvalidDataException("Blade Shot face leaves its actual packed vertex bank.");
                    }
                    ReadOnlySpan<byte> packed = mesh.Bytes.AsSpan().Slice(checked(8 + offset), 8);
                    if (!quad)
                    {
                        context = context.Write(0x34 + corner * 8, packed);
                    }
                    return NativeBattleGeometry.LoadVector(registers, slot, new(BinaryPrimitives.ReadInt16LittleEndian(packed), BinaryPrimitives.ReadInt16LittleEndian(packed.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(packed.Slice(4))), BinaryPrimitives.ReadInt16LittleEndian(packed.Slice(6)));
                }
                void Store(int offset, uint value)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
                    mask.AsSpan(offset, 4).Fill(NativeBattleGfPacketByteState.Known);
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, context, new(attempts.ToImmutable(), allocated) { NativeUnlinkedAllocation = unlinked });
    }

    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
}
