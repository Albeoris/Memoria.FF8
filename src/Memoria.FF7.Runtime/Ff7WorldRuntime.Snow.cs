using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldSnowState(int PoleIndex = 0, int Countdown = 0, int WindLength = 0, int WindVelocity = 0, int CycleLength = 0, int LockedFacing = 0)
{
    public Ff7WorldSnowLayerState[]? Layers { get; init; }
}

public sealed record Ff7WorldSnowLayerState(int Layer, int Brightness = 0, int Delta = 0, int Lifetime = 0, int U = 0, int V = 0, int DisplayedBrightness = 0, int DisplayedU = 0, int DisplayedV = 0, bool Displayed = false);

public sealed partial class Ff7WorldRuntime
{
    private Ff7WorldSnowState? snow;

    private static Ff7WorldSnowState? CopySnowState(Ff7WorldSnowState? state)
    {
        return state is null ? null : state with { Layers = state.Layers?.ToArray() };
    }

    private void BeginSnowWindLayers()
    {
        Ff7WorldSnowLayerState[] layers = snow?.Layers ?? [new(1), new(2)];
        snow = (snow ?? new()) with { Layers = layers.Select(layer => layer with { Delta = layer.Brightness < 128 ? 16 : layer.Delta, Lifetime = 100 }).ToArray() };
    }

    private void AdvanceSnowWindLayers()
    {
        if (Resources.World != 3 || snow?.Layers is not { } layers)
        {
            return;
        }
        snow = snow with
        {
            Layers = layers.Select(layer =>
            {
                int brightness = Math.Clamp(layer.Brightness + layer.Delta, 0, 128);
                int delta = brightness is 0 or 128 ? 0 : layer.Delta;
                int lifetime = layer.Lifetime;
                if (lifetime > 0)
                {
                    lifetime--;
                    if (lifetime == 0)
                    {
                        delta = -16;
                    }
                }
                int angle = (int)(presentationCounter << 4) & 4095;
                int u = unchecked((byte)(layer.Layer == 1 ? inputMath.Sine(angle) >> 3 : inputMath.Cosine(angle) >> 4));
                int v = unchecked((byte)(lifetime << (layer.Layer == 1 ? 3 : 2)));
                return new Ff7WorldSnowLayerState(layer.Layer, brightness, delta, lifetime, u, v, layer.Brightness, layer.U, layer.V, brightness != 0);
            }).ToArray()
        };
    }

    private void ApplySnowWorldControls(ref Ff7WorldEntity player, ushort held, ushort pressed, ref int dx, ref int dz, bool autoPlayPreview = false)
    {
        snow ??= new();
        int angle = camera.Heading;
        int countdown = snow.Countdown;
        int length = snow.WindLength;
        int locked = snow.LockedFacing;
        int wind = snow.WindVelocity;
        int cycle = snow.CycleLength;
        if (countdown == length)
        {
            locked = player.Direction + angle;
            BeginSnowWindLayers();
        }
        else if (countdown < length)
        {
            player = player with { TargetDirection = (locked - angle) & 4095 };
        }
        if ((held & 0xF000) != 0 || countdown < length)
        {
            countdown--;
        }
        if (countdown <= 0)
        {
            countdown = cycle = WorldRandom.Next() + 128;
            length = (WorldRandom.Next() >> 3) + 64;
            wind = ((WorldRandom.Next() & 2) - 1) << 4;
        }
        int poleIndex = snow.PoleIndex;
        if (countdown >= length && (pressed & 0x20) != 0)
        {
            int model = 21 + poleIndex;
            RejectAutoPlayMovement(autoPlayPreview && !Machine.IsModelAttached(model), "Placing a new snow marker requires its native presentation-model allocation.");
            Machine.EnsurePresentationModel(model);
            Ff7WorldEntity pole = RequireEntity(model);
            Vector3 offset = inputMath.TransformRotation(new(0, 0, 300), 0, -angle, 0);
            int direction = -angle & 4095;
            entities[model] = pole with { X = Wrap(player.X + (int)offset.X, 0x48000), Y = player.Y, Z = Wrap(player.Z + (int)offset.Z, 0x38000), Direction = direction, TargetDirection = direction, ScriptDirection = null, TurnOffset = 0, PresentationEpoch = pole.PresentationEpoch + 1 };
            poleIndex = (poleIndex + 1) % 3;
        }
        int target = ((input.CameraHeadingTarget ?? camera.Heading) + (countdown < length ? wind : 0)) & 4095;
        input = input with { CameraHeadingTarget = target };
        Vector3 displacement = inputMath.TransformRotation(new(dx, 0, dz), 0, -target, 0);
        dx = (int)displacement.X;
        dz = (int)displacement.Z;
        snow = new(poleIndex, countdown, length, wind, cycle, locked) { Layers = snow.Layers };
    }

    private static void ValidateSnowState(Ff7WorldSnowState? state, int world)
    {
        if (state is not null && (world != 3 || state.PoleIndex is < 0 or > 2 || state.Countdown is < 0 or > 383 || state.WindLength is < 0 or > 95 || state.WindVelocity is not (-16 or 0 or 16) || state.CycleLength is < 0 or > 383 || state.LockedFacing is < 0 or > 8190))
        {
            throw new InvalidDataException("FFVII snow-world wind and marker state is corrupt.");
        }
        if (state?.Layers is { } layers && (layers.Length != 2 || layers.Select(layer => layer.Layer).Distinct().Count() != 2 || layers.Any(layer => layer.Layer is not (1 or 2) || layer.Brightness is < 0 or > 128 || layer.Delta is not (-16 or 0 or 16) || layer.Lifetime is < 0 or > 100 || layer.U is < 0 or > 255 || layer.V is < 0 or > 255 || layer.DisplayedBrightness is < 0 or > 128 || layer.DisplayedU is < 0 or > 255 || layer.DisplayedV is < 0 or > 255)))
        {
            throw new InvalidDataException("FFVII snow-world layer presentation state is corrupt.");
        }
    }
}
