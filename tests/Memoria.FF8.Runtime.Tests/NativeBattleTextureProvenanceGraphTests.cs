using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTextureProvenanceGraphTests
{
    [Fact]
    public void Json_alias_restore_preserves_projected_file_descriptors_and_independent_authorities()
    {
        BattleSession session = Fixture();
        BattleSnapshot captured = session.Capture();
        string json = JsonSerializer.Serialize(captured);
        BattleSession first = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        NativeBattleNodeSource source = first.NativeEffectMemory!.Recover93!.Nodes.Data!;
        NativeBattleTextureFileSource file = Assert.Single(source.Arena.TextureFiles!.Value);
        Assert.Equal(16, file.Offset);
        Assert.Equal(4, file.Length);
        Assert.Equal("source.tim", file.Identity.Name);
        Assert.Equal(32, file.Identity.SourceOffset);
        Assert.Equal("container.bin", file.Identity.SourceOwner);
        Assert.Equal(new string('A', 64), file.Sha256);
        NativeBattleTextureFileSource absolute = Assert.Single(source.Arena.Working.TextureFiles!.Value);
        Assert.Equal(80, absolute.Offset);
        Assert.Same(first.NativeEffectMemory.Arena!.Working, source.Arena.Working);
        source.Arena.RememberTextureFile(16, [1, 2, 3, 4], new("next.tim", NativeBattleTextureDomain.Magic));
        Assert.Equal("next.tim", Assert.Single(source.Arena.TextureFiles!.Value).Identity.Name);
        Assert.Equal("source.tim", Assert.Single(second.NativeEffectMemory!.Recover93!.Nodes.Data!.Arena.TextureFiles!.Value).Identity.Name);
        Assert.Equal(json, JsonSerializer.Serialize(captured));
    }

    [Fact]
    public void Conflicting_view_file_receipt_is_rejected_instead_of_dropping_provenance()
    {
        JsonObject saved = JsonNode.Parse(JsonSerializer.Serialize(Fixture().Capture()))!.AsObject();
        saved["NativeEffectMemory"]!["Recover93"]!["Nodes"]!["Data"]!["TextureFiles"]![0]!["Sha256"] = new string('B', 64);
        InvalidDataException failure = Assert.Throws<InvalidDataException>((Action)(() => BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(saved.ToJsonString())!)));
        Assert.Contains("texture files disagree", Assert.IsType<InvalidOperationException>(failure.InnerException).Message, StringComparison.Ordinal);
    }

    private static BattleSession Fixture()
    {
        BattleSession session = new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        }, BattleAtbMode.Normal);
        NativeBattleEffectMemory memory = session.NativeEffectMemory!.BeginLoad(0);
        NativeBattleEffectArena arena = memory.Arena!;
        arena.Write(80, [1, 2, 3, 4]);
        arena.Working.SetTextureFiles([new(80, 4, new("source.tim", NativeBattleTextureDomain.Battle) { SourceOwner = "container.bin", SourceOffset = 32 }, new string('A', 64))]);
        NativeBattleNodeSource source = NativeBattleNodeSource.Borrow(arena, 64, 64);
        NativeBattleRecover93NodeState nodes = NativeBattleRecover93NodeState.FromColdProcess() with { Data = source };
        memory = memory with { Recover93 = new(null, [], null, null, null, 0, 0, nodes) };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, memory);
        return session;
    }
}
