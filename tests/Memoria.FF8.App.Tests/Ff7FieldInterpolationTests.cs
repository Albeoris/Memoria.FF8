using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7FieldInterpolationTests
{
    [Theory]
    [InlineData(150)]
    [InlineData(200)]
    public void AcceleratedMovementAndFollowCameraShareOnePresentationFraction(int movementPercent)
    {
        int distance = movementPercent * 4096 / 100;
        Ff7Actor previousActor = new() { Slot = 0, Entity = 3, Model = 0, Visible = true };
        Ff7Actor currentActor = previousActor with { X4096 = distance, AnimationFrame16 = 16 };
        Ff7FieldProcess previous = new() { Actors = [previousActor], CameraX = 0, CameraY = 0 };
        Ff7FieldProcess current = new() { Actors = [currentActor], CameraX = -movementPercent, CameraY = movementPercent / 2 };
        string nativeBefore = JsonSerializer.Serialize(new[] { previous, current });
        Vector2? previousPoint = null;
        Vector2? firstStep = null;
        foreach (float fraction in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
        {
            Ff7Actor actor = Ff7ActorPresentation.Interpolate(previousActor, currentActor, fraction, 30);
            Vector2 camera = Ff7FieldPresentation.InterpolateCameraOffset(previous, current, fraction);
            Vector2 screen = new(actor.X4096 / 4096f + camera.X, actor.Y4096 / 4096f + camera.Y);
            if (previousPoint is Vector2 before)
            {
                Vector2 step = screen - before;
                firstStep ??= step;
                Assert.InRange(Vector2.Distance(step, firstStep.Value), 0, 0.001f);
            }
            previousPoint = screen;
        }
        Assert.Equal(nativeBefore, JsonSerializer.Serialize(new[] { previous, current }));
    }

    [Fact]
    public void InstantScriptedCameraCutRemainsAnImmediateNativeEndpoint()
    {
        Ff7FieldProcess previous = new() { CameraX = 10, CameraY = 20, CameraFollowPlayer = true };
        Ff7FieldProcess cut = previous with { CameraX = 130, CameraY = -70, CameraFollowPlayer = false };
        Assert.Equal(new Vector2(130, -70), Ff7FieldPresentation.InterpolateCameraOffset(previous, cut, 0.25f));
        Ff7FieldProcess scrolling = cut with { CameraScroll = new(5, 10, 20, 130, -70, 10) };
        Assert.Equal(new Vector2(40, -2.5f), Ff7FieldPresentation.InterpolateCameraOffset(previous, scrolling, 0.25f));
    }

    [Fact]
    public void CameraShakeSharesTheSameEndpointsAndMovieCameraKeepsItsNativeOwner()
    {
        Ff7FieldProcess previous = new() { CameraX = 10, CameraY = 20, CameraShakeX = new(Enabled: false, Offset: -4), CameraShakeY = new(Enabled: true, Offset: 2) };
        Ff7FieldProcess current = previous with { CameraX = 20, CameraY = 40, CameraShakeX = new(Enabled: true, Offset: 6), CameraShakeY = new(Enabled: false, Offset: -8) };
        Assert.Equal(new Vector2(16, 27), Ff7FieldPresentation.InterpolateCameraOffset(previous, current, 0.5f));
        PcFieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 0, default, unchecked((ushort)31 | (ushort)-20 << 16), 386);
        current = current with { Movie = new(0, true, 0, false, 1) };
        Assert.Equal(new Vector2(-31, -20), Ff7FieldPresentation.InterpolateCameraOffset(previous, current, 0.5f, camera));
        Assert.Equal(Ff7Scene.FieldCameraOffset(previous), Ff7FieldPresentation.InterpolateCameraOffset(previous, previous, 0.5f));
    }
}
