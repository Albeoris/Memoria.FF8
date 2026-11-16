namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private Func<BattleModelOwner, int, NativeBattleAiHeapTextDirectory, int, (NativeBattleAiHeapMessage Message, string Text)>? readHostedAiHeapMessage;

    private void QueueNativeText(BattleCombatant actor, NativeBattleAiDefinition program, int text, int opcode, int delay = 0)
    {
        string display;
        BattleTextIdentity? identity = null;
        NativeBattleAiHeapMessage? heapMessage = null;
        if ((uint)text < program.Texts.Length)
        {
            display = program.Texts[text];
            identity = actor.IsPlayer ? null : BattleTextIdentity.Enemy(actor.ModelId, text);
        }
        else if (opcode is 1 or 24 or 26 or 34 && !actor.IsPlayer && program.AdmittedHeapTextDirectory is { } directory)
        {
            BattleModelOwner owner = CreatePresentationModelOwner(actor);
            (heapMessage, display) = readHostedAiHeapMessage is { } readHosted ? readHosted(owner, actor.ModelId, directory, text)
                : expandedPresentationProcess is { } process ? process.ReadAiHeapMessage(owner, actor.ModelId, directory, text)
                : ReadNativeAiHeapMessage(owner, actor.ModelId, directory, text);
        }
        else
        {
            throw new InvalidDataException("Native battle AI text index exceeds its installed bank.");
        }
        StopTurnAdvance();
        //487DF0 ->47E220;26 schedules48AC60,34 schedules48AC90 in the shared sixteen-slot callback bank.
        int duration = opcode == 24 ? 8 * (definition.NativeMessageSpeed ?? throw new NotSupportedException("Native battle message24 requires its saved message-speed BYTE.")) + 8 : 30;
        BattleEvent message = new(Frame, "NativeText", actor.Id, null, duration, display) { NativeMessageMode = opcode == 24 ? 1 : 3, NativeTextIdentity = identity, NativeAiHeapMessage = heapMessage };
        if (opcode is 26 or 34)
        {
            nativeScriptState = nativeScriptState with { Tasks = nativeScriptState.Tasks.AppendMessage(message, opcode == 34, checked((ushort)delay)) };
        }
        else if (producingExpandedAiCallback)
        {
            StageExpandedHostedControl(new HostedDispatchControl.Message(message, 0));
        }
        else
        {
            QueueNativeDescriptorMessage(message, 0);
        }
    }

    internal (NativeBattleAiHeapMessage Message, string Text) ReadNativeAiHeapMessage(BattleModelOwner owner, int modelId, NativeBattleAiHeapTextDirectory directory, int index)
    {
        NativeBattleGraphicsState graphics = NativeGraphics ?? throw new NotSupportedException("Native battle AI text requires its loaded enemy model.");
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native battle AI text requires its loaded enemy resource rows.");
        NativeBattleModelTextureCache cache = NativeModelTextureCache ?? throw new NotSupportedException("Native battle AI text requires its native resource directory.");
        if (owner is not BattleEnemyActorModel || !graphics.ActorModels.TryGet(owner, out NativeBattleGraphicsModel? model) ||
            model.OriginalResourceRow104 is not int row || (uint)row >= cpu.Rows.Length || (uint)row >= cache.Rows.Length ||
            cache.Rows[row] is not { Kind: 3 } resource || resource.ModelId != checked(modelId + 16) ||
            cache.PresentationActors is { } hostedOwners && (!hostedOwners.TryGetValue(row, out BattleModelOwner? bound) || bound != owner))
        {
            throw new NotSupportedException("Native battle AI text requires the current enemy participant's original model binding.");
        }
        NativeBattleModelCpuResourceRow source = cpu.Rows[row];
        NativeBattleModelCpuReference section = source.Require(32);
        if (section.Bank != 3 || !source.Lengths.TryGetValue(32, out int length) || length != directory.Offset)
        {
            throw new InvalidDataException("Native battle AI text field32 does not match its installed guest-heap extent.");
        }
        NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native battle AI text requires its retained native memory owners.");
        return directory.Read((address, length) => NativeBattleModelEnemyHeap.ReadGuest(cpu, staging, section, address, length), section.NativePlacementAddress, index);
    }
}
