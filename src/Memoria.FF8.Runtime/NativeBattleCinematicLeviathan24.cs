using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicLeviathanServices(NativeBattleCinematicModelServices Model, Func<NativeBattleWaterExecutionFrame, short, uint> ResolveShape, Func<NativeBattleWaterExecutionFrame, short, uint> ResolveSlot, Func<NativeBattleWaterExecutionFrame, int, NativeBattleCinematicTextureSelection> ResolveTexture, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareBasis, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleActionMatrix, NativeBattleWaterExecutionFrame> Project, Func<uint, int> ResolveBucket, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append)
{
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
    public IReadOnlyDictionary<uint, System.Numerics.Vector3>? PilotWorld { get; init; }
}

public static class NativeBattleCinematicLeviathan24
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicLeviathanServices services)
    {
        uint model = SlotDword(0xBC);
        if (model == 0)
        {
            uint payload = SlotDword(0xB8);
            (frame, model) = NativeBattleCinematicModel.Allocate(frame, SignedWord(payload), services.Model);
            frame = WriteSlotDword(frame, 0xBC, model);
            frame = Texture(SignedWord(payload + 2));
            frame = WriteSlotDword(frame, 0xC0, frame.ReadTemporaryDword(0xF0));
            uint descriptor = SlotDword(0xC4);
            if (descriptor != 0)
            {
                uint target = services.ResolveShape(frame, SignedWord(descriptor + 2));
                frame = frame.WriteTemporaryDword(0xF4, unchecked(target + Dword(target + 8)));
                uint source = services.ResolveShape(frame, SignedWord(descriptor));
                frame = frame.WriteTemporaryDword(0xF0, unchecked(source + Dword(source + 8))).WriteTemporaryDword(0xF8, unchecked((uint)(int)SignedWord(descriptor + 4))).WriteTemporaryDword(0xFC, unchecked((uint)(int)SignedWord(descriptor + 6)));
                frame = NativeBattleCinematicLeviathanMorph.AllocatePairs(frame);
                Store(model + 4, frame.ReadTemporaryDword(0xFC));
            }
        }
        uint morph = SlotDword(0xC4);
        if (morph != 0)
        {
            uint reference = services.ResolveSlot(frame, SignedWord(morph + 8));
            uint source = services.ResolveShape(frame, SignedWord(morph));
            frame = frame.WriteTemporaryDword(0xF0, unchecked(source + Dword(source + 8))).WriteTemporaryDword(0xF8, Dword(model + 4));
            frame = frame.WriteTemporaryDword(0xE0, unchecked((uint)(int)SignedWord(reference + 0x94))).WriteTemporaryDword(0xE4, unchecked((uint)(int)SignedWord(reference + 0x96))).WriteTemporaryDword(0xE8, unchecked((uint)(int)SignedWord(reference + 0x98)));
            frame = NativeBattleCinematicLeviathanMorph.Apply(frame);
        }
        frame = services.PrepareBasis(frame);
        frame = NativeBattleCinematicModel.AdvanceAndCompose(frame, model, services.Model);
        uint parameters = SlotDword(0xB8);
        frame = frame.WriteTemporaryDword(0xF0, model).WriteTemporaryDword(0xF4, unchecked((uint)(int)SignedWord(parameters + 4))).WriteTemporaryDword(0xF8, unchecked((uint)(int)SignedWord(parameters + 2)));
        frame = Render(frame, services);
        StoreWord(model, unchecked((ushort)(Word(model) + 1)));
        return frame;

        NativeBattleWaterExecutionFrame Texture(int index)
        {
            NativeBattleCinematicTextureSelection texture = services.ResolveTexture(frame, index);
            return frame.WriteTemporaryDword(0xF8, (uint)(texture.SourceMapByte & 0x80)).WriteTemporaryDword(0xFC, texture.SourceAddress).WriteTemporaryDword(0xF0, texture.RectangleAddress).WriteTemporaryDword(0xF4, texture.TexturePage ?? throw new InvalidDataException("Native cinematic texture selection lacks its actual page WORD."));
        }
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        short SignedWord(uint address) => unchecked((short)Word(address));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        void Store(uint address, uint value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(value)) };
        void StoreWord(uint address, ushort value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
    }

    public static NativeBattleWaterExecutionFrame Render(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicLeviathanServices services)
    {
        uint model = frame.ReadTemporaryDword(0xF0);
        frame = frame.WriteTemporaryDword(0x84, model);
        uint reference = services.ResolveSlot(frame, unchecked((short)frame.ReadTemporaryDword(0xF4)));
        NativeBattleCinematicTextureSelection texture = services.ResolveTexture(frame, unchecked((int)frame.ReadTemporaryDword(0xF8)));
        frame = frame.WriteTemporaryDword(0xF8, (uint)(texture.SourceMapByte & 0x80)).WriteTemporaryDword(0xFC, texture.SourceAddress).WriteTemporaryDword(0xF0, texture.RectangleAddress).WriteTemporaryDword(0xF4, texture.TexturePage ?? throw new InvalidDataException("Native cinematic texture selection lacks its actual page WORD."));
        int xy = unchecked(((int)texture.Rectangle.Y << 5) | (int)texture.Rectangle.X);
        uint window = unchecked((uint)(xy << 7)) | unchecked((uint)((-texture.Rectangle.Width >> 3) & 31)) | unchecked((uint)((-texture.Rectangle.Height << 2) & 0x3E0)) | 0xE2000000;
        frame = frame.WriteTemporaryDword(0x88, window).WriteTemporaryDword(0x78, unchecked((uint)(int)SignedWord(reference + 0x92))).WriteTemporaryDword(0x7C, unchecked((uint)(int)SignedWord(reference + 0x9A))).WriteTemporaryDword(0x80, Dword(reference + 0xCC)).WriteTemporaryDword(0x70, unchecked((uint)(int)unchecked((short)SlotWord(frame, 0x92)))).WriteTemporaryDword(0x74, unchecked((uint)(int)unchecked((short)SlotWord(frame, 0x9A))));
        uint manager = CoreDword(frame, 0x27973EC);
        frame = frame.WriteTemporaryDword(0x68, CoreDword(frame, unchecked(manager + 0x74)));
        uint skeleton = Dword(model + 0x30);
        uint geometry = Dword(model + 0x34);
        int sections = unchecked((int)Dword(geometry));
        uint table = unchecked(geometry + 4);
        uint lastSource = 0;
        for (int section = 0; section < sections; section++)
        {
            frame = services.PrepareBasis(frame);
            frame = RestoreAndCompose(frame, model);
            uint source = unchecked(geometry + Dword(table));
            table = unchecked(table + 4);
            frame = frame.WriteTemporaryDword(0x8C, source);
            frame = ProjectGroups(frame, source, frame.ReadTemporaryDword(0x68));
            frame = frame.WriteTemporaryDword(0x6C, frame.ReadTemporaryDword(0xFC));
            uint selected = CoreDword(frame, 0x27973E8);
            frame = WriteCoreDword(frame, 0x27973E8, reference);
            frame = services.PrepareBasis(frame);
            frame = WriteCoreDword(frame, 0x27973E8, selected);
            frame = RestoreAndCompose(frame, model);
            frame = ProjectGroups(frame, frame.ReadTemporaryDword(0x8C), frame.ReadTemporaryDword(0x6C));
            uint faces = unchecked((lastSource + 3) & ~3u);
            for (uint index = 0; index < 4; index++)
            {
                StoreWord(unchecked(model + 0x48 + index * 2), Word(faces + index * 2));
            }
            Store(model + 0x40, unchecked(faces + 12));
            uint output = CoreDword(frame, unchecked(manager + 0x7C));
            var drawn = NativeBattleCinematicLeviathanPackets.Emit(frame, services.ResolveBucket, services.PilotWorld, services.PilotIdentity);
            frame = services.Append(drawn.Frame, output, drawn.Packets);
        }
        return frame;

        NativeBattleWaterExecutionFrame ProjectGroups(NativeBattleWaterExecutionFrame current, uint source, uint output)
        {
            int groups = unchecked((short)WordAt(current, source));
            source = unchecked(source + 2);
            for (int group = 0; group < groups; group++)
            {
                int bone = unchecked((short)WordAt(current, source));
                uint matrix = unchecked(skeleton + 0x20u + (uint)(bone * 48));
                source = unchecked(source + 2);
                NativeBattleActionMatrix basis = NativeBattleArenaPose.ReadMatrix(current.Arena, ArenaOffset(matrix, 32));
                current = services.Project(current, source, output, basis);
                source = current.ReadTemporaryDword(0xF8);
                output = current.ReadTemporaryDword(0xFC);
            }
            lastSource = source;
            return current;
        }
        ushort Word(uint address) => WordAt(frame, address);
        short SignedWord(uint address) => unchecked((short)Word(address));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        void Store(uint address, uint value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(value)) };
        void StoreWord(uint address, ushort value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
        static ushort WordAt(NativeBattleWaterExecutionFrame current, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(current.Arena.Read(ArenaOffset(address, 2), 2));
    }

    private static NativeBattleWaterExecutionFrame RestoreAndCompose(NativeBattleWaterExecutionFrame frame, uint model)
    {
        uint skeleton = BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(model + 0x30, 4), 4));
        int offset = ArenaOffset(skeleton, 1);
        int count = frame.Arena.Read(offset, 1)[0];
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(frame.Arena, offset, count);
        NativeBattleEffectArena arena = NativeBattleArenaPose.RestoreBoneMatrices(frame.Arena, offset, pose);
        byte[] bytes = frame.ReadTemporary(0xE0, 18);
        NativeBattleActionMatrix parent = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)frame.ReadTemporaryDword(0xF4)), unchecked((int)frame.ReadTemporaryDword(0xF8)), unchecked((int)frame.ReadTemporaryDword(0xFC)), 4096);
        for (int index = 0; index < count; index++)
        {
            int record = checked(offset + 0x20 + index * 48);
            arena = NativeBattleArenaPose.WriteMatrix(arena, record, NativeBattleGfMatrices.Compose56C2F0(parent, NativeBattleArenaPose.ReadMatrix(arena, record)), true);
        }
        return frame with { Arena = arena };
    }
}
