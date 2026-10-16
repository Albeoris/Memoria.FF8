using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private static void InitializeNativePartyEntryGauges(List<BattleCombatantDefinition> combatants, ref NativeBattleRandomState random)
    {
        //48B7E0/48B5F0 run all party allocations before48BA10 allocates enemies in physical order.
        for (int slot = 0; slot < combatants.Count; slot++)
        {
            BattleCombatantDefinition actor = combatants[slot];
            int gauge = 0;
            if (((actor.NativeStatus0 | actor.NativeRetainedStatusBits) & 5) == 0)
            {
                uint passive = actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? throw new NotSupportedException("Native initial party ATB requires the actual equipped passive flags.");
                if ((passive & 0x10000) != 0)
                {
                    gauge = BattleSession.AtbMaximum;
                }
                else
                {
                    random = random.Draw(out byte roll);
                    //4844D0: maximum is divisible by100, so this percentage is exact in the HUD gauge scale.
                    int percent = (actor.Speed >> 2) + (roll & 0x7F) + 1 - 35;
                    gauge = Math.Clamp(percent, 0, 100) * (BattleSession.AtbMaximum / 100);
                }
            }
            combatants[slot] = actor with { InitialAtb = gauge };
        }
    }
}
