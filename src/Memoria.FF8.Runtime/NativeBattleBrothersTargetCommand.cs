using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBrothersTargetServices(ushort Encounter, Func<byte> ReadFlag, Action<byte> WriteFlag, Func<NativeBattleWaterExecutionFrame, NativeBattleGraphicsModel> ReadModel3);

public static class NativeBattleBrothersTargetCommand
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleBrothersTargetServices services)
    {
        if (services.Encounter != 63)
        {
            return NativeBattleScriptedCommandReads.Advance(frame, 4);
        }
        int mode = frame.ReadTemporaryWord(0x14A) >> 12;
        if (mode > 1)
        {
            return frame;
        }
        if (mode == 1)
        {
            if (services.ReadFlag() == 0 || ReadSlot(frame, 0x1B, 1)[0] != 3)
            {
                return NativeBattleScriptedCommandReads.Advance(frame, 4);
            }
            short displacement = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
            return WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + (uint)displacement));
        }
        uint header = CoreDword(frame, 0x279744C);
        if (frame.Core.Read(header + 0x48, 1)[0] == 3)
        {
            services.WriteFlag(0);
            return NativeBattleScriptedCommandReads.Advance(frame, 4);
        }
        services.WriteFlag(1);
        frame = HeaderByte(frame, 0x41, 4);
        frame = HeaderByte(frame, 0x4B, 3);
        NativeBattleGraphicsModel model = services.ReadModel3(frame);
        NativeBattleVector position = (model.Transform ?? throw new NotSupportedException("Native Brothers target correction requires physical model3 position28.")).Position28;
        frame = HeaderWord(frame, 0x9C, unchecked((ushort)position.X));
        frame = HeaderWord(frame, 0x9E, unchecked((ushort)position.Y));
        frame = HeaderWord(frame, 0xA0, unchecked((ushort)position.Z));
        frame = HeaderWord(frame, 0x54, model.Flags0);
        frame = WriteCoreDword(frame, CoreDword(frame, 0x279744C) + 0x6C, 0x1D97494);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    private static NativeBattleWaterExecutionFrame HeaderByte(NativeBattleWaterExecutionFrame frame, uint offset, byte value) => frame with { Core = frame.Core.Write(CoreDword(frame, 0x279744C) + offset, new byte[] { value }) };
    private static NativeBattleWaterExecutionFrame HeaderWord(NativeBattleWaterExecutionFrame frame, uint offset, ushort value) => WriteCoreWord(frame, CoreDword(frame, 0x279744C) + offset, value);
}
