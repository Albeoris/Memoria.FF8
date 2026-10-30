using System.Buffers.Binary;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OriginalSaveInitializationTests
{
    [Fact]
    public void Original_load_preserves_saved_system_bytes_before_native_field_entry()
    {
        string? fixture = Environment.GetEnvironmentVariable("MEMORIA_FF8_PC_SAVE_FIXTURE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(fixture) && File.Exists(fixture), "Set MEMORIA_FF8_PC_SAVE_FIXTURE to test original initialization.");
        NativePcSaveData image = NativePcSaveCodec.Decode(File.ReadAllBytes(fixture!));
        int bank = NativePcSaveData.EventBankOffset;
        image.Block[bank + 86] = 35;
        image.Block[bank + 213] = 37;
        image.Block[bank + 214] = 0;
        image.Block[bank + 242] = 91;
        using GameAssets assets = new(TestResources.GameDirectory);
        GameState imported = NativePcSaveConverter.Import(NativePcSaveCodec.Encode(image), assets);
        Assert.Equal((byte)35, imported.WorldConfiguration!.EntityByte);
        EventEngine engine = new(new GameSession(imported));
        engine.InitializeOriginalSave(NativeFieldRuntimeProfile.FF8En);
        Assert.Equal((byte)91, engine.Scene.DrawPointIndex);
        Assert.Equal((byte)37, engine.Scene.MusicProcess!.Requested213);
        Assert.Equal((byte)0, engine.Scene.MusicProcess.Complete214);
        Assert.Equal(-1, engine.Scene.MusicChannel108!.ChannelId);
        Assert.Equal(-1, engine.Scene.MusicChannel112!.ChannelId);
    }

    [Fact]
    public void Original_field_entry_refreshes_all_counter_mirrors_and_music_completion()
    {
        string? fixture = Environment.GetEnvironmentVariable("MEMORIA_FF8_PC_SAVE_FIXTURE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(fixture) && File.Exists(fixture), "Set MEMORIA_FF8_PC_SAVE_FIXTURE to test original field entry.");
        NativePcSaveData image = NativePcSaveCodec.Decode(File.ReadAllBytes(fixture!));
        int main = NativePcSaveData.MainOffset;
        Assert.SkipWhen(BinaryPrimitives.ReadUInt16LittleEndian(image.Block.AsSpan(main + 3312)) == 2, "This check requires an original field save.");
        for (int row = 0; row < 8; row++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(image.Block.AsSpan(main + 1088 + 152 * row + 144), (ushort)(10 + row));
            BinaryPrimitives.WriteUInt16LittleEndian(image.Block.AsSpan(main + 1088 + 152 * row + 146), (ushort)(20 + row));
        }
        image.Block[NativePcSaveData.EventBankOffset + 214] = 0;
        string directory = Path.Combine(Path.GetTempPath(), "Memoria.FF8-original-entry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "field.ff8");
        try
        {
            File.WriteAllBytes(path, NativePcSaveCodec.Encode(image));
            GameState state = OriginalPlayerSaveLoader.Load(path, TestResources.GameDirectory);
            for (int row = 0; row < 8; row++)
            {
                Assert.Equal(10 + row, state.EventMemory.Read(28 + 2 * row, 2).RequireExact());
                Assert.Equal(20 + row, state.EventMemory.Read(44 + 2 * row, 2).RequireExact());
            }
            Assert.Equal(108, state.EventMemory.Read(68, 4).RequireExact());
            GameSession resumed = new(state);
            using GameWorld world = new(resumed, TestResources.GameDirectory, validateWorldPresentation: false);
            world.EnsureField();
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1)).Accepted);
            Assert.Equal((byte)1, world.Events.Scene.MusicProcess!.Complete214);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
