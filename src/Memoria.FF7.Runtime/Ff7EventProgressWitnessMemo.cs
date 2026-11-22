namespace Memoria.FF7.Runtime;

/// <summary>Memoizes only the exact ordered values already included in the event-progress witness.</summary>
internal sealed class Ff7EventProgressWitnessMemo
{
    private sealed record Character(int Id, int Level, uint Experience, int HitPoints, int MagicPoints)
    {
        internal bool Matches(Ff7CharacterState? current)
        {
            return current is not null && Id == current.Id && Level == current.Level && Experience == current.Experience && HitPoints == current.HitPoints && MagicPoints == current.MagicPoints;
        }
    }

    private sealed record Battle(long Generation, long Request, ushort Formation, uint Gil, uint Experience, uint AbilityPoints, Ff7RewardItem[] Items)
    {
        internal bool Matches(Ff7BattleReceipt? current)
        {
            return current?.Reward is { Items: not null } reward && Generation == current.FieldGeneration && Request == current.RequestId && Formation == current.Formation && Gil == reward.Gil && Experience == reward.Experience && AbilityPoints == reward.AbilityPoints && Items.AsSpan().SequenceEqual(reward.Items);
        }
    }

    private byte[] persistent = [];
    private Ff7InventorySlot[] inventory = [];
    private Ff7MateriaState[] materia = [];
    private Character[] characters = [];
    private int[] party = [];
    private Battle[] battles = [];
    private uint gil;
    private ushort availableCharacters;
    private ushort lockedCharacters;
    private string? witness;

    internal bool TryRead(ReadOnlySpan<byte> currentPersistent, Ff7Progress progress, out string value)
    {
        value = string.Empty;
        if (witness is null || progress.Inventory is null || progress.Materia is null || progress.Characters is null || progress.Party is null || progress.CompletedBattles is null || !currentPersistent.SequenceEqual(persistent) || gil != progress.Gil || availableCharacters != progress.AvailableCharacters || lockedCharacters != progress.LockedCharacters || !inventory.AsSpan().SequenceEqual(progress.Inventory) || !materia.AsSpan().SequenceEqual(progress.Materia) || !party.AsSpan().SequenceEqual(progress.Party) || characters.Length != progress.Characters.Length || battles.Length != progress.CompletedBattles.Length)
        {
            return false;
        }
        for (int index = 0; index < characters.Length; index++)
        {
            if (!characters[index].Matches(progress.Characters[index]))
            {
                return false;
            }
        }
        for (int index = 0; index < battles.Length; index++)
        {
            if (!battles[index].Matches(progress.CompletedBattles[index]))
            {
                return false;
            }
        }
        value = witness;
        return true;
    }

    internal void Remember(byte[] currentPersistent, Ff7Progress progress, string value)
    {
        // Unsupported/null leaves retain the original computation path, rather than acquiring a partial cache key.
        if (progress.Inventory is null || progress.Materia is null || progress.Characters is null || progress.Party is null || progress.CompletedBattles is null || progress.Inventory.Any(item => item is null) || progress.Materia.Any(item => item is null) || progress.Characters.Any(character => character is null) || progress.CompletedBattles.Any(battle => battle?.Reward?.Items is not { } items || items.Any(item => item is null)))
        {
            witness = null;
            return;
        }
        persistent = currentPersistent.ToArray();
        inventory = progress.Inventory.ToArray();
        materia = progress.Materia.ToArray();
        characters = progress.Characters.Select(character => new Character(character.Id, character.Level, character.Experience, character.HitPoints, character.MagicPoints)).ToArray();
        party = progress.Party.ToArray();
        battles = progress.CompletedBattles.Select(battle => new Battle(battle.FieldGeneration, battle.RequestId, battle.Formation, battle.Reward.Gil, battle.Reward.Experience, battle.Reward.AbilityPoints, battle.Reward.Items.ToArray())).ToArray();
        gil = progress.Gil;
        availableCharacters = progress.AvailableCharacters;
        lockedCharacters = progress.LockedCharacters;
        witness = value;
    }
}
