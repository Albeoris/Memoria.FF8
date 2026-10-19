using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicActorCloneDraw(NativeBattleWaterExecutionFrame Frame, uint Cursor);
public sealed record NativeBattleCinematicActorClone48Services(Func<NativeBattleWaterExecutionFrame, ushort, uint> ResolveNode, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> SuppressActor, Func<uint, uint> ReadColor40, Func<uint, uint> ReadMeshMask124, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleWaterExecutionFrame> RefreshDescriptors, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> PublishInput, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleActionMatrix, NativeBattleCinematicActorCloneDraw> ComposeAndDraw, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> RestorePose);

public static class NativeBattleCinematicActorClone48
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicActorClone48Services services)
    {
        uint header = CoreDword(frame, 0x279744C);
        byte actorIndex = ReadSlot(frame, 0x1B, 1)[0];
        uint actor = CoreDword(frame, unchecked(header + 0x60u + 4u * actorIndex));
        uint clone = SlotDword(0xBC);
        if (clone == 0)
        {
            frame = services.SuppressActor(frame, actor);
            uint payload = SlotDword(0xB8);
            uint reference = services.ResolveNode(frame, unchecked((ushort)SignedWord(payload)));
            SlotDwordStore(0xC0, Dword(reference + 0x94));
            SlotWordStore(0xC4, Word(reference + 0x98));
            SlotDwordStore(0xA8, Dword(reference + 0x8C));
            SlotWordStore(0xAC, Word(reference + 0x98));
            (frame, clone) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 0x80);
            StoreDword(clone + 0xC, actor);
            frame = services.RefreshDescriptors(frame, clone, actor);
            StoreDword(clone + 0x54, 0);
            StoreDword(clone + 0x58, 0x00D00140);
            uint color = services.ReadColor40(actor);
            StoreDword(clone + 0x78, color);
            StoreDword(clone + 0x5C, color);
            StoreDword(clone + 0x60, services.ReadMeshMask124(actor));
            StoreWord(clone + 0x64, 0);
            StoreDword(clone + 0x68, 0);
            StoreDword(clone + 0x50, uint.MaxValue);
            StoreWord(clone, 0);
            StoreWord(clone + 2, 0);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(clone + 0x21, 1), [0]) };
            SlotDwordStore(0xBC, clone);
        }
        uint selected = services.ResolveNode(frame, unchecked((ushort)SignedWord(SlotDword(0xB8))));
        actorIndex = ReadSlot(frame, 0x1B, 1)[0];
        header = CoreDword(frame, 0x279744C);
        uint capturedRotation = unchecked(header + 0xD8u + 8u * actorIndex);
        NativeBattleVector angles = new(unchecked((short)(CoreWord(frame, capturedRotation) - SignedSlot(0xA8) + SignedWord(selected + 0x8C))), unchecked((short)(SignedWord(selected + 0x8E) - SignedSlot(0xAA) + CoreWord(frame, capturedRotation + 2))), unchecked((short)(SignedWord(selected + 0x98) - SignedSlot(0xAC) + CoreWord(frame, capturedRotation + 4))));
        frame = frame.WriteTemporaryWord(0x80, unchecked((ushort)angles.X)).WriteTemporaryWord(0x82, unchecked((ushort)angles.Y)).WriteTemporaryWord(0x84, unchecked((ushort)angles.Z));
        (frame, NativeBattleActionMatrix relative) = NativeBattleWaterMatrixCommands.BuildRotationB65590(frame, angles, 1);
        NativeBattleVector selectedAngles = new(SignedWord(selected + 0x8C), SignedWord(selected + 0x8E), SignedWord(selected + 0x90));
        NativeBattleActionMatrix selectedRotation = NativeBattleGfMatrices.Euler56CD50(selectedAngles);
        StoreRotation(0xA0, selectedRotation);
        NativeBattleActionMatrix positioned = selectedRotation with { X = SignedWord(selected + 0x94), Y = SignedWord(selected + 0x96), Z = SignedWord(selected + 0x98) };
        uint capturedPosition = unchecked(header + 0x84u + 8u * actorIndex);
        NativeBattleVector difference = new(unchecked((short)(CoreWord(frame, capturedPosition) - SignedSlot(0xC0))), unchecked((short)(CoreWord(frame, capturedPosition + 2) - SignedSlot(0xC2))), unchecked((short)(CoreWord(frame, capturedPosition + 4) - SignedSlot(0xC4))));
        frame = frame.WriteTemporaryWord(0xC0, unchecked((ushort)difference.X)).WriteTemporaryWord(0xC2, unchecked((ushort)difference.Y)).WriteTemporaryWord(0xC4, unchecked((ushort)difference.Z));
        frame = services.PublishInput(frame, frame.TemporaryFrame.Address + 0xC0);
        NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(positioned, difference);
        frame = frame.WriteTemporaryDword(0xF4, unchecked((uint)translated.X)).WriteTemporaryDword(0xF8, unchecked((uint)translated.Y)).WriteTemporaryDword(0xFC, unchecked((uint)translated.Z)).WriteTemporaryDword(0x9C, translated.Flags);
        byte[] rotation = frame.Core.Read(0x27979E8, 18);
        NativeBattleActionMatrix view = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), unchecked((int)CoreDword(frame, 0x27979FC)), unchecked((int)CoreDword(frame, 0x2797A00)), unchecked((int)CoreDword(frame, 0x2797A04)), 4096);
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(view, relative with { X = translated.X, Y = translated.Y, Z = translated.Z });
        StoreRotation(0xE0, composed);
        frame = frame.WriteTemporaryDword(0xF4, unchecked((uint)composed.X)).WriteTemporaryDword(0xF8, unchecked((uint)composed.Y)).WriteTemporaryDword(0xFC, unchecked((uint)composed.Z));
        clone = SlotDword(0xBC);
        uint sourceActor = Dword(clone + 0xC);
        frame = services.RefreshDescriptors(frame, clone, sourceActor);
        StoreDword(clone + 0x44, CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x74)));
        NativeBattleCinematicActorCloneDraw drawn = services.ComposeAndDraw(frame, clone, sourceActor, composed);
        frame = drawn.Frame;
        frame = WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), drawn.Cursor);
        return services.RestorePose(frame, sourceActor);

        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        short SignedSlot(int offset) => unchecked((short)SlotWord(frame, offset));
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        short SignedWord(uint address) => unchecked((short)Word(address));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        void StoreDword(uint address, uint value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(value)) };
        void StoreWord(uint address, ushort value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
        void SlotDwordStore(int offset, uint value) => frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 4), BitConverter.GetBytes(value)) };
        void SlotWordStore(int offset, ushort value) => frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, offset, 2), BitConverter.GetBytes(value)) };
        void StoreRotation(int offset, NativeBattleActionMatrix matrix)
        {
            for (int index = 0; index < 9; index++)
            {
                frame = frame.WriteTemporaryWord(offset + index * 2, unchecked((ushort)matrix.Rotation[index]));
            }
            frame = frame with { TemporaryFrame = frame.TemporaryFrame with { Known = frame.TemporaryFrame.Known.SetItem(offset + 18, false).SetItem(offset + 19, false) } };
        }
    }
}
