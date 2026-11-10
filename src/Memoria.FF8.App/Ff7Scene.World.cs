using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7WorldRuntime? worldPresentationOwner;
    private Ff7WorldPresentation? worldPresentation;

    private void ResetWorldPresentation()
    {
        worldPresentationOwner = null;
        worldPresentation = null;
    }

    internal static Ff7WorldRenderFrame WorldRenderFrame(Ff7WorldProcess state)
    {
        Ff7WorldCameraState camera = state.Camera ?? new();
        return new(state.ControlledModel, state.Initialized, state.MainState, state.ViewMode, state.RadarMode, state.WorldMapState, state.PresentationCounter, state.Sky?.CloudPhase ?? 0,
            new(camera.Heading, camera.Pitch, camera.Distance, camera.GroundHeight, camera.Roll, camera.Focal),
            state.Entities.Select(entity => new Ff7WorldVisual(entity.Model, new(entity.X, entity.Y, entity.Z), (entity.Direction + entity.TurnOffset) & 4095, entity.PoseAnimation, entity.PoseFrame, entity.Flags)
            {
                GroundHeight = entity.GroundHeight,
                Terrain = entity.Terrain,
                NativeAnimation = entity.Animation,
                Elevation = entity.Elevation
            }).ToArray(),
            state.Lights.Select(light => new Memoria.FF7.Resources.Ff7WorldSkyZone(light.Index, light.X, light.Z, light.Enabled, light.Radius, light.Radius2, new(light.Red, light.Green, light.Blue), new(light.Red2, light.Green2, light.Blue2))).ToArray())
        {
            BridgeDeformation = state.Input?.Bridge?.Deformation,
            HeightSmoothVelocity = state.Input?.HeightSmoothVelocity ?? 0
        };
    }

    private void RenderWorld(Ff7WorldRuntime world, Rectangle bounds)
    {
        if (!ReferenceEquals(worldPresentationOwner, world))
        {
            worldPresentation = new(installation, world.Resources, appearance);
            worldPresentationOwner = world;
        }
        worldPresentation!.Render(triangles, bounds.Width, bounds.Height, WorldRenderFrame(world.Process));
    }
}
