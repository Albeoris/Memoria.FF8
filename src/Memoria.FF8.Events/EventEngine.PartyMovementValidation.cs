using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void ValidateNativePartyMovement(IReadOnlyList<ActorState> savedActors, SceneEventState scene, IReadOnlyList<Fiber> restored, PartyJoinState? joinState, PartySplitState? splitState)
    {
        Dictionary<long, Fiber> bySequence = restored.ToDictionary(fiber => fiber.Sequence);
        if (joinState is PartyJoinState join && (!bySequence.TryGetValue(join.FiberSequence, out Fiber? caller) || caller.ObjectId != join.CallerObjectId || caller.Complete || caller.WaitReason != WaitReason.PartyJoin || (uint)caller.Position >= program!.Operations.Count || program!.Operations[caller.Position].Opcode.ToString() != "JOIN" || scene.NativeActorLayoutVersion != 1 || join.Targets.Length > 2 || join.Targets.Select(target => target.ObjectId).Distinct().Count() != join.Targets.Length || join.Targets.Any(target => !scene.NativeActorObjectIds.Contains(target.ObjectId) || target.ObjectId == join.CallerObjectId || savedActors[target.ObjectId].NativeMovementCompletion542 is null or > 2 || target.Speed is <= 0 or > short.MaxValue)))
        {
            throw new InvalidDataException("Saved native JOIN does not match its preserved caller, retained targets or movement state.");
        }
        if (joinState is { } nativeJoin && (nativeJoin.Targets.Any(target => (target.FixedX >> 12) is < short.MinValue or > short.MaxValue || (target.FixedY >> 12) is < short.MinValue or > short.MaxValue || (target.FixedZ >> 12) is < short.MinValue or > short.MaxValue || (target.FixedZ & 4095) != 0 || savedActors[target.ObjectId].X != target.FixedX / 4096f || savedActors[target.ObjectId].Y != target.FixedY / 4096f || savedActors[target.ObjectId].Z != target.FixedZ / 4096f)))
        {
            throw new InvalidDataException("Saved native JOIN movement does not match its exact preserved raw companion coordinates.");
        }
        if (joinState is { } boundJoin && NativeRuntimeProfile is { IsVerified: true })
        {
            NativePartyBindingMemory memory = scene.PartyBindingMemory ?? throw new InvalidDataException("Saved native JOIN lacks its original saved party body pointers.");
            int[] bindings = Enumerable.Range(0, 3).Select(slot => (int)memory.At(slot)).ToArray();
            if (memory.Version != 1 || bindings.Any(binding => binding != byte.MaxValue && (uint)binding >= scene.NativeActorObjectIds.Count) || bindings[0] == byte.MaxValue || scene.NativeActorObjectIds[bindings[0]] != boundJoin.CallerObjectId || !bindings.Skip(1).Where(binding => binding != byte.MaxValue).Select(binding => scene.NativeActorObjectIds[binding]).SequenceEqual(boundJoin.Targets.Select(target => target.ObjectId)))
            {
                throw new InvalidDataException("Saved native JOIN targets differ from the original saved party bodies; runtime follow slots are not substitutes.");
            }
        }
        if (joinState is null && restored.Any(fiber => fiber.WaitReason == WaitReason.PartyJoin))
        {
            throw new InvalidDataException("Saved JOIN wait has no native companion movement state.");
        }
        if (splitState is PartySplitState split && (joinState is not null || !bySequence.TryGetValue(split.FiberSequence, out Fiber? splitCaller) || splitCaller.ObjectId != split.CallerObjectId || splitCaller.Complete || splitCaller.WaitReason != WaitReason.PartySplit || (uint)splitCaller.Position >= program!.Operations.Count || program!.Operations[splitCaller.Position].Opcode.ToString() != "SPLIT" || scene.NativeActorLayoutVersion != 1 || split.Targets.Length is < 1 or > 3 || split.Targets.Select(target => target.ObjectId).Distinct().Count() != split.Targets.Length || split.Targets.Any(target => !scene.NativeActorObjectIds.Contains(target.ObjectId) || savedActors[target.ObjectId].NativeMovementCompletion542 is null or > 2 || target.Speed is not (2696 or 7929) || savedActors[target.ObjectId].X != target.FixedX / 4096f || savedActors[target.ObjectId].Y != target.FixedY / 4096f || savedActors[target.ObjectId].Z != target.FixedZ / 4096f)))
        {
            throw new InvalidDataException("Saved native SPLIT does not match its preserved caller, raw party targets or reached movement context.");
        }
        if (splitState is { } nativeSplit && (scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed } || nativeSplit.Targets.Any(target => new[] { target.FixedX, target.FixedY, target.FixedZ, target.TargetX, target.TargetY, target.TargetZ }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue) || (target.FixedZ & 4095) != 0 || new[] { target.TargetX, target.TargetY, target.TargetZ }.Any(value => (value & 4095) != 0))))
        {
            throw new InvalidDataException("Saved native SPLIT requires its exact saved party bindings and raw placement.");
        }
        if (splitState is { } boundSplit)
        {
            NativePartyBindingMemory memory = scene.PartyBindingMemory ?? throw new InvalidDataException("Saved native SPLIT lacks its original saved party body pointers.");
            int[] bindings = Enumerable.Range(0, 3).Select(slot => (int)memory.At(slot)).ToArray();
            if (memory.Version != 1 || bindings.Any(binding => binding != byte.MaxValue && (uint)binding >= scene.NativeActorObjectIds.Count) || !bindings.Where(binding => binding != byte.MaxValue).Select(binding => scene.NativeActorObjectIds[binding]).SequenceEqual(boundSplit.Targets.Select(target => target.ObjectId)))
            {
                throw new InvalidDataException("Saved native SPLIT targets differ from saved party bodies; runtime follow slots are not substitutes.");
            }
        }
        if (splitState is { } playerSplit && NativeFieldPartyBinding.TryGetObjectId(scene, 0, out int splitPlayerObjectId) && playerSplit.Targets.FirstOrDefault(target => target.ObjectId == splitPlayerObjectId) is PartyJoinTarget playerTarget && scene.NativePlayerPosition != new FieldFixedPosition(playerTarget.FixedX, playerTarget.FixedY, playerTarget.FixedZ))
        {
            throw new InvalidDataException("Saved native SPLIT current runtime leader differs from its raw player position.");
        }
        if (splitState is null && restored.Any(fiber => fiber.WaitReason == WaitReason.PartySplit))
        {
            throw new InvalidDataException("Saved SPLIT wait has no native party movement state.");
        }
    }
}
