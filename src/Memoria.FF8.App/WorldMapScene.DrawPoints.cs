using System.Globalization;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Events;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    public Func<NativeDrawPointCatalog>? ReadDrawPointCatalog { get; set; }

    private NativeDrawPointCatalog DrawPointCatalog => ReadDrawPointCatalog?.Invoke() ?? throw new NotSupportedException("World Draw requires the installed draw-point catalog.");

    private WorldDrawRecipient[] DrawPointParty(NativeWorldDrawPoint point)
    {
        NativeDrawPointCatalog catalog = DrawPointCatalog;
        NativeCharacterVitals vitals = session.State.CharacterVitals?.RebuildParty() ?? throw new NotSupportedException("World Draw requires its saved party.");
        if (catalog.KernelHash != vitals.KernelSha256)
        {
            throw new InvalidDataException("World Draw and the party use different kernel sources.");
        }
        NativeInputConfiguration input = session.State.InputConfiguration ?? throw new NotSupportedException("World Draw requires its saved command restrictions.");
        bool drawEnabled = (input.CommandRestrictions & 8) == 0;
        byte[] fieldParty = NativeWorldSaveCompatibility.InitialParty(session.State);
        byte[] combat = vitals.CombatParty.Where(row => row != byte.MaxValue).ToArray();
        int FieldSlot(byte row)
        {
            int slot = Array.IndexOf(fieldParty, vitals.Records[row].CharacterId);
            return slot >= 0 ? slot : fieldParty.Length;
        }
        return combat.OrderBy(FieldSlot).Select(row =>
        {
            NativeCharacterVital character = vitals.Records[row];
            int capacity = vitals.MagicSystem.SharedMagicStock ? vitals.GetMagicCapacity(row, point.MagicId) : NativeCharacterVitals.GetMagicCapacity(character, point.MagicId);
            return new WorldDrawRecipient(row, character.CharacterId, drawEnabled && catalog.HasDrawCommand(character), capacity, NativeCharacterVitals.IsMagicJunctioned(character, point.MagicId));
        }).ToArray();
    }

    private readonly record struct WorldDrawRecipient(int Row, int Identity, bool CanDraw, int Capacity, bool Junctioned);

    private DialogueState? DrawPointDialogue(NativeWorldDrawPoint point)
    {
        const int operation = 0x54E9B0;
        const int customMessage = -2; // The original window receives a composed text buffer with this selector.
        LocalizationCatalog? localization = resources?.Localization;
        string? language = resources?.Source.Language;
        switch (point.Phase)
        {
            case NativeWorldDrawPointPhase.NoDraw:
                return Message(3, 18, assets.ReadWorldMessage(18));
            case NativeWorldDrawPointPhase.Depleted:
                return Message(3, 19, assets.ReadWorldMessage(19));
            case NativeWorldDrawPointPhase.Found:
                return Message(3, customMessage, assets.ComposeWorldMessage(3, Fragment, MagicName()));
            case NativeWorldDrawPointPhase.Choose:
                WorldDrawRecipient[] party = DrawPointParty(point);
                string text = assets.ComposeWorldMessage(1, Fragment);
                foreach (WorldDrawRecipient member in party)
                {
                    text += assets.ComposeWorldMessage(2, Fragment, Fragment(member.CanDraw && member.Capacity == 0 ? 41 : 42), Fragment(30 + member.Identity), Fragment(member.Junctioned ? 47 : 48));
                }
                return Message(4, customMessage, text) with { FirstChoice = 1, LastChoice = party.Length + 1, InitialChoice = 2, CancelChoice = 1 };
            case NativeWorldDrawPointPhase.StockFull:
                return Message(point.Receiver + 5, 22, assets.ReadWorldMessage(22));
            case NativeWorldDrawPointPhase.DrawUnavailable:
                return Message(point.Receiver + 5, 23, assets.ReadWorldMessage(23));
            case NativeWorldDrawPointPhase.Acquired:
                int identity = session.State.CharacterVitals!.Records[point.Receiver].CharacterId;
                return Message(3, customMessage, assets.ComposeWorldMessage(4, Fragment, Fragment(30 + identity), MagicName(), point.Quantity.ToString(CultureInfo.InvariantCulture)));
            default:
                return null;
        }

        DialogueState Message(int window, int message, string text)
        {
            var layout = assets.ReadWorldMessageWindow(window);
            return new DialogueState(layout.Channel, message, text, layout.X, layout.Y, Operation: operation);
        }

        string Fragment(int id) => Translate("messages_world", id, assets.ReadWorldMessage(id));

        string MagicName() => Translate(LocalizationSourceExporter.KernelTable("MagicSpells", "Name"), point.MagicId, DrawPointCatalog.MagicNames[point.MagicId]);

        string Translate(string table, int id, string original)
        {
            if (localization is null || language is null || !localization.Values.TryGetValue(LocalizationSourceExporter.IndexedKey(language, table, id), out LocalizationValue? value) || value.Text == LocalizationValue.InvalidTarget)
            {
                return original;
            }
            return value.RenderedText;
        }
    }

    private WorldState BeginWorldDrawPoint(WorldState state, ref NativeWorldSalaryState salary, List<GlobalSoundRequest> sounds)
    {
        if (state.NativeFootRuntime is not { EntityType: < 10 or 128 } foot || salary.EventUi is not { ScriptMode: 0 } || salary.InputHistory is not { CurrentButtons: var current, PreviousButtons: ushort prior } || ((current & ~prior) & 0x40) == 0 || NativeWorldEntryControl.DelayAfterCallback(state.Frame, 0) > 0)
        {
            return state;
        }
        if (transportContactPublication is { } contact && contact.Frame == state.Frame && contact.BroadContact != -1)
        {
            return state;
        }
        NativeWorldGroundCacheState cache = foot.GroundCache ?? throw new InvalidDataException("World Draw requires its accepted ground.");
        byte story = salary.WorldEntryMemory?.StoryByte266 ?? throw new InvalidDataException("World Draw requires field byte266.");
        int[] matches = cache.Alternatives.Select(alternative => assets.FindNativeDrawPoint(alternative.CurrentGround, story)).Distinct().ToArray();
        if (matches.Length != 1)
        {
            throw new NotSupportedException("Retained world ground alternatives disagree on the active Draw point.");
        }
        if (matches[0] < 0)
        {
            return state;
        }
        if (transportContactPublication is not { } publication || publication.Frame != state.Frame)
        {
            if (foot.NormalObjects is not null && HasNormalFootObjectContact(foot, state.X, state.Z, state.NativeHeight, 200) ||
                foot.StationTrains is not null && HasStationContact(foot, state.X, state.Z, state.NativeHeight, 200))
            {
                return state;
            }
        }
        byte index = checked((byte)(matches[0] + 128));
        byte definition = DrawPointCatalog.PointBytes[index];
        NativeDrawPointMemory bank = salary.EventRandom?.DrawPoints ?? throw new NotSupportedException("World Draw requires its saved depletion bank.");
        NativeWorldDrawPoint point = new(index, definition, NativeWorldDrawPointPhase.Depleted);
        if (bank.GetDepletion(index) >= 2)
        {
            return state with { DrawPoint = point };
        }
        if (point.MagicId is < 1 or > 56)
        {
            throw new InvalidDataException("World Draw point has an invalid spell selector.");
        }
        NativeWorldForestState forest = foot.Forest!.DrawRandom(assets.ForestTables.Random, out byte roll);
        int basis = ((definition & 0x9F) | 0x40) >> 5;
        int quantity = basis * (roll + 128) / 512 + 1 + session.State.GameplayPreferences.Tuning.DrawBonus;
        NativeWorldDrawPointPhase phase = DrawPointParty(point).Any(member => member.CanDraw) ? NativeWorldDrawPointPhase.Found : NativeWorldDrawPointPhase.NoDraw;
        PrepareWorldDrawPointSound(false, ref salary, sounds);
        return PublishDrawPointForest(state with { DrawPoint = point with { Phase = phase, Quantity = quantity } }, forest);
    }

    private WorldState AdvanceWorldDrawPoint(WorldState state, PlayerAction action, ref NativeWorldSalaryState salary, List<GlobalSoundRequest> sounds)
    {
        NativeWorldDrawPoint point = state.DrawPoint!;
        if (point.Phase == NativeWorldDrawPointPhase.Acquiring)
        {
            return state with { DrawPoint = point.RemainingCallbacks == 1 ? point with { Phase = NativeWorldDrawPointPhase.Acquired, RemainingCallbacks = 0 } : point with { RemainingCallbacks = point.RemainingCallbacks - 1 } };
        }
        if (point.Phase == NativeWorldDrawPointPhase.Choose)
        {
            if (action.Kind != PlayerActionKind.Choose)
            {
                return state;
            }
            if (action.Value == 1)
            {
                return state with { DrawPoint = null };
            }
            int slot = action.Value - 2;
            WorldDrawRecipient member = DrawPointParty(point)[slot];
            if (!member.CanDraw || member.Capacity != 0)
            {
                return state with { DrawPoint = point with { Phase = member.CanDraw ? NativeWorldDrawPointPhase.StockFull : NativeWorldDrawPointPhase.DrawUnavailable, Receiver = slot } };
            }
            NativeCharacterVitals vitals = session.State.CharacterVitals!;
            int acquired = 0;
            while (acquired < point.Quantity)
            {
                vitals = vitals.AddMagicUnit(member.Row, point.MagicId, out int result);
                if (result != 0)
                {
                    break;
                }
                acquired++;
            }
            point = point with { Phase = NativeWorldDrawPointPhase.Acquiring, Receiver = member.Row, Quantity = acquired, RemainingCallbacks = NativeWorldDrawPoint.AcquisitionCallbacks };
            salary = salary.AcquireFromWorldDrawPoint(point, vitals);
            NativeWorldForestState forest = state.NativeFootRuntime!.Forest!;
            // The original acquisition initializes eight ribbons with six shared random draws each.
            // TODO(TD-039) Render the original world-space ribbons using their projected control points.
            for (int index = 0; index < 8 * 6; index++)
            {
                forest = forest.DrawRandom(assets.ForestTables.Random, out _);
            }
            PrepareWorldDrawPointSound(true, ref salary, sounds);
            return PublishDrawPointForest(state with { DrawPoint = point }, forest);
        }
        if (salary.InputHistory is not { CurrentButtons: var current, PreviousButtons: ushort prior } || ((current & ~prior) & 0x40) == 0)
        {
            return state;
        }
        return state with { DrawPoint = point.Phase is NativeWorldDrawPointPhase.Found or NativeWorldDrawPointPhase.StockFull or NativeWorldDrawPointPhase.DrawUnavailable ? point with { Phase = NativeWorldDrawPointPhase.Choose, Receiver = -1 } : null };
    }

    private void PrepareWorldDrawPointSound(bool acquire, ref NativeWorldSalaryState salary, List<GlobalSoundRequest> sounds)
    {
        var (id, operation, pan, volume) = assets.ReadWorldDrawPointSound(acquire);
        NativeGlobalSoundProcess process = salary.SoundProcess ?? throw new NotSupportedException("World Draw requires the global sound allocator.");
        PreparedGlobalSoundEmission prepared = (PrepareGlobalSound ?? throw new NotSupportedException("World Draw requires the global sound producer."))(id, operation, pan, volume, process);
        salary = salary.WithSoundProcess(prepared.Process);
        sounds.Add(prepared.Request);
    }

    private static WorldState PublishDrawPointForest(WorldState state, NativeWorldForestState forest)
    {
        return state with
        {
            NativeFootRuntime = state.NativeFootRuntime! with { Forest = forest },
            NativeGardenRuntime = state.NativeGardenRuntime is { } garden ? garden with { Forest = forest } : null,
            NativeRagnarokRuntime = state.NativeRagnarokRuntime is { } ragnarok ? ragnarok with { Forest = forest } : null
        };
    }
}
