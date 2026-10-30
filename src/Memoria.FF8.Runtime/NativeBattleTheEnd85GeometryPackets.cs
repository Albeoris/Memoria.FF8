using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTheEnd85MeshStep(NativeBattleFileStaging Staging, NativeBattleGfGeometryBatch Packets, uint? FarRed, uint? FarGreen, uint? FarBlue);

/// <summary>The model dispatcher reuses the proved restoration leaves; the separate title GT4 adds its signed red threshold.</summary>
public static class NativeBattleTheEnd85GeometryPackets
{
    private static readonly int[] Lengths = [12, 12, 20, 24, 20, 24, 28, 36];

    public static NativeBattleTheEnd85MeshStep Emit785A90(NativeBattleFileStaging staging, uint context, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<uint, int, byte[]> read, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return Emit(staging, context, matrix, projection, colors, read, false, pilotIdentity);
    }

    public static NativeBattleTheEnd85MeshStep Emit78C660(NativeBattleFileStaging staging, uint context, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<uint, int, byte[]> read, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        return Emit(staging, context, matrix, projection, colors, read, true, pilotIdentity);
    }

    private static NativeBattleTheEnd85MeshStep Emit(NativeBattleFileStaging staging, uint context, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<uint, int, byte[]> read, bool thresholdQuad, NativeBattlePilotVisualIdentity? pilotIdentity)
    {
        uint mesh = Dword(0);
        uint flags = Dword(20);
        if ((flags & 0x2000) == 0)
        {
            Store(4, checked(mesh + 8));
        }
        uint vertices = Dword(4);
        uint cursor = checked(mesh + BinaryPrimitives.ReadUInt32LittleEndian(Read(mesh, 4)));
        Store(44, cursor);
        (byte[] farBytes, bool[] farKnown) = staging.ReadSnapshot(context + 8, 3);
        uint? Far(int channel) => farKnown[channel] ? (uint)farBytes[channel] << 4 : null;
        uint? far = (flags & 0xC0) != 0 ? FarColor() : null;
        int? fade = (flags & 0xC0) != 0 ? unchecked((int)Dword(12)) : null;
        NativeBattleEsunaMeshContext operands = new(flags, far, fade, unchecked((int)Dword(16)), Word(24), Word(26), Window(28), Window(36), 17, 2);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        if (thresholdQuad)
        {
            cursor = checked(cursor + 28);
            Store(44, cursor);
        }
        for (int group = thresholdQuad ? 7 : 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(Read(cursor, 4));
            cursor = checked(cursor + 4);
            Store(44, cursor);
            for (int faceIndex = 0; faceIndex < count; faceIndex++)
            {
                byte[] face = Read(cursor, Lengths[group]);
                NativeBattleModuleProjectedVertex?[] projected = new NativeBattleModuleProjectedVertex?[(group & 1) == 0 ? 3 : 4];
                System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[projected.Length] : null;
                NativeBattleModuleProjectedVertex Corner(int corner)
                {
                    if (projected[corner] is not { } value)
                    {
                        ushort index = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2));
                        byte[] point = Read(checked(vertices + (uint)index * 4), 6);
                        NativeBattleVector position = new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(4)));
                        if (world is not null)
                        {
                            world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, position), projection);
                        }
                        value = NativeBattleModuleProjection.Project45FE10(matrix, position, projection);
                        projected[corner] = value;
                    }
                    return value;
                }
                uint firstFlags = Corner(0).Flags | Corner(1).Flags | Corner(2).Flags;
                Store(60, firstFlags);
                if ((firstFlags & 0x60000) == 0)
                {
                    Store(48, unchecked((uint)NativeBattleModulePackets.Nclip(Corner(0).Screen, Corner(1).Screen, Corner(2).Screen)));
                }
                NativeBattleGfGeometryBatch emitted = group switch
                {
                    0 => NativeBattleRestorationMeshPackets.EmitF3_88E370(face, Corner, operands, colors, allocation),
                    1 => NativeBattleRestorationMeshPackets.EmitF4_88E590(face, Corner, operands, colors, allocation),
                    2 => NativeBattleRestorationMeshPackets.EmitFt3_88E820(face, Corner, operands, colors, allocation),
                    3 => NativeBattleRestorationMeshPackets.EmitFt4_88ECA0(face, Corner, operands, colors, allocation),
                    4 => NativeBattleRestorationMeshPackets.EmitG3_88F220(face, Corner, operands, colors, allocation),
                    5 => NativeBattleRestorationMeshPackets.EmitG4_88F470(face, Corner, operands, colors, allocation),
                    6 => NativeBattleRestorationMeshPackets.EmitGt3_88F750(face, Corner, operands, colors, allocation),
                    _ => NativeBattleRestorationMeshPackets.EmitGt4_88FC50(face, Corner, operands, colors, allocation)
                };
                if (emitted.Attempts.Any(attempt => attempt.Primitive is not null))
                {
                    uint average = unchecked((uint)projected.Sum(point => (int)point!.Value.Depth) * ((group & 1) == 0 ? 0x155u : 0x100u)) >> 12;
                    Store(56, unchecked((uint)Math.Max(unchecked((int)average + operands.DepthBias), 0)));
                    if (thresholdQuad && (operands.UOffset | operands.VOffset) == 0)
                    {
                        emitted = ApplyThreshold78C6D0(emitted, unchecked((short)Word(100)));
                    }
                }
                if (pilotIdentity is { } identity && world is not null)
                {
                    emitted = emitted with { Attempts = emitted.Attempts.Select(attempt => attempt.Primitive is { Words.Length: > 1 } primitive && (primitive.Words[1] & 0xE0000000) == 0x20000000 ? attempt with { Primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + (int)cursor), ResourceGeneration = unchecked(identity.ResourceGeneration * 397 ^ mesh) }, world[0], world[1], world[2], world.Length == 4 ? world[3] : default, checked((byte)world.Length)) } } : attempt).ToImmutableArray() };
                }
                attempts.AddRange(emitted.Attempts);
                allocation = checked(allocation + emitted.AllocationBytes);
                cursor = checked(cursor + (uint)Lengths[group]);
            }
            Store(44, cursor);
        }
        return new(staging, new(attempts.ToImmutable(), allocation), Far(0), Far(1), Far(2));

        byte[] Read(uint address, int length)
        {
            byte[] bytes = read(address, length);
            if (bytes.Length != length)
            {
                throw new InvalidDataException("Native The End mesh requires its exact addressed source extent.");
            }
            return bytes;
        }

        uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(context + (uint)offset, 4));
        ushort Word(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(staging.Read(context + (uint)offset, 2));
        NativeBattleTextureWindow Window(int offset) => new(unchecked((short)Word(offset)), unchecked((short)Word(offset + 2)), unchecked((short)Word(offset + 4)), unchecked((short)Word(offset + 6)));
        void Store(int offset, uint value) => staging = staging.Write(context + (uint)offset, BitConverter.GetBytes(value));

        uint FarColor()
        {
            if (farKnown.Contains(false))
            {
                throw new NotSupportedException("Native The End depth cue requires its actual retained RGB bytes.");
            }
            return farBytes[0] | ((uint)farBytes[1] << 8) | ((uint)farBytes[2] << 16);
        }
    }

    public static NativeBattleGfGeometryBatch ApplyThreshold78C6D0(NativeBattleGfGeometryBatch emitted, short threshold)
    {
        NativeBattleGfGeometryAttempt attempt = emitted.Attempts.Single();
        if (attempt.Primitive is not { } primitive)
        {
            return emitted;
        }
        if (attempt.PacketLength != 52)
        {
            throw new InvalidDataException("Native The End threshold requires its one complete GT4 packet.");
        }
        byte[] bytes = attempt.Data.ToArray();
        short[] values = Enumerable.Range(0, 4).Select(corner => unchecked((short)(bytes[4 + corner * 12] - threshold))).ToArray();
        if (values.Any(value => value < 0))
        {
            return new([attempt with { Primitive = null }], 0);
        }
        for (int corner = 0; corner < 4; corner++)
        {
            byte color = unchecked((byte)(Math.Min(values[corner], (short)8) << 4));
            bytes.AsSpan(4 + corner * 12, 3).Fill(color);
        }
        ImmutableArray<uint> words = Enumerable.Range(0, 13).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        return new([attempt with { Data = bytes.ToImmutableArray(), Primitive = primitive with { Words = words } }], emitted.AllocationBytes);
    }
}
