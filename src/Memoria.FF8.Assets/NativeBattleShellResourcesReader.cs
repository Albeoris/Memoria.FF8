using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleShellResourcesReader
{
    public static NativeBattleShellResources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleGfAnimationSource shield = new(0x15EC42C, NativeBattleEffectStaticResourcesReader.Read(memory, 0x15EC42C, 0x64E4));
        _ = new NativeBattleGfAnimationWorkspace(new byte[0x258].ToImmutableArray()).Initialize7016B0(shield, 0x258);
        ImmutableArray<byte> bank = NativeBattleEffectStaticResourcesReader.Read(memory, NativeBattleShellResources.ActorBankAddress, NativeBattleShellResources.ActorBankLength);
        int directory = checked((int)(NativeBattleShellResources.ActorDirectory - NativeBattleShellResources.ActorBankAddress));
        if (BinaryPrimitives.ReadUInt32LittleEndian(bank.AsSpan().Slice(directory)) != 1)
        {
            throw new InvalidDataException("Native Shell requires the original initialized absolute actor directories.");
        }
        for (int index = 0; index < 16; index++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(bank.AsSpan().Slice(directory + 4 + index * 4));
            if (pointer == 0)
            {
                continue;
            }
            int offset = checked((int)(pointer - NativeBattleShellResources.ActorBankAddress));
            if (bank[offset + 0x16] != 0)
            {
                throw new InvalidDataException("Native Shell's original actor bank requires its three sprite-only definitions.");
            }
        }
        return new(new("FF8_EN:15F2E1C", NativeBattleEffectStaticResourcesReader.Read(memory, 0x15F2E1C, 0xCEC)), shield, bank, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x15EBD68, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleShellResources? nativeShellResources;

    public NativeBattleShellResources ReadNativeShellResources()
    {
        return nativeShellResources ??= NativeBattleShellResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
