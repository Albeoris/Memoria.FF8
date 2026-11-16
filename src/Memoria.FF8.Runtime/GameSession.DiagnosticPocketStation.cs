namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void SetDiagnosticPocketStation(NativePocketStationState prepared, bool miniMogPreparation = false)
    {
        if (liveBattle is not null || State.Battle is not null)
        {
            throw new InvalidOperationException("Diagnostic PocketStation preparation precedes battle allocation.");
        }
        NativePocketStationState source = State.PocketStation ?? throw new NotSupportedException("Diagnostic PocketStation preparation requires its initialized saved owner.");
        source.Validate();
        prepared.Validate();
        int writableFlags = miniMogPreparation ? 0x0F : 3;
        if (prepared.InitOutSha256 != source.InitOutSha256 || ((source.Flags ^ prepared.Flags) & ~writableFlags) != 0 || miniMogPreparation && (prepared.Flags & 0x0F) != 0x0D || prepared.Level is < 1 or > 100 || prepared.AttackVariant > 3 || (source.ChocoboName is null) != (prepared.ChocoboName is null) || source.ChocoboName is { } name && !name.AsSpan().SequenceEqual(prepared.ChocoboName!.Value.AsSpan()))
        {
            throw new InvalidDataException("Diagnostic PocketStation preparation must preserve initialization, unrelated flags and saved name.");
        }
        for (int index = 0; index < source.Raw.Length; index++)
        {
            if (index is not (0 or 1 or 45) && source.Raw[index] != prepared.Raw[index])
            {
                throw new InvalidDataException("Diagnostic PocketStation preparation changed an undeclared saved byte.");
            }
        }
        State = State with { PocketStation = prepared };
    }
}
