using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class NativeBattleModelBoundary
{
    public static NativeBattleModelBoundary FromRetainedMemory(NativeBattleRetainedMemory? retained)
    {
        return new(ClearOriginalCells(retained?.ModelColors40, retained?.ModelStatusTimers, retained?.ModelWorldMatrices64, retained?.ModelTransformRows, retained?.ModelSequenceLocals));
    }

    private static NativeBattleGraphicsModel[] ClearOriginalCells(ImmutableArray<uint?>? retainedColors40 = null, ImmutableArray<ImmutableArray<byte?>>? retainedStatusTimers = null, ImmutableArray<NativeBattleActionMatrix?>? retainedWorldMatrices64 = null, ImmutableArray<NativeBattleModelRetainedFields>? retainedTransformRows = null, ImmutableArray<ImmutableArray<short?>>? retainedSequenceLocals = null)
    {
        //502340 clears only Flags0 of each of seven records. Unknown retained fields must not be supplied as zeros.
        if (retainedColors40 is { } colors && (colors.IsDefault || colors.Length != CellCount))
        {
            throw new InvalidDataException("Native502340 requires the retained seven model-color DWORDs.");
        }
        if (retainedStatusTimers is { } timers && (timers.IsDefault || timers.Length != CellCount || timers.Any(row => row.IsDefault || row.Length != 32)))
        {
            throw new InvalidDataException("Native502340 requires seven retained32-byte status timer rows.");
        }
        if (retainedWorldMatrices64 is { } matrices)
        {
            if (matrices.IsDefault || matrices.Length != CellCount)
            {
                throw new InvalidDataException("Native502340 requires the retained seven model matrix fields.");
            }
            foreach (NativeBattleActionMatrix? matrix in matrices)
            {
                matrix?.Validate();
            }
        }
        if (retainedTransformRows is { } transforms && (transforms.IsDefault || transforms.Length != CellCount || transforms.Any(row => row is null)))
        {
            throw new InvalidDataException("Native502340 requires seven retained rotation, transform and normal-color rows.");
        }
        if (retainedSequenceLocals is { } locals && (locals.IsDefault || locals.Length != CellCount || locals.Any(row => row.IsDefault || row.Length != 8)))
        {
            throw new InvalidDataException("Native graphics sequence locals require seven physical eight-WORD rows.");
        }
        NativeBattleGraphicsModel[] bank = new NativeBattleGraphicsModel[CellCount];
        for (int row = 0; row < bank.Length; row++)
        {
            bank[row] = NativeBattleGraphicsModels.ClearUnloadedModel(retainedColors40?[row], retainedStatusTimers?[row], retainedWorldMatrices64?[row], retainedTransformRows?[row], retainedSequenceLocals?[row]);
        }
        return bank;
    }
}
