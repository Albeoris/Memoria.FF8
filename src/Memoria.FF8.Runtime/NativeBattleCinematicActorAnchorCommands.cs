using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicActorAnchorServices(Func<NativeBattleWaterExecutionFrame, uint, bool, bool, NativeBattleCinematicBonePose> ReadPose, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> Update, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> PublishInput)
{
    public Func<uint, short>? ReadScale38 { get; init; }
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
            byte actor = ReadSlot(frame, 0x1B, 1)[0];
            uint pointer = CoreDword(frame, unchecked(owner + 0x60 + 4u * actor));
            NativeBattleCinematicBonePose pose = services.ReadPose(frame, pointer, true, false);
            frame = pose.Frame;
            int count = pose.Skeleton[0];
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            int selected = (int)((uint)unchecked(value * count) >> 15);
            if (selected >= count)
            {
                selected = count - 1;
            }
            NativeBattleWaterActorAnchorResult result = NativeBattleWaterActorAnchor.EvaluateB66B80(frame, pose.Skeleton, pose.Bones, unchecked((short)selected), 4096, pose.PhysicalBasis ?? throw new NotSupportedException("Native random anchor requires the actor's actual world matrix."));
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
            byte actor = ReadSlot(frame, 0x1B, 1)[0];
            uint pointer = CoreDword(frame, unchecked(owner + 0x60 + 4u * actor));
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
            byte actor = ReadSlot(frame, 0x1B, 1)[0];
            uint pointer = CoreDword(frame, unchecked(owner + 0x60 + 4u * actor));
            bool linked = (instruction & 0x8000) != 0;
            NativeBattleWaterActorAnchorResult result = Anchor(pointer, true, linked);
            frame = result.Frame;
            uint node = CoreDword(frame, 0x27973E8);
            frame = services.PublishInput(frame, node + 0x94);
            byte[] local = ReadSlot(frame, 0x94, 6);
            NativeBattleFullMacVertex position = NativeBattleGfMatrices.TransformMac460840(result.Matrix, new(BinaryPrimitives.ReadInt16LittleEndian(local), BinaryPrimitives.ReadInt16LittleEndian(local.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(local.AsSpan(4))));
            frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)position.X)).WriteTemporaryDword(0xF4, unchecked((uint)position.Y)).WriteTemporaryDword(0xF8, unchecked((uint)position.Z)).WriteTemporaryDword(0xFC, position.Flags);
            Store(0, position.X);
            Store(1, position.Y);
            Store(2, position.Z);
        }
        else if (opcode == 0x6E)
        {
            int count = unchecked((sbyte)frame.Core.Read(owner + 0x41, 1)[0]);
            int x = 0;
            int y = 0;
            int z = 0;
            for (int index = 0; index < count; index++)
            {
                int actor = unchecked((sbyte)frame.Core.Read(unchecked(owner + 0x48u + (uint)index), 1)[0]);
                uint pointer = CoreDword(frame, unchecked(owner + 0x60u + (uint)(actor * 4)));
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

        NativeBattleWaterActorAnchorResult Anchor(uint actor, bool restore, bool linked)
        {
            NativeBattleCinematicBonePose pose = services.ReadPose(frame, actor, restore, linked);
            NativeBattleActionMatrix basis = pose.PhysicalBasis ?? throw new NotSupportedException("Native actor anchor requires the actual caller's physical matrix.");
            return NativeBattleWaterActorAnchor.EvaluateB66B80(pose.Frame, pose.Skeleton, pose.Bones, selector, 4096, basis);
        }

        void Store(int lane, int value)
        {
            frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0x5C + 4 * lane, 4), BitConverter.GetBytes(unchecked(value << 16))) };
        }
    }
}
