using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBrothersTargetServices(ushort? Encounter, Func<byte> ReadFlag, Action<byte> WriteFlag, Func<NativeBattleWaterExecutionFrame, NativeBattleGraphicsModel> ReadModel3);

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
        if (NativeBattleCinematicActorAccess.ReadContext(frame) is { } actors)
        {
            return PrepareHostedActors(frame, services, actors);
        }
        uint header = CoreDword(frame, 0x279744C);
        if (NativeBattleCinematicActorAccess.ReadCurrentActor(frame, 0, header) == 3)
        {
            services.WriteFlag(0);
            return NativeBattleScriptedCommandReads.Advance(frame, 4);
        }
        services.WriteFlag(1);
        frame = HeaderByte(frame, 0x41, 4);
        frame = HeaderByte(frame, 0x4B, 3);
        NativeBattleGraphicsModel model = services.ReadModel3(frame);
        NativeBattleVector position = (model.Transform ?? throw new NotSupportedException("Native Brothers target correction requires physical model3 position28.")).Position28;
        frame = RefreshModel3Header(frame, model.Flags0, position);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    private static NativeBattleWaterExecutionFrame PrepareHostedActors(NativeBattleWaterExecutionFrame frame, NativeBattleBrothersTargetServices services, NativeBattleCinematicActorContext actors)
    {
        if (actors.CurrentActors.Contains((byte)3) || actors.CurrentActors.IsEmpty && actors.FirstActor == 3)
        {
            services.WriteFlag(0);
            return NativeBattleScriptedCommandReads.Advance(frame, 4);
        }

        NativeBattleCinematicActorCache cached = actors.ReadCachedActor(3);
        actors = actors.WithCurrentActors(actors.CurrentActors.Add(3));
        NativeBattleGraphicsModel model = services.ReadModel3(frame);
        if (model.Index != 3)
        {
            throw new InvalidDataException("Hosted Brothers target correction requires the original graphics row 3.");
        }
        NativeBattleVector position = (model.Transform ?? throw new NotSupportedException("Hosted Brothers target correction requires model 3 position28.")).Position28;
        NativeBattleCinematicActorContext updated = actors with { CachedActors = actors.CachedActors.SetItem(3, cached with { Position28 = position, Flags0 = model.Flags0, ModelAddress = NativeBattleGraphicsModelAddress.Encode(3) }) };
        services.WriteFlag(1);
        int mirrorCount = Math.Min(updated.CurrentActors.Length, 6);
        uint header = CoreDword(frame, 0x279744C);
        frame = HeaderByte(frame, 0x41, checked((byte)updated.CurrentActors.Length));
        frame = frame with { Core = frame.Core.Write(header + 0x48, updated.CurrentActors.AsSpan(0, mirrorCount)) with { PresentationActors = updated } };
        frame = RefreshModel3Header(frame, model.Flags0, position);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    private static NativeBattleWaterExecutionFrame RefreshModel3Header(NativeBattleWaterExecutionFrame frame, ushort flags, NativeBattleVector position)
    {
        frame = HeaderWord(frame, 0x9C, unchecked((ushort)position.X));
        frame = HeaderWord(frame, 0x9E, unchecked((ushort)position.Y));
        frame = HeaderWord(frame, 0xA0, unchecked((ushort)position.Z));
        frame = HeaderWord(frame, 0x54, flags);
        return WriteCoreDword(frame, CoreDword(frame, 0x279744C) + 0x6C, NativeBattleGraphicsModelAddress.Encode(3));
    }

    private static NativeBattleWaterExecutionFrame HeaderByte(NativeBattleWaterExecutionFrame frame, uint offset, byte value) => frame with { Core = frame.Core.Write(CoreDword(frame, 0x279744C) + offset, new byte[] { value }) };
    private static NativeBattleWaterExecutionFrame HeaderWord(NativeBattleWaterExecutionFrame frame, uint offset, ushort value) => WriteCoreWord(frame, CoreDword(frame, 0x279744C) + offset, value);
}
