using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicBrothersProjection(NativeBattleWaterExecutionFrame Frame, uint Screen, uint Cue, uint Flags)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public System.Numerics.Vector3? PilotWorld { get; init; }
}
public sealed record NativeBattleCinematicBrothersModelServices(Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> DrawPrimary, Func<NativeBattleWaterExecutionFrame, ushort, uint> ResolveReference, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> PublishInput, Func<NativeBattleWaterExecutionFrame, NativeBattleActionMatrix, uint, NativeBattleCinematicBrothersProjection> Project, Func<uint> ReadFallbackOrdering, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append)
{
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}

public static class NativeBattleCinematicBrothersModel3
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicBrothersModelServices services)
    {
        frame = services.DrawPrimary(frame);
        uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        if ((frame.Arena.Read(ArenaOffset(model + 2, 1), 1)[0] & 4) == 0)
        {
            return frame;
        }
        uint reference = services.ResolveReference(frame, SlotWord(frame, 0x9C));
        NativeBattleVector angles = Vector(reference + 0x8C);
        (frame, _) = NativeBattleWaterMatrixCommands.BuildRotationB65590(frame, angles, 0);
        frame.TemporaryFrame.Copy(0xE0, 0x80, 32);
        int sx = unchecked((short)SlotWord(frame, 0x8C)) << 4;
        int sy = unchecked((short)SlotWord(frame, 0x8E)) << 4;
        int sz = unchecked((short)SlotWord(frame, 0x90)) << 4;
        frame = frame.WriteTemporaryDword(0x60, unchecked((uint)sx)).WriteTemporaryDword(0x64, unchecked((uint)sy)).WriteTemporaryDword(0x68, unchecked((uint)sz));
        NativeBattleActionMatrix parent = NativeBattlePoseMatrices.Scale56BEF0(ReadTemporaryMatrix(frame, 0x80), sx, sy, sz);
        for (int index = 0; index < 9; index++)
        {
            frame = frame.WriteTemporaryWord(0x80 + index * 2, unchecked((ushort)parent.Rotation[index]));
        }
        NativeBattleVector cached = Vector(reference + 0x94);
        frame = frame.WriteTemporaryDword(0x94, unchecked((uint)(int)cached.X)).WriteTemporaryDword(0x98, unchecked((uint)(int)cached.Y)).WriteTemporaryDword(0x9C, unchecked((uint)(int)cached.Z));
        parent = parent with { X = cached.X, Y = cached.Y, Z = cached.Z };
        uint parameters = Dword(model + 0x7C);
        short depth = SignedWord(parameters);
        short height = SignedWord(parameters + 2);
        return DrawRingB67040(frame, model + 0x10, parent, depth, height, 128, services);

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        short SignedWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        NativeBattleVector Vector(uint address) => new(SignedWord(address), SignedWord(address + 2), SignedWord(address + 4));
    }

    public static NativeBattleWaterExecutionFrame DrawRingB67040(NativeBattleWaterExecutionFrame frame, uint descriptor, NativeBattleActionMatrix? parent, int depth, short height, int intensity, NativeBattleCinematicBrothersModelServices services)
    {
        uint table = Dword(descriptor + 4);
        uint skeleton = Dword(table);
        int skeletonOffset = ArenaOffset(skeleton, 16);
        int count = frame.Arena.Read(skeletonOffset, 1)[0];
        if (parent is not null)
        {
            Restore();
            for (int index = 0; index < count; index++)
            {
                int offset = ArenaOffset(unchecked(skeleton + 32u + (uint)index * 48), 32);
                NativeBattleActionMatrix local = NativeBattleArenaPose.ReadMatrix(frame.Arena, offset);
                frame = frame with { Arena = NativeBattleArenaPose.WriteMatrix(frame.Arena, offset, NativeBattlePoseMatrices.Compose56C2F0(parent, local), true) };
            }
        }
        frame = DrawPreparedRing(frame, count, unchecked((int)Dword(skeleton + 0x34)), unchecked((int)Dword(skeleton + 0x3C)), (current, index) => NativeBattleArenaPose.ReadMatrix(current.Arena, ArenaOffset(unchecked(skeleton + 32u + (uint)index * 48), 32)), (current, index) => BinaryPrimitives.ReadInt16LittleEndian(current.Arena.Read(ArenaOffset(unchecked(skeleton + 18u + (uint)index * 48), 2), 2)), new(0, 0, 0), depth, height, intensity, NativeBattleCameraMath.Sine(512), null, services);
        if (parent is not null)
        {
            Restore();
        }
        return frame;

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));

        void Restore()
        {
            NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(frame.Arena, skeletonOffset, count);
            frame = frame with { Arena = NativeBattleArenaPose.RestoreBoneMatrices(frame.Arena, skeletonOffset, pose) };
        }
    }

    public static NativeBattleWaterExecutionFrame DrawPreparedRing(NativeBattleWaterExecutionFrame frame, int count, int rootX, int rootZ, Func<NativeBattleWaterExecutionFrame, int, NativeBattleActionMatrix> readBone, Func<NativeBattleWaterExecutionFrame, int, short> readLength, NativeBattleVector centerOffset, int depth, short height, int intensity, int diagonalFactor, Func<NativeBattleWaterExecutionFrame, uint>? readColor, NativeBattleCinematicBrothersModelServices services, bool storeCue = true)
    {
        int maxX = rootX;
        int maxZ = rootZ;
        int minX = unchecked(maxX - 500);
        int minZ = unchecked(maxZ - 500);
        maxX = unchecked(maxX + 500);
        maxZ = unchecked(maxZ + 500);
        frame = frame.WriteTemporaryDword(0xD0, 0);
        for (int index = 1; index < count; index++)
        {
            NativeBattleActionMatrix bone = readBone(frame, index);
            frame = frame.WriteTemporaryWord(0xD4, unchecked((ushort)readLength(frame, index)));
            NativeBattleVector input = TemporaryVector(0xD0);
            frame = services.PublishInput(frame, frame.TemporaryFrame.Address + 0xD0);
            NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(bone, input);
            NativeBattleVector end = new((short)Math.Clamp(mac.X, short.MinValue, short.MaxValue), (short)Math.Clamp(mac.Y, short.MinValue, short.MaxValue), (short)Math.Clamp(mac.Z, short.MinValue, short.MaxValue));
            StoreVector(0xD8, end);
            frame = frame.WriteTemporaryDword(0xFC, mac.Flags);
            if (end.X < minX)
            {
                minX = end.X;
            }
            else if (end.X > maxX)
            {
                maxX = end.X;
            }
            if (end.Z < minZ)
            {
                minZ = end.Z;
            }
            else if (end.Z > maxZ)
            {
                maxZ = end.Z;
            }
        }
        int centerX = unchecked(minX + maxX) / 2;
        int centerZ = unchecked(minZ + maxZ) / 2;
        int radiusX = unchecked(maxX - centerX);
        int radiusZ = unchecked(maxZ - centerZ);
        StoreVector(0xD0, new(unchecked((short)(centerX + centerOffset.X)), 0, unchecked((short)(centerZ + centerOffset.Z))));
        byte[] rotation = frame.Core.Read(0x27979E8, 18);
        NativeBattleActionMatrix view = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)CoreDword(frame, 0x27979FC)), unchecked((int)CoreDword(frame, 0x2797A00)), unchecked((int)CoreDword(frame, 0x2797A04)), 4096);
        frame = services.PublishInput(frame, frame.TemporaryFrame.Address + 0xD0);
        NativeBattleFullMacVertex centerMac = NativeBattleGfMatrices.TransformMac460840(view, TemporaryVector(0xD0));
        NativeBattleVector center = new((short)Math.Clamp(centerMac.X, short.MinValue, short.MaxValue), (short)Math.Clamp(centerMac.Y, short.MinValue, short.MaxValue), (short)Math.Clamp(centerMac.Z, short.MinValue, short.MaxValue));
        StoreVector(0xD8, center);
        frame = frame.WriteTemporaryDword(0xFC, centerMac.Flags);
        NativeBattleActionMatrix camera = view with { X = center.X, Y = center.Y, Z = center.Z };
        int sine = diagonalFactor;
        int diagonalZ = unchecked(sine * radiusZ) >> 12;
        int diagonalX = unchecked(sine * radiusX) >> 12;
        (int X, int Z)[] points = [(0, radiusZ), (diagonalX, diagonalZ), (radiusX, 0), (diagonalX, unchecked(-diagonalZ)), (0, unchecked(-radiusZ)), (unchecked(-diagonalX), unchecked(-diagonalZ)), (unchecked(-radiusX), 0), (unchecked(-diagonalX), diagonalZ), (0, 0)];
        for (int index = 0; index < points.Length; index++)
        {
            StoreVector(0xB0 + index * 8, new(unchecked((short)points[index].X), height, unchecked((short)points[index].Z)));
        }
        System.Numerics.Vector3?[] pilotWorld = new System.Numerics.Vector3?[9];
        for (int index = 0; index < 9; index++)
        {
            uint address = unchecked(frame.TemporaryFrame.Address + 0xB0u + (uint)index * 8);
            frame = services.PublishInput(frame, address);
            NativeBattleCinematicBrothersProjection projected = services.Project(frame, camera, address);
            pilotWorld[index] = projected.PilotWorld;
            frame = projected.Frame.WriteTemporaryDword(0xB0 + index * 8, projected.Screen).WriteTemporaryDword(0xFC, projected.Flags);
            if (storeCue)
            {
                frame = frame.WriteTemporaryDword(0xB4 + index * 8, projected.Cue);
            }
        }
        uint Screen(int index) => frame.ReadTemporaryDword(0xB0 + index * 8);
        if (NativeBattleModulePackets.Nclip(Screen(0), Screen(2), Screen(4)) > 0)
        {
            uint ordering = depth == -1 ? unchecked(services.ReadFallbackOrdering() + 0x4064u) : unchecked(ReadControlOrdering() + (unchecked((uint)depth) & 0x3FFC));
            uint output = CoreDword(frame, CoreDword(frame, 0x27973EC) + 0x7C);
            uint color = readColor is null ? 0x32000000u | (unchecked((uint)intensity) & 255) * 0x10101u : readColor(frame);
            ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>(8);
            for (int index = 0; index < 8; index++)
            {
                ImmutableArray<uint> words = [0x08000000, 0xE1000040, 0, color, Screen(8), 0, Screen(index), 0, Screen((index + 1) & 7)];
                byte[] bytes = new byte[36];
                for (int word = 0; word < words.Length; word++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(word * 4), words[word]);
                }
                ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, 36).Select(offset => offset >= 3 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
                NativeBattleGeometryPrimitive primitive = new(0, words, [0, 0, 0, 0]) { PhysicalWriteMask = mask };
                if (services.PilotIdentity is { } identity && pilotWorld[8] is { } a && pilotWorld[index] is { } b && pilotWorld[(index + 1) & 7] is { } c)
                {
                    primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked((int)0x70000000 + index) }, a, b, c, default, 3) { SourceDepths = System.Numerics.Vector4.Zero, UseSourceDepths = true } };
                }
                attempts.Add(new(index * 36, bytes.ToImmutableArray(), mask, primitive));
            }
            frame = services.Append(frame, output, ordering, new(attempts.MoveToImmutable(), 288));
            frame = WriteCoreDword(frame, CoreDword(frame, 0x27973EC) + 0x7C, unchecked(output + 288));
        }
        return frame;

        uint ReadControlOrdering()
        {
            uint address = unchecked(CoreDword(frame, 0x27973B8) + 0x4C);
            uint relative = unchecked(address - frame.TemporaryFrame.Address);
            return relative <= 0x17C ? frame.ReadTemporaryDword((int)relative) : CoreDword(frame, address);
        }

        NativeBattleVector TemporaryVector(int offset) => new(unchecked((short)frame.ReadTemporaryWord(offset)), unchecked((short)frame.ReadTemporaryWord(offset + 2)), unchecked((short)frame.ReadTemporaryWord(offset + 4)));
        void StoreVector(int offset, NativeBattleVector value) => frame = frame.WriteTemporaryWord(offset, unchecked((ushort)value.X)).WriteTemporaryWord(offset + 2, unchecked((ushort)value.Y)).WriteTemporaryWord(offset + 4, unchecked((ushort)value.Z));
    }

    private static NativeBattleActionMatrix ReadTemporaryMatrix(NativeBattleWaterExecutionFrame frame, int offset)
    {
        byte[] rotation = frame.ReadTemporary(offset, 18);
        return new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), 0, 0, 0, 4096);
    }
}
