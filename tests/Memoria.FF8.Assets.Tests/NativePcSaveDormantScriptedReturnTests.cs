using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed partial class NativePcSaveConverterTests
{
    [Fact]
    public void Dormant_scripted_endpoint_native_field_export_reimports_its_full_word_yaw_and_pose()
    {
        GameplaySaveWorldReturn endpoint = new(GameplaySaveWorldReturnKind.Scripted, 74, 0, new(111, -222, 3, 4), 63886, 22, 255, 255, 0, 0, 1) { ScriptedPacket = new(0, 22, 0), ScriptedFieldId = 74, ScriptedFieldPosition = new(10, 20, 0, 4) };
        GameState source = Defaults();
        source = source with { SavedWorld = source.SavedWorld! with { Origin = new(NativeSavedWorldState.CurrentProofVersion, NativeSavedWorldOriginKind.GameplaySave, NativeSavedWorldState.GameplaySaveInitializationRule), DormantScriptedReturn = endpoint } };
        byte[] exported = NativePcSaveConverter.Export(source);
        NativePcSaveData decoded = NativePcSaveCodec.Decode(exported);
        int savedWorld = NativePcSaveData.MainOffset + 4624;
        Assert.Equal((ushort)63886, BinaryPrimitives.ReadUInt16LittleEndian(decoded.Block.AsSpan(savedWorld + 96)));
        Assert.Equal(111, BinaryPrimitives.ReadInt32LittleEndian(decoded.Block.AsSpan(savedWorld)));
        Assert.Equal(-222, BinaryPrimitives.ReadInt32LittleEndian(decoded.Block.AsSpan(savedWorld + 4)));
        Assert.Equal((byte)22, decoded.Block[savedWorld + 105]);
        GameState imported = NativePcSaveConverter.Import(exported, Defaults(), _ => "fixture", "en");
        Assert.Equal(SceneKind.Field, imported.Scene);
        Assert.Equal((ushort)63886, imported.SavedWorld!.OriginalAngleWords!.CameraWord);
        Assert.Equal((ushort)63886, imported.SavedWorld.UnsupportedEntry!.CameraWord96);
        Assert.Null(imported.SavedWorld.ScriptedReturn);
    }
}
