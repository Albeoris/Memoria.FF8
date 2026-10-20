using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuake38SavedActor([property: JsonRequired] short? Rotation12, [property: JsonRequired] short? Rotation14, [property: JsonRequired] short? Rotation16, [property: JsonRequired] ushort? RotationPadding18, [property: JsonRequired] NativeBattleVector? Position28, [property: JsonRequired] short? Vertical34, [property: JsonRequired] short BouncePhase, [property: JsonRequired] short Velocity)
{
    public static NativeBattleQuake38SavedActor FromColdProcess() => new(0, 0, 0, 0, new(0, 0, 0), 0, 0, 0);
    public static NativeBattleQuake38SavedActor Capture8DBBA0(NativeBattleGraphicsModel model) => new(model.Rotation12, model.Rotation14, model.Rotation16, model.RotationPadding18, model.Transform?.Position28, model.Transform?.Vertical34, 0, 0);

    public NativeBattleGraphicsModel Restore8DBE40(NativeBattleGraphicsModel model)
    {
        NativeBattleModelTransform? transform = model.Transform;
        if (Position28 is { } position)
        {
            transform = (transform ?? throw new NotSupportedException("Native Quake restoration requires its current initialized actor transform.")) with { Position28 = position, Vertical34 = Vertical34 ?? throw new NotSupportedException("Native Quake restoration requires its saved vertical WORD.") };
        }
        else if (transform is not null)
        {
            throw new NotSupportedException("Native Quake cannot restore unknown saved actor position bytes into an initialized transform.");
        }
        return model with { Rotation12 = Rotation12, Rotation14 = Rotation14, Rotation16 = Rotation16, RotationPadding18 = RotationPadding18, Transform = transform };
    }
}
