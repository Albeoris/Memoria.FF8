using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterContextServices(Func<NativeBattleWaterExecutionFrame, uint, uint> ReadLinkedModelPointer, Func<NativeBattleWaterExecutionFrame, uint, uint> ResolveRing501FF0, Func<NativeBattleWaterExecutionFrame, uint, int, byte[]> ReadActionBytes, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> ApplyContact506690, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> ApplyCamera50A730, Func<NativeBattleWaterExecutionFrame, int, short, uint, NativeBattleWaterExecutionFrame> WriteStageColor)
{
    public NativeBattleActionPacketsState? ActionPackets { get; init; }
}

/// <summary>Selected current actor context, contact, camera and stage-color operations.</summary>
public static class NativeBattleWaterContextCommands
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, NativeBattleWaterContextServices services)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x1FF) == 0xCF)
        {
            return ExecuteRing(frame, services);
        }
        switch (instruction)
        {
            case 0x1022:
                return NativeBattleIfritContactCommand.ExecuteSavedActors(frame, services);
            case 0x1039:
                frame = services.ApplyCamera50A730(frame);
                return Advance(4);
            case 0x0020:
                uint packed = 0;
                for (int lane = 0; lane < 3; lane++)
                {
                    int color = unchecked((short)SlotWord(frame, 0x94 + 2 * lane));
                    frame = frame.WriteTemporaryDword(0xF0 + 4 * lane, unchecked((uint)color));
                }
                for (int lane = 0; lane < 3; lane++)
                {
                    int color = Math.Clamp(unchecked((int)frame.ReadTemporaryDword(0xF0 + 4 * lane)), 0, 255);
                    frame = frame.WriteTemporaryDword(0xF0 + 4 * lane, (uint)color);
                    packed |= (uint)color << (8 * lane);
                }
                frame = frame.WriteTemporaryDword(0xFC, packed);
                short depth = (short)Math.Clamp(unchecked((short)SlotWord(frame, 0x8C)) << 4, 0, 0x1000);
                for (int i = 0; i < 4; i++)
                {
                    frame = services.WriteStageColor(frame, i, depth, frame.ReadTemporaryDword(0xFC));
                }
                return Advance(2);
            default:
                throw new NotSupportedException("Native context commands admit only their selected raw mode words.");
        }

        NativeBattleWaterExecutionFrame Advance(uint amount) => WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + amount));
    }

    public static NativeBattleWaterExecutionFrame ExecuteRing(NativeBattleWaterExecutionFrame frame, NativeBattleWaterContextServices services)
    {
        int mode = frame.ReadTemporaryWord(0x14A) >> 12;
        if (mode > 1)
        {
            return frame;
        }
        int actor = ReadSlot(frame, 0x1B, 1)[0];
        if (NativeBattleCinematicActorAccess.ReadContext(frame) is { } context)
        {
            return ExecuteHostedRing(frame, services, context, mode, actor);
        }
        if (actor >= 7)
        {
            throw new NotSupportedException("Native ring context leaves its seven physical graphics rows.");
        }
        uint header = CoreDword(frame, 0x279744C);
        uint model = CoreDword(frame, unchecked(header + 0x60u + (uint)(4 * actor)));
        _ = PhysicalIndex(model);
        if (services.ReadLinkedModelPointer(frame, model) == 0)
        {
            frame = CoreByte(frame, unchecked(header + 0x17A), 0);
        }
        else
        {
            if (mode == 0)
            {
                frame = CoreByte(frame, unchecked(header + 0x174), (byte)actor);
                frame = CoreByte(frame, unchecked(header + 0x17A), 1);
            }
            else
            {
                frame = CoreByte(frame, unchecked(header + 0x17A), frame.Core.Read(unchecked(header + 0x41), 1)[0]);
                int count = unchecked((sbyte)frame.Core.Read(unchecked(header + 0x17A), 1)[0]);
                for (int index = count; index > 0; index--)
                {
                    byte saved = frame.Core.Read(unchecked(header + 0x47u + (uint)index), 1)[0];
                    frame = CoreByte(frame, unchecked(header + 0x173u + (uint)index), saved);
                }
            }
            byte minimum = PhysicalIndex(services.ResolveRing501FF0(frame, model));
            frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0x1B, 1), [minimum]) };
            frame = CoreByte(frame, unchecked(header + 0x48), minimum);
            frame = CoreByte(frame, unchecked(header + 0x41), 1);
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }

    private static NativeBattleWaterExecutionFrame ExecuteHostedRing(NativeBattleWaterExecutionFrame frame, NativeBattleWaterContextServices services, NativeBattleCinematicActorContext context, int mode, int actor)
    {
        uint header = NativeBattleScriptedEffectMemory.Address;
        uint model = context.ReadCachedActor(actor).ModelAddress;
        if (services.ReadLinkedModelPointer(frame, model) == 0)
        {
            NativeBattleScriptedEffectMemory unlinked = frame.Core.Write(header + 0x17A, [0]);
            frame = frame with { Core = unlinked with { PresentationActors = context.WithSavedActors([]) } };
            return NativeBattleScriptedCommandReads.Advance(frame, 2);
        }

        ImmutableArray<byte> saved = mode == 0 ? [(byte)actor] : context.CurrentActors;
        uint minimumPointer = services.ResolveRing501FF0(frame, model);
        if (!NativeBattleGraphicsModelAddress.TryDecode(minimumPointer, out int minimum, context.Roster.ActorCount))
        {
            throw new InvalidDataException("Hosted ring context requires an aligned model in its captured graphics bank.");
        }
        NativeBattleCinematicActorContext updated = context.WithSavedActors(saved).WithCurrentActors([(byte)minimum]);
        NativeBattleScriptedEffectMemory core = frame.Core.Write(header + 0x17A, [checked((byte)saved.Length)]);
        if (!saved.IsEmpty)
        {
            core = core.Write(header + 0x174, saved.AsSpan()[..Math.Min(saved.Length, 6)]);
        }
        core = core.Write(header + 0x48, [(byte)minimum]).Write(header + 0x41, [1]);
        frame = frame with { Core = core with { PresentationActors = updated }, Arena = frame.Arena.Write(SlotOffset(frame, 0x1B, 1), [(byte)minimum]) };
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }

    private static byte PhysicalIndex(uint pointer)
    {
        return NativeBattleGraphicsModelAddress.TryDecode(pointer, out int row) ? (byte)row : throw new InvalidDataException("Native ring context requires one of the seven aligned physical graphics models.");
    }

    private static NativeBattleWaterExecutionFrame CoreByte(NativeBattleWaterExecutionFrame frame, uint address, byte value) => frame with { Core = frame.Core.Write(address, new byte[] { value }) };
}
