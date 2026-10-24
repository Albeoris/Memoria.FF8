using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect66BodyDrawing
{
    private static NativeBattleEffect66BodyStep DrawFaces(NativeBattleEffect66Frame frame, NativeBattleEffectArena temporary, NativeBattleEffectArena scratch, int scratchLength, Func<uint, int, NativeBattleEffectArena> readGeometry, NativeBattleEffect66Inputs inputs)
    {
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect66 body requires current projection controls.");
        uint source = Dword(temporary, 0);
        uint flagAddress = Dword(temporary, 0x2C);
        bool allowBirth = true;
        for (int group = 0; group < 2; group++)
        {
            int count = Uword(temporary, 8 + group * 2);
            int corners = group == 0 ? 3 : 4;
            int stride = group == 0 ? 16 : 20;
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                ushort flag = Uword(ReadBytes(frame, flagAddress, 2), 0);
                if ((flag & 0x8000) == 0)
                {
                    NativeBattleEffectArena face = readGeometry(source, stride);
                    bool existing = (flag & 0x4000) == 0;
                    if (!existing)
                    {
                        for (int corner = 0; corner < corners; corner++)
                        {
                            int offset = checked((Uword(face, corner * 2) & 0xFFF) * 8);
                            if (offset > scratchLength - 8)
                            {
                                throw new InvalidDataException("Effect66 face leaves current transformed rows.");
                            }
                            temporary = NativeBattleEffectNodeContextMemory.Copy(scratch, offset, temporary, 0x58 + corner * 8, 8);
                        }
                        if (unchecked((int)Dword(temporary, 0x44)) > (flag & 0x3FFF) && allowBirth)
                        {
                            (frame, temporary, bool allocated) = BirthShard(frame, temporary, flagAddress, corners);
                            allowBirth = allocated;
                        }
                    }
                    else
                    {
                        (frame, temporary, registers) = ReadAndAdvanceShard(frame, temporary, flagAddress, flag, corners, registers, inputs);
                    }
                    for (int corner = 0; corner < 3; corner++)
                    {
                        registers = NativeBattleGeometry.LoadVector(registers, corner, Vector(temporary, 0x58 + corner * 8), S16(temporary, 0x5E + corner * 8));
                    }
                    registers = NativeBattleGeometry.ProjectTriple(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                    uint firstFlags = registers.Colors.Flags!.Value;
                    temporary = Put(temporary, 0x54, firstFlags);
                    NativeBattleEffect66PacketWrites packet = new(corners == 3 ? 32 : 40);
                    packet.Dword(0, corners == 3 ? 0x07000000u : 0x09000000u);
                    packet.Dword(4, Dword(temporary, 0x1C) | (Dword(face, 12) & 0x02000000u) | (corners == 3 ? 0x24000000u : 0x2C000000u));
                    packet.Copy(12, face, 8, 4);
                    packet.Dword(20, Dword(face, 12) & 0x01FFFFFF);
                    int? bucket = null;
                    uint[] screens = new uint[corners];
                    int[] depths = new int[4];
                    for (int corner = 0; corner < 3; corner++)
                    {
                        screens[corner] = registers.Geometry.Sxy[corner]!.Value;
                        depths[corner] = unchecked((ushort)registers.Geometry.Sz[corner + 1]!.Value);
                    }
                    if ((firstFlags & 0x60000) == 0)
                    {
                        registers = NativeBattleGeometry.Clip45EE10(registers);
                        temporary = Put(temporary, 0x48, registers.Geometry.Mac0!.Value);
                        if (unchecked((int)registers.Geometry.Mac0.Value) > 0)
                        {
                            for (int corner = 0; corner < 3; corner++)
                            {
                                packet.Dword(8 + corner * 8, screens[corner]);
                            }
                            if (corners == 3)
                            {
                                registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect66 triangle requires its current ZSF3."));
                                packet.Dword(28, unchecked((uint)(unchecked((int)Dword(face, 4)) >> 16)));
                            }
                            else
                            {
                                packet.Copy(28, face, 16, 4);
                                registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(registers, 0, Vector(temporary, 0x70), S16(temporary, 0x76)), inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                                screens[3] = registers.Geometry.Sxy[2]!.Value;
                                depths[3] = unchecked((ushort)registers.Geometry.Sz[3]!.Value);
                                packet.Dword(36, unchecked((uint)(unchecked((int)Dword(face, 16)) >> 16)));
                                packet.Dword(32, screens[3]);
                                registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect66 quad requires its current ZSF4."));
                            }
                            temporary = Put(temporary, 0x50, registers.Geometry.Otz!.Value);
                            bucket = checked(17 + ((int)registers.Geometry.Otz.Value >> 2));
                        }
                    }
                    NativeBattleGfGeometryAttempt attempt = packet.Attempt(0, bucket, depths.ToImmutableArray());
                    frame = inputs.Execute(Publish(frame, registers), new NativeBattleEffect66DeliverBatch(new([attempt], bucket is null ? 0 : corners == 3 ? 32 : 40), 0x1D8E054));
                    registers = Registers(frame);
                    if (bucket is int accepted && existing)
                    {
                        NativeBattleEffect66PacketWrites overlay = new(corners == 3 ? 20 : 24);
                        overlay.Dword(0, corners == 3 ? 0x04000000u : 0x05000000u);
                        overlay.Dword(4, Dword(temporary, 0x38) | (corners == 3 ? 0x22000000u : 0x2A000000u));
                        for (int corner = 0; corner < corners; corner++)
                        {
                            overlay.Dword(8 + corner * 4, screens[corner]);
                        }
                        NativeBattleGfGeometryAttempt extra = overlay.Attempt(0, Math.Max(17, accepted - 1), [0, 0, 0, 0]);
                        frame = inputs.Execute(Publish(frame, registers), new NativeBattleEffect66DeliverBatch(new([extra], corners == 3 ? 20 : 24), 0x1D8E054));
                        registers = Registers(frame);
                    }
                }
                source += (uint)stride;
                flagAddress += 2;
            }
        }
        return new(Publish(frame, registers), Put(temporary, 0x2C, flagAddress), scratch);
    }
}
