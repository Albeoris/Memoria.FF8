using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

internal static class CompletedSaveFixture
{
    internal static GameState State()
    {
        string hash = new('0', 64);
        NativeStatCurve curve = new(1, 0, 1, 10, 10);
        NativeStatModifier modifier = new(0, 0, 0);
        NativeCharacterVitals characters = new()
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = hash,
            KernelSha256 = hash,
            Records = Enumerable.Range(0, 8).Select(id => new NativeCharacterVital((byte)id, new NativeHpWord(10, 10), 0, new NativeCharacterJunction((ushort)(1 << id), 0, 0, new byte[20]), 12345, 0, 0, new byte[6], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), 1, 0)).ToArray(),
            CombatParty = [0, 1, 2],
            DreamEnabled = false,
            GuardianForces = Enumerable.Range(0, 16).Select(_ => new NativeGuardianForceVital(1234, 1, 10, new NativeGuardianForceLearnedBank(0x3FFFF, 0), 10, ImmutableArray.CreateRange(new byte[21]), 0, 0)).ToArray(),
            Definitions = new NativeStatDefinitions(Enumerable.Repeat(curve, 11).ToImmutableArray(), ImmutableArray.CreateRange(Enumerable.Repeat((byte)1, 57)), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray()),
            MenuDefinitions = new NativeGuardianForceMenuDefinitions(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), ImmutableArray.CreateRange(new byte[116]), ImmutableArray.CreateRange(new byte[19]), ImmutableArray.CreateRange(new byte[5])),
            DreamWeapons = new byte[3],
            JunctionPartyBackup = new byte[6]
        };
        NativeItemStock[] items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray();
        items[0] = new(17, 43);
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        cards.Cards[0] = 0x87;
        return GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.Field,
            FieldId = 76,
            CharacterVitals = characters,
            Economy = new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = hash, Items = items, ItemOrder = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray(), Gil = 12345, PastGil = 6789, EventBankGil72 = 3456, EventBankPastGil76 = 9876 },
            LimitProgress = new(hash, 0x1234, 0x5678, 0x91, 3, [1, 2, 3, 4, 5, 6, 7, 8]),
            CardInventory = cards,
            Playtime = new(1234, 17, 0),
            WorldControls = NativeWorldControlState.CreateNewGame(),
            SavedWorld = NativeSavedWorldState.CreateNewGame(),
            SeeD = NativeSeeDState.CreateNewGame(),
            Ff8Completion = new(1, Guid.NewGuid().ToString("N"), 76, 20054, Ff8CompletionState.OriginalEndingProgramHash, 3064, false)
        };
    }

    internal static GameplaySaveComponentDocument Document()
    {
        return CompletedGameData.Capture(State());
    }
}
