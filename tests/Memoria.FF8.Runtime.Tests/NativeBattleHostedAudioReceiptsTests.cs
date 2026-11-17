using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedAudioReceiptsTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly MethodInfo PublishSound = typeof(BattlePresentationProcess).GetMethod("PublishNativeModuleSound", PrivateInstance, [typeof(NativeBattleModuleSound), typeof(string), typeof(NativeBattleSequenceSound)])!;
    private static readonly MethodInfo DrainMusic = typeof(BattlePresentationProcess).GetMethod("DrainNativeMusic501B60", PrivateInstance)!;

    [Fact]
    public void HostedSoundCommitsBeforeMusicDrainAndReceiptsKeepContactAndLifecycleOrder()
    {
        NativeBattleModelLifecycleServices services = new([]);
        BattlePresentationProcess process = Fixture(services);
        NativeGlobalSoundProcess before = process.NativeGlobalSoundProcess!;
        NativeBattleMusicProcessState music = process.NativeMusicProcess!;
        NativeBattleModuleSound request = new(123, 0, null);
        NativeBattleSequenceSound sequence = new(0, true, 0, 123);
        BattleModelOwner enemy = new BattleEnemyActorModel(new("enemy"));
        NativeBattleContactPacket contact = new(new(enemy, 0, 1, 0, 0, 0, 0), null);
        NativeBattleModelParticipantReleased released = new(enemy, "enemy");
        var expectedSound = request.Commit46B2A0(before);
        NativeBattleMusicCommandStep expectedMusic = music.DrainCommands501B60(expectedSound.Process);

        services.EmitContact(contact);
        PublishSound.Invoke(process, [request, "ally", sequence]);
        Assert.Equal(NativeBattleTestSnapshots.Json(expectedSound.Process), NativeBattleTestSnapshots.Json(process.NativeGlobalSoundProcess));
        Assert.Equal(123u, process.NativeGlobalSoundProcess!.OriginalEffectIds[26]);
        Assert.Equal((byte)2, process.NativeGlobalSoundProcess.Rotation);
        Assert.Same(music, process.NativeMusicProcess);
        services.Emit(released);
        DrainMusic.Invoke(process, null);
        DrainMusic.Invoke(process, null);

        Assert.Equal(expectedMusic.State, process.NativeMusicProcess);
        Assert.Equal(NativeBattleTestSnapshots.Json(expectedMusic.Sound), NativeBattleTestSnapshots.Json(process.NativeGlobalSoundProcess));
        Assert.Collection(services.ReadOrderedReceipts(),
            item => Assert.Equal(new NativeBattlePresentationContactReceived(contact), item),
            item => Assert.Equal(new NativeBattlePresentationSoundCommitted(expectedSound.Sound, "ally", sequence), item),
            item => Assert.Equal(released, item),
            item => Assert.Equal(new NativeBattlePresentationMusicCommandCommitted(expectedMusic.Receipt!), item));
        Assert.Equal(expectedSound.Sound, Assert.Single(process.ReadAndClearHostedPresentationFrameOutputs().Sounds));
        Assert.Empty(process.ReadAndClearHostedPresentationFrameOutputs().Sounds);
    }

    [Fact]
    public void HostedStopAllKeepsItsPositionBetweenSoundReceiptsWithoutChangingSoundState()
    {
        const string message = "Native46B440 stops all32 sound-effect voices.";
        MethodInfo stop = typeof(BattlePresentationProcess).GetMethod("PublishNativeStopAllSounds", PrivateInstance)!;
        NativeBattleModelLifecycleServices services = new([]);
        BattlePresentationProcess process = Fixture(services);
        PublishSound.Invoke(process, [new NativeBattleModuleSound(123, 0, null), "ally", null]);
        NativeGlobalSoundProcess beforeStop = process.NativeGlobalSoundProcess!;

        stop.Invoke(process, [message]);
        Assert.Same(beforeStop, process.NativeGlobalSoundProcess);
        PublishSound.Invoke(process, [new NativeBattleModuleSound(124, 0, null), "enemy", null]);
        Assert.Collection(services.ReadOrderedReceipts(),
            item => Assert.Equal("ally", Assert.IsType<NativeBattlePresentationSoundCommitted>(item).ActorId),
            item => Assert.Equal(new NativeBattlePresentationStopAllSoundsRequested(message), item),
            item => Assert.Equal("enemy", Assert.IsType<NativeBattlePresentationSoundCommitted>(item).ActorId));
    }

    [Fact]
    public void HostedAudioOutsideAFrameRejectsBeforeChangingSoundMusicOrOutput()
    {
        BattlePresentationProcess process = Fixture();
        NativeGlobalSoundProcess before = process.NativeGlobalSoundProcess!;
        NativeBattleMusicProcessState music = process.NativeMusicProcess!;

        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => PublishSound.Invoke(process, [new NativeBattleModuleSound(123, 0, null), null, null])).InnerException);
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => DrainMusic.Invoke(process, null)).InnerException);

        Assert.Same(before, process.NativeGlobalSoundProcess);
        Assert.Same(music, process.NativeMusicProcess);
        Assert.Empty(process.ReadAndClearHostedPresentationFrameOutputs().Sounds);
    }

    [Fact]
    public void StandalonePreviewStillQueuesAnUncommittedSound()
    {
        NativeBattleEffectPresentationInputs input = NativeBattleHostedPresentationInitializationTests.Inputs(0, 3, NativeBattleScriptedEffectMemory.FromColdProcess(), 1, hosted: false);
        BattlePresentationProcess process = BattlePresentationProcess.CreateEffectPresentation(input);
        NativeGlobalSoundProcess before = process.NativeGlobalSoundProcess!;
        NativeBattleModuleSound request = new(123, 0, null);

        PublishSound.Invoke(process, [request, "ally", null]);

        Assert.Same(before, process.NativeGlobalSoundProcess);
        List<NativeBattleModuleSound> sounds = (List<NativeBattleModuleSound>)Field("presentationSounds").GetValue(process)!;
        Assert.Same(request, Assert.Single(sounds));
        Assert.Null(request.Allocation);
    }

    private static BattlePresentationProcess Fixture(NativeBattleModelLifecycleServices? services = null)
    {
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.FromBufferlessColdProcess() with { MusicPlayback = new(63, 42, 17, 3) };
        NativeBattleMusicProcessState music = new NativeBattleMusicProcessState(new(1, null, null, null, 0, null), 0, null).InitializeCommands501C60().QueueStop501A70();
        BattleSession session = new(new BattleDefinition
        {
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }],
            NativeGlobalSoundProcess = sound,
            NativeMusicProcess = music
        }, BattleAtbMode.TurnBased);
        var (roster, aliases) = session.ReadBattlePresentationPacketBindings();
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, _ => new(0));
        BattlePresentationProcess process = NativeBattleHostedTestFixture.SeedProcess(session, graphics);
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeGlobalSoundProcess), sound);
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeMusicProcess), music);
        NativeBattleHostedTestFixture.SetField(process, "hostedFrameServices", services);
        return process;
    }

    private static FieldInfo Field(string name) => typeof(BattlePresentationProcess).GetField(name, PrivateInstance)!;
}
