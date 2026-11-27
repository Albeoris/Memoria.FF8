using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class WorldMapScene
{
    private WorldFieldSelection? SelectWorldCallback(ref WorldState accepted, WorldState cameraState, byte scriptMode, ref NativeWorldSalaryState salary, uint currentButtons, bool? activity = null)
    {
        salary = PrepareSavedTerrainFlag(accepted, salary);
        // An already-open controls tutorial resumes even during a fresh allocation's entry hold.
        bool entryHeld = NativeWorldEntryControl.DelayAfterCallback(accepted.Frame, 0) > 0;
        if (entryHeld && salary.EventUi?.ScriptMode != 14)
        {
            return null;
        }
        NativeWorldSalaryState staged = salary;
        NativeSavedWorldState saved = staged.SavedWorldEvents ?? throw new NotSupportedException("Ordinary world callbacks require their saved-world state owner.");
        NativeWorldEventFlags flags = saved.WorldEventFlags116 ?? new NativeWorldEventFlags(0, 0);
        int entityType = accepted.NativeFootRuntime?.EntityType ?? accepted.NativeCarRuntime?.EntityType ?? accepted.NativeTrainRuntime?.EntityType ?? accepted.NativeScriptedRuntime?.Path.EntityType ?? (accepted.NativeChocoboRuntime is not null ? 49 : accepted.NativeRagnarokRuntime is not null ? 50 : 48);
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
        WorldState eventState = accepted;
        int choiceResult = -1;
        WorldFieldSelection? selected = assets.WorldEvents.SelectWorldEvent(input, shape => IsNearWorldEventShape(eventState, cameraState, shape), flags, EvaluatePredicate, ExecuteCommand);

        bool? EvaluatePredicate(ushort opcode, ushort operand)
        {
            switch (opcode)
            {
                case 0xFF29:
                    choiceResult = staged.EventUi?.ReadChoiceResult(operand) ?? -1;
                    return choiceResult >= 0;
                case 0xFF2A:
                    return choiceResult == operand;
                case 0xFF2F:
                    NativeWorldForestState forest = eventState.NativeFootRuntime?.Forest ?? throw new NotSupportedException("World event random selection requires its shared foot random stream.");
                    forest = forest.DrawRandom(assets.ForestTables.Random, out byte high);
                    forest = forest.DrawRandom(assets.ForestTables.Random, out byte low);
                    eventState = PublishFootForest(eventState, forest);
                    return ((high << 8) | low) < operand;
                default:
                    return EvaluateWorldEventPredicate(eventState, scriptMode, staged.SavedWorldEvents!, opcode, operand, callbackInput, staged.EventUi);
            }
        }

        void ExecuteCommand(ushort opcode, ushort operand)
        {
            if (opcode == 0xFF2B)
            {
                staged = StageWorldEventBattle(eventState, staged, operand);
                return;
            }
            NativeWorldMessage? message = opcode is 0xFF1F or 0xFF23 ? PrepareWorldMessage(unchecked((sbyte)operand), opcode == 0xFF23 ? unchecked((sbyte)(operand >> 8)) : operand >> 8) : null;
            staged = staged.WithWorldEventCommand(opcode, operand, message);
        }

        byte modeAfterCommands = accepted.NativeRagnarokRuntime?.Transfer is not null ? scriptMode : staged.EventUi?.ScriptMode ?? scriptMode;
        if (selected is null && staged.RandomEncounters?.Pending is null && modeAfterCommands == 0 && !entryHeld)
        {
            NativeWorldGroundCacheState cache = accepted.NativeFootRuntime?.GroundCache ?? accepted.NativeCarRuntime?.GroundCache ?? accepted.NativeGardenRuntime?.GroundCache ?? accepted.NativeRagnarokRuntime?.GroundCache ?? accepted.NativeChocoboRuntime?.GroundCache ?? throw new NotSupportedException("World terrain entry requires its accepted ground owner.");
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
            selected = assets.FieldEntries.Select(input, entrance == true ? (byte)8 : (byte)0, EvaluatePredicate);
        }
        accepted = eventState;
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
        byte[] bytes = assets.ReadWorldChannelMessageBytes(message);
        return binding with { Text = NativeWorldTextState.Create(System.Collections.Immutable.ImmutableArray.CreateRange(bytes), assets.ReadWorldMessageSpeed(speed)) };
    }

    private NativeWorldMessage PrepareFuelMessage(byte remaining)
    {
        byte speed = session.State.InputConfiguration?.FieldMessageSpeedByte ?? throw new NotSupportedException("World fuel text requires its saved native field-message speed.");
        NativeWorldMessage binding = assets.ReadNativeFuelMessage(remaining);
        return binding with { Text = NativeWorldTextState.Create(System.Collections.Immutable.ImmutableArray.CreateRange(assets.ReadFuelMessageBytes(remaining)), assets.ReadWorldMessageSpeed(speed)) };
    }
}
