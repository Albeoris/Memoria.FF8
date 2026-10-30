using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private NativeEventMemory? PrepareNativeBattleCounterMirrors(NativeFieldRuntimeProfile? profile, bool restoringCheckpoint)
    {
        if (restoringCheckpoint || profile is not { IsVerified: true } || services is not GameSession session)
        {
            return null;
        }
        NativeEventMemory memory = EventMemory;
        if (session.State.BattleStatistics is { } statistics)
        {
            memory = memory.Write(20, 4, NativeScalarValue.Exact(unchecked((int)statistics.Victories)))
                .Write(24, 2, NativeScalarValue.Exact(statistics.Defeats))
                .Write(26, 2, NativeScalarValue.Exact(statistics.Escapes));
        }
        if (session.State.CharacterVitals is { } vitals)
        {
            uint total = 0;
            for (int row = 0; row < 8; row++)
            {
                NativeCharacterVital character = vitals.Records[row];
                memory = memory.Write(28 + 2 * row, 2, NativeScalarValue.Exact(character.KillCount));
                total += character.KillCount;
                if (character.DeathCount is ushort deaths)
                {
                    memory = memory.Write(44 + 2 * row, 2, NativeScalarValue.Exact(deaths));
                }
            }
            memory = memory.Write(68, 4, NativeScalarValue.Exact(unchecked((int)total)));
        }
        return memory;
    }
}
