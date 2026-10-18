using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>A571900 heap allocation has a logical identity, not a portable native heap pointer.</summary>
public sealed record NativeBattleEffectHeapAllocation([property: JsonRequired] string Identity, [property: JsonRequired] ImmutableArray<byte> Data);

/// <summary>Process-retained571870 allocator and501230 publication fields. Only571870 proves the arena clear.</summary>
public sealed partial record NativeBattleEffectMemory([property: JsonRequired] NativeBattleEffectArena? Arena, [property: JsonRequired] int Cursor, [property: JsonRequired] int AllocationCount, [property: JsonRequired] ImmutableArray<NativeBattleEffectHeapAllocation?> Allocations, [property: JsonRequired] int Active, [property: JsonRequired] int SelectedIndex, [property: JsonRequired] uint SoundPendingFlags, [property: JsonRequired] uint SoundPublishedFlags)
{
    public NativeBattleEffectTextureState? Textures { get; init; }
    public byte? Parameter80 { get; init; }
    public short? CureContextF4 { get; init; }
    public short? CuraContextF4 { get; init; }
    public int? CuraSceneFade { get; init; }
    public NativeBattleCuragaExecutableState? CuragaExecutable { get; init; }
    public NativeBattleRestorationExecutableState? EsunaExecutable { get; init; }
    public NativeBattleRestorationExecutableState? FullLifeExecutable { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleExecutionStack? ExecutionStack { get; init; }

    public static NativeBattleEffectMemory FromColdProcess() => new(null, 0, 0, ImmutableArray.CreateRange(new NativeBattleEffectHeapAllocation?[256]), 0, 0, 0, 0) { Textures = NativeBattleEffectTextureState.FromColdProcess(), Parameter80 = 0, Parameter82 = 0, CureContextF4 = 0, CuraContextF4 = 0, CuraSceneFade = 0, CuragaExecutable = NativeBattleCuragaExecutableState.FromColdProcess(), EsunaExecutable = NativeBattleRestorationExecutableState.FromColdProcess(24), FullLifeExecutable = NativeBattleRestorationExecutableState.FromColdProcess(27), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GuardianWorkBuffer = NativeBattleGuardianWorkBuffer.FromColdProcess(), EffectIo = NativeBattleEffectIoState.FromColdProcess(), GuardianCasterBank = 0, QuezacotlPause = 0, QuezacotlCompletion17A8 = 0, QuezacotlTargetY1774 = 0, QuezacotlSecondaryHeads = [0, 0, 0, 0, 0], SummonStream = NativeBattleSummonStreamState.FromColdProcess(), SharedEffectRandom = NativeBattleSharedEffectRandomState.FromColdProcess(), ModelColorOverrides = NativeBattleModelColorOverrides.FromColdProcess(), SequenceSine = 0, SequenceCosine = 0, SequenceParticlePreviousBone = 0, ActionPhaseDEA = 0, ReturnedTaskPointer1D96AAC = 0, Rush91MorphScratch = new byte[0x5A0].ToImmutableArray(), FullscreenOverlay = NativeBattleFullscreenOverlayState.FromColdProcess(), FullscreenOverlayPause2795914 = 0, Moogle96 = NativeBattleMoogle96State.FromColdProcess(), Gilgamesh = NativeBattleGilgameshProcess.FromColdProcess(), Shiva = NativeBattleShivaProcess.FromColdProcess(), PcRenderRequest = NativeBattlePcRenderRequest.FromColdProcess(), GuardianCameraProcess = NativeBattleGuardianCameraProgram.FromColdProcess(), Cactuar199 = NativeBattleCactuar199State.FromColdProcess(), Thundaga105Capture = NativeBattleThundaga105CaptureMemory.FromColdProcess(), Thundaga105 = NativeBattleThundaga105State.FromColdProcess(), Holy175 = NativeBattleHoly175State.FromColdProcess(), Choco97 = NativeBattleChoco97State.FromColdProcess(), Choco98 = NativeBattleChoco98State.FromColdProcess(), Choco99 = NativeBattleChoco99State.FromColdProcess(), Choco100 = NativeBattleChoco100State.FromColdProcess(), NodeScratch2793E30 = NativeBattleEffectNodeScratch.FromColdProcess(), ScriptedEffects = NativeBattleScriptedEffectMemory.FromColdProcess(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess(), Bio22ActorMemory = NativeBattleBio22ActorMemory.FromColdProcess() };

    public void Validate()
    {
        if (DetachedReturnedTask is { } returnedTask)
        {
            returnedTask.Validate();
            if (ReturnedTaskPointer1D96AAC is not > 0 || returnedTask.ReturnedDescriptor != ReturnedTaskPointer1D96AAC)
            {
                throw new InvalidDataException("Native detached returned task disagrees with its canonical1D96AAC pointer.");
            }
        }
        ExecutionStack?.Validate();
        Cactuar199?.Validate();
        Holy175?.Validate();
        Choco97?.Validate();
        Choco98?.Validate();
        Choco99?.Validate();
        Choco100?.Validate();
        NodeScratch2793E30?.Validate();
        ScriptedEffects?.Validate();
        ScriptedGraphics?.Validate();
        Bio22Geometry?.ValidateForAddress(0x1615CE4);
        Bio22SecondaryGeometry?.ValidateForAddress(0x1617E3C);
        Bio22ThirdGeometry?.ValidateForAddress(0x1619888);
        Bio22FourthGeometry?.ValidateForAddress(0x161F750);
        Bio22Palette?.Validate();
        Bio22ActorMemory?.Validate();
        DoubleMorph?.Validate();
        TripleMorph?.Validate();
        ModelColorOverrides?.Validate();
        CuragaExecutable?.Validate();
        EsunaExecutable?.Validate();
        FullLifeExecutable?.Validate();
        Shiva?.Validate();
        ValidateRestorationVariants();
        ValidateRecoveryExecutables();
        if (EsunaExecutable is { Effect: not 24 } || FullLifeExecutable is { Effect: not 27 })
        {
            throw new InvalidDataException("Native restoration executable owners cannot be exchanged between spells.");
        }
        if (Cursor is < 0 or > NativeBattleEffectArena.Length || AllocationCount is < 0 or > 256 || Active is not (0 or 1) || SelectedIndex is < 0 or >= 400 || Allocations.IsDefault || Allocations.Length != 256 || Allocations.Any(allocation => allocation is not null && (string.IsNullOrEmpty(allocation.Identity) || allocation.Data.IsDefault)))
        {
            throw new InvalidDataException("Native effect allocator requires its retained cursor, count and256 physical heap slots.");
        }
        Arena?.Validate();
        Textures?.Validate();
        PrimitiveProcess?.Validate();
        GuardianWorkBuffer?.Validate();
        Gilgamesh?.Validate();
        PcRenderRequest?.Validate();
        QuezacotlExecutableMemory?.Validate();
    }

    public NativeBattleEffectMemory BeginLoad(int selectedIndex)
    {
        Validate();
        ImmutableArray<NativeBattleEffectHeapAllocation?> allocations = Allocations;
        for (int index = 0; index < AllocationCount; index++)
        {
            allocations = allocations.SetItem(index, null);
        }
        return this with { Arena = NativeBattleEffectArena.Clear(), Cursor = 0, AllocationCount = 0, Allocations = allocations, Active = 1, SelectedIndex = selectedIndex, EffectIo = EffectIo is { } io ? io with { Callback = 0x534150 } : null };
    }

    public NativeBattleEffectMemory LoadIntoArena(ReadOnlySpan<byte> bytes)
    {
        Validate();
        NativeBattleEffectArena arena = Arena ?? throw new InvalidDataException("Native571B80 requires the arena established by571870.");
        return this with { Arena = arena.Write(Cursor, bytes), Cursor = checked(Cursor + bytes.Length) };
    }

    public NativeBattleEffectMemory PublishSound()
    {
        //501230: the authenticated PC46C040 callback literally returns0; this is not an audio-duration approximation.
        return this with { SoundPublishedFlags = SoundPendingFlags | (SoundPublishedFlags & 0x80000000), SoundPendingFlags = 0 };
    }
}
