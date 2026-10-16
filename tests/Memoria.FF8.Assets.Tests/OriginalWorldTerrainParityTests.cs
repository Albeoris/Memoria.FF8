using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class OriginalWorldTerrainParityTests
{
    [Fact]
    public void Shared_contract_matches_156_executed_original_function_results()
    {
        string path = TestResources.FixturePath("Assets.Tests.OriginalWorldTerrainParityTests.1");
        using JsonDocument original = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("064d466b5fe2ba901fd44abf19f37c0fd6a2db40aabd95c9e5959195b6589570", original.RootElement.GetProperty("sha256").GetString());
        int count = 0;
        foreach (JsonElement sample in original.RootElement.GetProperty("cases").EnumerateArray())
        {
            int entity = sample.GetProperty("entity").GetInt32();
            int expected = sample.GetProperty("result").GetInt32();
            switch (sample.GetProperty("function").GetString())
            {
                case "53E6B0":
                case "53E730":
                    uint flags = 5u | (uint)sample.GetProperty("texture").GetInt32() << 8 | (uint)sample.GetProperty("vertex").GetInt32() << 16;
                    bool actual = sample.GetProperty("function").GetString() == "53E6B0" ? NativeWorldTerrain.CanTraverse(flags, entity) : NativeWorldTerrain.CanDisembark(flags, entity);
                    Assert.Equal(expected != 0, actual);
                    break;
                case "53ECE0":
                    NativeWorldTerrainHeightResult height = NativeWorldTerrain.ResolveHeight(entity, sample.GetProperty("groundHeight").GetInt32(), sample.GetProperty("groundType").GetByte(), 0);
                    Assert.Equal(expected, height.Height);
                    break;
                default:
                    throw new InvalidDataException("Unknown original world evidence function.");
            }
            count++;
        }
        Assert.Equal(156, count);
    }
}
