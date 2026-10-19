using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicModelDraw(NativeBattleWaterExecutionFrame Frame, int ProjectedBytesWritten, NativeBattleGfGeometryBatch Packets)
{
    public uint? SourceCursor { get; init; }
}

public static partial class NativeBattleCinematicModel
{
    public static NativeBattleWaterExecutionFrame PrepareDraw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicModelServices services)
    {
        uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        frame = AdvanceAndCompose(frame, model, services);
        model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        uint manager = CoreDword(frame, 0x27973EC);
        uint work = CoreDword(frame, unchecked(manager + 0x74));
        return frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(model + 0x44), 4), BitConverter.GetBytes(work)) };
    }

    public static NativeBattleCinematicModelDraw Render(NativeBattleWaterExecutionFrame frame, uint model, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, Func<uint, int> resolveOrderingTable)
    {
        uint table = Dword(unchecked(model + 0x14));
        uint skeleton = Dword(table);
        uint geometry = Dword(unchecked(table + 4));
        int objects = unchecked((int)Dword(geometry));
        if (objects != 1)
        {
            throw new NotSupportedException($"Native cinematic embedded geometry object count {objects} requires its selected rendering contract.");
        }
        uint mask = Dword(unchecked(model + 0x60));
        if ((mask & 1) == 0)
        {
            return new(frame, 0, new([], 0));
        }
        int boneCount = frame.Arena.Read(ArenaOffset(skeleton, 1), 1)[0];
        ImmutableArray<ushort> counts = default;
        int facePayload = 0;
        int sourceEnd = 0;
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => frame.Arena.Read(ArenaOffset(unchecked(geometry + (uint)offset), length), length), boneCount, (_, actualCounts, payload, end) =>
        {
            counts = actualCounts;
            facePayload = payload;
            sourceEnd = end;
        });
        if (counts.IsDefault || counts[2] != 0 || counts[3] != 0)
        {
            throw new NotSupportedException("Native cinematic embedded colored geometry groups require their selected rendering contract.");
        }
        NativeBattleRawMesh mesh = meshes[0];
        int length = checked(mesh.Vertices.Length * 8);
        uint work = Dword(unchecked(model + 0x44));
        int workOffset = ArenaOffset(work, length);
        if (Overlaps(work, length, geometry, sourceEnd) || Overlaps(work, length, skeleton, checked(16 + boneCount * 48)) || Overlaps(work, length, model, 0x80) || Overlaps(model, 0x80, geometry, sourceEnd))
        {
            throw new NotSupportedException("Native cinematic embedded rendering requires nonoverlapping current source, model and projection work owners.");
        }
        ImmutableArray<NativeBattleActionMatrix>.Builder bones = ImmutableArray.CreateBuilder<NativeBattleActionMatrix>(boneCount);
        for (int index = 0; index < boneCount; index++)
        {
            bones.Add(NativeBattleArenaPose.ReadMatrix(frame.Arena, ArenaOffset(unchecked(skeleton + 0x20u + 0x30u * (uint)index), 32)));
        }
        NativeBattleGeometryViewport viewport = new(Word(unchecked(model + 0x54)), Word(unchecked(model + 0x56)), Word(unchecked(model + 0x58)), Word(unchecked(model + 0x5A)));
        uint color = Dword(unchecked(model + 0x5C));
        short cue = Word(unchecked(model + 0x64));
        uint orderingTable = frame.ReadTemporaryDword(0x14C);
        NativeBattleRawModelBodyDraw draw = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, bones.MoveToImmutable(), projection, color, 1, new byte[length].ToImmutableArray(), colors, 4, viewport, resolveOrderingTable(orderingTable), cue);
        if (draw.ProjectedBytesWritten != length)
        {
            throw new InvalidDataException("Native cinematic embedded projection did not publish its complete output records.");
        }
        NativeBattleEffectArena arena = frame.Arena.Write(workOffset, draw.ProjectedScratch.AsSpan());
        byte[] header = new byte[8];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(index * 2), counts[index]);
        }
        arena = arena.Write(ArenaOffset(unchecked(model + 0x48), 8), header).Write(ArenaOffset(unchecked(model + 0x40), 4), BitConverter.GetBytes(unchecked(geometry + (uint)facePayload)));
        return new(frame with { Arena = arena }, length, draw.Packets) { SourceCursor = unchecked(geometry + (uint)sourceEnd) };

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
    }

    private static bool Overlaps(uint first, int firstLength, uint second, int secondLength) => (long)first < (long)second + secondLength && (long)second < (long)first + firstLength;
}
