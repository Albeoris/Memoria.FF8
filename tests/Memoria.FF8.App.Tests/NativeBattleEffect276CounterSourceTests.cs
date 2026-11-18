using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect276CounterSourceTests
{
    [Fact]
    public void Installed_death_program_requires_five_native_revivals_before_its_final_dispatch()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using BattleAssets assets = new(game!);
        NativeBattleAiDefinition original = assets.ReadEnemyAi(92, 7);
        NativeBattleComponentSourceTests.RequireOriginalEffect276Counter(original);
        byte[][] changed = original.Scripts.Select(script => script.ToArray()).ToArray();
        changed[3][4] = 5;
        Assert.Throws<InvalidDataException>(() => NativeBattleComponentSourceTests.RequireOriginalEffect276Counter(original with { Scripts = changed }));
        NativeBattleComponentSourceTests.RequireOriginalEffect276Counter(original);
    }
}
