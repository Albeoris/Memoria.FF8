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

    public static NativeBattleEffectMemory FromColdProcess() => new(null, 0, 0, ImmutableArray.CreateRange(new NativeBattleEffectHeapAllocation?[256]), 0, 0, 0, 0) { WhiteWind297 = NativeBattleWhiteWind297State.FromColdProcess(), MicroMissiles299 = NativeBattleMicroMissiles299State.FromColdProcess(), BadBreath300 = NativeBattleBadBreath300State.FromColdProcess(), Degenerator306 = NativeBattleDegenerator306State.FromColdProcess(), GatlingGun305 = NativeBattleGatlingGun305State.FromColdProcess(), LevelDeath74 = NativeBattleDeathState.FromColdProcess(), Curse47ColdSourcesPending = true, Doom8ColdSourcesPending = true, MightyGuard73 = NativeBattleMightyGuardState.FromColdProcess(73), MightyGuard78 = NativeBattleMightyGuardState.FromColdProcess(78), Textures = NativeBattleEffectTextureState.FromColdProcess(), GroundCapture = NativeBattleGroundCaptureMemory.FromColdProcess(), Parameter80 = 0, Parameter82 = 0, Aqua55 = NativeBattleAquaState.FromColdProcess(55), Aqua72 = NativeBattleAquaState.FromColdProcess(72), CureContextF4 = 0, Potion50ContextF4 = 0, CuraContextF4 = 0, CuraSceneFade = 0, Potion51ContextF4 = 0, Potion51SceneFade = 0, CuragaExecutable = NativeBattleCuragaExecutableState.FromColdProcess(), Potion52Executable = NativeBattleCuragaExecutableState.FromColdProcess(52), DrawStock15Executable = NativeBattleCuragaExecutableState.FromColdProcess(15), ActorDirector16 = NativeBattleActorDirectorState.FromColdProcess(16), ActorDirector61 = NativeBattleActorDirectorState.FromColdProcess(61), ActorDirector16Executable = NativeBattleCuragaExecutableState.FromColdProcess(16), ActorDirector61Executable = NativeBattleCuragaExecutableState.FromColdProcess(61), ActorDirector81 = NativeBattleActorDirectorState.FromColdProcess(81), ActorDirector82 = NativeBattleActorDirectorState.FromColdProcess(82), ActorDirector81Executable = NativeBattleCuragaExecutableState.FromColdProcess(81), ActorDirector82Executable = NativeBattleCuragaExecutableState.FromColdProcess(82), EsunaExecutable = NativeBattleRestorationExecutableState.FromColdProcess(24), FullLifeExecutable = NativeBattleRestorationExecutableState.FromColdProcess(27), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), GuardianWorkBuffer = NativeBattleGuardianWorkBuffer.FromColdProcess(), EffectIo = NativeBattleEffectIoState.FromColdProcess(), GuardianCasterBank = 0, QuezacotlPause = 0, QuezacotlCompletion17A8 = 0, QuezacotlTargetY1774 = 0, QuezacotlSecondaryHeads = [0, 0, 0, 0, 0], SummonStream = NativeBattleSummonStreamState.FromColdProcess(), SharedEffectRandom = NativeBattleSharedEffectRandomState.FromColdProcess(), ModelColorOverrides = NativeBattleModelColorOverrides.FromColdProcess(), SequenceSine = 0, SequenceCosine = 0, SequenceParticlePreviousBone = 0, ActionPhaseDEA = 0, ReturnedTaskPointer1D96AAC = 0, Rush91MorphScratch = new byte[0x5A0].ToImmutableArray(), FullscreenOverlay = NativeBattleFullscreenOverlayState.FromColdProcess(), FullscreenOverlayPause2795914 = 0, Moogle96 = NativeBattleMoogle96State.FromColdProcess(), Gilgamesh = NativeBattleGilgameshProcess.FromColdProcess(), Shiva = NativeBattleShivaProcess.FromColdProcess(), Scan = NativeBattleScanProcess.FromColdProcess(), PcRenderRequest = NativeBattlePcRenderRequest.FromColdProcess(), GuardianCameraProcess = NativeBattleGuardianCameraProgram.FromColdProcess(), Cactuar199 = NativeBattleCactuar199State.FromColdProcess(), Thundaga105Capture = NativeBattleThundaga105CaptureMemory.FromColdProcess(), Thundaga105 = NativeBattleThundaga105State.FromColdProcess(), Holy175 = NativeBattleHoly175State.FromColdProcess(), Choco97 = NativeBattleChoco97State.FromColdProcess(), Choco98 = NativeBattleChoco98State.FromColdProcess(), Choco99 = NativeBattleChoco99State.FromColdProcess(), Choco100 = NativeBattleChoco100State.FromColdProcess(), Tonberry90 = NativeBattleTonberry90State.FromColdProcess(), Siren95 = NativeBattleSiren95State.FromColdProcess(), Doomtrain191 = NativeBattleDoomtrain191State.FromColdProcess(), Pandemona291 = NativeBattlePandemona291State.FromColdProcess(), Diablos325 = NativeBattleDiablos325State.FromColdProcess(), Carbuncle278 = NativeBattleCarbuncle278State.FromColdProcess(), NodeScratch2793E30 = NativeBattleEffectNodeScratch.FromColdProcess(), NodeContextExtension = NativeBattleEffectNodeContextExtension.FromColdProcess(), ScriptedEffects = NativeBattleScriptedEffectMemory.FromColdProcess(), ScriptedTarget2796D40 = 0, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess(), ScriptedLighting = NativeBattleScriptedLightingMemory.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), Bio22ActorMemory = NativeBattleBio22ActorMemory.FromColdProcess(), Storm11 = NativeBattleStorm11State.FromColdProcess(), SleepingGas168 = NativeBattleSleepingGas168State.FromColdProcess(), GastricJuice169 = NativeBattleGastricJuice169State.FromColdProcess(), Protect32 = NativeBattleProtectState.FromColdProcess(), Shell33 = NativeBattleShellState.FromColdProcess(), Reflect106 = NativeBattleReflectState.FromColdProcess(), Meltdown148 = NativeBattleMeltdownState.FromColdProcess(), DarkMist13 = NativeBattleDarkMist13State.FromColdProcess(), StickyWeb153 = NativeBattleStickyWeb153State.FromColdProcess(), UltraWaves154 = NativeBattleUltraWaves154State.FromColdProcess(), SandStorm176 = NativeBattleSandStorm176State.FromColdProcess(), FunguarLaser277 = NativeBattleFunguarLaser277State.FromColdProcess(), RayBomb10 = NativeBattleRayBomb10State.FromColdProcess(), BombGlow179 = NativeBattleBombGlow179State.FromColdProcess(), Control110 = NativeBattleControl110State.FromColdProcess(), Confuse36ExecutableInitialized = false, Dribble41 = NativeBattleWendigoState.FromColdProcess(), Shoot42 = NativeBattleWendigoState.FromColdProcess(), Suicide180 = NativeBattleSuicide180State.FromColdProcess(), Ultrasonic152 = NativeBattleUltrasonic152State.FromColdProcess(), Death14 = NativeBattleDeathState.FromColdProcess(), Sleep145 = NativeBattleSleep145State.FromColdProcess(), Shot188 = NativeBattleShot188State.FromColdProcess(), Shot196 = NativeBattleShot196State.FromColdProcess(), Renzokuken160 = NativeBattleRenzokuken160State.FromColdProcess(), Shot193 = NativeBattleShot193State.FromColdProcess(), Shot192 = NativeBattleShot192State.FromColdProcess(), Kiros262Trail = NativeBattleKiros262TrailState.FromColdProcess(), Shot195 = NativeBattleShot195State.FromColdProcess(), FatedCircle162 = NativeBattleFatedCircle162State.FromColdProcess(), Shot194 = NativeBattleShot194State.FromColdProcess(), Shot197 = NativeBattleShot197State.FromColdProcess(), Shot198 = NativeBattleShot198State.FromColdProcess(), AngelWing84 = NativeBattleAngelWing84Storage.FromColdProcess(), Rinoa86 = NativeBattleRinoa86Storage.FromColdProcess(), Duel112 = NativeBattleDuel112State.FromColdProcess(), NoMercy200 = NativeBattleNoMercy200State.FromColdProcess(), Rinoa87 = NativeBattleRinoa87Storage.FromColdProcess(), Duel274 = NativeBattleDuel274State.FromColdProcess(), Rinoa88 = NativeBattleRinoa88State.FromColdProcess(), Duel212 = NativeBattleDuel212State.FromColdProcess(), Duel215 = NativeBattleDuel215State.FromColdProcess(), Rinoa89 = NativeBattleRinoa89State.FromColdProcess(), Duel216 = NativeBattleDuel216State.FromColdProcess(), Regen147 = NativeBattleRegen147State.FromColdProcess(), FullCure280 = NativeBattleFullCure280State.FromColdProcess(), Ultima149 = NativeBattleUltima149State.FromColdProcess(), Wall189 = NativeBattleWall189State.FromColdProcess(), Drain39Bank = NativeBattleDrain39BankState.FromColdProcess(), Tornado146 = NativeBattleTornado146State.FromColdProcess(), Quake38 = NativeBattleQuake38State.FromColdProcess(), TheEnd85 = NativeBattleTheEnd85State.FromColdProcess(), Pain34 = NativeBattlePain34State.FromColdProcess() };

    public void Validate()
    {
        if (CinematicBreakupPayload4?.Any(pair => !NativeBattleCinematicProfile.IsSupported(pair.Key) || pair.Value.IsDefault || pair.Value.Length != 4) == true)
        {
            throw new InvalidDataException("Native cinematic breakup requires four retained corner depths for each published module owner.");
        }
        if (DetachedReturnedTask is { } returnedTask)
        {
            returnedTask.Validate();
            if (ReturnedTaskPointer1D96AAC is not > 0 || returnedTask.ReturnedDescriptor != ReturnedTaskPointer1D96AAC)
            {
                throw new InvalidDataException("Native detached returned task disagrees with its canonical1D96AAC pointer.");
            }
        }
        ExecutionStack?.Validate();
        GeometryRegisters?.Validate();
        DrinkMagic37?.Validate();
        Raldo19?.Validate();
        Effect20?.Validate();
        Devour62?.Validate();
        MeltingBubble43?.Validate();
        MeltingBubble43Executable?.Validate();
        if (MeltingBubble43Executable is { } directory43 && (directory43.ResourceAddress != 0x15C6080 || directory43.InitializationFlag != 1 || directory43.Data is null))
        {
            throw new InvalidDataException("Melting Bubble requires its actual initialized directory and absolute pointers.");
        }
        WindBlast29?.Validate();
        WindBlast29Executable?.Validate();
        if (WindBlast29Executable is { ResourceAddress: not 0x17D27DC })
        {
            throw new InvalidDataException("The Wind Blast node bank belongs to another original module.");
        }
        Effect60?.Validate();
        Effect60PrimaryExecutable?.Validate();
        Effect60SecondaryExecutable?.Validate();
        if (Effect60PrimaryExecutable is { ResourceAddress: not 0x1797CC0 } || Effect60SecondaryExecutable is { ResourceAddress: not 0x179C998 })
        {
            throw new InvalidDataException("The retained effect60 banks disagree with their physical module owners.");
        }
        PsychoBlast23?.Validate();
        PsychoBlast23Executable?.Validate();
        if (PsychoBlast23Executable is { ResourceAddress: not 0x17EA4F0 })
        {
            throw new InvalidDataException("The Psycho Blast node bank belongs to another module.");
        }
        Raldo19Executable?.Validate();
        if (Raldo19Executable is { ResourceAddress: not NativeBattleRaldo19Profile.ExecutableAddress })
        {
            throw new InvalidDataException("The Raldo node bank belongs to another module.");
        }
        DrinkMagic37Executable?.Validate();
        if (DrinkMagic37Executable is { ResourceAddress: not 0x17C8420 })
        {
            throw new InvalidDataException("The Drink Magic node bank belongs to another module.");
        }
        MightyGuard73?.Validate();
        MightyGuard78?.Validate();
        if (MightyGuard73 is { Effect: not 73 } || MightyGuard78 is { Effect: not 78 })
        {
            throw new InvalidDataException("The Mighty Guard retained state belongs to another module.");
        }
        Hero309?.Validate();
        Hero310?.Validate();
        if (Hero309 is { Effect: not 309 } || Hero310 is { Effect: not 310 })
        {
            throw new InvalidDataException("The Hero retained state belongs to another original module.");
        }
        if (HeroCrtStack is { } heroStack)
        {
            heroStack.Validate(ExecutionStack ?? throw new InvalidDataException("The Hero CRT contract requires its actual execution-stack capture owner."));
        }
        ActorDirector16?.Validate();
        ActorDirector61?.Validate();
        ActorDirector16Executable?.Validate();
        ActorDirector61Executable?.Validate();
        ActorDirector81?.Validate();
        ActorDirector82?.Validate();
        ActorDirector81Executable?.Validate();
        ActorDirector82Executable?.Validate();
        if (ActorDirector81 is { Effect: not 81 } || ActorDirector82 is { Effect: not 82 } || ActorDirector81Executable is { ResourceAddress: not 0x1744A1C } || ActorDirector82Executable is { ResourceAddress: not 0x1741028 })
        {
            throw new InvalidDataException("The actor wave retained owners belong to another original module.");
        }
        if (ActorDirector16 is { Effect: not 16 } || ActorDirector61 is { Effect: not 61 } || ActorDirector16Executable is { ResourceAddress: not 0x17F7F1C } || ActorDirector61Executable is { ResourceAddress: not 0x1795000 })
        {
            throw new InvalidDataException("The actor director retained owners belong to another original module.");
        }
        ModuleSpriteBillboard21DFED0?.Validate();
        Shot188?.Validate();
        Shot196?.Validate();
        Shot193?.Validate();
        Shot195?.Validate();
        Shot197?.Validate();
        Shot198?.Validate();
        if (AngelWing84 is { } angelWing84)
        {
            NativeBattleAngelWing84Storage.Validate(angelWing84);
        }
        if (Rinoa86 is { } rinoa86)
        {
            NativeBattleRinoa86Storage.Validate(rinoa86);
        }
        if (Rinoa87 is { } rinoa87)
        {
            NativeBattleRinoa87Storage.Validate(rinoa87);
        }
        Rinoa88?.Validate();
        Rinoa89?.Validate();
        RoughDivide163?.Validate();
        BlastingZone164?.Validate();
        LionHeart165?.Validate();
        NoMercy200?.Validate();
        Duel112?.Validate();
        Duel212?.Validate();
        Duel215?.Validate();
        Duel216?.Validate();
        Duel274?.Resources?.Validate();
        FatedCircle162?.Validate();
        Shot194?.Validate();
        Shot192?.Validate();
        Kiros262Trail?.Validate();
        Renzokuken160?.Validate();
        ValidateRenzokukenVariants();
        SharedCustomMatrix?.Validate();
        Ultima149?.Validate();
        Wall189?.Validate();
        Drain39Bank?.Validate();
        Absorb56Bank?.Validate();
        Tornado146?.Validate();
        Quake38?.Validate();
        TheEnd85?.Validate();
        Pain34?.Validate();
        Cactuar199?.Validate();
        Holy175?.Validate();
        Choco97?.Validate();
        Choco98?.Validate();
        Choco99?.Validate();
        Choco100?.Validate();
        Tonberry90?.Validate();
        Siren95?.Validate();
        Doomtrain191?.Validate();
        Doom8?.Validate();
        Curse47?.Validate();
        WhiteWind297?.Validate();
        MicroMissiles299?.Validate();
        BadBreath300?.Validate();
        Degenerator306?.Validate();
        GatlingGun305?.Validate();
        Diablos325?.Validate();
        Carbuncle278?.Validate();
        CarbuncleExecutableMemory?.Validate();
        GroundCapture?.Validate();
        SharedEffectView2793E58?.Validate();
        NodeScratch2793E30?.Validate();
        NodeContextExtension?.Validate();
        ScriptedEffects?.Validate();
        ScriptedGraphics?.Validate();
        ScriptedLighting?.Validate();
        ValidateEffectLight1D99AB0();
        ScriptedColorRegisters?.Validate();
        Bio22Geometry?.ValidateForAddress(0x1615CE4);
        Bio22SecondaryGeometry?.ValidateForAddress(0x1617E3C);
        Bio22ThirdGeometry?.ValidateForAddress(0x1619888);
        Bio22FourthGeometry?.ValidateForAddress(0x161F750);
        Bio22Palette?.Validate();
        Bio22ActorMemory?.Validate();
        Regen147?.Validate();
        FullCure280?.Validate();
        DoubleMorph?.Validate();
        TripleMorph?.Validate();
        ModelColorOverrides?.Validate();
        CuragaExecutable?.Validate();
        Potion52Executable?.Validate();
        DrawStock15Executable?.Validate();
        if (DrawStock15Executable is { ResourceAddress: not 0x17FC68C })
        {
            throw new InvalidDataException("The Friendship node bank belongs to another original module.");
        }
        Aqua55?.Validate();
        Aqua72?.Validate();
        if (Aqua55 is { Effect: not 55 } || Aqua72 is { Effect: not 72 })
        {
            throw new InvalidDataException("The Aqua retained state belongs to another original module.");
        }
        if (CuragaExecutable is { ResourceAddress: not 0x17D5F4C } || Potion52Executable is { ResourceAddress: not 0x17AB0E8 })
        {
            throw new InvalidDataException("Native Curaga family retained banks must keep their original module owners.");
        }
        EsunaExecutable?.Validate();
        FullLifeExecutable?.Validate();
        Shiva?.Validate();
        Scan?.Validate();
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
