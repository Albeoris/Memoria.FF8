using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleEffect283BodyBank
{
    ProjectionScratch,
    Common
}

public sealed record NativeBattleEffect283BodyWrite(NativeBattleEffect283BodyBank Bank, NativeBattleGfGeometryAttempt Attempt);
public sealed record NativeBattleEffect283BodyBatch(ImmutableArray<NativeBattleEffect283BodyWrite> Writes, int CommonAllocationBytes);
public sealed record NativeBattleEffect283BodyDrawingStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary124, NativeBattleEffectArena Scratch, NativeBattleEffect283BodyBatch Batch)
{
    public short MaximumDeviation => BinaryPrimitives.ReadInt16LittleEndian(Temporary124.Read(0x76, 2));
}

public static partial class NativeBattleEffect283BodyDrawing
{
    public static NativeBattleEffect283BodyDrawingStep DrawBody(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary124, Func<uint, int, NativeBattleEffectArena> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, NativeBattleEffectArena scratch, uint scratchAddress, int scratchLength, ImmutableArray<byte> distanceBytes, ImmutableArray<ushort> radialWords, int tableBaseBucket = 17, byte depthShift = 0)
    {
        return DrawBodyWithPresentation(memory, temporary124, readGeometry, geometryAddress, bones, projection, controls, scratch, scratchAddress, scratchLength, distanceBytes, radialWords, tableBaseBucket, depthShift, null);
    }

    public static NativeBattleEffect283BodyDrawingStep DrawBodyWithPresentation(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary124, Func<uint, int, NativeBattleEffectArena> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, NativeBattleEffectArena scratch, uint scratchAddress, int scratchLength, ImmutableArray<byte> distanceBytes, ImmutableArray<ushort> radialWords, int tableBaseBucket = 17, byte depthShift = 0, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (U32(temporary124, 0x24) != scratchAddress || scratchLength < 0 || scratchLength > NativeBattleEffectArena.Length || scratchLength % 8 != 0 || depthShift > 31 || distanceBytes.IsDefault || distanceBytes.Length != 16513 || radialWords.IsDefault || radialWords.Length != 128)
        {
            throw new InvalidDataException("Effect283 body requires its actual bounded projection workspace and radial tables.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect283 body requires current GTE geometry."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect283 body requires current GTE arithmetic."));
        var writes = ImmutableArray.CreateBuilder<NativeBattleEffect283BodyWrite>();
        int common = 0;
        int objects = ReadInt(geometryAddress);
        for (int index = 0; index < objects; index++)
        {
            uint cursor = unchecked(geometryAddress + (uint)ReadInt(checked(geometryAddress + 4 + (uint)(index * 4))));
            if ((U32(temporary124, 0x38) & (1u << (index & 31))) == 0)
            {
                continue;
            }
            int groups = ReadWord(cursor);
            cursor += 2;
            int vertices = 0;
            Dictionary<int, System.Numerics.Vector3>? pilotWorld = pilotIdentity.HasValue ? new() : null;
            for (int group = 0; group < groups; group++)
            {
                int bone = ReadWord(cursor);
                cursor += 2;
                if ((uint)bone >= (uint)bones.Length)
                {
                    throw new InvalidDataException("Effect283 body selected a bone outside its current rig.");
                }
                NativeBattlePackedMatrix32 matrix = bones[bone];
                short? padding = matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
                NativeBattleActionMatrix matrixValue = matrix.ReadMatrix();
                registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrixValue, true, true, padding) };
                int count = ReadWord(cursor);
                cursor += 2;
                if (count < 0)
                {
                    throw new NotSupportedException("Effect283 body negative vertex count reaches an unsupported native loop.");
                }
                for (int vertex = 0; vertex < count; vertex++)
                {
                    int destination = checked(vertices * 8);
                    if (destination > scratchLength - 8)
                    {
                        throw new InvalidDataException("Effect283 body vertices leave their current renderer scratch capacity.");
                    }
                    byte[] xyz = readGeometry(cursor, 6).Read(0, 6);
                    NativeBattleVector inputPoint = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
                    registers = NativeBattleGeometry.LoadVector(registers, 0, inputPoint, 0);
                    registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                    uint screen = registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Effect283 body requires current SXY2.");
                    uint depth = unchecked((uint)(unchecked((int)(registers.Geometry.Sz[3] ?? throw new NotSupportedException("Effect283 body requires current SZ3."))) >> 2));
                    temporary124 = Dword(Dword(temporary124, 0x3C, screen), 0x40, depth);
                    if (unchecked((ushort)depth) == 0)
                    {
                        scratch = Word(scratch, destination + 6, 0x10);
                        writes.Add(new(NativeBattleEffect283BodyBank.ProjectionScratch, new(destination + 6, [0x10, 0], [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known], null)));
                    }
                    else
                    {
                        int dx = unchecked((short)(unchecked((short)screen) - S16(temporary124, 0x72)));
                        int dy = unchecked((short)(unchecked((short)(screen >> 16)) - S16(temporary124, 0x74)));
                        int absX = NativeMagnitude(dx);
                        int absY = NativeMagnitude(dy);
                        short maximum = S16(temporary124, 0x76);
                        if (maximum < absX)
                        {
                            maximum = unchecked((short)absX);
                        }
                        if (maximum < absY)
                        {
                            maximum = unchecked((short)absY);
                        }
                        temporary124 = Word(temporary124, 0x76, unchecked((ushort)maximum));
                        int distance = distanceBytes[(absY << 7) + absX];
                        int threshold = S16(temporary124, 0x70);
                        if (distance != 0 && distance < threshold)
                        {
                            int factor = radialWords[(distance << 7) / threshold];
                            short x = unchecked((short)((unchecked(dx * factor) >> 12) + S16(temporary124, 0x72)));
                            short y = unchecked((short)((unchecked(dy * factor) >> 12) + S16(temporary124, 0x74)));
                            temporary124 = Word(Word(temporary124, 0x3C, unchecked((ushort)x)), 0x3E, unchecked((ushort)y));
                        }
                        int px = S16(temporary124, 0x3C);
                        int py = S16(temporary124, 0x3E);
                        if (pilotWorld is not null)
                        {
                            uint deformedScreen = unchecked((ushort)px) | (uint)unchecked((ushort)py) << 16;
                            pilotWorld[vertices] = NativeBattleEffect32Presentation.DeformedWorld(matrixValue, inputPoint, screen, deformedScreen, projection);
                        }
                        byte clip = temporary124.Read(0x42, 1)[0];
                        if (px < (S16(temporary124, 0x2C) << 3))
                        {
                            clip |= 1;
                        }
                        else if (px >= (S16(temporary124, 0x30) << 3))
                        {
                            clip |= 2;
                        }
                        if (py < (S16(temporary124, 0x2E) << 3))
                        {
                            clip |= 4;
                        }
                        else if (py >= (S16(temporary124, 0x32) << 3))
                        {
                            clip |= 8;
                        }
                        temporary124 = temporary124.Write(0x42, [clip]);
                        scratch = NativeBattleEffectNodeContextMemory.Copy(temporary124, 0x3C, scratch, destination, 8);
                        (byte[] data, bool[] known) = temporary124.ReadSnapshot(0x3C, 8);
                        writes.Add(new(NativeBattleEffect283BodyBank.ProjectionScratch, new(destination, data.ToImmutableArray(), known.Select(value => value ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Unknown).ToImmutableArray(), null)));
                    }
                    vertices++;
                    cursor += 6;
                }
            }
            cursor = (cursor + 3) & ~3u;
            temporary124 = temporary124.Write(0x28, readGeometry(cursor, 4).Read(0, 4));
            cursor += 12;
            temporary124 = Dword(temporary124, 0x20, cursor);
            DrawFaces(false);
            DrawFaces(true);

            void DrawFaces(bool quad)
            {
                int counterOffset = quad ? 0x2A : 0x28;
                ushort remaining = U16(temporary124, counterOffset);
                temporary124 = Word(temporary124, counterOffset, unchecked((ushort)(remaining - 1)));
                while (remaining != 0)
                {
                    byte[] source = readGeometry(cursor, quad ? 20 : 16).Read(0, quad ? 20 : 16);
                    NativeBattleEffectArena[] corners = new NativeBattleEffectArena[quad ? 4 : 3];
                    byte clipOr = 0;
                    byte clipAnd = 255;
                    for (int corner = 0; corner < corners.Length; corner++)
                    {
                        int selected = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(corner * 2)) & 0xFFF;
                        if (selected >= vertices)
                        {
                            throw new InvalidDataException("Effect283 body face leaves its current projected object.");
                        }
                        corners[corner] = NativeBattleEffectNodeContextMemory.Copy(scratch, selected * 8, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
                        temporary124 = NativeBattleEffectNodeContextMemory.Copy(corners[corner], 0, temporary124, 0x3C + corner * 8, 8);
                        byte clip = corners[corner].Read(6, 1)[0];
                        clipOr |= clip;
                        clipAnd &= clip;
                    }
                    if (clipOr < 0x10 && clipAnd == 0)
                    {
                        registers = registers with { Geometry = registers.Geometry with { Sxy = Enumerable.Range(0, 3).Select(corner => (uint?)U32(corners[corner], 0)).ToImmutableArray() } };
                        registers = NativeBattleGeometry.Clip45EE10(registers);
                        uint area = registers.Geometry.Mac0 ?? throw new NotSupportedException("Effect283 body requires current NCLIP.");
                        temporary124 = Dword(temporary124, 0x5C, area);
                        if (unchecked((int)area) >= 0)
                        {
                            ImmutableArray<uint?> depths = registers.Geometry.Sz;
                            for (int corner = 0; corner < corners.Length; corner++)
                            {
                                depths = depths.SetItem(quad ? corner : corner + 1, U16(corners[corner], 4));
                            }
                            registers = registers with { Geometry = registers.Geometry with { Sz = depths } };
                            registers = quad ? NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect283 body requires current ZSF4.")) : NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect283 body requires current ZSF3."));
                            uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Effect283 body requires current averaged depth.");
                            temporary124 = Dword(temporary124, 0x64, depth);
                            int bucket = unchecked(tableBaseBucket + (unchecked((int)depth) >> depthShift));
                            EmitFaces(source, quad, corners, temporary124, registers.Geometry, bucket, common, writes, pilotIdentity is { } owner ? owner with { Primitive = unchecked(owner.Primitive * 65537 + (int)cursor) } : null, pilotWorld is null ? null : Enumerable.Range(0, corners.Length).Select(corner => pilotWorld[BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(corner * 2)) & 0xFFF]).ToArray());
                            common += quad ? 72 : 60;
                        }
                    }
                    cursor += quad ? 20u : 16u;
                    remaining = U16(temporary124, counterOffset);
                    temporary124 = Word(temporary124, counterOffset, unchecked((ushort)(remaining - 1)));
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary124, scratch, new(writes.ToImmutable(), common));

        int ReadInt(uint address) => BinaryPrimitives.ReadInt32LittleEndian(readGeometry(address, 4).Read(0, 4));
        short ReadWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(readGeometry(address, 2).Read(0, 2));
    }

    private static int NativeMagnitude(int value)
    {
        int magnitude = value < 0 ? -value : value;
        if (unchecked((short)magnitude) < 128)
        {
            if (magnitude > short.MaxValue)
            {
                throw new NotSupportedException("Effect283 body displacement leaves its reached projection domain.");
            }
            return magnitude;
        }
        return 128;
    }
}
