using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeTerminalModelActionTests
{
    internal static void Run()
    {
        Ff7BattleSecondaryState fade = Ff7BattleSecondaryEffects.ScheduleCallback(new(), new Ff7BattleScreenFadeCallback());
        for (int callback = 0; callback < 20; callback++)
        {
            Ff7BattleSecondaryState restored = JsonSerializer.Deserialize<Ff7BattleSecondaryState>(JsonSerializer.Serialize(fade))!;
            fade = Step(fade);
            byte opacity = (byte)(callback < 15 ? callback * 16 : 255);
            Check(fade.ScreenPulses is [{ Red: 0, Green: 0, Blue: 0 } pulse] && pulse.Opacity == opacity && fade.DrawOrder is [{ Kind: Ff7BattleSecondaryDrawKind.ScreenPulse, Index: 0 }], "The native secondary fade must emit black through the 16-step ramp and hold it in draw order.");
            Check(JsonSerializer.Serialize(fade) == JsonSerializer.Serialize(Step(restored)), "Restoring the secondary fade changed the next native callback.");
        }
        Check(fade.Slots.OfType<Ff7BattleScreenFadeCallback>().Single().Age == Ff7BattleScreenFadeCallback.HoldAge, "The fully faded callback must remain in its original secondary slot.");
        CheckInstalledTerminalAction();
    }

    private static Ff7BattleSecondaryState Step(Ff7BattleSecondaryState state) => Ff7BattleSecondaryEffects.Advance(state, (_, _, _) => throw new InvalidOperationException("Screen fade must not query actor geometry."), _ => throw new InvalidOperationException("Screen fade must not query actor rotation."), () => throw new InvalidOperationException("Screen fade must not consume random values."));

    private static void CheckInstalledTerminalAction()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed terminal model action: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        using Ff7Session original = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(entry => entry.FormationId == 925);
        IReadOnlyList<short> materia = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        using LgpArchive models = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(models, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        const int slot = 4;
        Ff7BattleSession Create(Ff7BattleSnapshot? snapshot = null) => new(formation, original.Progress, kernel, materia, 1, snapshot: snapshot, animations: animations);
        Ff7BattleSession initial = Create();
        SceneBattleEnemy enemy = initial.Data.Enemies.Single();
        SceneBattleAttack attack = initial.Data.Attacks.Single(candidate => candidate.Id == 0x3C0);
        int actionAnimation = enemy.Attacks.Single(candidate => candidate.AttackId == attack.Id).ActionAnimation;
        string prefix = initial.ActorModelPrefix(slot);
        BattleActionBank bank = BattleActionBank.Read(models.ReadEntry(models.Find(prefix + "ab").Single()));
        BattleModelData model = BattleModelData.Read(models.ReadEntry(models.Find(prefix + "aa").Single()), models.ReadEntry(models.Find(prefix + "da").Single()));
        IReadOnlyList<BattleActionInstruction> instructions = BattleActionInstructions.ReadUntilIdle(bank.Scripts[actionAnimation], model.BodyClips.Count);
        Check(instructions.Single(instruction => instruction.Opcode == 0x93) is { Offset: 8, Operands.IsEmpty: true } && instructions[^1] is { Opcode: 0xF1, Offset: 10, Operands.IsEmpty: true }, "The installed action must decode its zero-operand fade and hold before trailing padding.");
        Ff7BattleSnapshot source = initial.Capture() with { Action = new(new(0, slot, 0x20, checked((ushort)attack.Slot), 1 << slot, attack.Id), 0, actionAnimation, Applied: true) };
        Ff7BattleSession host = Create(source);
        Ff7BattleSession? resumed = null;
        bool finished = false;
        for (int tick = 0; tick < 2000; tick++)
        {
            host.Advance(waitForCommand: true);
            Ff7BattleSnapshot current = host.Capture();
            if (resumed is not null)
            {
                resumed.Advance(waitForCommand: true);
                Check(JsonSerializer.Serialize(current) == JsonSerializer.Serialize(resumed.Capture()), "The installed terminal sequence diverged after private restoration.");
            }
            else if (current.SecondaryEffects?.Slots.OfType<Ff7BattleScreenFadeCallback>().Any() == true)
            {
                resumed = Create(current);
            }
            if (current.Action is null)
            {
                Ff7BattleActorModelState actor = current.ActorModels.Single(candidate => candidate.Slot == slot);
                Check(actor.Ready && actor.Pose is { TerminalOpcode: 0xF1, Completed: true } pose && pose.NextInstruction == instructions.Count - 1 && pose.ClipIndex == 0x0B && actor.Modifiers?.Visible == source.ActorModels.Single(candidate => candidate.Slot == slot).Modifiers?.Visible, "The terminal hold must retain its cursor, pose, and model visibility while releasing action readiness.");
                Check(current.SecondaryEffects?.Slots.OfType<Ff7BattleScreenFadeCallback>().Count() == 1, "The installed fade must remain scheduled once after the actor reaches its terminal hold.");
                finished = true;
                break;
            }
        }
        Check(finished && resumed is not null, "The installed terminal action did not complete after its persistent fade began.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
