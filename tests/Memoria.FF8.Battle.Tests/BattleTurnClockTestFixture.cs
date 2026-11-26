using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Battle.Tests;

internal static class BattleTurnClockTestFixture
{
    internal static BattleSession Create(params BattleCombatantDefinition[] actors)
    {
        return Create([], actors);
    }

    internal static BattleSession Create(BattleNativeAction[] nativeActions, BattleCombatantDefinition[] actors)
    {
        int sceneSlot = 0;
        BattleDefinition definition = new()
        {
            Combatants = actors.Select(actor => actor.IsPlayer ? actor : actor with { SceneSlot = sceneSlot++, Information246 = 0 }).ToArray(),
            NativeClockRules = new(5),
            NativeBattleSpeed = 0,
            NativeMessageSpeed = 1,
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = nativeActions,
            StageId = 0,
            NativeStageSource = new(0, [], [], new("stage", [])) { PhysicalSource = new(1, 0, []) },
            NativeStageCameraBank = new([]),
            NativeEntryWaveFile = new(0, "entry", []),
            NativeMusicProcess = new(new(1, null, null, null, 0, null), 0, null),
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            NativeGlobalRandom = new(123),
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess()
        };
        NativeBattleGraphicsResource model = new([0, 0]) { AnimationFrames = [1], ModelLoadSources = [new(0, false, 0, 1, "body", [])] };
        ImmutableArray<NativeBattleGraphicsBinding> bindings = [.. definition.Combatants.Select(actor => new NativeBattleGraphicsBinding(actor.Id, actor.IsPlayer ? (byte)0 : (byte)16, model) { InitialPosition = new(0, 0, 0) })];
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [.. Enumerable.Repeat((sbyte)-1, 16)], [.. Enumerable.Repeat((sbyte)-1, 32)]);
        ImmutableArray<byte> bytes = [.. new byte[256]];
        ImmutableArray<uint> handlers = [.. new uint[256]];
        NativeBattleGraphicsDefinition graphics = new(rules, bindings, [])
        {
            PrimitiveTables = new(bytes, bytes, handlers, handlers, handlers),
            SoftwarePrimitiveTable = new(handlers),
            PrimitiveCapabilities = NativeBattlePrimitiveCapabilities.PrimaryRgba8()
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        session.AttachPresentation(new(definition, graphics));
        BattlePresentationProcess process = session.Presentation!;
        // The fixture starts at entry completion with inert, already bound model rows.
        var (roster, aliases) = session.ReadBattlePresentationPacketBindings();
        BattleActorModels models = new([]);
        NativeBattleModelBoundary boundary = process.Graphics.NativeBindings;
        foreach (BattleModelOwner owner in roster.Owners)
        {
            NativeBattleGraphicsModel row = new(0);
            if (aliases.TryEncodeExact(owner, out byte operand))
            {
                (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(operand), owner, models);
                models = models.Set(owner, row);
            }
            else
            {
                models = models.Add(owner, row);
            }
        }
        SetNativeGraphics(process, process.Graphics with
        {
            ActorModels = models,
            NativeBindings = boundary,
            Descriptors = NativeBattleDescriptorState.Clear(),
            EntryReady = true,
            EntryLifecycle = 4,
            UiServices = NativeBattleUiServices.Initialize()
        });
        EntryDescriptors(process)?.Clear();
        session.ActivateExpandedHostedClock();
        return session;
    }

    internal static void Step(BattleSession battle)
    {
        BattlePresentationProcess process = battle.Presentation!;
        DispatchTurnBasedFrameCore(battle);
        if (process.Graphics.Descriptors.Queue.IsEmpty)
        {
            return;
        }

        NativeBattleModelLifecycleServices services = battle.CaptureModelLifecycleServices(process.Graphics.ActorModels.Roster);
        HostedFrameServices(process) = services;
        try
        {
            SetNativeGraphics(process, AdvanceNativeDescriptors(process, process.Graphics));
        }
        finally
        {
            HostedFrameServices(process) = null;
        }
        ApplyExpandedHostedFrameReceipts(battle, services, null);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_NativeGraphics")]
    private static extern void SetNativeGraphics(BattlePresentationProcess process, NativeBattleGraphicsState graphics);

    [UnsafeAccessor(UnsafeAccessorKind.Method)]
    internal static extern void DispatchTurnBasedFrameCore(BattleSession battle);

    [UnsafeAccessor(UnsafeAccessorKind.Method)]
    private static extern NativeBattleGraphicsState AdvanceNativeDescriptors(BattlePresentationProcess process, NativeBattleGraphicsState graphics);

    [UnsafeAccessor(UnsafeAccessorKind.Method)]
    private static extern void ApplyExpandedHostedFrameReceipts(BattleSession battle, NativeBattleModelLifecycleServices services, long? actionToken);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "hostedFrameServices")]
    private static extern ref NativeBattleModelLifecycleServices? HostedFrameServices(BattlePresentationProcess process);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "hostedEntryDescriptors")]
    private static extern ref Queue<BattlePresentationProcess.HostedEntryDescriptor>? EntryDescriptors(BattlePresentationProcess process);
}
