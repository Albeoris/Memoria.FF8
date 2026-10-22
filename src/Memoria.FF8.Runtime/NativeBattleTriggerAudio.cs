using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleTriggerAudio
{
    public static NativeBattleModuleSound Resolve501640(byte modelId, NativeBattleVector position, byte pan, ImmutableArray<uint>? actorSounds1120)
    {
        ImmutableArray<uint> sounds = actorSounds1120 ?? throw new NotSupportedException("Native501640 requires its installed actor sound table.");
        if (modelId >= 160 || sounds.IsDefault || sounds.Length != 160 * 7)
        {
            throw new InvalidDataException("Native501640 requires a valid actor identity and1120 sound DWORDs.");
        }
        int soundIndex = modelId * 7 + (modelId == 0 ? 1 : 0);
        return new(sounds[soundIndex], 0, new(position.X, position.Y, position.Z, 0)) { Pan = pan };
    }
}
