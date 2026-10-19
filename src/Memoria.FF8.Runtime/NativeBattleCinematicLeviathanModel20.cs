using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicLeviathanModelServices(NativeBattleCinematicModelServices Model, Func<NativeBattleWaterExecutionFrame, short, uint> ResolveShape, Func<NativeBattleWaterExecutionFrame, short, uint> ResolveSlot, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareBasis);

public static partial class NativeBattleCinematicModel
{
    public static NativeBattleWaterExecutionFrame PrepareLeviathan20(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicLeviathanModelServices services)
    {
        uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        if (model == 0)
        {
            uint parameters = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
            (frame, model) = Allocate(frame, SignedWord(parameters), services.Model);
            frame = WriteSlotDword(frame, 0xBC, model);
            uint target = services.ResolveShape(frame, SignedWord(parameters + 2));
            frame = frame.WriteTemporaryDword(0xF4, unchecked(target + Dword(target + 8))).WriteTemporaryDword(0xF0, Dword(model + 0x34)).WriteTemporaryDword(0xF8, unchecked((uint)(int)SignedWord(parameters + 4))).WriteTemporaryDword(0xFC, unchecked((uint)(int)SignedWord(parameters + 6)));
            frame = NativeBattleCinematicLeviathanMorph.AllocatePairs(frame);
            Store(model + 4, frame.ReadTemporaryDword(0xFC));
        }
        if ((frame.Arena.Read(ArenaOffset(model + 2, 1), 1)[0] & 1) == 0)
        {
            if (Word(model) != 0)
            {
                bool complete;
                (frame, complete) = AdvanceRegular(frame, model, services.Model);
                StoreWord(model, unchecked((ushort)(Word(model) + 1)));
                if (complete)
                {
                    frame = Select509440(frame, model, 0, services.Model);
                }
            }
            if ((frame.Arena.Read(ArenaOffset(model + 2, 1), 1)[0] & 1) == 0)
            {
                frame = services.PrepareBasis(frame);
                byte[] rotation = frame.ReadTemporary(0xE0, 18);
                NativeBattleActionMatrix parent = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)frame.ReadTemporaryDword(0xF4)), unchecked((int)frame.ReadTemporaryDword(0xF8)), unchecked((int)frame.ReadTemporaryDword(0xFC)), 4096);
                uint skeleton = Dword(model + 0x30);
                int bones = frame.Arena.Read(ArenaOffset(skeleton, 1), 1)[0];
                for (int index = 0; index < bones; index++)
                {
                    int offset = ArenaOffset(unchecked(skeleton + 0x20u + (uint)index * 48), 32);
                    frame = frame with { Arena = NativeBattleArenaPose.WriteMatrix(frame.Arena, offset, NativeBattlePoseMatrices.Compose56C2F0(parent, NativeBattleArenaPose.ReadMatrix(frame.Arena, offset)), true) };
                }
            }
        }
        uint manager = CoreDword(frame, 0x27973EC);
        Store(model + 0x44, CoreDword(frame, unchecked(manager + 0x74)));
        uint payload = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
        uint reference = services.ResolveSlot(frame, SignedWord(payload + 8));
        frame = frame.WriteTemporaryDword(0xF0, Dword(model + 0x34)).WriteTemporaryDword(0xF8, Dword(model + 4)).WriteTemporaryDword(0xE0, unchecked((uint)(int)SignedWord(reference + 0x94))).WriteTemporaryDword(0xE4, unchecked((uint)(int)SignedWord(reference + 0x96))).WriteTemporaryDword(0xE8, unchecked((uint)(int)SignedWord(reference + 0x98)));
        return NativeBattleCinematicLeviathanMorph.Apply(frame);

        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        short SignedWord(uint address) => unchecked((short)Word(address));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        void Store(uint address, uint value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(value)) };
        void StoreWord(uint address, ushort value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
    }
}
