using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The larger cinematic grid spacing and the selected alternate row UV directory.</summary>
public static class NativeBattleApocalypse221Grid
{
    public static NativeBattleWaterExecutionFrame Draw51(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicGrid28Services services)
        => NativeBattleCinematicGrid28.DrawApocalypse(frame, services, []);

    public static NativeBattleWaterExecutionFrame Draw68(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicGrid28Services services, ImmutableArray<byte> rowV)
    {
        if (rowV.IsDefault || rowV.Length != 17)
        {
            throw new InvalidDataException("Native Apocalypse alternate grid requires its actual seventeen addressed row UV bytes.");
        }
        return NativeBattleCinematicGrid28.DrawApocalypse(frame, services, rowV);
    }
}
