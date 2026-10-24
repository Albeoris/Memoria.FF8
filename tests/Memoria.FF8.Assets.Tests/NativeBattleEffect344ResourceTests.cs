using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect344Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect344ResourceTests
{
    [Fact]
    public void Original_embedded_directory_retains_same_address_music_and_exact_section_lengths()
    {
        using GameAssets game = Open();
        NativeBattleEffect344Resources resources = game.ReadNativeEffect344Resources();
        resources.Validate();
        Assert.Equal(MusicLength, resources.Music.Data.Length);
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        NativeBattleMusicLoadSource source = NativeBattleMusicLoadSource.Read501A20(resources.Music.Data);
        Assert.Equal(129568, source.SoundBank.Length);
        Assert.Equal(10528, source.Music.Length);
        Assert.Equal(new NativeMusicBufferIdentity(91, NativeMusicBufferProducer.Battle501A70), source.ReadIdentity());
        Assert.Equal("AKAO"u8.ToArray(), resources.ReadStatic(MusicAddress + 129584, 4));
        Assert.Equal(source.Music.ToArray(), resources.ReadStatic(MusicAddress + 129584, 10528));
        Assert.Equal(CameraAddress, MusicAddress + (uint)MusicLength);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(CameraAddress - 1, 2));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x19F1018, 4));
    }

    [Fact]
    public void Original_camera_registers_both_owners_and_runs_all_selected_track_segments()
    {
        using GameAssets game = Open();
        NativeBattleEffect344Resources resources = game.ReadNativeEffect344Resources();
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Register5099A0(resources.Camera);
        Assert.Equal(8, textures.Animation!.StreamOffset);
        Assert.Equal(12, textures.Animation.ModelTableOffset);
        NativeBattleCameraBank bank = new(resources.Camera.Data.AsSpan().Slice(12).ToArray().ToImmutableArray());
        Assert.Equal(24, bank.TrackOffset(0));
        NativeBattleCameraState camera = NativeBattleCameraState.Initialize().RegisterControlResource(textures.Animation);
        camera = camera.AdvanceControl(bank, resources.Camera.Data.AsSpan().Slice(8).ToArray().ToImmutableArray(), new());
        Assert.Same(textures.Animation, camera.ControlResource);
        Assert.Null(camera.ControlCursor);
        HashSet<byte> references = [];
        HashSet<ushort> activeFlags = [];
        bool finalSegmentCaptured = false;
        NativeBattleCameraPoseInputs inputs = new()
        {
            TransformReference = (reference, point) =>
            {
                references.Add(reference);
                return point;
            }
        };
        for (int callback = 0; callback < 80; callback++)
        {
            NativeBattleCameraPose? priorPose = camera.CurrentPose;
            bool captured = camera.CapturedPose is not null;
            camera = camera.Tick(0, inputs);
            foreach (NativeBattleCameraTask task in camera.Tasks.OfType<NativeBattleCameraTask>())
            {
                activeFlags.Add(task.Pose!.Flags);
            }
            if (!captured && camera.CapturedPose is not null)
            {
                Assert.Equal(priorPose, camera.CapturedPose);
                finalSegmentCaptured = true;
            }
        }
        Assert.Empty(camera.Order);
        Assert.Equal(new byte[] { 0x90 }, references.Order().ToArray());
        Assert.Equal(new ushort[] { 0x3C1, 0x3F7 }, activeFlags.Order().ToArray());
        Assert.True(finalSegmentCaptured);
        Assert.NotNull(camera.CurrentPose);
    }

    [Fact]
    public void Selected_RET_catalog_keeps_resources_and_json_preserves_exact_addressed_fronts()
    {
        using GameAssets game = Open();
        NativeBattleEffect344Resources resources = game.ReadNativeEffect344Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([344])[343];
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.True(binding.InitializerIsRet);
        Assert.Null(binding.InitializerResource);
        Assert.Null(binding.InitializerAsset);
        Assert.Same(resources, binding.Effect344Resources);
        NativeBattleEffect344Resources restored = JsonSerializer.Deserialize<NativeBattleEffect344Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.ReadStatic(CameraAddress, CameraLength), restored.ReadStatic(CameraAddress, CameraLength));
        byte[] malformed = resources.Music.Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(12), MusicLength - 1);
        Assert.Throws<InvalidDataException>(() => (resources with { Music = resources.Music with { Data = malformed.ToImmutableArray() } }).Validate());
    }

    [Fact]
    public void Original_actor_DAT_text_bank_is_available_without_AI_and_survives_resource_json()
    {
        using GameAssets game = Open();
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR")!;
        using BattleAssets assets = new(directory);
        NativeBattleGraphicsResource resource = assets.ReadEnemyNativeGraphicsResource(122);
        Assert.NotNull(resource.TextBank);
        Assert.Equal(8, resource.TextBank!.Value.Length);
        Assert.NotNull(resource.ModelLoadSources);
        NativeBattleModelDatSource dat = Assert.Single(resource.ModelLoadSources!.Value);
        byte[] bytes = dat.Bytes.ToArray();
        int section = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(32));
        int indices = section + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(section + 8));
        int text = section + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(section + 12));
        NativeBattleGraphicsResource restored = JsonSerializer.Deserialize<NativeBattleGraphicsResource>(JsonSerializer.Serialize(resource))!;
        for (int index = 3; index <= 6; index++)
        {
            int start = text + BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(indices + 2 * index));
            int length = bytes.AsSpan(start).IndexOf((byte)0);
            string expected = FF8Encoding.CreateEuropean().GetString(bytes.AsSpan(start, length));
            Assert.NotEmpty(expected);
            Assert.Equal(expected, resource.ReadText91(index));
            Assert.Equal(expected, restored.ReadText91(index));
        }
        int tagged = text + BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(indices + 12));
        int taggedLength = bytes.AsSpan(tagged).IndexOf((byte)0);
        Assert.Contains((byte)3, bytes.AsSpan(tagged, taggedLength).ToArray());
        Assert.Throws<InvalidDataException>(() => restored.ReadText91(-1));
        Assert.Throws<InvalidDataException>(() => restored.ReadText91(8));
        Assert.Throws<NotSupportedException>(() => (restored with { TextBank = null }).ReadText91(3));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect344 resources.");
        return new(directory!);
    }
}
