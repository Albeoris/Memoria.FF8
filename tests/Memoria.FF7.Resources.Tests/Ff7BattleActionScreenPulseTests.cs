using System.Text.Json;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7BattleActionScreenPulseTests
{
    [Fact]
    public void QueuedScreenPulsesUseIndependentNativeDelays()
    {
        Ff7BattleSecondaryState state = Ff7BattleSecondaryEffects.ScheduleCallback(new(), new Ff7BattleDelayedScreenPulseCallback(1));
        state = Ff7BattleSecondaryEffects.ScheduleCallback(state, new Ff7BattleDelayedScreenPulseCallback(7));
        state = Advance(state);
        Assert.Empty(state.ScreenPulses);
        Ff7BattleSecondaryState cold = JsonSerializer.Deserialize<Ff7BattleSecondaryState>(JsonSerializer.Serialize(state))!;

        for (int callback = 2; callback <= 9; callback++)
        {
            state = Advance(state);
            cold = Advance(cold);
            Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(cold));
            if (callback is 2 or 8)
            {
                Assert.Equal(new Ff7BattleActorScreenPulse(250, 250, 250), Assert.Single(state.ScreenPulses));
                Assert.Equal(new Ff7BattleSecondaryDraw(Ff7BattleSecondaryDrawKind.ScreenPulse, 0), Assert.Single(state.DrawOrder));
            }
            else
            {
                Assert.Empty(state.ScreenPulses);
            }
        }
        Assert.False(state.HasCallbacks);
    }

    [Fact]
    public void InstalledActionDecodesBothScreenPulseOperands()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(directory), "Select original FFVII resources for the action pulse check.");
        Ff7Installation installation = Ff7Installation.Discover(directory!);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[980];
        string prefix = Ff7BattleAnimationResources.ModelPrefix(Assert.Single(formation.Enemies, enemy => enemy.EnemyId >= 0).EnemyId, true);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        byte[] Read(string name) => archive.ReadEntry(Assert.Single(archive.Find(name)));
        BattleActionBank bank = BattleActionBank.Read(Read(prefix + "ab"));
        BattleModelData model = BattleModelData.Read(Read(prefix + "aa"), Read(prefix + "da"));
        var decoded = BattleActionInstructions.ReadUntilIdle(bank.Scripts[6], model.BodyClips.Count);
        Assert.Equal(new[] { (7, (byte)1), (9, (byte)7) }, decoded.Where(instruction => instruction.Opcode == 0x91).Select(instruction => (instruction.Offset, instruction.Operands.Span[0])));
        Assert.Equal(12, decoded[^1].Offset);
        Assert.Equal(0xEE, decoded[^1].Opcode);
    }

    private static Ff7BattleSecondaryState Advance(Ff7BattleSecondaryState state)
    {
        return Ff7BattleSecondaryEffects.Advance(state, (_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), () => throw new InvalidOperationException());
    }
}
