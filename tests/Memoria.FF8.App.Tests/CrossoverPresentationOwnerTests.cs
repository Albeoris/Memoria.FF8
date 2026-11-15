using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverPresentationOwnerTests(ITestOutputHelper output)
{
    [Fact]
    public void OriginalTheEndPublishesAllStagesAndReleasesActualArenaAndUiOwners()
    {
        HashSet<int> stages = [];
        HashSet<int> modes = [];
        HashSet<int> musicGains = [];
        NativeBattleEffectPresentation? owner = null;
        bool hidden = false;
        bool faded = false;
        bool privatePackets = false;
        bool commonPackets = false;
        bool partyHidden = false;
        bool partyFaded = false;
        bool arenaRendered = false;
        bool partyColorRendered = false;
        CrossoverGuestHostActions? provider = null;
        new CrossoverForeignBodyBridgeTests(output).Run("The End", 0, 0, observe: (renderer, host, current) =>
        {
            provider = current;
            if (current.ActiveAbility?.Presentation.Effect is not { } effect)
            {
                return;
            }
            owner = effect;
            int musicGain = Assert.IsType<int>(effect.MusicGain);
            Assert.InRange(musicGain, 0, 127);
            Assert.Equal(effect.MusicGain, current.CurrentRelativeMusicGain);
            musicGains.Add(musicGain);
            modes.Add(effect.Ui.Flags942 & 0x300);
            if (effect.Memory.TheEnd85 is { } state)
            {
                stages.Add(state.Word(0x25C1C60 + 64));
            }
            faded |= effect.StageColorDepth > 0;
            Assert.Contains(current.ActiveAbility!.Presentation.BoundParticipants, participant => participant.HostSlot == 1);
            Assert.DoesNotContain(current.ActiveAbility.Presentation.BoundTargets, target => target.HostSlot == 1);
            partyHidden |= current.SuppressForeignActor(1);
            partyFaded |= current.ReadForeignColor(1) is { } color && color.Apply(Vector4.One).X < 1;
            if (!partyColorRendered && !current.SuppressForeignActor(1) && current.ReadForeignColor(1) is { } tint)
            {
                MethodInfo readVisual = typeof(Ff7Scene).GetMethod("BattleActorVisual", BindingFlags.NonPublic | BindingFlags.Static)!;
                Ff7BattleActorVisual actor = (Ff7BattleActorVisual)readVisual.Invoke(null, [host, host.Actors.Single(actor => actor.Actor.Slot == 1)])!;
                HashSet<NativeBattleArenaPrimitiveId> arena = renderer.CaptureArenaGeometry().Faces.Select(face => face.Id).ToHashSet();
                CountRenderer normal = new();
                renderer.Render(normal, 320, 240, new(host.GuestRenderCameraOutput, [actor]) { SuppressArenaPrimitives = arena });
                CountRenderer colored = new();
                renderer.Render(colored, 320, 240, new(host.GuestRenderCameraOutput, [actor with { NativeColor = tint }]) { SuppressArenaPrimitives = arena });
                Assert.True(normal.Count > 0);
                Assert.Equal(normal.Count, colored.Count);
                Assert.True(colored.MaximumColor.X < normal.MaximumColor.X);
                Assert.True(colored.MaximumColor.Y < normal.MaximumColor.Y);
                Assert.True(colored.MaximumColor.Z < normal.MaximumColor.Z);
                Assert.Contains(colored.Materials, material => material.Texture is not null && material.Blend != TriangleBlend.Replace && !material.DepthWrite && !material.TextureSelectsAlpha);
                if (current.Visual is { } source)
                {
                    CountRenderer native = new();
                    renderer.Render(native, 320, 240, new(host.GuestRenderCameraOutput, []) { Guests = [source], SuppressArenaPrimitives = arena });
                    Assert.Contains(native.Materials, material => material.Texture is not null && material.Blend != TriangleBlend.Replace && !material.DepthWrite && material.TextureSelectsAlpha && material.PreserveTargetAlpha);
                }
                partyColorRendered = true;
            }
            if (!effect.SuppressedArenaPrimitives.IsEmpty)
            {
                hidden = true;
                Assert.True(renderer.CaptureArenaGeometry().Faces.Select(face => face.Id).ToHashSet().SetEquals(effect.SuppressedArenaPrimitives));
                Assert.Equal(effect.SuppressedArenaPrimitives, current.SuppressedArenaPrimitives);
                if (!arenaRendered)
                {
                    foreach ((int width, int height) in new[] { (320, 240), (640, 360) })
                    {
                        CountRenderer normal = new();
                        renderer.Render(normal, width, height, new(host.GuestRenderCameraOutput, []));
                        CountRenderer suppressed = new();
                        renderer.Render(suppressed, width, height, new(host.GuestRenderCameraOutput, []) { SuppressArenaPrimitives = current.SuppressedArenaPrimitives.ToHashSet() });
                        Assert.True(normal.Count > 0);
                        Assert.Equal(0, suppressed.Count);
                    }
                    arenaRendered = true;
                }
            }
            foreach (var packet in effect.Graphics.PrimitiveFrame?.Pending.Packets ?? [])
            {
                privatePackets |= packet.Reference.Bank == "20DFAB8";
                commonPackets |= packet.Reference.Bank is "off_B6D070" or "off_B6D094";
            }
            Assert.Equal((short)0, current.ActiveAbility!.Presentation.ViewTranslationY);
        });
        Assert.NotNull(owner);
        Assert.NotNull(provider);
        Assert.True(faded);
        Assert.True(hidden);
        Assert.True(privatePackets);
        Assert.True(commonPackets);
        Assert.True(partyFaded);
        Assert.True(partyHidden);
        Assert.True(arenaRendered);
        Assert.True(partyColorRendered);
        Assert.All(new[] { 1, 2, 3, 4, 5, 6 }, stage => Assert.Contains(stage, stages));
        Assert.Contains(0x200, modes);
        Assert.Contains(0x100, modes);
        Assert.Contains(48, musicGains);
        Assert.Contains(127, musicGains);
        AssertReleased(owner);
        Assert.Null(provider.PresentationUi);
        Assert.Null(provider.CurrentRelativeMusicGain);
        Assert.Empty(provider.SuppressedArenaPrimitives);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HiddenPresentationReleasesUiArenaAndCaptureOwnersOnCancellationOrRejectedCallback(bool reject)
    {
        RequireInstallations();
        bool checkedRelease = false;
        Assert.Throws<OperationCanceledException>(() => new CrossoverForeignBodyBridgeTests(output).Run("The End", 0, 0, observe: (_, _, provider) =>
        {
            if (provider.ActiveAbility?.Presentation.Effect is not { } effect || effect.SuppressedArenaPrimitives.IsEmpty)
            {
                return;
            }
            if (reject)
            {
                NativeBattleForeignActor actor = new(OriginalOwner(1), new(default, default, 0, 0, 4096), new(0, 0, 0, 0), (_, _) => default);
                Assert.Throws<ArgumentException>(() => effect.Advance(effect.Source, effect.Random, new(null, 216, 160, 108), [actor, actor], effect.Memory.Textures!));
            }
            else
            {
                provider.CancelAtTerminal();
                Assert.Null(provider.PresentationUi);
                Assert.Empty(provider.SuppressedArenaPrimitives);
            }
            AssertReleased(effect);
            checkedRelease = true;
            throw new OperationCanceledException("The owner release boundary was checked.");
        }));
        Assert.True(checkedRelease);
    }

    [Fact]
    public void HostStandingRequestReleasesReactionWithoutCombatOrStockPublication()
    {
        RequireInstallations();
        bool checkedStanding = false;
        Assert.Throws<OperationCanceledException>(() => new CrossoverForeignBodyBridgeTests(output).Run("Stop", 0, 0, observe: (_, host, provider) =>
        {
            if (provider.ActiveAbility is null)
            {
                return;
            }
            host.PresentGuestReaction(0, 1);
            Assert.False(host.IsGuestTargetStanding(1));
            var before = host.Capture();
            string stock = JsonSerializer.Serialize(provider.WorkingStock);
            host.PresentGuestStanding(0, 1);
            var after = host.Capture();
            Assert.True(host.IsGuestTargetStanding(1));
            Assert.Equal(JsonSerializer.Serialize(before.Actors), JsonSerializer.Serialize(after.Actors));
            Assert.Equal(JsonSerializer.Serialize(before.Random), JsonSerializer.Serialize(after.Random));
            Assert.Equal(before.LastHit, after.LastHit);
            Assert.Equal(JsonSerializer.Serialize(before.Queue), JsonSerializer.Serialize(after.Queue));
            Assert.Equal(JsonSerializer.Serialize(before.Inventory), JsonSerializer.Serialize(after.Inventory));
            Assert.Equal(stock, JsonSerializer.Serialize(provider.WorkingStock));
            checkedStanding = true;
            provider.CancelAtTerminal();
            throw new OperationCanceledException("The standing publication boundary was checked.");
        }));
        Assert.True(checkedStanding);
    }

    private static void AssertReleased(NativeBattleEffectPresentation owner)
    {
        Assert.Equal((ushort)0x40, owner.Ui.Flags942);
        Assert.Equal((short)0, owner.StageColorDepth);
        Assert.Empty(owner.SuppressedArenaPrimitives);
        Assert.Empty(owner.ArenaReplacements);
        Assert.Equal(127, owner.MusicGain);
    }

    private static void RequireInstallations()
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR")) && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")), "Select both original game installations.");
    }

    private sealed class CountRenderer : ITriangleRenderer
    {
        public int Count { get; private set; }
        public Vector4 MaximumColor { get; private set; }
        public HashSet<TriangleMaterial> Materials { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Count++;
            MaximumColor = Vector4.Max(MaximumColor, Vector4.Max(a.Color, Vector4.Max(b.Color, c.Color)));
            Materials.Add(material);
        }

        public void End()
        {
        }
    }
}
