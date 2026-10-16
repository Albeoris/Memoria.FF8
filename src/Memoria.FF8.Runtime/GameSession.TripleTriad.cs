using System.Globalization;
using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    private static void ValidateTripleTriadCheckpoint(GameState state)
    {
        if (state.TripleTriadPlay is not { } play)
        {
            return;
        }
        play.Validate();
        if (state.Scene != SceneKind.Field || state.Battle is not null || state.CardInventory?.TripleTriadRandomState != play.InitialRandomState || state.CardInventory.TripleTriadStatistics is null || state.EventState is not JsonElement vm || !vm.TryGetProperty("Scene", out JsonElement scene) || !scene.TryGetProperty("TripleTriadRequest", out JsonElement request) || request.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A saved Triple Triad module requires its frozen field request and original card RNG.");
        }
        string identity = string.Create(CultureInfo.InvariantCulture, $"{request.GetProperty("FieldId").GetInt32()}:{request.GetProperty("ProgramHash").GetString()}:{request.GetProperty("Frame").GetInt64()}:{request.GetProperty("ObjectId").GetInt32()}:{request.GetProperty("ScriptId").GetInt32()}:{request.GetProperty("Operation").GetInt32()}:{request.GetProperty("FiberSequence").GetInt64()}");
        if (identity != play.RequestIdentity || request.GetProperty("FieldId").GetInt32() != state.FieldId || play.SelectedCards.GroupBy(id => id).Any(group => state.CardInventory.AvailableCount(group.Key) < group.Count()))
        {
            throw new InvalidDataException("Saved Triple Triad request identity or selected card ownership disagrees.");
        }
    }

    public static bool CanUseCardMod(NativeCharacterVitals? vitals)
    {
        return vitals is not null && Enumerable.Range(0, 16).Any(id => (vitals.GuardianForces[id].Flags & 1) != 0 && vitals.GuardianForceAbilities(id).Any(ability => ability.Learned && ability.Ability == 115));
    }

    public bool VisitCardMenu()
    {
        if (State.CardInventory is not { } cards)
        {
            return false;
        }
        NativeCardInventory candidate = cards.NativeVisitNormalMenu();
        if (candidate.CollectionFlags != cards.CollectionFlags)
        {
            SetCardInventory(candidate);
            return true;
        }
        return false;
    }

    public bool RefineCard(TripleTriadRefinement recipe, int batches = 1)
    {
        if (State.TripleTriadPlay is not null || State.Battle is not null || State.CardInventory is not { } cards || State.Economy is not { } economy || !CanUseCardMod(State.CharacterVitals))
        {
            return false;
        }
        if (!TripleTriadRefinementService.TryApply(cards, economy, recipe, out TripleTriadRefinementResult result, batches))
        {
            return false;
        }
        GameState candidate = State with { CardInventory = result.Cards, Economy = result.Economy };
        ValidateLiveEventMemoryOwners(candidate, State.EventMemory);
        State = candidate;
        return true;
    }

    public void SetTripleTriadPlay(NativeTripleTriadPlayState? play)
    {
        State = State with { TripleTriadPlay = play?.DeepCopy() };
    }

    public void CompleteTripleTriad(NativeCardInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        GameState candidate = State with { CardInventory = inventory.DeepCopy(), TripleTriadPlay = null };
        ValidateLiveEventMemoryOwners(candidate, State.EventMemory);
        State = candidate;
    }
}
