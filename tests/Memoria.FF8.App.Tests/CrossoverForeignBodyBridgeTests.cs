using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class CrossoverForeignBodyBridgeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Blind", (byte)0, (byte)0)]
    [InlineData("Pain", (byte)0, (byte)0)]
    [InlineData("Break", (byte)0, (byte)0)]
    [InlineData("Death", (byte)0, (byte)0)]
    [InlineData("Reflect", (byte)0, (byte)0)]
    [InlineData("Confuse", (byte)0, (byte)0)]
    [InlineData("Laser Eye", (byte)3, (byte)1)]
    [InlineData("Aqua Breath", (byte)3, (byte)1)]
    [InlineData("LV?Death", (byte)3, (byte)1)]
    [InlineData("Acid", (byte)3, (byte)1)]
    [InlineData("Acid", (byte)3, (byte)2)]
    [InlineData("Acid", (byte)3, (byte)3)]
    [InlineData("Acid", (byte)3, (byte)4)]
    [InlineData("Meltdown", (byte)0, (byte)0)]
    [InlineData("Stop", (byte)0, (byte)0)]
    [InlineData("The End", (byte)0, (byte)0)]
    [InlineData("Bad Breath", (byte)3, (byte)1)]
    [InlineData("Bad Breath", (byte)3, (byte)2)]
    [InlineData("Bad Breath", (byte)3, (byte)3)]
    [InlineData("Bad Breath", (byte)3, (byte)4)]
    public void OriginalBodyConsumersCompleteNativeContactsReleaseAndFollowingFire(string name, byte character, byte crisis)
    {
        Run(name, character, crisis);
    }

    [Theory]
    [InlineData("Break", (byte)0, (byte)0)]
    [InlineData("Acid", (byte)3, (byte)1)]
    public void OriginalBodyPacketsRenderActualHostMaterialsAtBothDisplaySizes(string name, byte character, byte crisis)
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR")) && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")), "Select both original game installations.");
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        bool rendered = false;
        int foreignCallbacks = 0;
        int texturedSurfaces = 0;
        int untexturedSurfaces = 0;
        int submittedTriangles = 0;
        int matchedTriangles = 0;
        int maximumChangedPixels = 0;
        Vector2 minimumPosition = new(float.PositiveInfinity);
        Vector2 maximumPosition = new(float.NegativeInfinity);
        float minimumDepth = float.PositiveInfinity;
        float maximumDepth = float.NegativeInfinity;
        Vector4 minimumColor = new(float.PositiveInfinity);
        Vector4 maximumColor = new(float.NegativeInfinity);
        Run(name, character, crisis, observe: Observe);
        output.WriteLine($"{name} GPU: foreign callbacks {foreignCallbacks}, textured/untextured surfaces {texturedSurfaces}/{untexturedSurfaces}, submitted/matched triangles {submittedTriangles}/{matchedTriangles}, maximum changed pixels {maximumChangedPixels}, screen bounds {minimumPosition}..{maximumPosition}, depth {minimumDepth}..{maximumDepth}, colors {minimumColor}..{maximumColor}.");
        Assert.True(rendered, "The original body operation must emit visible geometry using the actual posed host's material.");

        void Observe(Ff7BattleRenderer renderer, Ff7BattleSession host, CrossoverGuestHostActions provider)
        {
            if (rendered || provider.EffectVisual is not { Ordering: { } ordering } visual)
            {
                return;
            }
            NativeBattleForeignRenderMaterial[] materials = ordering.Packets.Select(packet => packet.PilotGeometry?.ForeignSurface?.Material).OfType<NativeBattleForeignRenderMaterial>().ToArray();
            if (materials.Length == 0)
            {
                return;
            }
            foreignCallbacks++;
            texturedSurfaces += materials.Count(material => material.Material.Texture is not null);
            untexturedSurfaces += materials.Count(material => material.Material.Texture is null);
            MethodInfo actorVisual = typeof(Ff7Scene).GetMethod("BattleActorVisual", BindingFlags.Static | BindingFlags.NonPublic)!;
            HashSet<TriangleMaterial> hostMaterials = host.Actors.Where(actor => actor.Actor.Slot != 0).SelectMany(actor => renderer.CaptureActorGeometry((Ff7BattleActorVisual)actorVisual.Invoke(null, [host, actor])!).Faces).Select(face => NormalizeMaterial(((NativeBattleForeignRenderMaterial)face.Surface!.Material).Material)).ToHashSet();
            Assert.NotEmpty(hostMaterials);
            Assert.All(materials, material => Assert.Contains(NormalizeMaterial(material.Material), hostMaterials));
            HashSet<TriangleMaterial> foreignMaterials = materials.Select(material => NormalizeMaterial(material.Material)).ToHashSet();
            foreach ((int width, int height) in new[] { (320, 240), (640, 360) })
            {
                gpu.Begin(width, height, Vector4.Zero, 0);
                gpu.End();
                Microsoft.Xna.Framework.Color[] baseline = GpuTestDevice.Read(gpu.Target);
                gpu.Begin(width, height, Vector4.Zero, 0);
                HostMaterialRenderer body = new(gpu, foreignMaterials);
                visual.Draw(body, host.GuestRenderCameraOutput, width, height);
                gpu.End();
                submittedTriangles += body.Submitted;
                matchedTriangles += body.Triangles;
                minimumPosition = Vector2.Min(minimumPosition, body.MinimumPosition);
                maximumPosition = Vector2.Max(maximumPosition, body.MaximumPosition);
                minimumDepth = Math.Min(minimumDepth, body.MinimumDepth);
                maximumDepth = Math.Max(maximumDepth, body.MaximumDepth);
                minimumColor = Vector4.Min(minimumColor, body.MinimumColor);
                maximumColor = Vector4.Max(maximumColor, body.MaximumColor);
                if (body.Triangles == 0)
                {
                    return;
                }
                Microsoft.Xna.Framework.Color[] drawn = GpuTestDevice.Read(gpu.Target);
                int changedPixels = baseline.Where((pixel, index) => pixel != drawn[index]).Count();
                maximumChangedPixels = Math.Max(maximumChangedPixels, changedPixels);
                if (changedPixels <= 16)
                {
                    return;
                }
                gpu.Begin(width, height, Vector4.Zero, 0);
                gpu.End();
                Assert.Equal(baseline, GpuTestDevice.Read(gpu.Target));
            }
            rendered = true;
        }
    }

    [Theory]
    [InlineData("Blind")]
    [InlineData("Pain")]
    [InlineData("Reflect")]
    [InlineData("Confuse")]
    public void OriginalNativeGroupSelectionRemainsAtomic(string name)
    {
        Run(name, 0, 0, group: true);
    }

    internal void Run(string name, byte character, byte crisis, bool group = false, bool unsupported = false, Action<Ff7BattleRenderer, Ff7BattleSession, CrossoverGuestHostActions>? observe = null)
    {
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff8) && !string.IsNullOrWhiteSpace(ff7), "Select both original game installations.");
        using GameAssets game = new(ff8!);
        using BattleAssets assets = new(ff8!);
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals().DeepCopy();
        int row = CrossoverGuestRecord.FindOriginalRow(origin, character);
        int magicCommand = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2) + 20;
        origin.Records[row] = origin.Records[row] with { HitPointBonus = 9999, HitPoints = new(9000, 9000), Status = 0, Junction = origin.Records[row].Junction with { Commands = (uint)magicCommand } };
        origin = origin with { MagicSystem = origin.MagicSystem with { SharedMagicStock = true } };
        BattleNativeAction fire = Assert.Single(assets.NativeActions, action => action.Command == 2 && action.Name == "Fire");
        BattleNativeAction action = Assert.Single(assets.NativeActions, action => action.Command == (crisis == 0 ? 2 : 15) && action.CrisisLevel == (crisis == 0 ? (byte?)null : crisis) && Normalize(action.Name) == Normalize(name));
        origin = origin.ChangeMagicStock(row, fire.ActionId, 4, MagicOperationReason.ScriptReward, out _);
        if (crisis == 0)
        {
            origin = origin.ChangeMagicStock(row, action.ActionId, 4, MagicOperationReason.ScriptReward, out _);
        }
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(origin, assets, character);
        GameplaySaveLimitProgress learned = GameplaySaveLimitProgress.Capture(game.ReadNewGameLimitProgress()) with { BlueMagicFlags = ushort.MaxValue };
        CrossoverGuestAbility ability = Assert.Single(CrossoverGuestAbilityCatalog.Read(assets, origin, kit, learned, crisis), choice => choice.NativeAction == action);
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7Progress progress = original.Progress with { Party = [0, 1, 255] };
        Ff7BattleSession initial = Create();
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), initial.Capture(), [new(0, 0, character)], []);
        Ff7BattleGuestProjection projection = CrossoverGuestHostActions.Projection(kit);
        initial.BindGuestVitals(new(roster.EncounterId, new('A', 64), 0, 0, character, row, kit.HitPoints, (ushort)kit.MaximumHitPoints, projection.Level, projection.Luck, projection.Defense, projection.MagicDefense));
        Ff7BattleSnapshot fixture = initial.Capture();
        fixture = fixture with
        {
            Actors = fixture.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Gauge = ushort.MaxValue, AwaitingTurn = true } : actor with
            {
                Gauge = 0,
                AwaitingTurn = false,
                StatusProtection = actor.Actor.Slot == 5 ? actor.StatusProtection | 0x4001u : actor.StatusProtection,
                Actor = actor.Actor with { HitPoints = actor.Actor.Slot >= 4 ? 60000u : 100u, MaxHitPoints = actor.Actor.Slot >= 4 ? 60000u : actor.Actor.MaxHitPoints, Statuses = 0x400 },
                Timers = Ff7NativeBattleStatusClock.Apply(actor.Timers, actor.Actor.Statuses, 0x400)
            }).ToArray()
        };
        Ff7BattleSession host = Create(fixture);
        CrossoverGunbladeHostAttack attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.RenderCameraOutput, _ => 128, readHostImpactAnchor: host.GuestEffectTargetAnchor, readHostTargetBone: host.GuestEffectTargetBone);
        attack.PrepareStanding(host.VisualPlacement(0));
        CrossoverGuestHostActions provider = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets);
        provider.BindLimitProgress(learned);
        Ff7BattleRenderer renderer = new(archive, formation.LocationId);
        MethodInfo actorVisual = typeof(Ff7Scene).GetMethod("BattleActorVisual", BindingFlags.Static | BindingFlags.NonPublic)!;
        int capturedPoses = 0;
        provider.BindForeignGeometry(slot =>
        {
            Ff7BattleActorGeometry geometry = renderer.CaptureActorGeometry(Visual(slot));
            Assert.NotEmpty(geometry.Faces);
            Assert.NotEmpty(geometry.Bones);
            Assert.All(geometry.Faces, face => Assert.IsType<NativeBattleForeignRenderMaterial>(face.Surface!.Material));
            capturedPoses++;
            return geometry;
        });
        provider.BindArena(renderer.CaptureArenaGeometry);
        host.BindGuestAttack(provider);
        Ready();
        if (crisis != 0)
        {
            typeof(CrossoverGuestHostActions).GetField("limitSelection", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(provider, new CrossoverNativeRenzokukenSelection(NativeBattleRandomState.Initialize(0), host.Tick, crisis));
        }
        Ff7BattleCommandOption option = Assert.Single(host.Commands(0), option => option.Command == Ff7BattleCommand.Magic && option.Argument == ability.Key);
        ushort targets = group ? (ushort)0x30 : (ushort)0x10;
        if (!group && (!option.SelectSingle || option.GroupUses != 0))
        {
            targets = (ushort)((option.CandidateTargets ?? option.ValidTargets) & (option.AllTargets ? 0x3F7 : 0x3F0));
        }
        string stockBefore = JsonSerializer.Serialize(provider.WorkingStock);
        if (unsupported || group && option.SelectSingle && option.GroupUses == 0)
        {
            string before = JsonSerializer.Serialize(host.Capture());
            if (unsupported)
            {
                string? reason = host.GuestAbilityTargetUnsupportedReason(0, Ff7BattleCommand.Magic, ability.Key, 4);
                Assert.False(string.IsNullOrWhiteSpace(reason));
                Assert.Equal(0, option.ValidTargets & 0x10);
                output.WriteLine($"{name}: target unavailable: {reason}");
            }
            Assert.False(host.Submit(0, Ff7BattleCommand.Magic, ability.Key, targets));
            Assert.Equal(before, JsonSerializer.Serialize(host.Capture()));
            Assert.Equal(stockBefore, JsonSerializer.Serialize(provider.WorkingStock));
            Assert.Null(provider.Invocation);
            return;
        }
        Assert.True(option.UnsupportedReason is null, option.UnsupportedReason);
        Assert.NotEqual((ushort)0, targets);
        Assert.Equal(targets, (ushort)(targets & option.ValidTargets));
        foreach (Ff7BattleActorState target in host.Actors.Where(actor => (targets & 1 << actor.Actor.Slot) != 0))
        {
            Assert.Null(host.GuestAbilityTargetUnsupportedReason(0, Ff7BattleCommand.Magic, ability.Key, target.Actor.Slot));
        }
        Assert.True(host.Submit(0, Ff7BattleCommand.Magic, ability.Key, targets));
        Assert.Equal(targets, Assert.Single(host.Capture().Queue.Ready.OfType<Ff7BattleQueuedCommand>()).TargetMask);
        Assert.Equal(stockBefore, JsonSerializer.Serialize(provider.WorkingStock));
        bool began = false;
        int contacts = 0;
        HashSet<int> publications = [];
        HashSet<int> foreignIndices = [];
        NativeBattleEffectPresentation? effectOwner = null;
        for (int tick = 0; tick < 10000; tick++)
        {
            host.Advance(waitForCommand: true);
            Assert.True(host.GuestAbilityFailure is null, host.GuestAbilityFailure?.Reason);
            observe?.Invoke(renderer, host, provider);
            began |= provider.Invocation is not null;
            if (provider.ActiveAbility is { } active)
            {
                contacts = Math.Max(contacts, active.Presentation.PublishedContacts);
                effectOwner = active.Presentation.Effect ?? effectOwner;
                foreach (var bound in active.Presentation.BoundTargets.Where(target => target.Contact.ActorIndex != 0))
                {
                    foreignIndices.Add(bound.Contact.ActorIndex);
                }
                IReadOnlyList<Ff7BattleGuestPhysicalHit> hits = active.GroupPrepared.IsEmpty ? [active.Prepared] : active.GroupPrepared;
                foreach (Ff7BattleGuestPhysicalHit hit in hits)
                {
                    Ff7BattleGuestPreparedContact receipt = hit.PreparedContact!;
                    int slot = receipt.Invocation.TargetSlot;
                    if ((active.Presentation.PublishedTargetMask & 1 << slot) != 0 && publications.Add(slot))
                    {
                        Ff7BattleActorState currentState = host.Actors.Single(actor => actor.Actor.Slot == slot);
                        Ff7BattleAiActor current = currentState.Actor;
                        Assert.Equal(receipt.Receipt.Target.Hp, current.HitPoints);
                        Assert.Equal(receipt.Receipt.Target.Statuses, current.Statuses);
                        if (receipt.Receipt.AddedParameterStatus is { } parameter)
                        {
                            Assert.Equal((current.Statuses & Ff7NativeBattleActionResolver.Death) == 0 ? parameter : null, currentState.ParameterStatus);
                        }
                        if (receipt.Receipt.AddedOriginSavedStatuses is ushort saved && saved != 0)
                        {
                            Assert.Equal((current.Statuses & Ff7NativeBattleActionResolver.Death) == 0 ? saved : (ushort)0, (ushort)(host.GetOriginSavedStatuses(slot) & saved));
                        }
                    }
                }
            }
            if (began && provider.Invocation is null && host.Action is null)
            {
                break;
            }
        }
        Assert.True(began);
        Assert.True(capturedPoses > 0);
        Assert.Null(provider.Invocation);
        Assert.Null(host.Action);
        Assert.Equal(BitOperations.PopCount(targets), contacts);
        Assert.Equal(contacts, publications.Count);
        Assert.Equal(targets, (ushort)publications.Aggregate(0, (mask, slot) => mask | 1 << slot));
        Assert.NotNull(effectOwner);
        Assert.True(effectOwner.Complete);
        Assert.Empty(effectOwner.ArenaReplacements);
        foreach (int index in foreignIndices)
        {
            NativeBattleActionMatrix placement = effectOwner.ForeignPlacement(index);
            Assert.Equal((0, 0, 0), (placement.X, placement.Y, placement.Z));
            Assert.True(placement.Rotation.SequenceEqual(NativeBattleForeignModelGeometry.Identity.Rotation));
        }
        Assert.Null(provider.EffectVisual);
        Assert.Equal((byte)1, provider.Visual!.Model.Sequence);
        Assert.Null(provider.Visual.Model.ResourceMagicId);
        Assert.Null(provider.Visual.Model.ActiveStagedResourceRow100);
        Assert.Equal(provider.Visual.Model.OriginalResourceRow104, provider.Visual.Model.ActiveResourceRow100);
        if (crisis == 0)
        {
            Assert.Equal(3, provider.WorkingStock.GetMagicCount(row, action.ActionId));
        }
        else
        {
            Assert.Equal(stockBefore, JsonSerializer.Serialize(provider.WorkingStock));
        }
        Assert.Equal(4, provider.WorkingStock.GetMagicCount(row, fire.ActionId));
        if (host.Outcome != Ff7BattleOutcome.Fighting || !host.Actors.Any(actor => actor.Actor.Slot >= 4 && actor.Actor.HitPoints > 0 && (actor.Actor.Statuses & 0x4001) == 0))
        {
            Assert.True(host.Actors.Where(actor => actor.Actor.Slot >= 4).All(actor => actor.Actor.HitPoints == 0 || (actor.Actor.Statuses & 0x4001) != 0));
            output.WriteLine($"{name} Crisis{crisis}: native mask {targets:X}, {contacts} contacts, presentation released and no combat-eligible enemy remains.");
            return;
        }
        Ready();
        Ff7BattleCommandOption nextOption = Assert.Single(host.Commands(0), choice => choice.Command == Ff7BattleCommand.Magic && choice.Argument == fire.ActionId);
        Assert.True(nextOption.UnsupportedReason is null, nextOption.UnsupportedReason);
        int nextTarget = host.Actors.First(actor => actor.Actor.Slot >= 4 && (nextOption.ValidTargets & 1 << actor.Actor.Slot) != 0).Actor.Slot;
        Assert.True(host.Submit(0, Ff7BattleCommand.Magic, fire.ActionId, (ushort)(1 << nextTarget)));
        bool nextBegan = false;
        for (int tick = 0; tick < 4000; tick++)
        {
            host.Advance(waitForCommand: true);
            Assert.True(host.GuestAbilityFailure is null, host.GuestAbilityFailure?.Reason);
            nextBegan |= provider.Invocation is not null;
            if (nextBegan && provider.Invocation is null && host.Action is null)
            {
                break;
            }
        }
        Assert.True(nextBegan);
        Assert.Null(provider.Invocation);
        Assert.Null(host.Action);
        Assert.Equal(3, provider.WorkingStock.GetMagicCount(row, fire.ActionId));
        output.WriteLine($"{name} Crisis{crisis}: native mask {targets:X}, {contacts} contacts, posed host geometry, release and following Fire completed.");

        Ff7BattleSession Create(Ff7BattleSnapshot? saved = null) => new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, snapshot: saved, animations: animations);
        Ff7BattleActorVisual Visual(int slot) => (Ff7BattleActorVisual)actorVisual.Invoke(null, [host, host.Actors.Single(actor => actor.Actor.Slot == slot)])!;
        void Ready()
        {
            for (int tick = 0; tick < 10000 && host.ReadyPlayer != 0; tick++)
            {
                host.Advance();
            }
            Assert.Equal(0, host.ReadyPlayer);
        }
    }

    private static string Normalize(string? name) => (name ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal);

    private static TriangleMaterial NormalizeMaterial(TriangleMaterial material) => material with { DepthComparison = material.DepthComparison == TriangleDepthComparison.Always ? TriangleDepthComparison.Always : TriangleDepthComparison.GreaterEqual, ActorOpacity = 1 };

    private sealed class HostMaterialRenderer(ITriangleRenderer renderer, HashSet<TriangleMaterial> materials) : ITriangleRenderer
    {
        public int Triangles { get; private set; }
        public int Submitted { get; private set; }
        public Vector2 MinimumPosition { get; private set; } = new(float.PositiveInfinity);
        public Vector2 MaximumPosition { get; private set; } = new(float.NegativeInfinity);
        public float MinimumDepth { get; private set; } = float.PositiveInfinity;
        public float MaximumDepth { get; private set; } = float.NegativeInfinity;
        public Vector4 MinimumColor { get; private set; } = new(float.PositiveInfinity);
        public Vector4 MaximumColor { get; private set; } = new(float.NegativeInfinity);

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Submitted++;
            if (!materials.Contains(NormalizeMaterial(material)))
            {
                return;
            }
            Assert.True(float.IsFinite(a.Uv.X) && float.IsFinite(a.Uv.Y));
            Assert.True(float.IsFinite(b.Uv.X) && float.IsFinite(b.Uv.Y));
            Assert.True(float.IsFinite(c.Uv.X) && float.IsFinite(c.Uv.Y));
            MinimumPosition = Vector2.Min(MinimumPosition, Vector2.Min(a.Position, Vector2.Min(b.Position, c.Position)));
            MaximumPosition = Vector2.Max(MaximumPosition, Vector2.Max(a.Position, Vector2.Max(b.Position, c.Position)));
            MinimumDepth = Math.Min(MinimumDepth, Math.Min(a.Depth, Math.Min(b.Depth, c.Depth)));
            MaximumDepth = Math.Max(MaximumDepth, Math.Max(a.Depth, Math.Max(b.Depth, c.Depth)));
            MinimumColor = Vector4.Min(MinimumColor, Vector4.Min(a.Color, Vector4.Min(b.Color, c.Color)));
            MaximumColor = Vector4.Max(MaximumColor, Vector4.Max(a.Color, Vector4.Max(b.Color, c.Color)));
            renderer.Draw(a, b, c, material);
            Triangles++;
        }

        public void End()
        {
        }
    }
}
