using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CidIdleModTests
{
    [Fact]
    public void Bundled_cid_binding_uses_the_original_idle_animation_and_stays_still_while_the_field_advances()
    {
        string source = TestResources.FixturePath("App.Tests.CidIdleMod.1");
        string game = TestResources.GameDirectory;
        string patchPath = Path.Combine(AppContext.BaseDirectory, "BundledMods", "Memoria", "Depixelation", "fields", "bghall_6.json");
        ModFile file = new("Memoria/Depixelation", ModFormat.Memoria, "fields/bghall_6.json", 1, () => File.ReadAllBytes(patchPath));
        Assert.True(FieldSceneMods.TryPrepare(new(1, [], [file]), out FieldSceneMods mods, out var diagnostics));
        Assert.Empty(diagnostics);
        FieldActorPatch binding = Assert.Single(Assert.Single(mods.Patches).Patch.Actors);

        GameSession session = new(GameSession.Load(source));
        GameSession baselineSession = new(GameSession.Load(source));
        using GameWorld world = new(session, game, readSceneMods: () => mods);
        using GameWorld baseline = new(baselineSession, game);
        world.EnsureField();
        baseline.EnsureField();
        FieldAssets field = world.Field!;
        Assert.Equal(175, field.Id);
        FieldModel model = field.Models[binding.Model.ModelId];
        JsmProgram program = JsmProgram.Read(field.Jsm);
        EventScript cidInitialization = program.Objects[9].Scripts[0];
        var baseAnimation = Assert.Single(cidInitialization.ReachableOperations.Select(index => program.Operations[index]), operation => operation.Opcode == Jsm.Opcode.BASEANIME);
        Assert.Equal(baseAnimation.Parameter, binding.AnimationId);
        Assert.Single(model.Animations[binding.AnimationId].Frames);
        Assert.True(model.Animations[1].Frames.Count > 1);
        ActorState actor = Assert.Single(world.Events.Actors, actor => actor.ObjectId == binding.ObjectId);
        ActorVisual initial = world.CreateVisual(actor);
        Vector3[] idlePose = model.Pose(initial.AnimationId, initial.AnimationFrame);
        Assert.True(initial.Visible);
        Assert.True(JsonElement.DeepEquals(baseline.Events.Capture(), world.Events.Capture()));

        for (int callback = 0; callback < 45; callback++)
        {
            PlayerActionResult result = world.Apply(new(PlayerActionKind.Wait));
            PlayerActionResult baselineResult = baseline.Apply(new(PlayerActionKind.Wait));
            Assert.True(result.Accepted);
            Assert.Equal(baselineResult.FramesAdvanced, result.FramesAdvanced);
            ActorVisual visual = world.CreateVisual(actor);
            Assert.Equal(0, visual.AnimationFrame);
            Assert.Equal(initial.AnimationId, visual.AnimationId);
            Assert.Equal((initial.X, initial.Y, initial.Z, initial.Direction), (visual.X, visual.Y, visual.Z, visual.Direction));
            Assert.Equal(idlePose, model.Pose(visual.AnimationId, visual.AnimationFrame));
            Assert.True(JsonElement.DeepEquals(baseline.Events.Capture(), world.Events.Capture()));
        }
        Assert.Null(world.Events.LastFault);
    }
}
