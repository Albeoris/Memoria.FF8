using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>A supported logical field execution continuation. Current resources are rebuilt; completed initialization is never replayed.</summary>
public sealed record GameplaySaveFieldContinuation([property: JsonRequired] int Version, [property: JsonRequired] GameplaySaveFieldScripts Scripts, [property: JsonRequired] GameplaySaveFieldScene Scene, [property: JsonRequired] GameplaySaveFieldActors Actors)
{
    public void Validate()
    {
        if (Version != 1 || Scripts is null || Scene is null || Actors is null)
        {
            throw new InvalidDataException("Unsupported or incomplete gameplay field continuation.");
        }
        Scripts.Validate();
        Scene.Validate();
        Actors.Validate();
        if (Scripts.FieldId != Actors.FieldId || Scene.PartyBindings.Any(binding => binding != 255 && binding >= Actors.RegisteredActors.Length))
        {
            throw new InvalidDataException("Gameplay field continuation owners disagree about the current field or registered actor layout.");
        }
    }

    public GameplaySaveFieldContinuation Detach()
    {
        Validate();
        return GameplaySaveCodec.Detach(this);
    }

    public GameplaySaveFieldPosition GetLeaderPosition()
    {
        Validate();
        int binding = Scene.PartyBindings[0];
        if (binding == 255 || (uint)binding >= Actors.RegisteredActors.Length)
        {
            throw new NotSupportedException("Gameplay continuation requires a current registered leader for its field resume descriptor.");
        }
        int objectId = Actors.RegisteredActors[binding];
        GameplaySaveFieldActor actor = Actors.Actors.Single(item => item.ObjectId == objectId);
        if (actor.Pose.Direction != MathF.Truncate(actor.Pose.Direction))
        {
            throw new NotSupportedException("Gameplay field resume requires the current exact native facing byte.");
        }
        return new(actor.Pose.Position.X, actor.Pose.Position.Y, actor.Pose.Triangle, checked((byte)actor.Pose.Direction));
    }

    public void ValidateResume(GameplaySaveResumeDescriptor resume, GameplaySaveSceneFacts facts)
    {
        ArgumentNullException.ThrowIfNull(resume);
        ArgumentNullException.ThrowIfNull(facts);
        resume.Validate();
        facts.Validate();
        GameplaySaveFieldPosition position = GetLeaderPosition();
        GameplaySaveFieldActor actor = Actors.Actors.Single(item => item.ObjectId == Actors.RegisteredActors[Scene.PartyBindings[0]]);
        if (resume.Kind != GameplaySaveResumeKind.Field || resume.FieldId != Scripts.FieldId || facts.FieldId != Scripts.FieldId || facts.Origin != GameplaySaveSceneFactsOrigin.Field || resume.Field != position || actor.Identity.CharacterId != facts.FieldParty[0])
        {
            throw new InvalidDataException("Gameplay field resume descriptor contradicts its current logical continuation leader.");
        }
    }
}
