using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicActorAnchorServices(Func<NativeBattleWaterExecutionFrame, BattleModelOwner, bool, bool, NativeBattleCinematicBonePose> ReadPose, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> Update, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> PublishInput, NativeBattleWaterActorAnchorServices ActorAnchor)
{
    public Func<BattleModelOwner, short>? ReadScale38 { get; init; }
}

public static class NativeBattleCinematicActorAnchorCommands
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleCinematicActorAnchorServices services)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        int opcode = instruction & 0x1FF;
        uint owner = CoreDword(frame, 0x279744C);
        if (opcode == 0xB4)
        {
            BattleModelOwner pointer = NativeBattleCinematicActorAccess.ReadSlotActor(frame);
            NativeBattleCinematicBonePose pose = services.ReadPose(frame, pointer, true, false);
            frame = pose.Frame;
            int count = pose.BoneCount;
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            int selected = (int)((uint)unchecked(value * count) >> 15);
            if (selected >= count)
            {
                selected = count - 1;
            }
            NativeBattleWaterActorAnchorResult result = pose.Evaluate(services.ActorAnchor, frame, unchecked((short)selected), 4096, pose.PhysicalBasis ?? throw new NotSupportedException("Native random anchor requires the actor's actual world matrix."));
            frame = services.PublishInput(result.Frame, result.Frame.TemporaryFrame.Address + 0xC0);
            Store(0, result.Anchor.X);
            Store(1, result.Anchor.Y);
            Store(2, result.Anchor.Z);
            frame = services.Update(frame, ReadSlot(frame, 0x18, 1)[0]);
            return NativeBattleScriptedCommandReads.Advance(frame, 2);
        }
        short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        if (opcode == 0x60)
        {
            BattleModelOwner pointer = NativeBattleCinematicActorAccess.ReadSlotActor(frame);
            NativeBattleWaterActorAnchorResult result = Anchor(pointer, false, false);
            frame = services.PublishInput(result.Frame, result.Frame.TemporaryFrame.Address + 0xC0);
            uint advance = 4;
            if ((instruction & 0x8000) != 0)
            {
                short radius = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
                int angle = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 6, 2)) << 4;
                short scale = (services.ReadScale38 ?? throw new NotSupportedException("Native radial anchor requires its actual actor scale WORD."))(pointer);
                int z = unchecked((unchecked(NativeBattleCameraMath.Sine(angle) * scale) >> 4) * radius) / 65536;
                int x = unchecked((unchecked(NativeBattleCameraMath.Cosine(angle) * scale) >> 4) * radius) / 65536;
                Store(2, unchecked(result.Anchor.Z + z));
                Store(0, unchecked(result.Anchor.X + x));
                Store(1, result.Anchor.Y);
                advance = 8;
            }
            else
            {
                Store(0, result.Anchor.X);
                Store(1, result.Anchor.Y);
                Store(2, result.Anchor.Z);
            }
            frame = NativeBattleScriptedCommandReads.Advance(frame, advance);
            return services.Update(frame, ReadSlot(frame, 0x18, 1)[0]);
        }
        if (opcode == 0xAD)
        {
            BattleModelOwner pointer = NativeBattleCinematicActorAccess.ReadSlotActor(frame);
            bool linked = (instruction & 0x8000) != 0;
            NativeBattleWaterActorAnchorResult result = Anchor(pointer, true, linked);
            frame = result.Frame;
            uint node = CoreDword(frame, 0x27973E8);
            frame = services.PublishInput(frame, node + 0x94);
            NativeBattleGeometryRegisterStep registers = services.ActorAnchor.ReadRegisters(frame);
            registers = registers with { Colors = NativeBattleCinematicRibbonInput.Capture(frame, node + 0x94, registers.Colors) };
            registers = NativeBattleGeometry.Transform(registers);
            frame = services.ActorAnchor.PublishRegisters(frame, registers);
            NativeBattleFullMacVertex position = new(registers.Colors.Mac[0]!.Value, registers.Colors.Mac[1]!.Value, registers.Colors.Mac[2]!.Value, registers.Colors.Flags!.Value);
            frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)position.X)).WriteTemporaryDword(0xF4, unchecked((uint)position.Y)).WriteTemporaryDword(0xF8, unchecked((uint)position.Z)).WriteTemporaryDword(0xFC, position.Flags);
            Store(0, position.X);
            Store(1, position.Y);
            Store(2, position.Z);
        }
        else if (opcode == 0x6E)
        {
            int count = NativeBattleCinematicActorAccess.ReadCurrentCount(frame, owner);
            int x = 0;
            int y = 0;
            int z = 0;
            for (int index = 0; index < count; index++)
            {
                BattleModelOwner pointer = NativeBattleCinematicActorAccess.ReadCurrentActor(frame, index, owner);
                NativeBattleWaterActorAnchorResult result = Anchor(pointer, false, false);
                frame = services.PublishInput(result.Frame, result.Frame.TemporaryFrame.Address + 0xC0);
                x = unchecked(x + result.Anchor.X);
                y = unchecked(y + result.Anchor.Y);
                z = unchecked(z + result.Anchor.Z);
            }
            if ((instruction & 0x1000) != 0)
            {
                Store(0, x / count);
            }
            if ((instruction & 0x2000) != 0)
            {
                Store(1, y / count);
            }
            if ((instruction & 0x4000) != 0)
            {
                Store(2, z / count);
            }
        }
        else
        {
            throw new NotSupportedException("Native actor anchor command requires its selected instruction.");
        }
        frame = services.Update(frame, ReadSlot(frame, 0x18, 1)[0]);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);

        NativeBattleWaterActorAnchorResult Anchor(BattleModelOwner actor, bool restore, bool linked)
        {
            NativeBattleCinematicBonePose pose = services.ReadPose(frame, actor, restore, linked);
            NativeBattleActionMatrix basis = pose.PhysicalBasis ?? throw new NotSupportedException("Native actor anchor requires the actual caller's physical matrix.");
            return pose.Evaluate(services.ActorAnchor, pose.Frame, selector, 4096, basis);
        }

        void Store(int lane, int value)
        {
            frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0x5C + 4 * lane, 4), BitConverter.GetBytes(unchecked(value << 16))) };
        }
    }
}
