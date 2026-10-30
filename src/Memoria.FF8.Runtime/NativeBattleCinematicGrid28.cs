using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicGridValue(NativeBattleWaterExecutionFrame Frame, uint Value);
public sealed record NativeBattleCinematicGridProjection(NativeBattleWaterExecutionFrame Frame, int QuarterDepth)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public System.Numerics.Vector3? PilotWorld { get; init; }
}
public sealed record NativeBattleCinematicGrid28Services(Func<NativeBattleWaterExecutionFrame, short, NativeBattleWaterExecutionFrame> TexturePage, Func<NativeBattleWaterExecutionFrame, short, NativeBattleCinematicGridValue> TextureClut, Func<NativeBattleWaterExecutionFrame, short, uint> ResolveSlot, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareBasis, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> SetLighting, Func<NativeBattleWaterExecutionFrame, short, short, int, NativeBattleWaterExecutionFrame> SetDepthCue, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> SetFarColor, Func<NativeBattleWaterExecutionFrame, uint, uint, uint, uint, uint, uint, NativeBattleCinematicGridProjection> ProjectNormal, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append)
{
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
    public Func<long>? PilotResourceGeneration { get; init; }
}

/// <summary>A textured grid with projected normals, interpolated colors and per-cell ordering.</summary>
public static class NativeBattleCinematicGrid28
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicGrid28Services services)
        => DrawCore(frame, services, 256, 16, 255, []);

    internal static NativeBattleWaterExecutionFrame DrawApocalypse(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicGrid28Services services, ImmutableArray<byte> rowV)
        => DrawCore(frame, services, 512, 32, 511, rowV);

    internal static NativeBattleWaterExecutionFrame DrawEnemy67(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicGrid28Services services)
    {
        return DrawCore(frame, services, 512, 32, 511, [], enemyUvPolicy: true);
    }

    private static NativeBattleWaterExecutionFrame DrawCore(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicGrid28Services services, int coordinateStep, int textureStep, int positionMask, ImmutableArray<byte> rowV, bool enemyUvPolicy = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        uint payload = SlotDword(0xB8);
        frame = frame.WriteTemporaryDword(0x60, unchecked((uint)SignedWord(unchecked(payload + 4))));
        frame = frame.WriteTemporaryDword(0x64, unchecked((uint)SignedWord(unchecked(payload + 6))));
        short texture = SignedWord(unchecked(payload + 2));
        frame = services.TexturePage(frame, texture);
        frame = frame.WriteTemporaryDword(0x68, frame.ReadTemporaryDword(0xFC));
        NativeBattleCinematicGridValue clut = services.TextureClut(frame, texture);
        frame = clut.Frame.WriteTemporaryDword(0x6C, clut.Value);
        int columns = unchecked((int)frame.ReadTemporaryDword(0x60));
        int rows = unchecked((int)frame.ReadTemporaryDword(0x64));
        int firstU = unchecked(-(columns / 2 * textureStep) - ((SlotWord(frame, 0x98) >> 4) & (enemyUvPolicy ? 0xE0 : textureStep * 15)));
        frame = frame.WriteTemporaryDword(0x70, unchecked((uint)firstU));
        int firstV = unchecked(-(rows / 2 * textureStep) - ((SlotWord(frame, 0x94) >> 4) & (textureStep * 15)));
        frame = frame.WriteTemporaryDword(0x74, unchecked((uint)firstV));
        frame = frame.WriteTemporaryDword(0x78, unchecked((uint)((columns + 1) * 12)));
        uint reference = services.ResolveSlot(frame, SignedWord(payload));
        if (services.PilotIdentity is { Effect: 219 } sourceIdentity)
        {
            services = services with { PilotIdentity = sourceIdentity with { ResourceGeneration = unchecked(((long)payload * 397 ^ reference) * 397 ^ (services.PilotResourceGeneration?.Invoke() ?? 0)) } };
        }
        frame = frame.WriteTemporaryDword(0x7C, (ReadSlot(frame, 0xDE, 1)[0] & 0x20) != 0 ? SlotDword(0xC0) : uint.MaxValue);
        ushort savedScale = SlotWord(frame, 0x8E);
        frame = WriteSlotWord(frame, 0x8E, 0x1000);
        frame = services.PrepareBasis(frame);
        frame = WriteSlotWord(frame, 0x8E, savedScale);
        frame = services.SetLighting(frame, SlotDword(0xCC));
        short far = SignedWord(unchecked(payload + 10));
        short near = SignedWord(unchecked(payload + 8));
        frame = services.SetDepthCue(frame, near, far, 512);
        frame = services.SetFarColor(frame, ReferenceColor(unchecked(reference + 0xCC)));
        Dictionary<uint, System.Numerics.Vector3>? pilotVertices = services.PilotIdentity is null ? null : [];
        uint work = ManagerDword(0x74);
        rows = unchecked((int)frame.ReadTemporaryDword(0x64));
        int positionX = positionMask == 255 ? ReadSlot(frame, 0x94, 1)[0] : SlotWord(frame, 0x94) & positionMask;
        frame = frame.WriteTemporaryWord(0xC0, unchecked((ushort)(positionX - rows / 2 * coordinateStep)));
        int positionZ = positionMask == 255 ? ReadSlot(frame, 0x98, 1)[0] : SlotWord(frame, 0x98) & positionMask;
        int initialZ = unchecked(positionZ - unchecked((int)frame.ReadTemporaryDword(0x60)) / 2 * coordinateStep);
        frame = frame.WriteTemporaryWord(0xC2, 0);
        frame = frame.WriteTemporaryWord(0xC8, unchecked((ushort)(SlotWord(frame, 0x96) << 4)));
        frame = frame.WriteTemporaryWord(0xCA, 0);
        frame = frame.WriteTemporaryWord(0xCC, 0);
        frame = frame.WriteTemporaryDword(0xE0, 0x3C808080);
        uint command = (SlotDword(0xCC) & 0x02000000) | 0x3C000000;
        for (int row = 0; row <= rows; row++)
        {
            frame = frame.WriteTemporaryWord(0xC4, unchecked((ushort)initialZ));
            columns = unchecked((int)frame.ReadTemporaryDword(0x60));
            for (int column = 0; column <= columns; column++)
            {
                uint temporary = CoreDword(frame, 0x2797624);
                NativeBattleCinematicGridProjection projection = services.ProjectNormal(frame, unchecked(temporary + 0xC0), unchecked(temporary + 0xC8), unchecked(temporary + 0xE0), work, unchecked(work + 8), unchecked(temporary + 0xFC));
                frame = projection.Frame;
                if (pilotVertices is not null && projection.PilotWorld is { } world)
                {
                    pilotVertices[work] = world;
                }
                uint color = Dword(unchecked(work + 8));
                StoreWord(unchecked(work + 4), unchecked((ushort)projection.QuarterDepth));
                StoreDword(unchecked(work + 8), color | command);
                work = unchecked(work + 12);
                frame = frame.WriteTemporaryWord(0xC4, unchecked((ushort)(frame.ReadTemporaryWord(0xC4) + coordinateStep)));
            }
            frame = frame.WriteTemporaryWord(0xC0, unchecked((ushort)(frame.ReadTemporaryWord(0xC0) + coordinateStep)));
        }
        uint top = ManagerDword(0x74);
        uint bottom = unchecked(top + frame.ReadTemporaryDword(0x78));
        uint output = ManagerDword(0x7C);
        rows = unchecked((int)frame.ReadTemporaryDword(0x64));
        int v = unchecked((int)frame.ReadTemporaryDword(0x74));
        if (!rowV.IsDefaultOrEmpty)
        {
            v &= 15;
        }
        for (int row = 0; row < rows; row++)
        {
            columns = unchecked((int)frame.ReadTemporaryDword(0x60));
            int u = unchecked((int)frame.ReadTemporaryDword(0x70));
            for (int column = 0; column < columns; column++)
            {
                byte[] data = new byte[52];
                data[3] = 12;
                PutDword(4, Dword(unchecked(top + 8)));
                PutDword(0x10, Dword(unchecked(top + 20)));
                PutDword(0x1C, Dword(unchecked(bottom + 8)));
                PutDword(0x28, Dword(unchecked(bottom + 20)));
                PutWord(0x1A, unchecked((ushort)frame.ReadTemporaryDword(0x68)));
                PutWord(0xE, unchecked((ushort)frame.ReadTemporaryDword(0x6C)));
                if (enemyUvPolicy)
                {
                    u &= 0x60;
                }
                data[0x24] = data[0xC] = unchecked((byte)u);
                u = unchecked(u + textureStep);
                data[0x30] = data[0x18] = (u & (enemyUvPolicy ? 0x70 : 0xF0)) == 0 ? enemyUvPolicy ? (byte)0x7F : (byte)0xFF : unchecked((byte)u);
                data[0x19] = data[0xD] = rowV.IsDefaultOrEmpty ? unchecked((byte)v) : rowV[v];
                data[0x31] = data[0x25] = rowV.IsDefaultOrEmpty ? (unchecked(v + textureStep) & 0xF0) == 0 ? (byte)255 : unchecked((byte)(v + textureStep)) : rowV[v + 1];
                PutDword(8, Dword(top));
                PutDword(0x14, Dword(unchecked(top + 12)));
                PutDword(0x20, Dword(bottom));
                PutDword(0x2C, Dword(unchecked(bottom + 12)));
                int depth = unchecked((int)frame.ReadTemporaryDword(0x7C));
                if (depth < 0)
                {
                    depth = (SignedWord(unchecked(bottom + 16)) + SignedWord(unchecked(bottom + 4)) + SignedWord(unchecked(top + 16)) + SignedWord(unchecked(top + 4))) / 4;
                }
                uint ordering = unchecked(frame.ReadTemporaryDword(0x14C) + ((uint)depth & 0xFFFFFFFC));
                ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, 52).Select(index => index >= 3 && index is not (0x26 or 0x27 or 0x32 or 0x33) ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
                ImmutableArray<uint> words = Enumerable.Range(0, 13).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
                NativeBattleGeometryPrimitive primitive = new(0, words, []) { PhysicalWriteMask = mask };
                if (services.PilotIdentity is { } identity && pilotVertices is not null && pilotVertices.TryGetValue(top, out System.Numerics.Vector3 a) && pilotVertices.TryGetValue(top + 12, out System.Numerics.Vector3 b) && pilotVertices.TryGetValue(bottom, out System.Numerics.Vector3 c) && pilotVertices.TryGetValue(bottom + 12, out System.Numerics.Vector3 d))
                {
                    primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(row * 65537 + column) }, a, b, c, d, 4) };
                }
                frame = services.Append(frame, output, ordering, new([new(0, data.ToImmutableArray(), mask, primitive)], 52));
                top = unchecked(top + 12);
                bottom = unchecked(bottom + 12);
                output = unchecked(output + 52);

                void PutDword(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
                void PutWord(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
            }
            v = rowV.IsDefaultOrEmpty ? unchecked(v + textureStep) : (v + 1) & 15;
            top = unchecked(top + 12);
            bottom = unchecked(bottom + 12);
        }
        return WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), output);

        uint ReferenceColor(uint address)
        {
            if (address >= frame.TemporaryFrame.Address && (long)address - frame.TemporaryFrame.Address <= 0x17C)
            {
                return frame.ReadTemporaryDword(checked((int)(address - frame.TemporaryFrame.Address)));
            }
            if (address >= NativeBattleScriptedEffectMemory.Address && (long)address - NativeBattleScriptedEffectMemory.Address <= NativeBattleScriptedEffectMemory.Length - 4)
            {
                return CoreDword(frame, address);
            }
            return Dword(address);
        }

        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        uint ManagerDword(uint offset) => CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + offset));
        byte[] Read(uint address, int length) => frame.Arena.Read(ArenaOffset(address, length), length);
        short SignedWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(Read(address, 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

        void StoreWord(uint address, ushort value)
        {
            byte[] data = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(data, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), data) };
        }

        void StoreDword(uint address, uint value)
        {
            byte[] data = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(data, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), data) };
        }
    }
}
