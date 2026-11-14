using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossoverAllGuardianRenderingTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Quezacotl", false)]
    [InlineData("Shiva", false)]
    [InlineData("Ifrit", false)]
    [InlineData("Siren", false)]
    [InlineData("Brothers", false)]
    [InlineData("Diablos", false)]
    [InlineData("Carbuncle", true)]
    [InlineData("Leviathan", false)]
    [InlineData("Pandemona", false)]
    [InlineData("Cerberus", true)]
    [InlineData("Alexander", false)]
    [InlineData("Doomtrain", false)]
    [InlineData("Bahamut", false)]
    [InlineData("Cactuar", false)]
    [InlineData("Tonberry", false)]
    [InlineData("Eden", false)]
    public void Every_assigned_original_guardian_renders_in_the_host_completes_and_releases_before_following_Fire(string name, bool friendly)
    {
        Run(name, friendly);
    }

    [Theory]
    [InlineData("Holy", 1)]
    [InlineData("Holy", 4)]
    [InlineData("Dispel", 1)]
    [InlineData("Dispel", 4)]
    [InlineData("Drain", 1)]
    [InlineData("Drain", 4)]
    [InlineData("Zombie", 1)]
    [InlineData("Zombie", 4)]
    public void Original_body_effect_renders_actual_host_party_and_enemy_targets_and_releases_before_following_Fire(string name, int targetSlot)
    {
        Run(name, false, targetSlot);
    }

    private void Run(string name, bool friendly, int? targetSlot = null)
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR")) && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")), "Select both original game installations.");
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        using CrossoverArenaSurfaceCapture capture = new(() => new GpuTriangleRenderer(device.Device));
        using CrossoverCompositeActionTests.Fixture fixture = new(name, 0, 0, arenaCapture: capture);
        HashSet<(int Width, int Height)> visibleSizes = [];
        Dictionary<(int Width, int Height), int> maximumChanges = [];
        int eligibleCallbacks = 0;
        int renderedCallbacks = 0;
        int submittedTriangles = 0;
        bool zombieOverlayRendered = false;
        CrossoverCompositeActionTests.Result result = fixture.Execute(queuedRoundTrip: true, observe: Observe, targetSlot: targetSlot);
        int[] targets = result.Contacts.Select(contact => contact.HitDescription.PreparedContact!.Invocation.TargetSlot).Distinct().Order().ToArray();
        if (targetSlot is int selected)
        {
            Assert.Equal(new[] { selected }, targets);
        }
        else if (friendly)
        {
            Assert.Equal(new[] { 0, 1 }, targets);
        }
        else
        {
            Assert.NotEmpty(targets);
            Assert.All(targets, target => Assert.InRange(target, 4, 9));
        }
        Assert.Equal(fixture.NativeHitCount * targets.Length, result.Contacts.Length);
        Assert.Equal(2, visibleSizes.Count);
        Assert.True(submittedTriangles > 0);
        if (name == "Zombie")
        {
            Assert.True(zombieOverlayRendered, "The original age45 host-body wave must change actual GPU pixels.");
        }
        fixture.FollowWithFire();
        capture.Reset();
        Assert.Equal(0, capture.RetainedImages);
        Assert.Equal(0, capture.RetainedImageBytes);
        output.WriteLine($"{name}: {renderedCallbacks}/{eligibleCallbacks} native visual callbacks rendered through the full host scene; {submittedTriangles} submitted triangles; maximum changed RGB pixels {string.Join(", ", maximumChanges.Select(pair => $"{pair.Key.Width}x{pair.Key.Height}={pair.Value}"))}; {result.Contacts.Length} settled contacts, queued JSON continuation, original process cleanup and following Fire completed.");

        void Observe(Ff7BattleRenderer renderer, Ff7BattleSession host, CrossoverGuestHostActions provider)
        {
            if (provider.EffectVisual is not { } effect || effect.Draws.IsEmpty && effect.Ordering is not { Packets.Count: > 0 } && effect.StageColor is null)
            {
                return;
            }
            eligibleCallbacks++;
            bool zombieAge45 = name == "Zombie" && provider.ActiveAbility?.Presentation.Effect?.Graphics.OriginalEffect is { Pools.Length: > 1 } zombie && zombie.Pools[1].Slots.Any(task => (task.Flags & 1) != 0 && task.ReadWord(12) == 46);
            if ((eligibleCallbacks - 1) % 64 != 0 && !zombieAge45)
            {
                return;
            }
            renderedCallbacks++;
            string before = JsonSerializer.Serialize(host.Capture());
            string random = JsonSerializer.Serialize(provider.CurrentCrtRandom);
            CrossoverGuestFireHostState? invocation = provider.ActiveAbility;
            capture.Prepare(effect.Ordering?.Packets.Select(packet => packet.PilotGeometry?.ForeignSurface?.Material).OfType<INativeBattleForeignMaterial>() ?? []);
            Ff7BattleRenderFrame frame = Frame(host, provider);
            foreach ((int width, int height) in new[] { (320, 240), (640, 360) })
            {
                ValidatingRenderer validation = new(gpu);
                renderer.Render(validation, width, height, frame);
                Microsoft.Xna.Framework.Color[] full = GpuTestDevice.Read(gpu.Target);
                submittedTriangles += validation.Triangles;
                renderer.Render(gpu, width, height, frame with { GuestEffectLayers = [], SuppressArenaPrimitives = new HashSet<NativeBattleArenaPrimitiveId>() });
                Microsoft.Xna.Framework.Color[] control = GpuTestDevice.Read(gpu.Target);
                int changed = full.Where((pixel, index) => pixel.R != control[index].R || pixel.G != control[index].G || pixel.B != control[index].B).Count();
                maximumChanges[(width, height)] = Math.Max(maximumChanges.GetValueOrDefault((width, height)), changed);
                if (changed > 16)
                {
                    visibleSizes.Add((width, height));
                }
                if (zombieAge45 && effect.Ordering is { } ordering)
                {
                    NativeBattleLogicalPacket[] overlay = ordering.Packets.Where(packet => packet.PilotGeometry?.ForeignSurface is not null).ToArray();
                    Assert.NotEmpty(overlay);
                    HashSet<NativeBattlePrimitiveReference> omitted = overlay.Select(packet => NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference)).ToHashSet();
                    NativeBattleLogicalOrdering withoutOverlay = new(ordering.Table.ToImmutableArray(), ordering.Proxies.ToImmutableArray(), ordering.Packets.Select(packet => omitted.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference)) ? packet with { Words = RemoveCommands(packet.Words) } : packet).ToImmutableArray(), ordering.ProxyCursor, ordering.TableBuffer, ordering.OtherTable?.ToImmutableArray());
                    NativeBattleEffectMemory memory = effect.Memory!;
                    gpu.Begin(width, height, new(0, 0, 0, 1), 0);
                    effect.Draw(gpu, host.GuestRenderCameraOutput, width, height);
                    gpu.End();
                    Microsoft.Xna.Framework.Color[] withWave = GpuTestDevice.Read(gpu.Target);
                    gpu.Begin(width, height, new(0, 0, 0, 1), 0);
                    effect.Renderer.DrawOrdering(gpu, withoutOverlay, memory.Textures!, width, height, effect.Scale, ReadControlPacket);
                    effect.Renderer.Draw(gpu, effect.Draws, effect.Textures, CrossoverNativeSpriteRenderer.Camera(host.GuestRenderCameraOutput, effect.OriginToHost), width, height, effect.Scale);
                    gpu.End();
                    Microsoft.Xna.Framework.Color[] withoutWave = GpuTestDevice.Read(gpu.Target);
                    int overlayChanges = withWave.Where((pixel, index) => pixel.R != withoutWave[index].R || pixel.G != withoutWave[index].G || pixel.B != withoutWave[index].B).Count();
                    Assert.True(overlayChanges > 0, $"The original Zombie age45 overlay must affect host pixels at {width}x{height}.");
                    zombieOverlayRendered = true;

                    byte[] ReadControlPacket(NativeBattlePrimitiveReference reference, int count)
                    {
                        byte[] bytes = reference.Bank == "20DFAB8" ? memory.Arena!.Read(reference.ByteOffset, count) : (memory.PrimitiveProcess! with { PacketBank = reference.Bank, PacketBuffer = reference.Buffer }).ReadCommonPacket(reference.ByteOffset, count);
                        if (count > 4 && omitted.Contains(NativeBattlePrimitiveProcess.NormalizeCommonReference(reference)))
                        {
                            uint header = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
                            Array.Clear(bytes, 4, bytes.Length - 4);
                            Assert.Equal(header, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
                        }
                        return bytes;
                    }
                }
            }
            Assert.Equal(before, JsonSerializer.Serialize(host.Capture()));
            Assert.Equal(random, JsonSerializer.Serialize(provider.CurrentCrtRandom));
            Assert.Same(invocation, provider.ActiveAbility);
        }
    }

    private static ImmutableArray<uint> RemoveCommands(ImmutableArray<uint> words)
    {
        uint[] result = new uint[words.Length];
        result[0] = words[0];
        return result.ToImmutableArray();
    }

    private static Ff7BattleRenderFrame Frame(Ff7BattleSession host, CrossoverGuestHostActions provider)
    {
        MethodInfo actorVisual = typeof(Ff7Scene).GetMethod("BattleActorVisual", BindingFlags.Static | BindingFlags.NonPublic)!;
        Ff7BattleActorVisual[] actors = host.Actors.Where(actor => actor.Actor.Slot != 0 && host.IsActorPresentationReady(actor.Actor.Slot) && !provider.SuppressForeignActor(actor.Actor.Slot)).Select(actor =>
        {
            int slot = actor.Actor.Slot;
            return ((Ff7BattleActorVisual)actorVisual.Invoke(null, [host, actor])!) with { Slot = slot, PresentationTransform = provider.ReadForeignPlacement(slot), ShadowSuppressed = provider.SuppressForeignShadow(slot), NativeColor = provider.ReadForeignColor(slot) };
        }).ToArray();
        CrossoverFf8BattleActorVisual? guest = host.IsActorPresentationReady(0) && !provider.SuppressForeignActor(0) ? provider.Visual : null;
        if (guest is not null && provider.ReadForeignPlacement(0) is { } placement)
        {
            guest = guest with { OriginToHost = guest.OriginToHost * placement, ShadowSuppressed = provider.SuppressForeignShadow(0), NativeColor = provider.ReadForeignColor(0) };
        }
        HashSet<NativeBattleArenaPrimitiveId> suppressed = provider.ArenaReplacements.SelectMany(replacement => replacement.Geometry.Faces).Select(face => face.Id).ToHashSet();
        suppressed.UnionWith(provider.SuppressedArenaPrimitives);
        return new(host.GuestRenderCameraOutput, actors)
        {
            Guests = guest is null ? [] : [guest with { HostSlot = 0 }],
            GuestEffectLayers = provider.EffectVisual is { } effect ? [effect] : [],
            SuppressArenaPrimitives = suppressed,
            Hitmarks = host.Hitmarks,
            SecondaryEffects = host.SecondaryEffects,
            PrimaryEffects = host.PrimaryEffects,
            StageFade = host.RenderStageFade
        };
    }

    private sealed class ValidatingRenderer(GpuTriangleRenderer renderer) : ITriangleRenderer, ILineRenderer, IFramebufferCaptureRenderer
    {
        public int Triangles { get; private set; }

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            renderer.Begin(width, height, clearColor, clearDepth);
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            AssertVertex(a);
            AssertVertex(b);
            AssertVertex(c);
            Triangles++;
            renderer.Draw(a, b, c, material);
        }

        public void End()
        {
            renderer.End();
        }

        public void DrawLine(in TriangleVertex a, in TriangleVertex b, in TriangleMaterial material)
        {
            AssertVertex(a);
            AssertVertex(b);
            renderer.DrawLine(a, b, material);
        }

        public bool TryCaptureArgb(out uint[] pixels)
        {
            return renderer.TryCaptureArgb(out pixels);
        }

        public bool TryCaptureArgb(int sourceWidth, int sourceHeight, PixelBounds sourceBounds, int destinationWidth, int destinationHeight, out uint[] pixels)
        {
            return renderer.TryCaptureArgb(sourceWidth, sourceHeight, sourceBounds, destinationWidth, destinationHeight, out pixels);
        }

        private static void AssertVertex(in TriangleVertex vertex)
        {
            Assert.True(float.IsFinite(vertex.Position.X) && float.IsFinite(vertex.Position.Y) && float.IsFinite(vertex.Depth), "The original producer must submit finite host coordinates and depth.");
            Assert.True(float.IsFinite(vertex.Uv.X) && float.IsFinite(vertex.Uv.Y), "The original producer must submit finite texture coordinates.");
            Assert.True(float.IsFinite(vertex.ReciprocalW), "The original producer must submit a finite perspective divisor.");
        }
    }
}
