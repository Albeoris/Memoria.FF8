using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeMidBattleSaveMigration
{
    internal static void MigratePersistentFlags(JsonNode document, GameAssets assets)
    {
        JsonNode events = document["EventState"]!;
        JsonNode scene = events["Scene"]!;
        Assert.Null(scene["NativeBattleFlagWrites"]);
        Assert.NotNull(scene["NativeBattleMusicEntry"]);
        Assert.Equal("ff8-en-music-new-game-470560-52ca90-v1", scene["MusicProcess"]!["Bootstrap"]!["Rule"]!.GetValue<string>());
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, scene["MusicProcess"]!["Bootstrap"]!["ExecutableHash"]!.GetValue<string>());
        Assert.Equal(130, document["FieldId"]!.GetValue<int>());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(assets.LoadField(130).Jsm)), events["ProgramHash"]!.GetValue<string>());
        JsonArray completions = scene["NativeBattleCompletions"]!.AsArray();
        JsonNode battle = Assert.Single(completions)!;
        Assert.Equal(94, battle["Encounter"]!.GetValue<int>());
        Assert.Equal(4, battle["RawFlags"]!.GetValue<int>());
        Assert.Equal(4, battle["Flags"]!.GetValue<int>());
        Assert.Equal("Interactive", battle["Backend"]!.GetValue<string>());
        Assert.False(battle["Suppressed"]!.GetValue<bool>());
        Assert.Null(battle["Result"]);
        Assert.Null(battle["ReturnFrame"]);
        Assert.Null(battle["FlagConsumer"]!["PersistentModeFlags"]);
        foreach (JsonNode memory in new[] { document["EventMemory"]!, events["EventMemory"]! })
        {
            foreach (var cell in memory["Cells"]!.AsObject())
            {
                int address = int.Parse(cell.Key, System.Globalization.CultureInfo.InvariantCulture);
                Assert.False(address <= 183 && address + cell.Value!["Width"]!.GetValue<int>() > 182);
            }
        }
        foreach (JsonNode timer in new[] { document["ExamTimer"]!, events["ExamTimer"]!, document["Battle"]!["NativeExamTimer"]!, document["Battle"]!["Definition"]!["NativeExamTimerAtEntry"]! })
        {
            Assert.True(timer["TimerMemoryActivated"]!.GetValue<bool>());
            Assert.True(timer["Event182Mask4"]!.GetValue<bool>());
            Assert.True(timer["SaveGlobalMask4"]!.GetValue<bool>());
            Assert.Null(timer["Event183Mask1"]);
        }
        NativeBattleFlagWrite[] history =
        [
            new(1, NativeBattleFlagWriter.SavedStateMigration, 130, events["ProgramHash"]!.GetValue<string>(), 0, -1, -1, -1, null, 4, 4),
            new(1, NativeBattleFlagWriter.Battle, 130, events["ProgramHash"]!.GetValue<string>(), battle["Frame"]!.GetValue<long>(), battle["ObjectId"]!.GetValue<int>(), battle["ScriptId"]!.GetValue<int>(), battle["Operation"]!.GetValue<int>(), 4, 4, 4)
        ];
        scene["NativeBattleFlagWrites"] = JsonSerializer.SerializeToNode(history, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } });
        battle["FlagConsumer"]!["PersistentModeFlags"] = 4;
        foreach (JsonNode timer in new[] { document["ExamTimer"]!, events["ExamTimer"]!, document["Battle"]!["NativeExamTimer"]!, document["Battle"]!["Definition"]!["NativeExamTimerAtEntry"]! })
        {
            timer["Event183Mask1"] = false;
        }
        Assert.Equal(0, document["Battle"]!["Definition"]!["NativeFieldBattleMode"]!.GetValue<int>());
        document["Battle"]!["Definition"]!["NativeFieldBattleMode"] = 4;
    }
}
