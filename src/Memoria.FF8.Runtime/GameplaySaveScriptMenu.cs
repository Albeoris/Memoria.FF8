using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum GameplaySaveScriptMenuKind
{
    Normal,
    Save
}

public enum GameplaySaveScriptMenuPhase
{
    Active,
    Closing,
    Teardown,
    Restoring,
    FieldReentry,
    FieldResumeInitialization
}

/// <summary>Logical script-menu ownership and close timing; committed edits remain in their gameplay owners.</summary>
public sealed record GameplaySaveScriptMenu([property: JsonRequired] int Version, [property: JsonRequired] GameplaySaveScriptMenuKind Kind, [property: JsonRequired] GameplaySaveScriptMenuPhase Phase, [property: JsonRequired] long OwnerSequence, [property: JsonRequired] int ObjectId, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] long Callbacks, [property: JsonRequired] byte EntrySelection, [property: JsonRequired] ushort ReturnResult, [property: JsonRequired] bool TimerDisplayBeforeMenu, [property: JsonRequired] ushort? PreviousField, [property: JsonRequired] NativeFieldLocationSlot[] EntryLocation, [property: JsonRequired] short? SaveFade)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CombatPartyIndependent { get; init; }

    [JsonIgnore]
    public bool PendingPartyRefresh => Kind == GameplaySaveScriptMenuKind.Normal && ReturnResult == 1 && Phase != GameplaySaveScriptMenuPhase.FieldResumeInitialization;

    public void Validate(GameplaySaveFieldScripts scripts, GameplaySaveFieldScene scene, GameplaySaveFieldActors actors)
    {
        if (Version != 1 || !Enum.IsDefined(Kind) || !Enum.IsDefined(Phase) || ObjectId < 0 || ScriptId < 0 || Operation < 0 || Callbacks < 2 || Callbacks > scripts.Clock.Menu || EntryLocation is not { Length: 3 } || EntryLocation.Any(slot => slot is null || slot.RuntimeBinding is null || slot.RuntimeBinding != 255 && slot.RuntimeBinding >= actors.RegisteredActors.Length || slot.Pose is not null && slot.RuntimeBinding == 255) || (Kind == GameplaySaveScriptMenuKind.Save ? ReturnResult != 0 || EntrySelection != 1 || SaveFade is null : ReturnResult > 1 || ReturnResult == 1 && (EntrySelection & 2) == 0 || SaveFade is not null))
        {
            throw new InvalidDataException("Gameplay script menu requires supported Normal/Save state, known results and logical entry bindings.");
        }
        if (CombatPartyIndependent && (Kind != GameplaySaveScriptMenuKind.Normal || ReturnResult != 1))
        {
            throw new InvalidDataException("An independent combat lineup requires a native Normal-menu field selection.");
        }
        GameplaySaveScriptFiber? owner = scripts.Fibers.SingleOrDefault(fiber => fiber.Sequence == OwnerSequence);
        if (owner is null || owner.ObjectId != ObjectId || owner.ScriptId != ScriptId || owner.Position != Operation + 1 || owner.Complete || owner.Initialization || owner.Wait != GameplaySaveScriptWait.ScriptMenu || scripts.Fibers.Count(fiber => fiber.Wait == GameplaySaveScriptWait.ScriptMenu) != 1)
        {
            throw new InvalidDataException("Gameplay script menu has no unique waiting owner after its consumed instruction.");
        }
        if (Kind == GameplaySaveScriptMenuKind.Save)
        {
            bool validFade = Phase switch
            {
                GameplaySaveScriptMenuPhase.Active => SaveFade == 4096 && Callbacks >= 14,
                GameplaySaveScriptMenuPhase.Closing => SaveFade is > 0 and <= 4096 && (4096 - SaveFade.Value) % 409 == 0,
                _ => SaveFade == 0
            };
            if (!validFade)
            {
                throw new InvalidDataException("Gameplay save menu has an invalid logical closing fade phase.");
            }
        }
        if (Phase == GameplaySaveScriptMenuPhase.FieldResumeInitialization && scripts.Clock.Loader == 0)
        {
            throw new InvalidDataException("Gameplay script-menu resume requires its completed kind6 clock callback.");
        }
        if (PendingPartyRefresh && !scene.PartyBindings.SequenceEqual(EntryLocation.Select(slot => (int)slot.RuntimeBinding!.Value)))
        {
            throw new InvalidDataException("Pending script-menu party refresh lost its retained entry actor bindings.");
        }
        if (PendingPartyRefresh)
        {
            foreach (NativeFieldLocationSlot slot in EntryLocation)
            {
                if (slot.Pose is not { } pose)
                {
                    continue;
                }
                GameplaySaveFieldActor? actor = actors.Actors.SingleOrDefault(actor => actor.ObjectId == actors.RegisteredActors[slot.RuntimeBinding!.Value]);
                if (actor?.Pose.FixedPosition is not { } position || actor.Pose.Direction != MathF.Truncate(actor.Pose.Direction) || pose != NativeFieldLocationPose.FromPhysicalWords(position.X, position.Y, checked((ushort)actor.Pose.Triangle), checked((byte)actor.Pose.Direction)))
                {
                    throw new InvalidDataException("Pending script-menu party refresh changed its frozen entry actor pose.");
                }
            }
        }
    }
}
