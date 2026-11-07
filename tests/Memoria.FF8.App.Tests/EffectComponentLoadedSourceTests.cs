using System.Text.Json;
using Memoria.FF8.App.Tests;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class EffectComponentLoadedSourceTests
{
    [Theory]
    [InlineData(110)]
    [InlineData(162)]
    [InlineData(248)]
    public void Original_component_keeps_its_native_preinvocation_owner_and_cold_next_callback(int selected)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for native component invocation validation.");
        ushort effect = checked((ushort)selected);
        string output = Path.Combine(Path.GetTempPath(), "component-loaded-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        try
        {
            BattleSnapshot source = effect == 162 ? NativeBattleLimitComponentSources.Prepare(effect, directory!, output) : Assert.IsType<BattleSnapshot>(NativeBattleLifecycleComponentSources.Prepare(effect, directory!, output));
            NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(source.NativeGraphics);
            NativeBattleOriginalEffectState module = Assert.IsType<NativeBattleOriginalEffectState>(graphics.OriginalEffect);
            Assert.Equal(effect, module.RequestedEffect);
            Assert.Null(module.ReturnedDescriptor);
            Assert.Empty(module.Pools);
            Assert.Null(module.Parameters);
            NativeBattleActionGraphicsState context = Assert.IsType<NativeBattleActionGraphicsState>(graphics.ActionGraphics);
            NativeBattleActionHeaderSlot header = source.NativeActionPackets!.Headers[context.HeaderIndex];
            Assert.Equal(NativeBattleActionGraphics.SelectService(header).Callback, context.Callback);
            if (header.ReadByte(1) == 252)
            {
                Assert.Equal(0x50B190u, context.Callback);
            }
            else
            {
                Assert.Equal(ushort.MaxValue, NativeBattleActionGraphics.Word(header, 4));
                Assert.Equal(0x50BC20u, context.Callback);
            }
            Assert.Contains(graphics.Descriptors.Tasks, task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Action104 && (context.Callback == 0x50BC20 && task.Phase is 1 or 2 || context.Callback == 0x50B190 && task.Phase is 2 or 3));
            string frozen = JsonSerializer.Serialize(source);
            BattleSession direct = BattleSession.Restore(source);
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(frozen)!);
            ComponentSourceAdmission admission = Assert.IsType<ComponentSourceAdmission>(GameWorld.ValidateEffectComponentSource(source, restored, effect));
            Assert.Equal(effect, admission.Effect);
            Assert.Equal(header.ReadByte(1), admission.Command);
            Assert.Equal(NativeBattleActionGraphics.Word(header, 4), admission.Action);
            if (effect != 162)
            {
                Assert.Equal((byte)252, admission.Command);
                Assert.Equal((ushort)1, admission.Action);
            }
            Assert.Equal(context.HeaderIndex, admission.HeaderIndex);
            Assert.Equal(frozen, JsonSerializer.Serialize(restored.Capture()));
            Assert.Throws<InvalidDataException>(() => GameWorld.ValidateEffectComponentSource(source, restored, checked((ushort)(effect + 1))));
            NativeBattleGraphicsState invalidGraphics = graphics with { ActionGraphics = context with { Callback = 0x50BD00 } };
            BattleSnapshot invalid = source with { NativeGraphics = invalidGraphics };
            Assert.Throws<InvalidDataException>(() => GameWorld.ValidateEffectComponentSource(invalid, BattleSession.Restore(invalid), effect));
            using GameAssets game = new(directory!);
            direct.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
            restored.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
            using IDisposable scope = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
            direct.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(restored.Capture()));
            Assert.Equal(frozen, JsonSerializer.Serialize(source));
        }
        finally
        {
            Directory.Delete(output, true);
        }
    }
}
