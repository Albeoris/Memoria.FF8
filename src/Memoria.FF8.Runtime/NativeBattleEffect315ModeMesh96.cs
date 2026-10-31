using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect315ModeMesh96Step(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context96, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect315ModeMesh96
{
    public static NativeBattleEffect315ModeMesh96Step DrawLoaded(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context96, NativeBattleSummonColors colors, int tableBaseBucket = 17, byte depthShift = 2)
    {
        return DrawLoadedWithPresentation(memory, mesh, projection, context96, colors, tableBaseBucket, depthShift, null);
    }

    public static NativeBattleEffect315ModeMesh96Step DrawLoadedWithPresentation(NativeBattleEffectMemory memory, NativeBattleStaticMeshResource mesh, NativeBattleCameraProjection projection, NativeBattleEffectArena context96, NativeBattleSummonColors colors, int tableBaseBucket = 17, byte depthShift = 2, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        context96.Validate();
        if (depthShift > 31 || Dword(context96, 0) != mesh.Address || Dword(context96, 0x1C) is not (0x33 or 0xF3) || context96.ExceedsExtent(96))
        {
            throw new InvalidDataException("Effect315 mode mesh requires its reached 96-byte caller context.");
        }
        if (mesh.Bytes.IsDefault || mesh.Bytes.Length != 1216 || BinaryPrimitives.ReadInt32LittleEndian(Read(4, 4)) != 48)
        {
            throw new InvalidDataException("Effect315 mode mesh requires its actual selected vertex and polygon extent.");
        }
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(Read(0, 4));
        int validation = cursor;
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(validation, 4));
            if (count != (group == 5 ? 33 : 0))
            {
                throw new NotSupportedException("Effect315 mode mesh requires its selected Gouraud quad group.");
            }
            validation = checked(validation + 4 + count * 24);
        }
        if (cursor != 392 || validation != mesh.Bytes.Length)
        {
            throw new InvalidDataException("Effect315 mode mesh leaves its original packed source rows.");
        }
        uint flags = Dword(context96, 0x1C);
        context96 = Put(context96, 4, checked(mesh.Address + 8));
        context96 = Put(context96, 0x20, checked(mesh.Address + (uint)cursor));
        context96 = Put(context96, 0x18, 0);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect315 mode mesh requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect315 mode mesh requires its current color registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect315 mode mesh requires its current projection controls.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Effect315 mode mesh requires its current primitive owner.");
        NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Effect315 mode mesh requires its current far-color owner.");
        byte[] rgb = context96.Read(8, 3);
        far = far with { FarRed = (uint)rgb[0] << 4, FarGreen = (uint)rgb[1] << 4, FarBlue = (uint)rgb[2] << 4 };
        uint farColor = (uint)(rgb[0] | rgb[1] << 8 | rgb[2] << 16);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4));
            cursor += 4;
            context96 = Put(context96, 0x20, checked(mesh.Address + (uint)cursor));
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = Read(cursor, 24).ToArray();
                NativeBattleActionMatrix? pilotMatrix = pilotIdentity.HasValue ? NativeBattleGeometry.CaptureMatrix(registers.Geometry) : null;
                System.Numerics.Vector3[]? pilotWorld = pilotIdentity.HasValue ? new System.Numerics.Vector3[4] : null;
                for (int corner = 0; corner < 3; corner++)
                {
                    Load(corner, corner);
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                SkinWrites packet = new(44, allocated);
                packet.Word(0, 0x0A000000);
                uint firstColor = BinaryPrimitives.ReadUInt32LittleEndian(face);
                if ((flags & 2) != 0)
                {
                    firstColor |= 0x02000000;
                }
                if ((flags & 8) != 0)
                {
                    firstColor &= 0xFDFFFFFF;
                }
                packet.Word(8, firstColor);
                uint projectionFlags = registers.Colors.Flags!.Value;
                context96 = Put(context96, 0x30, projectionFlags);
                bool accepted = false;
                if ((projectionFlags & 0x60000) == 0)
                {
                    registers = NativeBattleGeometry.Clip45EE10(registers);
                    int area = unchecked((int)registers.Geometry.Mac0!.Value);
                    context96 = Put(context96, 0x24, unchecked((uint)area));
                    if (area != 0 && (area >= 0 || (flags & 0x20) != 0))
                    {
                        uint[] screens = registers.Geometry.Sxy.Select(value => value!.Value).ToArray();
                        for (int corner = 0; corner < 3; corner++)
                        {
                            packet.Word(12 + corner * 8, screens[corner]);
                        }
                        Load(3, 0);
                        registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                        uint fourth = registers.Geometry.Sxy[2]!.Value;
                        packet.Word(36, fourth);
                        registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect315 mode mesh requires its current ZSF4."));
                        uint[] allScreens = [screens[0], screens[1], screens[2], fourth];
                        if (!allScreens.All(screen => unchecked((short)screen) is < 0 or > 2560) && !allScreens.All(screen => unchecked((short)(screen >> 16)) is < 0 or > 1728))
                        {
                            uint depth = registers.Geometry.Otz!.Value;
                            context96 = Put(context96, 0x2C, depth);
                            ImmutableArray<uint> faceColors = [firstColor, BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(12)), BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(16)), BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(20))];
                            if ((flags & 0x80) != 0)
                            {
                                uint cue = Dword(context96, 12);
                                faceColors = NativeBattleGteColorInterpolation.Gouraud45F4C0(faceColors, true, unchecked((int)cue), farColor, colors);
                                registers = NativeBattleGouraudMeshRegisters.Shade(registers, face, true, false, flags, cue, far, colors);
                            }
                            for (int corner = 0; corner < 4; corner++)
                            {
                                packet.Word(8 + corner * 8, faceColors[corner]);
                            }
                            packet.Word(4, Dword(context96, 0x58));
                            packet.Word(40, Dword(context96, 0x5C));
                            int bucket = unchecked(tableBaseBucket + (unchecked((int)depth) >> depthShift));
                            ImmutableArray<int> payload = registers.Geometry.Sz.Select(value => unchecked((int)value!.Value)).ToImmutableArray();
                            NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), payload);
                            primitive = NativeBattleEffect32Presentation.Capture(primitive, NativeBattleEffect32Presentation.Resource(pilotIdentity, mesh.Address, cursor), pilotWorld);
                            attempts.Add(packet.Attempt(primitive));
                            allocated += 44;
                            accepted = true;
                        }
                    }
                }
                if (!accepted)
                {
                    attempts.Add(packet.Attempt(null));
                }
                cursor += 24;
                context96 = Put(context96, 0x20, checked(mesh.Address + (uint)cursor));

                void Load(int corner, int register)
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) * 4;
                    if ((offset & 7) != 0 || offset >= 48 * 8)
                    {
                        throw new InvalidDataException("Effect315 mode mesh selected a vertex outside its current packed source.");
                    }
                    ReadOnlySpan<byte> vertex = Read(8 + offset, 8);
                    NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(vertex), BinaryPrimitives.ReadInt16LittleEndian(vertex[2..]), BinaryPrimitives.ReadInt16LittleEndian(vertex[4..]));
                    registers = NativeBattleGeometry.LoadVector(registers, register, point, BinaryPrimitives.ReadInt16LittleEndian(vertex[6..]));
                    if (pilotWorld is not null)
                    {
                        pilotWorld[corner] = NativeBattleEffect32Presentation.World(pilotMatrix!, point, projection);
                    }
                }
            }
        }
        memory = memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors, PrimitiveProcess = process with { ModelRendererRegisters = far } };
        return new(memory, context96, new(attempts.ToImmutable(), allocated));

        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (mesh.Bytes.IsDefault || offset < 0 || length < 0 || offset > mesh.Bytes.Length - length)
            {
                throw new InvalidDataException("Effect315 mode mesh read leaves its actual geometry source.");
            }
            return mesh.Bytes.AsSpan().Slice(offset, length);
        }
    }

    private static uint Dword(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena value, int offset, uint data) => value.Write(offset, BitConverter.GetBytes(data));
}
