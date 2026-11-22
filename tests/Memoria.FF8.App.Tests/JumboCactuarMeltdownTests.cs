using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class JumboCactuarMeltdownTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MeltdownKeepsTheTargetsTextureBankThroughTheBodyHandoff(bool drawCast)
    {
        string source = TestResources.FixturePath("jumbo-cactuar-meltdown-source");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(PlayerStateLoader.Load(source, directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        WorldMapScene scene = Assert.IsType<WorldMapScene>(world.WorldScene);
        RandomEncounterSessionState random = session.State.RandomEncounters!;
        session.SetRandomEncounters(random with { Enabled = true, Pending = new(SceneKind.WorldMap, session.State.FieldId, scene.State.Frame, 712, scene.AllocationProgramSha256) });
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);

        const int spell = 49;
        BattleCombatant target = Assert.Single(battle.Combatants, actor => actor.IsActive && actor.Name == "Jumbo Cactuar");
        BattleCombatant caster = battle.Combatants.First(actor => actor.IsPlayer && (drawCast
            ? actor.Abilities.Contains(BattleAbility.Draw)
            : actor.Abilities.Contains(BattleAbility.Magic) && actor.MagicStocks.Any(stock => stock.Id == spell && stock.Count > 0)));
        caster.Atb = BattleSession.AtbMaximum;
        world.ApplyBattleInput(new(BattleInputKind.Action, ActorId: caster.Id, TargetId: target.Id, Ability: drawCast ? BattleAbility.Draw : BattleAbility.Magic, ActionId: spell, DrawCast: drawCast) { DrawCastTargetId = drawCast ? target.Id : null });

        BattleSession? continuation = null;
        List<int> visibleAges = [];
        bool ordinaryBodyRestored = false;
        bool completed = false;
        for (int tick = 0; tick < 320 && !completed; tick++)
        {
            world.ApplyBattleInput(new(BattleInputKind.Tick));
            NativeBattlePreparedPresentation prepared = Assert.IsType<NativeBattlePreparedPresentation>(battle.PreparedPresentation);
            NativeBattleLogicalResolvedCommand[] body = ReadMeltdownBody(battle, prepared, out int age);
            if (body.Length != 0)
            {
                Assert.All(body.Select(command => command.Command.ModelTextureOwner?.RequireActorId()), owner => Assert.Equal(target.Id, owner));
                visibleAges.Add(age);
                if (age == 1)
                {
                    AssertBodyContributesPixels(prepared, body);
                    if (drawCast)
                    {
                        continuation = battle.ForkForContinuation();
                    }
                }
                else if (age == 2 && drawCast)
                {
                    Assert.NotNull(continuation);
                    continuation.Tick();
                    NativeBattleLogicalResolvedCommand[] continuedBody = ReadMeltdownBody(continuation, continuation.PreparedPresentation!, out int continuedAge);
                    Assert.Equal(age, continuedAge);
                    Assert.All(continuedBody.Select(command => command.Command.ModelTextureOwner?.RequireActorId()), owner => Assert.Equal(target.Id, owner));
                    Assert.Equal(body.Select(command => (command.Command.Packet, command.Command.OrderingBucket)), continuedBody.Select(command => (command.Command.Packet, command.Command.OrderingBucket)));
                    continuation = null;
                }
            }
            else if (visibleAges.Count != 0 && !ordinaryBodyRestored)
            {
                Assert.Contains(prepared.Primitives.LastCommands, command => command.Command.PresentationActorId == target.Id && command.Command.ModelTextureOwner?.RequireActorId() == target.Id);
                ordinaryBodyRestored = true;
            }
            completed = battle.Events.Any(item => item.Kind == "ActionComplete" && item.ActorId == caster.Id);
        }

        Assert.Equal(Enumerable.Range(1, 34), visibleAges);
        Assert.True(ordinaryBodyRestored);
        Assert.True(completed);
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));
    }

    private static NativeBattleLogicalResolvedCommand[] ReadMeltdownBody(BattleSession battle, NativeBattlePreparedPresentation prepared, out int age)
    {
        NativeBattleModulePoolTask? task = battle.Presentation!.Graphics.OriginalEffect?.Pools.SelectMany(pool => pool.Slots)
            .SingleOrDefault(task => task.Callback == 0x612540 && task.ReadWord(12) is >= 1 and <= 34);
        if (task is null)
        {
            age = 0;
            return [];
        }

        age = task.ReadWord(12);
        NativeBattlePilotVisualIdentity producer = NativeBattlePilotVisualLifetimes.Identity(148, task);
        NativeBattleLogicalResolvedCommand[] body = prepared.Primitives.LastCommands.Where(command =>
            command.Pass == 1 && (command.Command.Opcode & 0xFC) is 0x24 or 0x2C &&
            command.Command.PilotGeometry?.Identity is { } identity && (identity with { Primitive = 0 }) == producer).ToArray();
        Assert.NotEmpty(body);
        return body;
    }

    private static void AssertBodyContributesPixels(NativeBattlePreparedPresentation prepared, NativeBattleLogicalResolvedCommand[] body)
    {
        HashSet<NativeBattlePrimitiveReference> bodyPackets = body.Select(command => command.Command.Packet).ToHashSet();
        BattleRenderer renderer = new(320, 216);
        byte[] scene = Render(prepared.Primitives);
        byte[] withoutBody = Render(prepared.Primitives with { LastCommands = NativeBattleResolvedCommandList.From(prepared.Primitives.LastCommands.Where(command => !bodyPackets.Contains(command.Command.Packet))) });
        Assert.False(scene.AsSpan().SequenceEqual(withoutBody), "The model contributed no pixels on the first deformation callback.");

        byte[] Render(NativeBattlePreparedPrimitiveFrame frame)
        {
            return renderer.RenderNativePrimitiveFrame(frame, prepared.Textures, capabilities: prepared.PrimitiveCapabilities, textureWindowCache: prepared.TextureWindows).ToArray();
        }
    }
}
