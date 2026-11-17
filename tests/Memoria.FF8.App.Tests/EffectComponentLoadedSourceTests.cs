using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class EffectComponentLoadedSourceTests
{
    [Fact]
    public void Loaded_component_preserves_the_preinvocation_boundary_and_forked_callback()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = GameWorld.CreateColdProcess(directory);
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        BattleSession source = EffectComponentPreviewTests.PrepareOriginalEnemyComponent(world);
        BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(source.Presentation);
        for (int callback = 0; callback < 240 && process.Graphics.OriginalEffect is null; callback++)
        {
            source.Tick();
        }
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 219);
        Assert.True(admission.ModuleLoaded);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        NativeBattleOriginalEffectState effect = Assert.IsType<NativeBattleOriginalEffectState>(process.Graphics.OriginalEffect);
        Assert.Empty(effect.Pools);
        Assert.Null(effect.ReturnedDescriptor);
        BattleSession restored = source.ForkForContinuation();
        Assert.Equal(admission, GameWorld.ValidateEffectComponentSource(restored, 219));
        Assert.Throws<InvalidDataException>(() => GameWorld.ValidateEffectComponentSource(restored, 220));

        using IDisposable visibleArea = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
        source.Tick();
        restored.Tick();

        Assert.Equal(JsonSerializer.Serialize(source.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }
}
