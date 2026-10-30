using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldFieldSelection? SelectWorldCallback(WorldState accepted, WorldState cameraState, byte scriptMode, ref NativeWorldSalaryState salary, uint currentButtons, bool? activity = null)
    {
        salary = PrepareSavedTerrainFlag(accepted, salary);
        if (NativeWorldEntryControl.DelayAfterCallback(accepted.Frame, 0) > 0)
        {
            return null;
        }
        NativeWorldSalaryState staged = salary;
        NativeSavedWorldState saved = staged.SavedWorldEvents ?? throw new NotSupportedException("Ordinary world callbacks require their saved-world state owner.");
        NativeWorldEventFlags flags = saved.WorldEventFlags116 ?? new NativeWorldEventFlags(0, 0);
        int entityType = accepted.NativeFootRuntime?.EntityType ?? accepted.NativeCarRuntime?.EntityType ?? accepted.NativeTrainRuntime?.EntityType ?? accepted.NativeScriptedRuntime?.Path.EntityType ?? (accepted.NativeRagnarokRuntime is not null ? 50 : 48);
        ushort story = staged.WorldEntryMemory?.StoryWord ?? WorldEntryVariables.Read(session.State).StoryWord;
        NativeWorldInputHistory history = staged.InputHistory ?? throw new NotSupportedException("World event input requires its actual shared input callback publication.");
        if (history.CurrentButtons != currentButtons)
        {
            throw new InvalidDataException("World event input differs from the actual sampled callback.");
        }
        if (activity is null && (staged.EventUi?.ScriptMode ?? scriptMode) != 0)
        {
            // The input callback resets activity; ordinary action writers require script mode zero.
            activity = false;
        }
        //PlayerAction is the digital input adapter; no analog controller axes are submitted.
        WorldFieldInputContext callbackInput = new(currentButtons, history.PreviousButtons, activity, -1, -1, -1, -1);
        WorldFieldSelectionInput input = new(accepted.X, accepted.Z, story, entityType);
        WorldFieldSelection? selected = assets.WorldEvents.SelectWorldEvent(input, shape => IsNearWorldEventShape(accepted, cameraState, shape), flags, (opcode, operand) => EvaluateWorldEventPredicate(accepted, scriptMode, staged.SavedWorldEvents!, opcode, operand, callbackInput, staged.EventUi), (opcode, operand) => staged = staged.WithWorldEventCommand(opcode, operand, opcode == 0xFF1F ? PrepareWorldMessage(unchecked((sbyte)operand), unchecked((byte)(operand >> 8))) : null));
        byte modeAfterCommands = accepted.NativeRagnarokRuntime?.Transfer is not null ? scriptMode : staged.EventUi?.ScriptMode ?? scriptMode;
        if (selected is null && modeAfterCommands == 0)
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
            selected = assets.FieldEntries.Select(input, entrance == true ? (byte)8 : (byte)0, (opcode, operand) => EvaluateWorldEventPredicate(accepted, scriptMode, staged.SavedWorldEvents!, opcode, operand, callbackInput, staged.EventUi));
        }
        salary = staged;
        return selected;
    }

    private NativeWorldMessage PrepareWorldMessage(int window, int message)
    {
        if (window == 1 && message == 43)
        {
            return PrepareFuelMessage(0);
        }
        byte speed = session.State.InputConfiguration?.FieldMessageSpeedByte ?? throw new NotSupportedException("World text requires its saved native field-message speed; migrate the historical input configuration explicitly.");
        NativeWorldMessage binding = assets.ReadNativeWorldMessage(window, message);
        byte[] bytes = assets.ReadWorldMessageBytes(message);
        // TODO(TD-003): world page waits require the native focused-channel input owner.
        if (bytes.Any(value => value is 1 or 7))
        {
            throw new NotSupportedException("World message pages require their executed native channel focus and input owner.");
        }
        return binding with { Text = NativeWorldTextState.Create(System.Collections.Immutable.ImmutableArray.CreateRange(bytes), assets.ReadWorldMessageSpeed(speed)) };
    }

    private NativeWorldMessage PrepareFuelMessage(byte remaining)
    {
        byte speed = session.State.InputConfiguration?.FieldMessageSpeedByte ?? throw new NotSupportedException("World fuel text requires its saved native field-message speed.");
        NativeWorldMessage binding = assets.ReadNativeFuelMessage(remaining);
        return binding with { Text = NativeWorldTextState.Create(System.Collections.Immutable.ImmutableArray.CreateRange(assets.ReadFuelMessageBytes(remaining)), assets.ReadWorldMessageSpeed(speed)) };
    }
}
