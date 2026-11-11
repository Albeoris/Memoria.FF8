using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleActorModelTests
{
    public static void Run()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            Console.WriteLine("SKIP installed actor model callbacks: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }
        Ff7Installation installation = Ff7Installation.Discover(directory);
        Ff7BattleActorDeathResources resources = Ff7PcExecutableData.LoadBattleActorModelResources(installation).Death;
        Check(resources.AngleStep == 22.5f && resources.LiftSteps.Count == 15 && resources.LiftSteps[14] == -100 && resources.LiftSteps[1] == -160, "Original mechanical-death rotation and signed lift inputs changed.");
        foreach ((byte kind, int count, string expected) in new (byte, int, string)[]
        {
            (0, 15, "CD28C6BE4A6263288BE726DB87CF7D0D5107D47776C0AAF07E6E2376C5A2798D"),
            (1, 15, "0FAB43A37FAA6109F49A0910A2186739CCE51A4F26B22EE54C516E0F79F5AC22"),
            (17, 66, "2999CCF22D5BB6E87C3C8E6B9523A3507778451BC6B3D0D7B75877910732406C")
        })
        {
            Ff7BattleActorDeathState? state = new(4, kind);
            Ff7BattleBossDeathShakeState? shake = null;
            Ff7BattleActorModelModifiers modifiers = new();
            short rootZ = 30;
            int sounds = 0;
            int flashes = 0;
            int callback = 0;
            using MemoryStream bytes = new();
            using BinaryWriter writer = new(bytes);
            while (state is not null || shake is not null)
            {
                if (shake is not null)
                {
                    Ff7BattleBossDeathShakeState cold = JsonSerializer.Deserialize<Ff7BattleBossDeathShakeState>(JsonSerializer.Serialize(shake))!;
                    Ff7BattleBossDeathShakeUpdate update = Ff7BattleActorDeath.AdvanceShake(shake);
                    Check(update == Ff7BattleActorDeath.AdvanceShake(cold), "A retained boss shake changed after JSON restore.");
                    shake = update.State;
                    if (update.Write?.WorldZ is short z)
                    {
                        rootZ = z;
                    }
                    if (update.Flash)
                    {
                        flashes++;
                    }
                }
                if (state is not null)
                {
                    Ff7BattleActorDeathState cold = JsonSerializer.Deserialize<Ff7BattleActorDeathState>(JsonSerializer.Serialize(state))!;
                    Ff7BattleActorModelModifiers coldModifiers = JsonSerializer.Deserialize<Ff7BattleActorModelModifiers>(JsonSerializer.Serialize(modifiers))!;
                    Ff7BattleActorDeathUpdate update = Ff7BattleActorDeath.Advance(state, modifiers, resources);
                    Check(update == Ff7BattleActorDeath.Advance(cold, coldModifiers, resources), "A native KO controller changed after JSON restore.");
                    modifiers = update.Write.Modifiers!;
                    state = update.State;
                    if (update.Write.RestoreHome)
                    {
                        rootZ = 30;
                    }
                    if (update.Sound is { } sound)
                    {
                        Check(sound.Target == 4 && sound.Channel == 3 && sound.Sound == (kind == 17 ? 0x1C0 : 0x16), "A KO callback changed its native sound, source pan owner or channel.");
                        sounds++;
                    }
                    if (update.StartShake)
                    {
                        shake = new(4, rootZ);
                    }
                }
                writer.Write(modifiers.Red);
                writer.Write(modifiers.Green);
                writer.Write(modifiers.Blue);
                writer.Write(modifiers.Transparency);
                writer.Write(modifiers.Visible);
                writer.Write(modifiers.Deferred);
                writer.Write(modifiers.ExtraEnabled);
                writer.Write(modifiers.ScaleOverride);
                writer.Write(true);
                writer.Write(modifiers.ExtraAngleX);
                writer.Write(modifiers.ExtraAngleY);
                writer.Write(modifiers.ExtraAngleZ);
                writer.Write(modifiers.ExtraX);
                writer.Write(modifiers.ExtraY);
                writer.Write(modifiers.ExtraZ);
                writer.Write((short)10);
                writer.Write((short)20);
                writer.Write(rootZ);
                writer.Write((ushort)(kind != 1 && state is not null ? 1 : 0));
                writer.Write((ushort)(kind == 1 && state is not null ? 1 : 0));
                writer.Write((ushort)(shake is not null ? 1 : 0));
                writer.Write((ushort)sounds);
                writer.Write((ushort)flashes);
                callback++;
                Check(callback <= count, "A native KO controller exceeded its original retirement callback.");
            }
            writer.Flush();
            Check(callback == count && Convert.ToHexString(SHA256.HashData(bytes.ToArray())) == expected, "A KO actor write, extra transform, shake, sound or physical-pool retirement differs from original executable callbacks.");
        }
        Check(new Ff7BattleActorModelModifiers().Alpha == 255 && new Ff7BattleActorModelModifiers(Transparency: 0x880).Alpha == 119 && new Ff7BattleActorModelModifiers(Transparency: 0x1000).Alpha == 0, "Actor transparency must use the original WORD-to-alpha consumer.");
        MechanicalDeathPose();
        InstalledModelContacts(installation);
    }

    private static void MechanicalDeathPose()
    {
        BattleClipFrame frame = new(100, -200, 300, [new(128, 256, 384), new(), new()]);
        BattleActorPoseTransform extra = new(22.5f, 22.5f, 22.5f, Y: -100);
        Matrix4x4 root = new(0.719488204f, 0.691341698f, -0.066206746f, 0, -0.369683415f, 0.461939752f, 0.806192160f, 0, 0.587937772f, -0.555570245f, 0.587937772f, 0, 100, -300, 300, 1);
        Matrix4x4 topBone = new(0.909596026f, 0.353553414f, -0.218254358f, 0, -0.218254358f, 0.853553355f, 0.473087370f, 0, 0.353553414f, -0.382683456f, 0.853553355f, 0, 0, 0, 0, 1);
        Matrix4x4 child = topBone with { M42 = -100, M43 = 100 };
        BattleModelData model = new(0, [new(-1, 100, true), new(0, 40, true)], [], []);
        Span<Matrix4x4> bones = stackalloc Matrix4x4[2];
        BattleRigidPose.WriteTransforms(model, frame, Matrix4x4.Identity, bones, extra);
        CheckMatrix(BattleRigidPose.FrameTransform(frame, extra), root);
        CheckMatrix(bones[0], topBone * root);
        CheckMatrix(bones[1], child * topBone * root);

        static void CheckMatrix(Matrix4x4 actual, Matrix4x4 expected)
        {
            foreach (Vector3 point in new[] { Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
            {
                Check(Vector3.Distance(Vector3.Transform(point, actual), Vector3.Transform(point, expected)) < 0.0002f, "Mechanical KO altered the original packed battle local Euler order or extra translation.");
            }
        }
    }

    private static void InstalledModelContacts(Ff7Installation installation)
    {
        using Ff7Session original = Ff7Session.NewGame(installation);
        Ff7Progress progress = original.Progress with { Party = [0, 255, 255] };
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        IReadOnlyList<SceneFormation> formations = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path));
        IReadOnlyList<short> materia = Ff7PcExecutableData.Load(installation).NativeMateriaStatModifiers;
        using LgpArchive models = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        Ff7BattleAnimationResources animations = new(models, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        foreach ((int id, byte kind) in new (int, byte)[] { (300, 0), (309, 1), (324, 17) })
        {
            SceneFormation formation = formations[id];
            Ff7BattleSession initial = new(formation, progress, kernel, materia, 1, animations: animations);
            Ff7BattleSnapshot snapshot = initial.Capture();
            int index = Array.FindIndex(snapshot.Actors, actor => actor.Actor.Slot == 4);
            Ff7BattleActorState target = snapshot.Actors[index];
            snapshot.Actors[index] = target with { Actor = target.Actor with { HitPoints = 1 } };
            Ff7BattleQueuedCommand command = new(2, 0, (byte)Ff7BattleCommand.Attack, 0, 0x10);
            snapshot = snapshot with { Queue = Ff7BattleCommandQueue.Enqueue(snapshot.Queue, command) };
            Ff7BattleSession battle = new(formation, progress, kernel, materia, 1, snapshot: snapshot, animations: animations);
            Ff7BattleSession? cold = null;
            bool numericBeforeContact = false;
            bool hurtBody = false;
            bool deathStarted = false;
            bool hidden = false;
            List<Ff7BattleSoundReceipt> sounds = [];
            for (int tick = 0; tick < 1500 && !hidden; tick++)
            {
                battle.Advance(waitForCommand: true);
                Ff7BattleSnapshot captured = battle.Capture();
                Ff7BattleActorModelState model = captured.ActorModels.Single(row => row.Slot == 4);
                numericBeforeContact |= (captured.Actors[index].Actor.Statuses & 1) != 0 && !battle.HasActorDeathPresentation(4) && !model.Reaction && model.Modifiers!.Visible;
                hurtBody |= model.Reaction && model.PendingSince < 0 && battle.RenderActorPose(4).Clip == 1;
                if (!deathStarted && model.Modifiers!.Red > 0)
                {
                    Check(model.Modifiers.Red == (kind == 17 ? 251 : 232) && model.Modifiers.Transparency == (kind == 17 ? 0x820 : 0x880) && model.Modifiers.Deferred && model.Modifiers.ExtraEnabled == (kind == 1), $"Original F6 selected the wrong initial death writes in formation {id}.");
                    Check(battle.HasActorDeathPresentation(4), $"Formation {id} dropped the death owner while the target was still drawn.");
                    deathStarted = true;
                }
                if (cold is not null)
                {
                    cold.Advance(waitForCommand: true);
                    Check(JsonSerializer.Serialize(captured) == JsonSerializer.Serialize(cold.Capture()), $"Formation {id} changed its independent target/KO callbacks after restore.");
                    cold.DrainSoundRequests();
                }
                else if (model is { Reaction: true, PendingSince: >= 0, ReactionKnockedOut: true })
                {
                    Check(!model.Ready && battle.RenderActorPose(4).Clip == 0 && !deathStarted, $"Formation {id} started the target VM before the next model callback.");
                    cold = new(formation, progress, kernel, materia, 1, snapshot: JsonSerializer.Deserialize<Ff7BattleSnapshot>(JsonSerializer.Serialize(captured)), animations: animations);
                    cold.DrainSoundRequests();
                }
                sounds.AddRange(battle.DrainSoundRequests());
                hidden = deathStarted && !battle.ActorModelOutput(4).Visible;
            }
            ushort deathSound = kind == 17 ? (ushort)0x1C0 : (ushort)0x16;
            Check(numericBeforeContact && cold is not null && hurtBody && deathStarted && hidden && sounds.Count(sound => sound.Sound == deathSound && sound.Channel == 3) == 1, $"Original action did not complete its hurt and KO model callbacks in formation {id}.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
