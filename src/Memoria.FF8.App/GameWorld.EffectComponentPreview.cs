using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record ComponentSourceAdmission(ushort Effect, byte Command, ushort Action, byte Actor, int HeaderIndex, long Callback);

internal sealed partial class GameWorld
{
    internal bool IsEffectComponentPreview => effectPreviewRequest?.SourceSnapshot is not null;
    internal ComponentSourceAdmission? ComponentSourceAdmission { get; private set; }

    private BattleSession StartEffectComponentPreview(EffectPreviewRequest request)
    {
        if (battle is not null || session.State.PendingBattleEncounter is not null || diagnosticBattle is not null)
        {
            throw new InvalidOperationException("A component preview requires an unallocated standalone host.");
        }
        BattleSnapshot source = ReadEffectComponentSource(request);
        string executable = source.Definition.NativeRetainedMemory?.ColdExecutableSha256 ?? throw new InvalidDataException("The component source lacks its authenticated executable owner.");
        if (!string.Equals(executable, assets.NativeExecutableHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The component source executable differs from the installed resources.");
        }
        NativeBattleEffectBinding installed = assets.ReadNativeEffectCatalogForEffects([request.EffectId!.Value]).Single(binding => binding.Effect == request.EffectId);
        NativeBattleEffectBinding supplied = source.Definition.NativeEffectCatalog.Single(binding => binding.Effect == request.EffectId);
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(installed), JsonSerializer.SerializeToElement(supplied)))
        {
            throw new InvalidDataException("The component source differs from the installed native effect binding and resources.");
        }
        BattleSession active = AttachNativeEffectResources(BattleSession.Restore(source));
        ComponentSourceAdmission? admission = ValidateEffectComponentSource(source, active, request.EffectId.Value);
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source), JsonSerializer.SerializeToElement(active.Capture())))
        {
            throw new InvalidDataException("Component restore changed the standalone source state.");
        }
        long callbacks = active.NativeHostCallbacks ?? throw new NotSupportedException("Component preview requires its restored native callback ledger.");
        effectPreviewRequest = request;
        ComponentSourceAdmission = admission;
        battle = active;
        synchronizedNativeBattleCallbacks = callbacks;
        session.SetLiveBattle(active);
        Events.SynchronizeNativeBattleMusicProcess(active.NativeMusicProcess);
        return active;
    }

    internal static BattleSnapshot ReadEffectComponentSource(EffectPreviewRequest request)
    {
        request.Validate();
        if (request.SourceSnapshot is null)
        {
            throw new InvalidDataException("A component preview requires its declared standalone source.");
        }
        byte[] bytes = File.ReadAllBytes(request.SourceSnapshot);
        string actual = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(actual, request.SourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The component source SHA256 differs from the declared file.");
        }
        return JsonSerializer.Deserialize<BattleSnapshot>(bytes) ?? throw new InvalidDataException("The component source snapshot is null.");
    }

    internal static ComponentSourceAdmission? ValidateEffectComponentSource(BattleSnapshot source, BattleSession active, ushort expectedEffect)
    {
        if (!active.NativeEntryReady || active.Outcome != BattleOutcome.Active || active.NativeGraphics is not { } graphics || source.NativeEffectMemory is null)
        {
            throw new InvalidDataException("A component preview requires completed original entry and one unstarted native effect.");
        }
        int[] queued = graphics.Descriptors.Queue.Where(index => graphics.Descriptors.Packets[index].Command == 104).ToArray();
        if (graphics.OriginalEffect is { ReturnedDescriptor: null } admitted)
        {
            NativeBattleActionPacketsState admittedPackets = active.NativeActionPackets;
            int headerIndex;
            byte actorIndex;
            if (admitted.Parameters is { } parameters)
            {
                headerIndex = parameters.HeaderIndex7C;
                actorIndex = parameters.Actor78;
            }
            else
            {
                NativeBattleActionGraphicsState context = graphics.ActionGraphics ?? throw new InvalidDataException("The loaded component lacks its native invocation context.");
                headerIndex = context.HeaderIndex;
                actorIndex = context.ActorIndex;
                if (headerIndex < 0 || headerIndex >= NativeBattleActionPacketsState.HeaderCapacity)
                {
                    throw new InvalidDataException("The loaded component context lacks a physical native header.");
                }
                uint invocationPointer = checked(0x1D280C4u + (uint)headerIndex * 20);
                bool pendingInvocation = graphics.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Action104 && (context.Callback == 0x50BC20 && task.Phase is 1 or 2 || context.Callback == 0x50B190 && task.Phase is 2 or 3) && graphics.Descriptors.Packets[task.PacketSlot].Command == 104 && graphics.Descriptors.Packets[task.PacketSlot].Argument == invocationPointer);
                if (!pendingInvocation)
                {
                    throw new InvalidDataException("The loaded component lacks its unstarted native module invocation descriptor.");
                }
            }
            if (admitted.RequestedEffect != expectedEffect || admitted.Binding.Effect != expectedEffect || !admitted.Pools.IsEmpty || headerIndex < 0 || headerIndex >= admittedPackets.ActionCount || headerIndex >= NativeBattleActionPacketsState.HeaderCapacity || actorIndex >= graphics.ActorIds.Length || graphics.ActorIds[actorIndex] is null)
            {
                throw new InvalidDataException("The admitted component does not retain its exact unstarted native effect and physical owners.");
            }
            NativeBattleActionHeaderSlot admittedHeader = admittedPackets.Headers[headerIndex];
            NativeBattleEffectBinding selected = source.Definition.NativeEffectCatalog.Single(binding => binding.Effect == expectedEffect);
            if (NativeBattleActionGraphics.Word(admittedHeader, 6) != expectedEffect || admittedHeader.ReadByte(0) != actorIndex || admitted.Parameters is null && NativeBattleActionGraphics.SelectService(admittedHeader).Callback != graphics.ActionGraphics!.Callback || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(selected), JsonSerializer.SerializeToElement(admitted.Binding)) || queued.Length > 1 || queued.Any(index => graphics.Descriptors.Packets[index].Argument != checked(0x1D280C4u + (uint)headerIndex * 20)))
            {
                throw new InvalidDataException("The admitted component parameters, installed binding and physical action header disagree.");
            }
            return new(expectedEffect, admittedHeader.ReadByte(1), NativeBattleActionGraphics.Word(admittedHeader, 4), actorIndex, headerIndex, graphics.Callback);
        }
        if (queued.Length != 1)
        {
            throw new InvalidDataException("A component preview requires exactly one original queued action104 descriptor.");
        }
        uint pointer = graphics.Descriptors.Packets[queued[0]].Argument;
        if (pointer < 0x1D280C4 || (pointer - 0x1D280C4) % 20 != 0)
        {
            throw new InvalidDataException("The component action104 does not refer to its original physical header table.");
        }
        uint header = (pointer - 0x1D280C4) / 20;
        NativeBattleActionPacketsState packets = active.NativeActionPackets;
        if (header >= packets.ActionCount || header >= NativeBattleActionPacketsState.HeaderCapacity || NativeBattleActionGraphics.Word(packets.Headers[(int)header], 6) != expectedEffect)
        {
            throw new InvalidDataException("The component physical header does not identify the declared queued native effect.");
        }
        if (graphics.OriginalEffect is { } previous)
        {
            IReadOnlyList<string> blockers = active.ObserveDiagnosticDrainBlockers();
            bool resourceBlocker = blockers.Any(blocker => blocker is "effect-or-auxiliary-task" or "file-camera-sound-or-effect-resource");
            bool descriptorBlocker = !graphics.PendingContacts.IsEmpty || graphics.Descriptors.Queue.Length != 1 || graphics.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind != NativeBattleDescriptorTaskKind.Pump500CC0);
            if (previous.ReturnedDescriptor != 0 || active.NativeEffectMemory?.ReadReturnedTask5044B0() != 0 || resourceBlocker || descriptorBlocker)
            {
                throw new InvalidDataException("The component source retains an unreleased previous module: " + string.Join(", ", blockers));
            }
        }
        return null;
    }

    private void AdvanceEffectComponentClock(BattleSession active, int callbacks)
    {
        if (active.NativeExamTimer is { } retained)
        {
            session.SetExamTimer(retained);
        }
        else
        {
            session.SetExamTimer((session.ExamTimer ?? throw new InvalidDataException("Component presentation lost its standalone transport clock.")).AdvanceCallbacks(NativeTimerCallbackKind.Battle, callbacks));
        }
    }
}
