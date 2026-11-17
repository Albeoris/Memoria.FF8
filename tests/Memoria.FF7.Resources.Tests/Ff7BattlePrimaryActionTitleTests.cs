using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Resources.Tests;

public sealed class Ff7BattlePrimaryActionTitleTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(255)]
    public void DelayedTitlePreservesItsUnsignedDelayAcrossColdContinuation(byte delay)
    {
        IReadOnlyList<BattleActionInstruction> decoded = BattleActionInstructions.ReadUntilIdle(new byte[] { 0x98, delay, 0xEE }, 1);
        Assert.Equal(2, decoded.Count);
        Assert.Equal(2, decoded[1].Offset);
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleActionTitle(new(), 0x20, 7, 128, delay);
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), () => throw new InvalidOperationException());
        for (int frame = 0; frame < delay; frame++)
        {
            state = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            Assert.Null(state.ActionTitle);
            Assert.Equal((byte)(delay - frame - 1), Assert.IsType<Ff7BattlePrimaryActionTitle>(Assert.Single(state.Slots, slot => slot is not null)).Delay);
        }
        for (int frame = 0; frame < 36; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            Assert.Equal(new Ff7BattleActionTitle(0x20, 7), state.ActionTitle);
        }
        state = Ff7BattlePrimaryEffects.Advance(state, context);
        Assert.Null(state.ActionTitle);
        Assert.False(state.HasCallbacks);
    }

    [Fact]
    public void InstalledTerminalReactionDecodesTheOriginalDelayedTitle()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(directory), "Select original FFVII resources for the delayed title reaction check.");
        Ff7Installation installation = Ff7Installation.Discover(directory!);
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path))[600];
        int identity = formation.Enemies[0].EnemyId;
        string prefix = Ff7BattleAnimationResources.ModelPrefix(identity, true);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        byte[] Read(string name) => archive.ReadEntry(Assert.Single(archive.Find(name)));
        BattleActionBank bank = BattleActionBank.Read(Read(prefix + "ab"));
        BattleModelData model = BattleModelData.Read(Read(prefix + "aa"), Read(prefix + "da"));
        ReadOnlyMemory<byte> script = Assert.Single(bank.Scripts, script => script.Length > 16 && script.Span[15] == 0x98);
        IReadOnlyList<BattleActionInstruction> decoded = BattleActionInstructions.ReadUntilIdle(script, model.BodyClips.Count);
        BattleActionInstruction title = Assert.Single(decoded, instruction => instruction.Opcode == 0x98);
        Assert.Equal(15, title.Offset);
        Assert.Single(title.Operands.ToArray());
        Console.WriteLine($"Delayed title resource {prefix}ab, actor {identity}, delay {title.Operands.Span[0]}, script {Convert.ToHexString(script.Span)}");
    }

    [Fact]
    public void NativeTitlePublishesThirtySixFramesAndRetiresOnTheFollowingCallback()
    {
        Ff7BattlePrimaryState state = Ff7BattlePrimaryEffects.ScheduleActionTitle(new(), 0x20, 7, 128);
        Ff7BattlePrimaryContext context = new((_, _, _) => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), () => throw new InvalidOperationException());
        Assert.Null(state.ActionTitle);
        for (int frame = 0; frame < 36; frame++)
        {
            state = Ff7BattlePrimaryEffects.Advance(state, context);
            Assert.Equal(new Ff7BattleActionTitle(0x20, 7), state.ActionTitle);
            Assert.True(state.HasCallbacks);
            if (frame == 18)
            {
                state = JsonSerializer.Deserialize<Ff7BattlePrimaryState>(JsonSerializer.Serialize(state))!;
            }
        }
        state = Ff7BattlePrimaryEffects.Advance(state, context);
        Assert.Null(state.ActionTitle);
        Assert.False(state.HasCallbacks);
    }
}
