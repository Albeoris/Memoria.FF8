using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedAudioReceiptsTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly MethodInfo PublishSound = typeof(BattleSession).GetMethod("PublishNativeModuleSound", PrivateInstance, [typeof(NativeBattleModuleSound), typeof(string), typeof(NativeBattleSequenceSound)])!;
    private static readonly MethodInfo DrainMusic = typeof(BattleSession).GetMethod("DrainNativeMusic501B60", PrivateInstance)!;

    [Fact]
    public void HostedSoundCommitsBeforeMusicDrainAndReceiptsKeepContactAndLifecycleOrder()
    {
        NativeBattleModelLifecycleServices services = new([]);
        BattleSession session = Fixture(hosted: true, foreign: true, services);
        NativeGlobalSoundProcess before = session.NativeGlobalSoundProcess!;
        NativeBattleMusicProcessState music = session.NativeMusicProcess!;
        NativeBattleModuleSound request = new(123, 0, null);
        NativeBattleSequenceSound sequence = new(0, true, 0, 123);
        NativeBattleContactPacket contact = new(new(OriginalOwner(3), 0, 1, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0));
        NativeBattleModelParticipantReleased released = new(OriginalOwner(3), "enemy");
        var expectedSound = request.Commit46B2A0(before);
        NativeBattleMusicCommandStep expectedMusic = music.DrainCommands501B60(expectedSound.Process);

        services.EmitContact(contact);
        PublishSound.Invoke(session, [request, "ally", sequence]);
        Assert.Equal(NativeBattleTestSnapshots.Json(expectedSound.Process), NativeBattleTestSnapshots.Json(session.NativeGlobalSoundProcess));
        Assert.Equal(123u, session.NativeGlobalSoundProcess!.OriginalEffectIds[26]);
        Assert.Equal((byte)2, session.NativeGlobalSoundProcess.Rotation);
        Assert.Same(music, session.NativeMusicProcess);
        services.Emit(released);
        DrainMusic.Invoke(session, null);
        DrainMusic.Invoke(session, null);

        Assert.Equal(expectedMusic.State, session.NativeMusicProcess);
        Assert.Equal(NativeBattleTestSnapshots.Json(expectedMusic.Sound), NativeBattleTestSnapshots.Json(session.NativeGlobalSoundProcess));
        Assert.Collection(services.ReadOrderedReceipts(),
            item => Assert.Equal(new NativeBattlePresentationContactReceived(contact), item),
            item => Assert.Equal(new NativeBattlePresentationSoundCommitted(expectedSound.Sound, "ally", sequence), item),
            item => Assert.Equal(released, item),
            item => Assert.Equal(new NativeBattlePresentationMusicCommandCommitted(expectedMusic.Receipt!), item));
        Assert.Equal(expectedSound.Sound, Assert.Single(session.ReadAndClearHostedPresentationFrameOutputs().Sounds));
        Assert.Empty(session.ReadAndClearHostedPresentationFrameOutputs().Sounds);
        Assert.Empty(session.Events);
    }

    [Fact]
    public void HostedAudioOutsideAFrameRejectsBeforeChangingSoundMusicOrOutput()
    {
        BattleSession session = Fixture(hosted: true, foreign: true);
        NativeGlobalSoundProcess before = session.NativeGlobalSoundProcess!;
        NativeBattleMusicProcessState music = session.NativeMusicProcess!;

        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => PublishSound.Invoke(session, [new NativeBattleModuleSound(123, 0, null), null, null])).InnerException);
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => DrainMusic.Invoke(session, null)).InnerException);

        Assert.Same(before, session.NativeGlobalSoundProcess);
        Assert.Same(music, session.NativeMusicProcess);
        Assert.Empty(session.ReadAndClearHostedPresentationFrameOutputs().Sounds);
        Assert.Empty(session.Events);
    }

    [Fact]
    public void NativeAudioStillCommitsAndLogsBothRawCallSites()
    {
        BattleSession session = Fixture(hosted: false, foreign: false);
        NativeBattleModuleSound request = new(123, 0, null);
        var committed = request.Commit46B2A0(session.NativeGlobalSoundProcess);
        NativeBattleMusicCommandStep music = session.NativeMusicProcess!.DrainCommands501B60(committed.Process);

        PublishSound.Invoke(session, [request, "ally", null]);
        DrainMusic.Invoke(session, null);

        Assert.Equal(NativeBattleTestSnapshots.Json(music.Sound), NativeBattleTestSnapshots.Json(session.NativeGlobalSoundProcess));
        Assert.Collection(session.Events,
            entry =>
            {
                Assert.Equal("NativeModuleSound", entry.Kind);
                Assert.Equal(committed.Sound, entry.NativeModuleSound);
            },
            entry =>
            {
                Assert.Equal("NativeMusicCommand", entry.Kind);
                Assert.Equal(music.Receipt, entry.NativeMusicCommand);
            });
    }

    [Fact]
    public void StandalonePreviewStillQueuesAnUncommittedSound()
    {
        BattleSession session = Fixture(hosted: false, foreign: true);
        NativeGlobalSoundProcess before = session.NativeGlobalSoundProcess!;
        NativeBattleModuleSound request = new(123, 0, null);

        PublishSound.Invoke(session, [request, "ally", null]);

        Assert.Same(before, session.NativeGlobalSoundProcess);
        List<NativeBattleModuleSound> sounds = (List<NativeBattleModuleSound>)Field("presentationSounds").GetValue(session)!;
        Assert.Same(request, Assert.Single(sounds));
        Assert.Null(request.Allocation);
        Assert.Empty(session.Events);
    }

    private static BattleSession Fixture(bool hosted, bool foreign, NativeBattleModelLifecycleServices? services = null)
    {
        NativeGlobalSoundProcess sound = NativeGlobalSoundProcess.FromBufferlessColdProcess() with { MusicPlayback = new(63, 42, 17, 3) };
        NativeBattleMusicProcessState music = new NativeBattleMusicProcessState(new(1, null, null, null, 0, null), 0, null).InitializeCommands501C60().QueueStop501A70();
        BattleSession session = new(new BattleDefinition
        {
            Combatants = [new() { Id = "ally", Name = "Ally", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }],
            NativeGlobalSoundProcess = sound,
            NativeMusicProcess = music
        }, BattleAtbMode.TurnBased);
        Field("hostedBattlePresentation").SetValue(session, hosted);
        if (foreign)
        {
            Field("presentationForeignActors").SetValue(session, new Dictionary<int, NativeBattleForeignActor>());
        }
        Field("hostedFrameServices").SetValue(session, services);
        return session;
    }

    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, PrivateInstance)!;
}
