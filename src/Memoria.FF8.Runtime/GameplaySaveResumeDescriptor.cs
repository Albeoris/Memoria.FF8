using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum GameplaySaveResumeKind
{
    Field,
    World
}

public sealed record GameplaySaveFieldPosition([property: JsonRequired] float X, [property: JsonRequired] float Y, [property: JsonRequired] int Triangle, [property: JsonRequired] byte Direction);
public sealed record GameplaySaveWorldPosition([property: JsonRequired] int X, [property: JsonRequired] int Z, [property: JsonRequired] short Height, [property: JsonRequired] ushort BodyYaw, [property: JsonRequired] byte EntityType);

/// <summary>Logical destination only. Eligibility and a registered semantic reconstruction handler are separate prerequisites.</summary>
public sealed record GameplaySaveResumeDescriptor([property: JsonRequired] int Version, [property: JsonRequired] GameplaySaveResumeKind Kind, [property: JsonRequired] int FieldId, [property: JsonRequired] int? PreviousFieldId, [property: JsonRequired] GameplaySaveFieldPosition? Field, [property: JsonRequired] GameplaySaveWorldPosition? World)
{
    public void Validate()
    {
        if (Version != 1 || !Enum.IsDefined(Kind) || FieldId is < 72 or > ushort.MaxValue || PreviousFieldId is < 0 or > ushort.MaxValue)
        {
            throw new InvalidDataException("Gameplay resume descriptor has an unsupported version, kind or field identity.");
        }
        if (Kind == GameplaySaveResumeKind.Field ? Field is null || World is not null : World is null || Field is not null)
        {
            throw new InvalidDataException("Gameplay resume descriptor requires exactly the position matching its kind.");
        }
        if (Field is { } field && (!float.IsFinite(field.X) || !float.IsFinite(field.Y) || field.Triangle is < 0 or > ushort.MaxValue) || World is { BodyYaw: >= 4096 })
        {
            throw new InvalidDataException("Gameplay resume position is invalid.");
        }
    }
}
