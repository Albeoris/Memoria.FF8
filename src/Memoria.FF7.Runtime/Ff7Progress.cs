namespace Memoria.FF7.Runtime;

public sealed record Ff7CharacterState(int Id, string Name, int Level, uint Experience, int HitPoints, int MagicPoints, byte[] NativeRecord);
public sealed record Ff7InventorySlot(int ItemId, int Quantity);
public sealed record Ff7MateriaState(int MateriaId, uint AbilityPoints);
public sealed record Ff7RewardItem(int ItemId, int Quantity);
public sealed record Ff7BattleReward(uint Gil, uint Experience, uint AbilityPoints, Ff7RewardItem[] Items);
public sealed record Ff7BattleReceipt(long FieldGeneration, long RequestId, ushort Formation, Ff7BattleReward Reward);

/// <summary>FFVII progression has no FFVIII character, item, variable or reward identifiers.</summary>
public sealed record Ff7Progress
{
    public required byte[] InitialKernelData { get; init; }
    public required Ff7CharacterState[] Characters { get; init; }
    public required int[] Party { get; init; }
    public required Ff7InventorySlot[] Inventory { get; init; }
    public required Ff7MateriaState[] Materia { get; init; }
    public uint Gil { get; init; }
    public ushort AvailableCharacters { get; init; }
    public ushort LockedCharacters { get; init; }
    public Ff7EncounterState Encounters { get; init; } = new();
    public long NextEncounterRequestId { get; init; } = 1L << 60;
    public byte FieldRandomIndex { get; init; }
    public Ff7BattleRngState? BattleRng { get; init; }
    public string DebugVictoryPolicy { get; init; } = "instant-defeat-v1";
    public Ff7BattleReceipt[] CompletedBattles { get; init; } = [];

    public void Validate()
    {
        BattleRng?.Validate();
        if (Encounters is null || NextEncounterRequestId < 1L << 60)
        {
            throw new InvalidDataException("FFVII encounter continuation is invalid.");
        }
        if (DebugVictoryPolicy != "instant-defeat-v1")
        {
            throw new NotSupportedException("The FFVII automatic victory policy is unsupported.");
        }
        if (InitialKernelData is null || InitialKernelData.Length == 0 || Characters is null || Party is not { Length: 3 } || Inventory is null || Materia is null || CompletedBattles is null)
        {
            throw new InvalidDataException("FFVII progression is missing required owners.");
        }
        if (Characters.Select(character => character.Id).Distinct().Count() != Characters.Length || Characters.Any(character => character.Id is < 0 or > 8 || character.Level is < 0 or > 99 || character.HitPoints < 0 || character.MagicPoints < 0 || character.NativeRecord is not { Length: 132 } || character.Name is null))
        {
            throw new InvalidDataException("FFVII character progression is invalid.");
        }
        if (Party.Any(character => character != 255 && Characters.All(candidate => candidate.Id != character)) || Inventory.Any(item => item.ItemId is < 0 or > 511 || item.Quantity is < 1 or > 99) || Inventory.Select(item => item.ItemId).Distinct().Count() != Inventory.Length || Materia.Any(materia => materia.MateriaId is < 0 or > 254 || materia.AbilityPoints > 0xFFFFFF))
        {
            throw new InvalidDataException("FFVII party or inventory is invalid.");
        }
        if (CompletedBattles.Any(receipt => receipt.FieldGeneration < 0 || receipt.RequestId < 1 || receipt.Reward is null) || CompletedBattles.Select(receipt => (receipt.FieldGeneration, receipt.RequestId)).Distinct().Count() != CompletedBattles.Length)
        {
            throw new InvalidDataException("FFVII battle receipts are invalid.");
        }
    }
}
