using System.Buffers.Binary;
using System.Reflection;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleControl110ResourcesTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void Original_stream_static_frame_and_flipbooks_preserve_loader_and_resource_owners()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleControl110Resources resources = game.ReadNativeControl110Resources();
        using BattleAssets battle = new(TestResources.GameDirectory);
        NativeBattleAiDefinition biggs = battle.ReadEnemyAi(74, 10);
        NativeBattleAiDefinition wedge = battle.ReadEnemyAi(73, 10);
        Assert.Equal(new byte[] { 4, 205, 30, 110, 29, 0, 29, 1, 31, 2 }, biggs.Scripts[3].AsSpan(32, 10).ToArray());
        Assert.Equal(new byte[] { 4, 205, 30, 110, 29, 0, 29, 1, 31, 2 }, wedge.Scripts[3].AsSpan(11, 10).ToArray());
        Assert.False(string.IsNullOrWhiteSpace(biggs.Texts[10]));
        Assert.False(string.IsNullOrWhiteSpace(wedge.Texts[1]));
        Assert.Equal(0x2588, resources.Stream.Length);
        Assert.Equal(308, BinaryPrimitives.ReadInt32LittleEndian(resources.Stream.AsSpan().Slice(4)));
        Assert.Equal(20, resources.StaticFrame.Length);
        Assert.All(resources.StaticFrame, value => Assert.Equal((byte)0, value));
        Assert.Equal(308 * 8, NativeBattleControl110Motion.Normalize(resources.Stream.Slice(8, 308 * 8)).Length);
        foreach (var item in new[] { (0, 8), (1, 13) })
        {
            for (int frame = 0; frame < item.Item2; frame++)
            {
                Assert.NotNull(resources.Sprites[item.Item1].ProjectedFrame571C80(frame));
            }
        }
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 1)]
    [InlineData(true, 4)]
    [InlineData(true, 14)]
    [InlineData(true, 33)]
    [InlineData(true, 110)]
    public void Installed_control_director_births_and_drains_its_owned_targets(bool expanded, int count)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        var (logical, selected) = ExpandedBattlePresentationProcessEntryTests.CreateDefinitions(game, assets, expanded ? 4 : 3, expanded ? 5 : 4, 4, expanded);
        BattleSession owner = new(logical, BattleAtbMode.TurnBased);
        owner.AttachPresentation(new(selected));
        BattlePresentationProcess runner = Assert.IsType<BattlePresentationProcess>(owner.Presentation);
        for (int callback = 0; callback < 1200 && !runner.EntryReady; callback++)
        {
            owner.Tick();
        }
        Assert.True(runner.EntryReady);

        NativeBattleGraphicsState graphics = runner.NativeGraphics!;
        BattleActorModels models = graphics.ActorModels;
        NativeBattleModelAliases aliases = graphics.NativeBindings.CaptureAliases(models);
        BattleModelOwner source = models.Owners.First(actor => actor.Side == BattleSide.Enemies);
        BattleModelOwner[] allies = models.Owners.Where(actor => actor.Side == BattleSide.Allies).ToArray();
        BattleModelOwner[] recipients = Enumerable.Range(0, count).Select(index => allies[index % allies.Length]).ToArray();
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Create(models.Roster, runner.ActionPackets.ExportRetained(), aliases);
        (packets, BattleActionHeaderReference header) = packets.Prepare(new(source, 8, 4, 0, 30, 110, null));
        foreach (BattleModelOwner recipient in recipients)
        {
            (packets, _) = packets.AppendContact(new(new(recipient, 1, 0, 0, 0, 0, 0), null));
        }
        (packets, _) = packets.Publish(count);
        typeof(BattlePresentationProcess).GetField("nativeActionPackets", PrivateInstance)!.SetValue(runner, packets);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([110])[109];
        var effect = new NativeBattleOriginalEffectState(110, 112, binding, new(source, 0, BattleModelTargets.From(recipients), header, 0, 0), 0);
        MethodInfo enter = typeof(BattlePresentationProcess).GetMethod("EnterNativeControl110", PrivateInstance)!;
        MethodInfo advance = typeof(BattlePresentationProcess).GetMethod("AdvanceNativeControl110", PrivateInstance)!;
        SetMemory(runner.NativeEffectMemory! with { Arena = NativeBattleEffectArena.Clear() });
        graphics = Assert.IsType<NativeBattleGraphicsState>(enter.Invoke(runner, [graphics with { OriginalEffect = effect }]));

        var births = new Dictionary<int, (int Frame, BattleModelOwner Owner)>();
        bool copied = false;
        bool hosted = false;
        for (int callback = 0; callback < 160 && graphics.OriginalEffect!.ReturnedDescriptor != 0; callback++)
        {
            FieldInfo hostedServices = typeof(BattlePresentationProcess).GetField("hostedFrameServices", PrivateInstance)!;
            hostedServices.SetValue(runner, owner.CaptureModelLifecycleServices(models.Roster));
            try
            {
                graphics = Assert.IsType<NativeBattleGraphicsState>(advance.Invoke(runner, [graphics with { PrimitiveFrame = NativeBattlePrimitiveFrame.Clear() }]));
            }
            finally
            {
                hostedServices.SetValue(runner, null);
            }
            if (graphics.OriginalEffect!.Pools.Length != 6)
            {
                continue;
            }
            NativeBattleEffectTaskPool targetPool = graphics.OriginalEffect.Pools[1];
            NativeBattleModulePoolTask director = graphics.OriginalEffect.Pools[5].Slots[0];
            int directorAge = director.ReadWord(12) - 1;
            Assert.Equal(Math.Max(2, count), targetPool.Slots.Length);
            hosted |= NativeBattlePresentationTaskStorage.IsHostedBank(targetPool.BankAddress, 44, targetPool.Slots.Length);
            for (int? slot = targetPool.Head; slot is int index; slot = targetPool.Slots[index].Next)
            {
                NativeBattleModulePoolTask task = targetPool.Slots[index];
                int ordinal = task.ReadWord(32);
                BattleModelOwner recipient = task.ReadExecutionReference<BattleExecutionReference.Model>(28, 4).Owner;
                births.TryAdd(ordinal, (directorAge, recipient));
            }
            if (count > 2 && !copied && directorAge >= 24)
            {
                var frozen = NativeBattleStorageSnapshots.Capture(new ControlContinuation(graphics, runner.NativeEffectMemory!));
                ControlContinuation restored = NativeBattleStorageSnapshots.Restore(frozen);
                Assert.NotSame(targetPool.RequireOwner().Storage, restored.Graphics.OriginalEffect!.Pools[1].RequireOwner().Storage);
                graphics = restored.Graphics;
                SetMemory(restored.Memory);
                copied = true;
            }
        }
        Assert.Equal(Enumerable.Range(0, count), births.Keys);
        Assert.Equal(recipients, births.Values.Select(value => value.Owner));
        Assert.Equal((0, recipients[0]), births[0]);
        if (count > 2)
        {
            Assert.True(hosted);
            Assert.True(copied);
            Assert.All(births.Skip(1), item => Assert.Equal(14 + (54 * item.Key + count - 1) / count, item.Value.Frame));
        }
        Assert.Equal(0u, graphics.OriginalEffect!.ReturnedDescriptor);
        Assert.All(graphics.OriginalEffect.Pools, pool => Assert.Null(pool.Head));
        Assert.DoesNotContain(graphics.OriginalEffect.Pools, pool => NativeBattlePresentationTaskStorage.IsHostedBank(pool.BankAddress, pool.Stride, pool.Slots.Length));
        NativeBattleGraphicsState next = Assert.IsType<NativeBattleGraphicsState>(enter.Invoke(runner, [graphics with { OriginalEffect = effect }]));
        Assert.Equal(0x6CE2A0u, Assert.Single(Assert.Single(next.OriginalEffect!.Pools).Slots).Callback);

        void SetMemory(NativeBattleEffectMemory memory)
        {
            typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeEffectMemory))!.SetValue(runner, memory);
        }
    }

    private sealed record ControlContinuation(NativeBattleGraphicsState Graphics, NativeBattleEffectMemory Memory);
}
