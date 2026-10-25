using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    internal static NativeCharacterVitals PrepareDiagnosticCharacterLevel(NativeCharacterVitals source, int row, int level, NativeBattleExperienceDefinition definition)
    {
        return source.WithDiagnosticBattleLevel(row, level, definition);
    }
}