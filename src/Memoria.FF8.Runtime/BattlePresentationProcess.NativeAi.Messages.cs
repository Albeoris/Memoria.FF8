namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{

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
