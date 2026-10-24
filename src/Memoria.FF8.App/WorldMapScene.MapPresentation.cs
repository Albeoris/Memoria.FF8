using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    internal static byte MapDisplayMode(WorldState state)
    {
        return state.NativeFootRuntime?.MapDisplayMode ?? state.NativeCarRuntime?.MapDisplayMode ?? state.NativeGardenRuntime?.MapDisplayMode ?? state.NativeRagnarokRuntime?.MapDisplayMode ?? state.NativeTrainRuntime?.MapDisplayMode ?? state.NativeScriptedRuntime?.MapDisplayMode ?? throw new NotSupportedException("World map presentation requires its retained native display mode.");
    }

    private static WorldState PrepareMapDisplay(WorldState state, NativeWorldSalaryState salary, out byte scriptMode)
    {
        NativeWorldEventUi ui = salary.EventUi ?? throw new NotSupportedException("World map controls require the current world UI owner.");
        scriptMode = state.NativeScriptedRuntime?.Path.Mode ?? ui.ScriptMode;
        if (salary.InputHistory is not { } input || (input.CurrentButtons & 0x100) == 0 || state.NativeScriptedRuntime?.Path.EndpointRequested == true || NativeWorldEntryControl.DelayAfterCallback(state.Frame + 1, 0) > 0)
        {
            return state;
        }
        byte prior = MapDisplayMode(state);
        bool ordinaryMessageActive = ui.Windows.Any(window => window.WindowId < 9) || state.NativeTrainRuntime?.Primary.Pause == -1;
        NativeWorldMapDisplay selection = NativeWorldMapControl.Select(prior, scriptMode, input, ordinaryMessageActive, ui.Windows.Any(window => window.WindowId >= 9));
        scriptMode = selection.ScriptMode;
        if (prior == selection.Mode)
        {
            return state;
        }
        NativeFootWorldRuntimeState? foot = state.NativeFootRuntime is { } currentFoot ? currentFoot with { MapDisplayMode = selection.Mode } : null;
        NativeGardenWorldRuntimeState? garden = state.NativeGardenRuntime is { } currentGarden ? currentGarden with { MapDisplayMode = selection.Mode } : null;
        NativeRagnarokWorldRuntimeState? ragnarok = state.NativeRagnarokRuntime;
        if (ragnarok is not null)
        {
            ragnarok = ragnarok with { MapDisplayMode = selection.Mode };
        }
        return state with
        {
            NativeFootRuntime = foot,
            NativeCarRuntime = state.NativeCarRuntime is { } car ? car with { MapDisplayMode = selection.Mode } : null,
            NativeGardenRuntime = garden,
            NativeRagnarokRuntime = ragnarok,
            NativeTrainRuntime = state.NativeTrainRuntime is { } train ? train with { MapDisplayMode = selection.Mode } : null,
            NativeScriptedRuntime = state.NativeScriptedRuntime is { } scripted ? scripted with { MapDisplayMode = selection.Mode } : null
        };
    }

    private static WorldState CompleteMapFootMotor(WorldState state, uint buttons)
    {
        NativeFootWorldRuntimeState foot = state.NativeFootRuntime!;
        if (foot.EntityType == 2 && foot.NormalObjects is { } objects && NativeWorldEntryControl.DelayAfterCallback(state.Frame, 0) == 0)
        {
            foot = foot with { NormalObjects = objects with { ForcedAnimationC76628 = -1 } };
        }
        state = state with { NativeFootRuntime = foot };
        NativeWorldFootCameraState camera = NativeWorldFootCameraState.Create(state);
        if (state.NativeGardenRuntime is { } garden)
        {
            garden = garden with { Forest = foot.Forest!, CameraPitch = foot.Forest!.CameraPitch, ProjectionCamera = camera, Buttons = garden.Buttons.Observe(buttons) };
            garden = garden.PublishBody(new(state.X, state.NativeHeight, -state.Z), 0, checked((short)state.BodyYaw), 0, gardenControlled: false);
            state = state with { NativeGardenRuntime = garden };
        }
        if (state.NativeRagnarokRuntime is { } ragnarok)
        {
            NativeRagnarokWorldObject previous = ragnarok.Objects[0];
            short pitch = checked((short)(previous.Pitch + Math.Clamp(-(int)previous.Pitch, -16, 16)));
            ragnarok = ragnarok with { Forest = foot.Forest, ProjectionCamera = camera, PreviousButtons = buttons };
            ragnarok = PublishRagnarokFootBody(ragnarok, new(state.X, state.NativeHeight, -state.Z), pitch, checked((short)state.BodyYaw), previous.Roll);
            state = state with { NativeRagnarokRuntime = ragnarok };
        }
        return state;
    }
}
