using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Fresh Ifrit continuation dependencies reconstructed from the current original resource.</summary>
public static class NativeBattleIfritContinuation
{
    public const int LandingInstructionOffset = 0x154;
    public const int LandingAnimationFrame = 49;

    public static NativeBattleWaterExecutionFrame Initialize(NativeBattleWaterExecutionFrame frame, NativeBattleWaterInitializationResource resource0, NativeBattleWaterInitializationResource resource1, uint magicTextureBufferAddress, NativeBattleCinematicModelServices modelServices, Func<NativeBattleWaterExecutionFrame, int, NativeBattleWaterExecutionFrame> initializeResourceInstruction)
    {
        ArgumentNullException.ThrowIfNull(initializeResourceInstruction);
        NativeCrtRandomState random = frame.Random;
        frame = InitializeSceneDependencies(frame, resource0, resource1, magicTextureBufferAddress);
        NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resource1.Length);
        Select(0);
        foreach (int offset in new[] { 0x112, 0x114, 0x118 })
        {
            PrepareResource(offset);
        }
        Select(4);
        PrepareResource(0x3EC);
        Select(1);
        foreach (int offset in new[] { 0x246, 0x24A, 0x24E, 0x252, 0x258, 0x25C, 0x260, 0x264, 0x26A, 0x26E, 0x272, 0x276, 0x27A, 0x27E, 0x286, 0x288, 0x28E, 0x290, 0x298, 0x2A4, 0x2A6, 0x2AE })
        {
            PrepareResource(offset);
        }
        uint header = CoreDword(frame, 0x279744C);
        uint command = CoreDword(frame, 0x27973C0);
        ushort tick = CoreWord(frame, command + 0xC);
        frame = frame with { Core = frame.Core.Write(0x2797454, Enumerable.Repeat((byte)0xFF, 128).ToArray()).Write(0x2797554, Enumerable.Repeat((byte)0xFF, 128).ToArray()) };
        byte[] order = [0, 1, 2, 3, 4, 5, 6, 7, 8, 14, 15, 17, 18];
        frame = frame with { Core = frame.Core.Write(0x2797454, order) };
        frame = WriteCoreWord(frame, header + 0x18, checked((ushort)order.Length));
        frame = WriteCoreWord(frame, header, 0x8010);
        frame = WriteCoreWord(frame, header + 2, (ushort)(ReadWord(0x136) | ReadWord(0x148)));
        Select(0);
        frame = WriteSlotDword(frame, 0, checked(resource0.Address + LandingInstructionOffset));
        frame = WriteSlotWord(frame, 0xC, checked((ushort)(((ReadWord(0x152) >> 9) - 1) * tick)));
        frame = InitializeModelDependency(frame, resource0, resource1, modelServices);
        frame = WriteSlotWord(frame, 0x16, 0x8001);
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, bank, 18, tick, CoreWord(frame, command + 0xE), CoreWord(frame, header + 0x1C)) };
        Select(18);
        frame = WriteSlotWord(frame, 0x12, 0x800F);
        frame = WriteSlotWord(frame, 0x14, 0x8001);
        frame = WriteSlotDword(frame, 0, checked(resource0.Address + 0x702));
        frame = WriteSlotDword(frame, 4, checked(resource0.Address + 0x770));
        int postFlagTicks = (ReadWord(0x14A) >> 9) + (ReadWord(0x14C) >> 9);
        byte remainingShakeIterations = checked((byte)(ReadWord(0x6F4) - (postFlagTicks - ReadWord(0x6CC) - ReadWord(0x6E0))));
        frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0x20, 1), [remainingShakeIterations]) };
        foreach (byte index in order)
        {
            frame = frame with { Arena = NativeBattleWaterSlots.UpdateSelected(frame.Arena, bank, index, order.Concat(Enumerable.Repeat((byte)0xFF, 128 - order.Length)).ToArray().ToImmutableArray()) };
            frame = frame with { Arena = NativeBattleWaterSlots.CachePositionAA17D0(frame.Arena, bank, index) };
        }
        Select(0);
        return frame with { Random = random };

        void Select(int index)
        {
            frame = WriteCoreDword(frame, 0x27973E8, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(frame.Arena, index)));
            frame = frame.WriteTemporaryByte(0x142, checked((byte)index));
        }

        void PrepareResource(int offset)
        {
            frame = WriteCoreDword(frame, 0x2797450, checked(resource0.Address + (uint)offset));
            frame = frame.WriteTemporaryWord(0x14A, ReadWord(offset));
            frame = initializeResourceInstruction(frame, offset);
        }

        ushort ReadWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(resource0.ArenaOffset + offset, 2));
    }

    public static NativeBattleWaterExecutionFrame InitializeSceneDependencies(NativeBattleWaterExecutionFrame frame, NativeBattleWaterInitializationResource resource0, NativeBattleWaterInitializationResource resource1, uint magicTextureBufferAddress)
    {
        NativeBattleWaterScriptProgram program = new(resource0.ArenaOffset, resource0.Length, resource0.Address);
        uint header = CoreDword(frame, 0x279744C);
        uint command = CoreDword(frame, 0x27973C0);
        foreach (int offset in new[] { 0xFE, 0x106, 0x11C })
        {
            frame = WriteCoreDword(frame, 0x2797450, checked(resource0.Address + (uint)offset));
            frame = frame.WriteTemporaryWord(0x14A, ReadWord(offset));
            frame = NativeBattleWaterWorkCommands.Execute(frame, program, magicTextureBufferAddress);
        }
        NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resource1.Length);
        ushort tick = CoreWord(frame, command + 0xC);
        ushort secondaryTick = CoreWord(frame, command + 0xE);
        ushort flags = CoreWord(frame, header + 0x1C);
        int postFlagTicks = Wait(ReadWord(0x14A)) + Wait(ReadWord(0x14C));
        // Only persistent source dependencies are allocated; identifiers for newly created children remain local to this invocation.
        Create(1, 0x8000, 0, 0, 0x2B0);
        Create(2, 0x8001, 0, 0, 0x388, Wait(ReadWord(0x386)) - postFlagTicks - 1);
        Create(3, 0x8002, 0, 0, 0x324, Wait(ReadWord(0x320)) - 1);
        Create(4, ReadWord(0x3F2), 0x8001, 0, 0x418, Wait(ReadWord(0x416)) - postFlagTicks - 1);
        int accelerationTicks = Wait(ReadWord(0x40A));
        int flagInterval = Wait(ReadWord(0x138)) + Wait(ReadWord(0x13A)) + Wait(ReadWord(0x140));
        int accelerationDelay = Wait(ReadWord(0x404));
        int acceleration = unchecked((short)ReadWord(0x408)) >> 8;
        int cameraAngle = unchecked((short)ReadWord(0x3FE)) + acceleration * (accelerationTicks * (accelerationTicks + 1) / 2 + accelerationTicks * (flagInterval - accelerationDelay - accelerationTicks));
        Angles(0, cameraAngle, unchecked((short)ReadWord(0x3FA)));
        Create(5, ReadWord(0x4A8), 0x8001, 0, 0x4E6, Wait(ReadWord(0x4E4)) - postFlagTicks - 1, 0x19BC);
        frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0x18, 1), [3]) };
        frame = WriteSlotWord(frame, 0xB0, ReadWord(0x4AE));
        int firstSpan = Wait(ReadWord(0x4CE));
        int secondSpan = flagInterval - firstSpan;
        int speedX = unchecked((short)ReadWord(0x4CC));
        int accelerationX = unchecked((short)ReadWord(0x4D2)) >> 8;
        int accelerationY = unchecked((short)ReadWord(0x4D4)) >> 8;
        int angleX = unchecked((short)ReadWord(0x4C0)) + speedX * flagInterval + accelerationX * secondSpan * (secondSpan + 1) / 2;
        int angleY = unchecked((short)ReadWord(0x4C2)) + accelerationY * secondSpan * (secondSpan + 1) / 2 + accelerationY * secondSpan * postFlagTicks;
        Position(0, unchecked((short)ReadWord(0x4B8)));
        Angles(angleX, angleY, unchecked((short)ReadWord(0x4C4)));
        Velocity(4, accelerationY * secondSpan);
        Create(6, ReadWord(0x59C), 0x8001, 0, 0x5B0, 0, 0x1AC6);
        Angles(ReadSigned(0x5A0), ReadSigned(0x5A2), ReadSigned(0x5A4));
        Create(7, ReadWord(0x5F8), 0x8001, 0, 0x616, 0, 0x1AD0);
        Angles(ReadSigned(0x5FC), ReadSigned(0x5FE) + ReadSigned(0x610) * Wait(ReadWord(0x612)), ReadSigned(0x600));
        Create(8, 0x8007, 0x8001, 0, 0x642, 0, 0x1ABC);
        Angles(ReadSigned(0x626), ReadSigned(0x628), ReadSigned(0x62A));
        Create(14, ReadWord(0x7E2), 0x8001, 0, 0x818, 0, 0x1B0E);
        Angles(0, 0, ReadSigned(0x7F8));
        Create(15, 0x800E, 0x8001, 0, 0x8D6, 0, 0x1B0E);
        Angles(ReadSigned(0x8C2), ReadSigned(0x8C4) + ReadSigned(0x8D0) * Wait(ReadWord(0x8D2)), ReadSigned(0x8C6));
        return frame;

        void Create(int index, ushort id, ushort parent, ushort ancestor, int pc, int wait = 0, int secondary = 0)
        {
            frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, bank, index, tick, secondaryTick, flags) };
            frame = WriteCoreDword(frame, 0x27973E8, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(frame.Arena, index)));
            frame = frame.WriteTemporaryByte(0x142, checked((byte)index));
            frame = WriteSlotWord(frame, 0x12, id);
            frame = WriteSlotWord(frame, 0x14, parent);
            frame = WriteSlotWord(frame, 0x16, ancestor);
            frame = WriteSlotDword(frame, 0, checked(resource0.Address + (uint)pc));
            frame = WriteSlotWord(frame, 0xC, checked((ushort)(wait * tick)));
            if (secondary != 0)
            {
                frame = WriteSlotDword(frame, 4, checked(resource0.Address + (uint)secondary));
            }
        }

        void Angles(int x, int y, int z)
        {
            Position(3, x);
            Position(4, y);
            Position(5, z);
        }

        void Position(int lane, int value)
        {
            frame = WriteSlotDword(frame, 0x50 + lane * 4, unchecked((uint)(value << 16)));
        }

        void Velocity(int lane, int value)
        {
            frame = WriteSlotDword(frame, 0x68 + lane * 4, unchecked((uint)(value << 16)));
        }

        int ReadSigned(int offset) => unchecked((short)ReadWord(offset));
        ushort ReadWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(resource0.ArenaOffset + offset, 2));
        static int Wait(ushort instruction) => instruction >> 9;
    }

    public static NativeBattleWaterExecutionFrame InitializeModelDependency(NativeBattleWaterExecutionFrame frame, NativeBattleWaterInitializationResource resource0, NativeBattleWaterInitializationResource resource1, NativeBattleCinematicModelServices services, int modelSlot = 17)
    {
        NativeBattleWaterScriptProgram program = new(resource0.ArenaOffset, resource0.Length, resource0.Address);
        RequireInstruction(0x11C, 0x7D);
        frame = WriteCoreDword(frame, 0x2797450, checked(resource0.Address + 0x11C));
        frame = frame.WriteTemporaryWord(0x14A, ReadWord(0x11C));
        frame = NativeBattleWaterWorkCommands.Execute(frame, program);
        RequireInstruction(0x7E0, 0x33);
        RequireInstruction(0x856, 0x33);
        RequireInstruction(0x85A, 0x68);
        RequireInstruction(0x85E, 0x3F);
        NativeBattleWaterSlotBank bank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resource1.Length);
        uint command = CoreDword(frame, 0x27973C0);
        uint header = CoreDword(frame, 0x279744C);
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, bank, modelSlot, CoreWord(frame, command + 0xC), CoreWord(frame, command + 0xE), CoreWord(frame, header + 0x1C)) };
        frame = WriteCoreDword(frame, 0x27973E8, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(frame.Arena, modelSlot)));
        frame = frame.WriteTemporaryByte(0x142, checked((byte)modelSlot));
        frame = WriteSlotWord(frame, 0x12, ReadWord(0x858));
        frame = WriteSlotWord(frame, 0x14, ReadWord(0x7E2));
        frame = WriteSlotWord(frame, 0x9C, ReadWord(0x85C));
        frame = WriteCoreDword(frame, 0x2797450, checked(resource0.Address + 0x85E));
        frame = frame.WriteTemporaryWord(0x14A, ReadWord(0x85E));
        frame = NativeBattleCinematicModel.Register(frame, program, services);
        uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        frame = NativeBattleCinematicModel.DecodePose(frame, model, 0, LandingAnimationFrame, services);
        frame = frame with { Arena = frame.Arena.Write(ArenaOffset(model, 2), BitConverter.GetBytes(checked((ushort)LandingAnimationFrame))) };
        // The original scale rise ends at 0x100 before the landing handoff; only its settled transform is needed.
        for (int lane = 0; lane < 3; lane++)
        {
            frame = WriteSlotDword(frame, 0x50 + 4 * lane, 0x01000000);
            frame = WriteSlotWord(frame, 0x8C + 2 * lane, 0x100);
        }
        frame = WriteSlotDword(frame, 0, checked(resource0.Address + 0x874));
        return frame;

        ushort ReadWord(int offset)
        {
            if (offset < 0 || offset > resource0.Length - 2)
            {
                throw new InvalidDataException("Ifrit continuation initialization leaves its original resource.");
            }
            return BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(resource0.ArenaOffset + offset, 2));
        }

        void RequireInstruction(int offset, ushort opcode)
        {
            if ((ReadWord(offset) & 0x1FF) != opcode)
            {
                throw new InvalidDataException("Ifrit continuation resource differs from its supported initialization program.");
            }
        }
    }
}
