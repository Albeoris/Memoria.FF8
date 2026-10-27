using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterIoQueued(NativeBattleEffectMemory Memory, NativeBattleFileIoState FileIo);

/// <summary>Delivery into the existing resource arena, followed by the shared native completion scheduler.</summary>
public static class NativeBattleWaterIoService
{
    public static NativeBattleWaterIoQueued CopyAndQueueFile(NativeBattleEffectMemory memory, NativeBattleFileIoState fileIo, NativeBattleWaterFileReadRequest request, NativeBattleWaterIoResources resources, int? outerBattlePhase = null)
    {
        memory.Validate();
        fileIo.Validate();
        resources.Validate();
        if (request.FileId is < 363 or > 365 || request.Argument != 0 || request.CompletionCallback != 0xAA0ED0)
        {
            throw new NotSupportedException("Native Water delivery requires its selected external read profile.");
        }
        NativeBattleScriptedEffectMemory core = memory.ScriptedEffects ?? throw new NotSupportedException("Native Water delivery requires its current shared scripted-effect owner.");
        uint manager = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x27973EC, 4));
        if (core.Read(0x2798219, 1)[0] != 0xFF || BinaryPrimitives.ReadUInt32LittleEndian(core.Read(checked(manager + 0xB4), 4)) != request.DestinationAddress)
        {
            throw new InvalidDataException("Native Water delivery disagrees with its executed busy and destination stores.");
        }
        NativeBattleArchiveFile file = resources.Files[request.FileId - 363];
        long offset = (long)request.DestinationAddress - NativeBattleEffectNodeMemory.ArenaAddress;
        if (offset < 0 || offset > memory.Cursor - file.Bytes.Length)
        {
            throw new NotSupportedException("Native Water delivery leaves its current allocated arena prefix.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Water delivery requires its actual current resource arena.");
        arena = arena.Write((int)offset, file.Bytes.AsSpan()).RememberTextureFile((int)offset, file.Bytes.AsSpan(), new(file.Name, NativeBattleTextureDomain.Battle));
        NativeBattleEffectMemory delivered = memory with { Arena = arena, Textures = memory.Textures?.OpenTextureFile(new(file.Name, NativeBattleTextureDomain.Battle)) };
        return new(delivered, fileIo.Queue482610(file.Bytes.Length, request.CompletionCallback, outerBattlePhase));
    }
}
