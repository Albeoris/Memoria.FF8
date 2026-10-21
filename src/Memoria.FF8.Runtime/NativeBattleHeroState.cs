using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHeroState([property: JsonRequired] ushort Effect, [property: JsonRequired] int WorkingArenaOffset, [property: JsonRequired] int? PacketCursor)
{
    public void Validate()
    {
        NativeBattleHeroProfile profile = NativeBattleHeroProfile.ForEffect(Effect);
        if (WorkingArenaOffset < 0 || WorkingArenaOffset > NativeBattleEffectArena.Length - profile.FirstDrawOffset - 0x10000 || PacketCursor is int cursor && (cursor < 0 || cursor > NativeBattleEffectArena.Length))
        {
            throw new InvalidDataException("The Hero working arena or packet cursor leaves its native allocation.");
        }
    }
}
