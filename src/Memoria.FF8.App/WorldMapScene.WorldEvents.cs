using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldFieldSelection? SelectWorldCallback(WorldState accepted, WorldState cameraState, byte scriptMode, ref NativeWorldSalaryState salary, uint currentButtons, uint? previousButtons, bool? activity = null)
    {
        if (NativeWorldEntryControl.DelayAfterCallback(accepted.Frame, 0) > 0)
        {
            return null;
        }
        NativeWorldSalaryState staged = salary;
        NativeSavedWorldState saved = staged.SavedWorldEvents ?? throw new NotSupportedException("Ordinary world callbacks require their saved-world state owner.");
        NativeWorldEventFlags flags = saved.WorldEventFlags116 ?? new NativeWorldEventFlags(0, 0);
        int entityType = accepted.NativeFootRuntime?.EntityType ?? accepted.NativeCarRuntime?.EntityType ?? accepted.NativeTrainRuntime?.EntityType ?? accepted.NativeScriptedRuntime?.Path.EntityType ?? (accepted.NativeRagnarokRuntime is not null ? 50 : 48);
        ushort story = staged.WorldEntryMemory?.StoryWord ?? WorldEntryVariables.Read(session.State).StoryWord;
        //PlayerAction is the digital input adapter; no analog controller axes are submitted.
        WorldFieldInputContext callbackInput = new(currentButtons, previousButtons, activity, -1, -1, -1, -1);
        WorldFieldSelectionInput input = new(accepted.X, accepted.Z, story, entityType);
        WorldFieldSelection? selected = assets.WorldEvents.SelectWorldEvent(input, shape => IsNearWorldEventShape(accepted, cameraState, shape), flags, (opcode, operand) => EvaluateWorldEventPredicate(accepted, scriptMode, staged.SavedWorldEvents!, opcode, operand, callbackInput), (opcode, operand) => staged = staged.WithWorldEventCommand(opcode, operand));
        if (selected is null && scriptMode == 0)
        {
            NativeWorldGroundCacheState cache = accepted.NativeFootRuntime?.GroundCache ?? accepted.NativeCarRuntime?.GroundCache ?? accepted.NativeGardenRuntime?.GroundCache ?? accepted.NativeRagnarokRuntime?.GroundCache ?? throw new NotSupportedException("World terrain entry requires its accepted ground owner.");
            bool? entrance = null;
            foreach (NativeWorldGroundAlternative alternative in cache.Alternatives)
            {
                bool candidate = (assets.ResolveNativeGroundIdentity(alternative.CurrentGround, replacements).TextureFlags & 8) != 0;
                if (entrance is bool prior && prior != candidate)
                {
                    throw new NotSupportedException("World terrain entry differs across retained accepted ground alternatives.");
                }
                entrance = candidate;
            }
            selected = assets.FieldEntries.Select(input, entrance == true ? (byte)8 : (byte)0, (opcode, operand) => EvaluateWorldEventPredicate(accepted, scriptMode, staged.SavedWorldEvents!, opcode, operand, callbackInput));
        }
        salary = staged;
        return selected;
    }
}
