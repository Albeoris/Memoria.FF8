using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleShotTaskTarget
{
    private const int Offset = 26;
    private const int Width = 2;

    internal static BattleModelOwner Read(NativeBattleModulePoolTask task) => task.ReadExecutionReference<BattleExecutionReference.Model>(Offset, Width).Owner;

    internal static NativeBattleModulePoolTask Write(NativeBattleModulePoolTask task, BattleModelOwner owner, NativeBattleModelAliases aliases)
    {
        task.WriteExecutionReference(Offset, Width, new BattleExecutionReference.Model(owner), aliases.EncodeOperand(owner, Width));
        return task;
    }

    internal static NativeBattleModulePoolTask Copy(NativeBattleModulePoolTask source, NativeBattleModulePoolTask destination)
    {
        BattleExecutionReference.Model reference = source.ReadExecutionReference<BattleExecutionReference.Model>(Offset, Width);
        Span<byte> bytes = stackalloc byte[Width];
        Span<bool> known = stackalloc bool[Width];
        source.ReadWithKnownness(Offset, bytes, known);
        ImmutableArray<byte>? projection = known.Contains(false) ? null : [bytes[0], bytes[1]];
        destination.WriteExecutionReference(Offset, Width, reference, projection);
        return destination;
    }
}
