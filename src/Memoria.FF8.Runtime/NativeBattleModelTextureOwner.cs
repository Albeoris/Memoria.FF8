using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The actor material of a model packet, or its invalidation by a physical overwrite.</summary>
public sealed record NativeBattleModelTextureOwner
{
    public static NativeBattleModelTextureOwner InvalidatedOwner { get; } = new(null, true);

    public string? ActorId { get; }
    public bool Invalidated { get; }

    [JsonConstructor]
    public NativeBattleModelTextureOwner(string? actorId, bool invalidated = false)
    {
        if (invalidated ? actorId is not null : string.IsNullOrWhiteSpace(actorId))
        {
            throw new InvalidDataException("A model texture owner requires a nonblank actor ID or an explicit invalidation.");
        }
        ActorId = actorId;
        Invalidated = invalidated;
    }

    public string RequireActorId()
    {
        return ActorId ?? throw new InvalidDataException("A physical packet overwrite invalidated its model texture owner.");
    }
}
