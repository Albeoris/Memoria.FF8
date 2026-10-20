using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel216Echo
{
    public const int Offset = 3668;
    public const int Length = 2440;
    public static ImmutableArray<byte> SelectedBones { get; } = [1, 2, 4, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22];

    public static NativeBattleModulePoolTask Initialize695AB0(NativeBattleModulePoolTask task)
    {
        return NativeBattleDuelEcho.Initialize6A1230(task, Offset, SelectedBones);
    }
}
