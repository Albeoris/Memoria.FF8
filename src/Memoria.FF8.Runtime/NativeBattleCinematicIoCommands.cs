using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicIoStep(NativeBattleWaterExecutionFrame Frame, NativeBattleWaterFileReadRequest? Request);

public static class NativeBattleCinematicIoCommands
{
    public static NativeBattleCinematicIoStep Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, INativeBattleScriptedTextureProfile profile, uint completionCallback = 0xB2BB40)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        int opcode = instruction & 0x1FF;
        if (opcode is not (6 or 8))
        {
            throw new NotSupportedException("Native cinematic IO requires its selected file-read or completion-wait instruction.");
        }
        if (frame.Core.Read(0x2798219, 1)[0] != 0)
        {
            return Wait();
        }
        if (opcode == 8)
        {
            return frame.Core.Read(0x2798218, 1)[0] == 0 ? Wait() : new(NativeBattleScriptedCommandReads.Advance(frame, 2), null);
        }
        frame = frame with { Core = frame.Core.Write(0x2798219, [0xFF]) };
        uint manager = CoreDword(frame, 0x27973EC);
        uint destination;
        int id;
        bool special = false;
        if ((instruction & 0x8000) != 0)
        {
            destination = CoreDword(frame, manager + 0xB8);
            id = (instruction >> 9) & 0x3F;
            special = (id & 0x20) != 0;
        }
        else
        {
            byte[] address = NativeBattleScriptedCommandReads.Program(frame, program, 2, 2);
            int low = address[0];
            int high = address[1];
            id = instruction >> 9;
            if (low == 0xFF)
            {
                destination = CoreDword(frame, manager + 0xB8);
            }
            else if ((low & 0x80) != 0)
            {
                destination = unchecked(CoreDword(frame, 0x2798A68u + (uint)(4 * (low & 0x7F))) + (uint)(high << 12));
            }
            else
            {
                destination = CoreDword(frame, 0x2798A68u + (uint)(4 * low));
                if (high != 0xFF)
                {
                    uint descriptor = profile.InitializationAddress;
                    uint table = unchecked(descriptor + ReadDword(descriptor + 0x1C));
                    table = unchecked(table + ReadDword(unchecked(table + (uint)(4 * low))));
                    destination = unchecked(destination + ReadDword(unchecked(table + (uint)(4 * high))));
                }
            }
        }
        short file;
        if (special)
        {
            file = unchecked((short)((id & 0x1F) + 0x16B));
        }
        else
        {
            id += frame.Core.Read(manager + 0xA2, 1)[0];
            frame = WriteCoreDword(frame, unchecked(0x2798A68u + (uint)(4 * id)), destination);
            file = unchecked((short)(id + (short)CoreWord(frame, manager + 0xA0)));
        }
        frame = WriteCoreDword(frame, manager + 0xB4, destination);
        frame = NativeBattleScriptedCommandReads.Advance(frame, unchecked((short)instruction) > 0 ? 4u : 2u);
        return new(frame, new(file, destination, 0, completionCallback));

        uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(profile.ReadStatic(address, 4));
        NativeBattleCinematicIoStep Wait() => new(frame.WriteTemporaryWord(0x13E, SlotWord(frame, 0xC8)), null);
    }

    public static NativeBattleWaterIoQueued CopyAndQueue(NativeBattleEffectMemory memory, NativeBattleFileIoState fileIo, NativeBattleWaterFileReadRequest request, NativeBattleCinematicResources resources)
    {
        if (request.CompletionCallback != NativeBattleCinematicProfile.ForInitializationAddress(resources.TextureProfile.InitializationAddress).LoadCallback || request.Argument != 0)
        {
            throw new InvalidDataException("Native cinematic delivery requires its selected file-read callback and argument.");
        }
        NativeBattleArchiveFile file = resources.Files.SingleOrDefault(file => file.FileId == unchecked((ushort)request.FileId)) ?? throw new NotSupportedException($"Native cinematic file{unchecked((ushort)request.FileId):X4} requires its original archive bytes.");
        NativeBattleScriptedEffectMemory core = memory.ScriptedEffects ?? throw new NotSupportedException("Native cinematic delivery requires its current scripted process.");
        uint manager = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x27973EC, 4));
        if (core.Read(0x2798219, 1)[0] != 0xFF || BinaryPrimitives.ReadUInt32LittleEndian(core.Read(manager + 0xB4, 4)) != request.DestinationAddress || file.Bytes.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native cinematic file input disagrees with its executed request.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native cinematic delivery requires its current effect arena.");
        int offset = ArenaOffset(request.DestinationAddress, file.Bytes.Length);
        return new(memory with { Arena = arena.Write(offset, file.Bytes.AsSpan()) }, fileIo.Queue482610(file.Bytes.Length, request.CompletionCallback, null));
    }
}
