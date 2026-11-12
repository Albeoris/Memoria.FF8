using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

if (args.Contains("--special-enemies-only", StringComparer.Ordinal))
{
    NativeSpecialAiTests.RunContracts();
    NativeElixirActionTests.Run();
    NativeSpecialStatusTests.Run();
    GuestFractionRecoveryTests.Run();
    SpecialEnemyOutcomeTests.Run();
    SpecialEnemyInitializationTests.Run();
    return 0;
}

if (args.Contains("--enemy-extensions-only", StringComparer.Ordinal))
{
    EnemyExtensionTests.Run();
    return 0;
}

if (args.Contains("--guest-only", StringComparer.Ordinal))
{
    NativeGuestLimitTests.Run();
    GuestBattleConversionTests.Run();
    return 0;
}

if (args.Contains("--native-saves-only", StringComparer.Ordinal))
{
    NativeSaveBankTests.Run();
    return 0;
}

if (args.Contains("--pub-only", StringComparer.Ordinal))
{
    NativeWallMarketPubCoverageTests.Run();
    return 0;
}

if (args.Contains("--abandoned-item-store-only", StringComparer.Ordinal))
{
    NativeWallMarketAbandonedItemStoreCoverageTests.Run();
    return 0;
}

if (args.Contains("--meal-only", StringComparer.Ordinal))
{
    NativeMealTests.Run();
    return 0;
}

if (args.Contains("--restaurant-only", StringComparer.Ordinal))
{
    NativeWallMarketRestaurantCoverageTests.Run();
    return 0;
}

if (args.Contains("--restaurant-return-only", StringComparer.Ordinal))
{
    NativeWallMarketRestaurantCoverageTests.RunReturn();
    return 0;
}

if (args.Contains("--coupon-only", StringComparer.Ordinal))
{
    NativeWallMarketItemShopCoverageTests.RunCoupon();
    return 0;
}

if (args.Contains("--shop-only", StringComparer.Ordinal))
{
    NativeShopTests.Run();
    return 0;
}

if (args.Contains("--palette-only", StringComparer.Ordinal))
{
    NativeProcessTests.RunPalette();
    Console.WriteLine("PASS native palette buffer ranges, color arithmetic and cold continuation");
    return 0;
}

if (args.Contains("--world-only", StringComparer.Ordinal))
{
    WorldRuntimeTests.Run();
    return 0;
}

if (args.Contains("--eyes-only", StringComparer.Ordinal))
{
    NativeEyeControlTests.Run();
    NativeFixedEyeControlTests.Run();
    NativeBlinkTests.Run();
    return 0;
}

var tests = new (string Name, Action Run)[]
{
    ("native save bank projection and slot integrity", NativeSaveBankTests.Run),
    ("envelope preserves both game namespaces and exact FFVIII payload", EnvelopeRoundTrip),
    ("envelope corruption and missing active game rejected", InvalidEnvelope),
    ("world switch requires save permission and prepares before commit", SwitchGuard),
    ("world revisits resume each independent continuation", ResumeBothWorlds),
    ("native field color fade and cold continuation", NativeProcessTests.RunFade),
    ("native new-fade endpoint and cold continuation", NativeNewFadeTests.Run),
    ("native entry fade table and cold continuation", NativeEntryFadeTests.Run),
    ("native field item acquisition and cold continuation", NativeItemTests.Run),
    ("native gil acquisition and cold continuation", NativeGilTests.Run),
    ("native fixed animation move and cold continuation", NativeProcessTests.RunFixedAnimationMove),
    ("native fixed facing move and cold continuation", NativeProcessTests.RunFixedFacingMove),
    ("native movement probe triangle carry and cold continuation", NativeMovementTests.RunProbeCarry),
    ("native movie camera gate and cold continuation", NativeProcessTests.RunMovieCameraGate),
    ("native arrow suppression and cold continuation", NativeArrowControlTests.Run),
    ("native movie camera default service and cold continuation", NativeMovieCameraServiceTests.Run),
    ("native instant entity facing and cold continuation", NativeProcessTests.RunImmediateFace),
    ("native turn wait and cold continuation", NativeTurnWaitTests.Run),
    ("native party facing and cold continuation", NativePartyTurnTests.Run),
    ("native party recovery and cold continuation", NativePartyRecoveryTests.Run),
    ("native all-character recovery and cold continuation", NativePartyRecoveryTests.RunAll),
    ("native wait-free window size and cold continuation", NativeProcessTests.RunWindowSizeWhenFree),
    ("native dynamic actor approach and cold continuation", NativeActorApproachTests.Run),
    ("native camera shake and cold continuation", NativeCameraShakeTests.Run),
    ("native player entry movement and cold continuation", NativePlayerEntryTests.Run),
    ("native player slot selection and cold continuation", NativePlayerSelectionTests.Run),
    ("native preload request and cold continuation", NativePreloadTests.Run),
    ("native previous map and cold continuation", NativePreviousMapTests.Run),
    ("native eye disable owner and cold continuation", NativeEyeControlTests.Run),
    ("native automatic blink eligibility and cold continuation", NativeBlinkTests.Run),
    ("native materia acquisition and cold continuation", NativeMateriaTests.Run),
    ("native equipment removal and cold continuation", NativeEquipmentRemovalTests.Run),
    ("native autoplay lifecycle and standalone continuation", NativeAutoPlayTests.Run),
    ("native autoplay successful target completion", NativeAutoPlayTests.RunCompletion),
    ("native autoplay observed church target", NativeAutoPlayTests.RunGoal),
    ("native autoplay crowd escape and pure previews", NativeAutoPlayCrowdTests.Run),
    ("native autoplay occupied waypoint and pure previews", NativeAutoPlayCrowdTests.RunOccupiedWaypoint),
    ("native run button projection and pending input ownership", NativeRunInputTests.Run),
    ("native world input displacement and terrain arithmetic", WorldInputTests.Run),
    ("native autoplay event observations and coverage ledger", NativeEventCoverageTests.Run),
    ("native optional-area frontier and initialized field ownership", NativeTourFrontierTests.Run),
    ("native window parameter binding and numeric cursor", NativeWindowParameterTests.Run),
    ("native shop autoplay cadence and clock ownership", NativeShopAutoPlayTests.Run),
    ("native optional inn production tour and return receipts", NativeOptionalInnTourTests.Run),
    ("native optional item shop production tour and return receipts", NativeOptionalInnTourTests.RunItemShop),
    ("native optional materia production tour and return receipts", NativeOptionalInnTourTests.RunMateria),
    ("native optional restaurant production tour and coupon availability", NativeOptionalInnTourTests.RunRestaurant),
    ("native optional coupon production tour and changed information", NativeOptionalInnTourTests.RunCoupon),
    ("native optional abandoned item store production tour", NativeOptionalInnTourTests.RunAbandonedItemStore),
    ("native main-street local subset and frontiers", NativeWallMarketMainStreetCoverageTests.Run),
    ("native main-street nested tour entry and gate", NativeWallMarketMainStreetCoverageTests.RunNested),
    ("native autoplay rail crossing and pure previews", NativeAutoPlayRailTests.Run),
    ("native autoplay triangle handoff and pure previews", NativeAutoPlayBridgeTests.Run),
    ("native rejected movement binding and cold continuation", NativeMovementBindingTests.Run),
    ("native movement authored contracts", NativeMovementTests.Run),
    ("native animation authored contracts", NativeAnimationTests.Run),
    ("native dialogue continuation contracts", NativeDialogueTests.Run),
    ("native message lifecycle and cold continuation", NativeMessageLifecycleTests.Run),
    ("native queued message ownership and cold continuation", NativeQueuedMessageTests.Run),
    ("installed native message scroll and cold continuation", InstalledMessageScrollTests.Run),
    ("native turn, camera and global process contracts", NativeProcessTests.Run),
    ("native global clock and timer contracts", NativeClockTests.Run),
    ("native special window timer metadata", NativeProcessTests.RunSpecialWindow),
    ("native camera focus and tracking contracts", NativeProcessTests.RunViewOffset),
    ("native character growth contracts", NativeGrowthTests.Run),
    ("native debug victory and exactly-once rewards", NativeBattleTests.Run),
    ("FFVII Turn Based scheduling and cold continuation", NativeBattleTests.RunTurnBased),
    ("FFVII Order and PHS native party continuation", NativePartyRecoveryTests.RunMenuParty),
    ("FFVII native Equip transactions and cold continuation", NativeMenuEquipmentTests.Run),
    ("FFVII native Limit level selection and cold continuation", NativeLimitMenuTests.Run),
    ("FFVII native field Item and Magic recovery", NativeFieldRecoveryTests.Run),
    ("installed opening arena and battle actor resources", NativeBattleTests.RunOpeningRoster),
    ("native opening combat damage", NativeBattleDamageTests.Run),
    ("native opening action transactions and Limit reel", NativeBattleActionTests.Run),
    ("native battle sound dispatch and cold continuation", NativeBattleSoundTests.Run),
    ("native battle actor model callbacks", NativeBattleActorModelTests.Run),
    ("native guest limit contact continuation", NativeGuestLimitTests.Run),
    ("mixed battle element and status conversion", GuestBattleConversionTests.Run),
    ("native destroyed battle effect and cold continuation", NativeBattleDestroyedTests.Run),
    ("native battle camera archive and action selection", NativeBattleCameraTests.Run),
    ("native field encounter-step contracts", NativeEncounterTests.Run),
    ("native field audio commands and continuation", NativeAudioTests.Run),
    ("native field initializer audio binding", NativeAudioTransitionTests.Run),
    ("native pending transition and atomic field publication", NativePendingTransitionTests.Run),
    ("native partial tick prefix and cold continuation", NativePartialTickTests.Run),
    ("native music gain fade and cold continuation", NativeMusicFadeTests.Run),
    ("native resident sound gain ramp and cold continuation", NativeSoundRampTests.Run),
    ("native resident sound pan and cold continuation", NativeSoundPanTests.Run),
    ("native resident sound frequency ramp and cold continuation", NativeSoundFrequencyRampTests.Run),
    ("native resident sound frequency and cold continuation", NativeSoundFrequencyTests.Run),
    ("standalone FFVII cold checkpoint continuation", NativeCheckpointTests.Run),
    ("native procedural model color and visibility contracts", NativeModelEffectTests.Run),
    ("native model color offset and cold continuation", NativeColorOffsetTests.Run),
    ("native line talk physical facing and preview purity", NativeLineTalkInputTests.Run),
    ("native field catalog and physical event continuation", NativeFieldEventCatalogTests.Run),
    ("native road catalog and physical handoff continuation", NativeRoadCoverageTests.Run),
    ("native park catalog and physical scene continuation", NativeParkCoverageTests.Run),
    ("native market catalog and unvisited room frontier", NativeWallMarketCoverageTests.Run),
    ("native inn catalog and physical visit", NativeWallMarketInnCoverageTests.Run),
    ("native item-shop catalog and physical visit", NativeWallMarketItemShopCoverageTests.Run),
    ("native materia-room catalog and physical visit", NativeWallMarketMateriaCoverageTests.Run),
    ("native boutique researched subset and frontier", NativeWallMarketBoutiqueCoverageTests.Run),
    ("native room reset ownership and cold coverage", NativeRoomResetCoverageTests.Run),
    ("native part visibility and cold continuation", NativePartVisibilityTests.Run),
    ("native interactive ladder continuation contracts", NativeLadderTests.Run),
    ("native jump and actor position query contracts", NativeJumpTests.Run),
    ("installed native movement contracts", NativeMovementTests.RunInstalled),
    ("installed New Game and cold continuation", InstalledSession),
    ("installed opening guest instant victory", InstalledOpeningTests.RunGuestInstantVictory),
    ("installed opening deferred battle handoff", InstalledOpeningTests.RunDeferredBattle),
    ("installed opening playable battle handoff", InstalledOpeningTests.RunPlayableBattle),
    ("installed opening full combat matrix", InstalledOpeningCombatMatrixTests.Run),
    ("installed opening and cold process continuation", InstalledOpeningTests.Run)
};
var selected = tests.Where(test => args.Length == 0 || test.Name.Contains(args[0], StringComparison.OrdinalIgnoreCase)).ToArray();
int failed = 0;
foreach (var test in selected)
{
    try
    {
        test.Run();
        Console.WriteLine("PASS " + test.Name);
    }
    catch (Exception error)
    {
        failed++;
        Console.Error.WriteLine("FAIL " + test.Name + ": " + error);
    }
}
Console.WriteLine($"{selected.Length - failed}/{selected.Length} groups passed");
return failed == 0 ? 0 : 1;

static Ff7SessionSave Fixture(long tick = 0)
{
    FieldProgram program = new([0, 0x5F, 0x12, 1], [new ushort[32]]);
    FieldMachine machine = new(program);
    machine.Initialize();
    return new Ff7SessionSave
    {
        ResourceProfile = "fixture",
        Language = "en",
        FieldName = "fixture",
        Progress = new Ff7Progress { InitialKernelData = [1], Characters = [new Ff7CharacterState(0, "Cloud", 6, 100, 314, 54, new byte[132])], Party = [0, 255, 255], Inventory = [new Ff7InventorySlot(0, 2)], Materia = [], Gil = 240 },
        Vm = machine.Capture() with { Tick = tick },
        Process = new Ff7FieldProcess { Tick = tick }
    };
}

static void EnvelopeRoundTrip()
{
    byte[] ff8 = [0x4D, 0x46, 0x46, 0x38, 1, 2, 3, 255];
    CombinedSessionSave save = new() { ActiveGame = SessionGame.Ff7, Ff8Payload = ff8, Ff8PayloadFormat = "fixture-ff8", Ff7 = Fixture(18), Mappings = [new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, "standard-field-squall")] };
    using MemoryStream stream = new();
    CombinedSessionCodec.Write(stream, save);
    stream.Position = 0;
    Check(CombinedSessionCodec.HasSignature(stream) && stream.Position == 0, "Signature inspection consumed input.");
    CombinedSessionSave restored = CombinedSessionCodec.Read(stream);
    Check(restored.Ff8Payload!.SequenceEqual(ff8), "FFVIII payload changed.");
    Check(restored.Ff7!.Vm.Tick == 18 && restored.Ff7.Progress.Gil == 240 && restored.Ff7.Progress.Inventory[0].Quantity == 2, "FFVII progression or PC clock changed.");
    Check(restored.Mappings.Single().AppearanceGame == SessionGame.Ff8, "Appearance identity was lost.");
    for (int appearance = 0; appearance <= 10; appearance++)
    {
        CharacterAppearanceMapping[] mappings = Enumerable.Range(0, 9).Select(story => new CharacterAppearanceMapping(SessionGame.Ff7, story, SessionGame.Ff8, appearance, "fixture-general-profile")).ToArray();
        using MemoryStream mappedStream = new();
        CombinedSessionCodec.Write(mappedStream, save with { Mappings = mappings });
        mappedStream.Position = 0;
        CombinedSessionSave mapped = CombinedSessionCodec.Read(mappedStream);
        Check(mapped.Mappings.SequenceEqual(mappings), "A general character association changed during its codec round trip.");
        Check(mapped.Ff8Payload!.SequenceEqual(ff8) && JsonSerializer.Serialize(mapped.Ff7) == JsonSerializer.Serialize(save.Ff7), "An appearance association changed native progress.");
    }
}

static void InvalidEnvelope()
{
    Throws<InvalidDataException>(() => (new CombinedSessionSave { ActiveGame = SessionGame.Ff7 }).Validate());
    CombinedSessionSave source = new() { ActiveGame = SessionGame.Ff7, Ff7 = Fixture() };
    using MemoryStream stream = new();
    CombinedSessionCodec.Write(stream, source);
    byte[] corrupt = stream.ToArray();
    corrupt[^1] ^= 127;
    Throws<InvalidDataException>(() => CombinedSessionCodec.Read(new MemoryStream(corrupt)));
    Throws<NotSupportedException>(() => (source with { Mappings = [new(SessionGame.Ff7, 9, SessionGame.Ff8, 0, "fixture")] }).Validate());
    Throws<NotSupportedException>(() => (source with { Mappings = [new(SessionGame.Ff7, 0, SessionGame.Ff8, 11, "fixture")] }).Validate());
    Throws<NotSupportedException>(() => (source with { Mappings = [new(SessionGame.Ff8, 0, SessionGame.Ff7, 0, "fixture")] }).Validate());
    Throws<NotSupportedException>(() => (source with { Mappings = [new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, "fixture"), new(SessionGame.Ff7, 0, SessionGame.Ff8, 1, "fixture")] }).Validate());
}

static void SwitchGuard()
{
    WorldSwitch coordinator = new(new CombinedSessionSave { ActiveGame = SessionGame.Ff8, Ff8Payload = [10], Ff8PayloadFormat = "fixture" });
    int created = 0;
    Throws<InvalidOperationException>(() => coordinator.ToFf7(new(false), [11], "fixture", () => { created++; return Fixture(); }, _ => { }));
    Check(created == 0 && coordinator.State.ActiveGame == SessionGame.Ff8, "Denied switch created or advanced a session.");
    Throws<InvalidOperationException>(() => coordinator.ToFf7(new(true), [11], "fixture", FixtureFactory, _ => throw new InvalidOperationException("unavailable destination")));
    Check(coordinator.State.ActiveGame == SessionGame.Ff8 && coordinator.State.Ff8Payload![0] == 10 && coordinator.State.Ff7 is null, "Failed preparation committed a partial world switch.");
}

static Ff7SessionSave FixtureFactory()
{
    return Fixture();
}

static void ResumeBothWorlds()
{
    WorldSwitch coordinator = new(new CombinedSessionSave { ActiveGame = SessionGame.Ff8, Ff8Payload = [1], Ff8PayloadFormat = "fixture" });
    int newGames = 0;
    coordinator.ToFf7(new(true), [2], "fixture", () => { newGames++; return Fixture(0); }, target => Check(target.Vm.Tick == 0, "First visit did not start New Game."));
    coordinator.ToFf8(new(true), Fixture(123), (payload, _) => Check(payload[0] == 2, "FFVIII did not resume its outgoing state."));
    coordinator.ToFf7(new(true), [3], "fixture", () => { newGames++; return Fixture(0); }, target => Check(target.Vm.Tick == 123, "FFVII revisit replayed New Game."));
    Check(newGames == 1 && coordinator.State.Ff8Payload![0] == 3 && coordinator.State.Ff7!.Vm.Tick == 123, "Independent world continuations changed.");
}

static void InstalledSession()
{
    string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
    if (string.IsNullOrWhiteSpace(root))
    {
        Console.WriteLine("SKIP installed New Game: MEMORIA_FF7_GAME_DIR is unset.");
        return;
    }
    Ff7Installation installation = Ff7Installation.Discover(root);
    using Ff7Session original = Ff7Session.NewGame(installation);
    Ff7SessionSave snapshot = original.Capture();
    Console.WriteLine($"initial-field={original.Field.Name} actors={original.Host.Process.Actors.Length} party={string.Join(',', original.Progress.Party)} movie={original.Host.Process.Movie?.PreparedId}");
    Check(original.Field.Name == "md1stin" && snapshot.Vm.PersistentVariables[768] == 1 && original.Progress.Party.SequenceEqual(new[] { 0, 255, 255 }), "Native cold New Game owners were not applied.");
    using MemoryStream stream = new();
    CombinedSessionCodec.Write(stream, new CombinedSessionSave { ActiveGame = SessionGame.Ff7, Ff7 = snapshot });
    stream.Position = 0;
    using Ff7Session restored = Ff7Session.Restore(installation, CombinedSessionCodec.Read(stream).Ff7!);
    original.AdvanceTick();
    restored.AdvanceTick();
    Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(restored.Capture()), "Installed New Game cold restore changed the next tick.");
}

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void Throws<T>(Action action) where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        return;
    }
    throw new InvalidOperationException("Expected " + typeof(T).Name);
}
