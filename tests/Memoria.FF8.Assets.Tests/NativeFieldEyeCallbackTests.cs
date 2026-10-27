using System.Buffers.Binary;
using System.Text.Json;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeFieldEyeCallbackTests
{
    [Fact]
    public void Original_rinoa_blink_uses_registered_counters_shared_crt_and_actual_vram_with_independent_restore()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        FieldAssets field = assets.LoadField(689);
        int count = field.Jsm[0] + field.Jsm[1] + field.Jsm[2] + field.Jsm[3];
        ushort group = Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(field.Jsm.AsSpan(8 + 2 * index))).OrderBy(value => value >> 7).ElementAt(3);
        NativeFieldRetainedActor[] actors = [new(3, 3, group >> 7, (group >> 7) + (group & 127) + 1)];
        NativeFieldRegistrationStep registered = NativeFieldRegistrationMachine.ReloadKind3(field.NativeRegistration!, actors, NativeBattleRetainedMemory.FromColdProcess());
        NativeCrtRandomState before = NativeCrtRandomState.FromColdProcess();
        NativeCrtRandomState expected = before.Draw(out ushort first).Draw(out ushort second);
        NativeBattleFieldResourceReloadResult started = NativeFieldRegistrationMachine.SetEyes532340(registered.State, registered.Memory, 3, 0x80000, 4, before);
        uint instance = registered.State.Instances[0].Address;
        uint resource = registered.State.Resources[0].Address;
        uint bank = BinaryPrimitives.ReadUInt32LittleEndian(started.Memory.FileStaging!.Read(resource + 20, 4));
        Assert.Equal(expected, started.CrtRandomAfter);
        Assert.Equal((byte)(first % 8 + 1), started.Memory.FileStaging.Read(instance + 102, 1)[0]);
        Assert.Equal((byte)(second % 8 + 1), started.Memory.FileStaging.Read(instance + 116, 1)[0]);
        Assert.Equal((ushort)4, BinaryPrimitives.ReadUInt16LittleEndian(started.Memory.FileStaging.Read(bank + 10, 2)));
        Assert.Equal((byte)0x10, (byte)(started.Memory.FileStaging.Read(instance + 114, 1)[0] & 0xF0));
        NativeBattleRetainedMemory live = started.Memory;
        NativeCrtRandomState? random = started.CrtRandomAfter;
        NativeFieldEyeProcess saved = new NativeFieldEyeProcess(1, live, random).CaptureSnapshot();
        string savedJson = JsonSerializer.Serialize(saved);
        NativeFieldEyeProcess restoredProcess = JsonSerializer.Deserialize<NativeFieldEyeProcess>(savedJson)!.RestoreOwned();
        NativeBattleRetainedMemory restored = restoredProcess.Memory;
        NativeCrtRandomState? restoredRandom = restoredProcess.CrtRandom;
        byte[] original = live.EffectMemory!.Textures!.ReadVram();
        bool copied = false;
        bool idle = false;
        for (int callback = 0; callback < 40; callback++)
        {
            NativeBattleFieldResourceReloadResult step = NativeFieldRegistrationMachine.AdvanceEyes530810(registered.State, live, random);
            NativeBattleFieldResourceReloadResult independent = NativeFieldRegistrationMachine.AdvanceEyes530810(registered.State, restored, restoredRandom);
            NativeBattleFieldResourceReloadResult capturedStep = step with { Memory = new NativeFieldEyeProcess(1, step.Memory, step.CrtRandomAfter).CaptureSnapshot().Memory };
            NativeBattleFieldResourceReloadResult capturedIndependent = independent with { Memory = new NativeFieldEyeProcess(1, independent.Memory, independent.CrtRandomAfter).CaptureSnapshot().Memory };
            Assert.Equal(JsonSerializer.Serialize(capturedStep), JsonSerializer.Serialize(capturedIndependent));
            Assert.Equal(savedJson, JsonSerializer.Serialize(saved));
            live = step.Memory;
            random = step.CrtRandomAfter;
            restored = independent.Memory;
            restoredRandom = independent.CrtRandomAfter;
            copied |= !original.AsSpan().SequenceEqual(live.EffectMemory!.Textures!.ReadVram());
            idle |= (live.FileStaging!.Read(instance + 114, 1)[0] & 0xF0) == 0x80;
        }
        Assert.True(copied);
        Assert.True(idle);
        Assert.NotEqual(expected, random);
        FieldAssets visible = assets.BindNativeRegisteredTextures(field, registered.State, live.EffectMemory!.Textures!);
        Assert.Equal(field.Models[3].Textures[0].Width, visible.Models[3].Textures[0].Width);
        NativeBattleFieldResourceReloadResult closed = NativeFieldRegistrationMachine.SetEyes532340(registered.State, live, 3, 0x200000, null, random);
        Assert.Equal(random, closed.CrtRandomAfter);
        Assert.Equal(0x20, closed.Memory.FileStaging!.Read(instance + 114, 1)[0] & 0xF0);
        PixelImage openTexture = assets.BindNativeRegisteredTextures(field, registered.State, registered.Memory.EffectMemory!.Textures!).Models[3].Textures[0];
        PixelImage closedTexture = assets.BindNativeRegisteredTextures(field, registered.State, closed.Memory.EffectMemory!.Textures!).Models[3].Textures[0];
        // A controlled camera/quad magnifies the actual Rinoa destination rectangle,
        // so a renderer that ignores the registered texture produces identical pixels.
        FieldModel quad = new("eye-copy-projection", [new(-1, 0)], [new(new Vector3(0, -300, -300), 0), new(new Vector3(0, -300, 300), 0), new(new Vector3(0, 300, -300), 0), new(new Vector3(0, 300, 300), 0)], [new([0, 1, 2, 3], [new(70, 81), new(70, 89), new(82, 81), new(82, 89)], 0, true)], [openTexture], [new([new(Vector3.Zero, [Vector3.Zero])])]);
        FieldAssets projection = new(0, "eye-copy-projection", [], [], [], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), [quad]);
        FieldRenderer renderer = new(projection, modelTextureSampling: ModelTextureSampling.Nearest);
        ActorVisual visual = new(0, 0, 0, 0, 100, 192, 0, 0, true, PolygonColor: new Vector3(128));
        byte[] unchanged = renderer.Render(new Dictionary<int, int>(), [visual], TimeSpan.Zero, backgroundVisible: false).ToArray();
        byte[] patched = renderer.Render(new Dictionary<int, int>(), [visual with { RegisteredTextures = [closedTexture] }], TimeSpan.Zero, backgroundVisible: false).ToArray();
        Assert.NotEqual(unchanged, patched);
    }
}
