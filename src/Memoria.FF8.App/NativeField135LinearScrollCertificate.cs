using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

internal static class NativeField135LinearScrollCertificate
{
    // TODO(TD-003): admit supported linear phases through shared native state/geometry contracts
    public static bool CanSet(FieldAssets field, NativeLinearScrollQuery query)
    {
        return NativeField135ZeroCenterCertificate.Supports(field, 0) && query == new NativeLinearScrollQuery(1, 0, 0, 351, 0, 351);
    }

    public static bool SupportsPhase(FieldAssets field, SceneEventState scene)
    {
        if (!NativeField135ZeroCenterCertificate.Supports(field, scene.CameraIndex) || scene.NativeFieldLoadVersion != 1 || scene.NativeActorLayoutVersion != 1 || scene.BackgroundScrollLayers.Count != 0 || scene.BackgroundScrollPositions.Count != 1 || !scene.BackgroundScrollPositions.TryGetValue(1, out BackgroundScrollPosition? position) || position.NativeLinearVersion != 1 || position.Mode != 4 || position.Duration != 351 || position.TargetX != 351 || position.TargetY != 0 || position.Y != 0 || position.NativeActorId is not null || position.X is < 0 or > 351)
        {
            return false;
        }
        return position.Status switch
        {
            0 => position.X == 0,
            1 => position.PreviousX == 0 && position.PreviousY == 0 && position.Counter is >= 1 and <= 351 && position.X == position.Counter - 1,
            2 => position.PreviousX == 0 && position.PreviousY == 0 && position.Counter == 351 && position.X == 351,
            _ => false
        };
    }

    public static BackgroundScrollFrame? Prepare(FieldAssets field, SceneEventState scene, IReadOnlyList<ActorState> actors)
    {
        if (field.Id != 135 || scene.BackgroundScrollPositions.Count == 0)
        {
            return null;
        }
        if (!SupportsPhase(field, scene))
        {
            throw new NotSupportedException("Native final-cavern lava wrapping requires its executed original linear layer1 phase.");
        }
        BackgroundScrollFrame normal = NormalCameraCertificate.Prepare(field, scene, actors) ?? throw new NotSupportedException("Native final-cavern lava wrapping requires the original zero camera center.");
        if (normal.ActorOffset != Vector2.Zero || normal.LayerOffsets.Values.Any(offset => offset != Vector2.Zero))
        {
            throw new NotSupportedException("Native final-cavern lava wrapping cannot substitute for a nonzero camera or actor offset.");
        }
        BackgroundScrollPosition position = scene.BackgroundScrollPositions[1];
        FieldNativeBackgroundWrapFrame wrap = new(135, ProgramHash, -192, 176, -144, 128, ImmutableArray.Create(new FieldNativeBackgroundWrapLayer(1, checked((short)-position.X), 0, 352, 96)));
        return normal with { NativeWrap = wrap };
    }

    private const string ProgramHash = "4E838898400B0B44D1FFAD537A77FDD685111A7C25DF34366CD77CACE8A65344";
}
