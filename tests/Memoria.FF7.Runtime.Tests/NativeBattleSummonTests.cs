using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleSummonTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP original Choco/Mog resources: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }

        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.NewGame(installation);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        byte[] record = (byte[])session.Progress.Characters[0].NativeRecord.Clone();
        record.AsSpan(0x40, 0x40).Fill(0xFF);
        record[28] = 3;
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(0x40), 0x4A | (2000u << 8));
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(0x44), 0x1A);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(0x60), 0x4A | (35000u << 8));
        Ff7CharacterState cloud = session.Progress.Characters[0] with { NativeRecord = record };
        Ff7BattleSummon summon = Ff7NativeBattleSummons.Read(cloud, kernel).Single();
        Check(summon is { AttackId: 0x38, InitialUses: 7, MpTurboLevel: 1, Attack: { CastingCost: 16, TargetFlags: 0x17, AdditionalEffect: 2, AdditionalEffectModifier: 15, DamageCalculation: 0x22 } }, $"Original Choco/Mog materia did not combine native rank and MP Turbo. Summon={summon}.");
        ReadOnlySpan<byte> originalRandom = kernel[2].Data.Span.Slice(0xE1C, 256);
        foreach ((byte input, byte expected) in new[] { ((byte)14, (byte)0x60), ((byte)15, (byte)0x38) })
        {
            int index = originalRandom.IndexOf(input);
            Check(index >= 0, "The original battle RNG lacks the requested branch sample.");
            byte[] indices = new byte[8];
            indices[0] = (byte)index;
            Ff7BattleRng rng = new(kernel[2].Data.Span, new(indices));
            Check(Ff7NativeBattleSummons.SelectChocoMogAttack(summon, rng) == expected && rng.Capture().Indices[0] == unchecked((byte)(index + 1)) && rng.Capture().WordCounter == 0, "Original Choco/Mog alternation did not consume its exact unsigned battle byte.");
        }

        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        foreach (bool fat in new[] { false, true })
        {
            Ff7BattleSummonModel model = Ff7BattleSummonModel.Load(magic, fat);
            Check(model.Model.Bones.Count == (fat ? 29 : 47) && model.Parts.Count(part => part is not null) > 0, "The original summon skeleton did not bind its native parts.");
            int[] clips = model.Model.BodyClips.Select(clip => BattleClipDecoder.Decode(clip).Count).ToArray();
            Check(clips.SequenceEqual(fat ? new[] { 5, 10 } : new[] { 8, 40 }), "The original summon bank did not expose its authored frame counts.");
        }

        CheckCamera(true, "5BDA84F6BE638F514A0034419F38EE0D0D3112B4116C34E360E37248E29CDA8B");
        CheckCamera(false, "A13BCC62120C9384B473ABE480E4D0ECCFF48B20588850DE2726BC5BD9E05D26");

        CheckCamera(true, "A90F4C6604F000681B8D234C611E557A789059921A75CFBEC369F7523A90B240", fat: true);
        CheckCamera(false, "A90F4C6604F000681B8D234C611E557A789059921A75CFBEC369F7523A90B240", fat: true);

        CheckBattle(14, 0x60);
        CheckBattle(15, 0x38);

        void CheckBattle(byte roll, ushort attack)
        {
            Ff7CharacterState[] characters = session.Progress.Characters.ToArray();
            byte[] native = (byte[])record.Clone();
            native.AsSpan(0x40, 0x40).Fill(0xFF);
            BinaryPrimitives.WriteUInt32LittleEndian(native.AsSpan(0x40), 0x4A);
            characters[0] = cloud with { NativeRecord = native };
            Ff7Progress progress = session.Progress with { Party = [0, 255, 255], Characters = characters, FullCombatEnabled = true };
            using Ff7Session host = Ff7Session.Restore(installation, session.Capture() with { Progress = progress });
            host.Host.StartEncounter(new FieldHostRequest(1L << 40, host.Host.Process.Actors[0].Entity, 7, 0, new FieldBattleCommand(300)), 0);
            Ff7SessionSave entry = host.Capture();
            Ff7BattleSnapshot snapshot = entry.Battle!;
            Ff7BattleActorState caster = snapshot.Actors[0];
            snapshot.Actors[0] = caster with { AwaitingTurn = true, Gauge = ushort.MaxValue, Actor = caster.Actor with { MagicPoints = 28, MaxMagicPoints = 28 } };
            for (int index = 1; index < snapshot.Actors.Length; index++)
            {
                Ff7BattleActorState enemy = snapshot.Actors[index];
                snapshot.Actors[index] = enemy with { Actor = enemy.Actor with { HitPoints = 10, MaxHitPoints = 10 } };
            }
            byte[] indices = new byte[8];
            byte position = (byte)kernel[2].Data.Span.Slice(0xE1C, 256).IndexOf(roll);
            indices[0] = unchecked((byte)(position - 2));
            indices[1] = unchecked((byte)(position - 1));
            snapshot = snapshot with { Intro = null };
            using Ff7Session initialHost = Ff7Session.Restore(installation, entry with { Battle = snapshot });
            Ff7BattleSession battle = initialHost.Battle!;
            Ff7BattleCommandOption command = battle.Commands(0).Single(option => option.Command == Ff7BattleCommand.Summon);
            Check(command is { Argument: 0x38, Cost: 14, RemainingUses: 1, AllTargets: true, ValidTargets: 0x30 }, $"Choco/Mog is missing its original command or group flags. Option={command}.");
            Check(battle.Submit(0, Ff7BattleCommand.Summon, 0x38, 0x10), "Choco/Mog could not enter the ordinary command queue.");
            for (int callback = 0; callback < 100 && battle.Action is null; callback++)
            {
                battle.Advance(waitForCommand: true);
            }
            Check(battle.Action is { Applied: false, Command.CommandId: 3 }, "Choco/Mog did not reach its pre-resolution queue boundary.");
            Ff7SessionSave before = initialHost.Capture();
            Ff7BattleSnapshot pending = before.Battle!;
            using Ff7Session battleHost = Ff7Session.Restore(installation, before with { Battle = pending with { Random = pending.Random with { Stream = 0, Indices = indices } } });
            battle = battleHost.Battle!;
            Ff7Session? cold = null;
            bool sawFade = false;
            bool sawModel = false;
            bool sawContact = false;
            bool sawCold = false;
            try
            {
                for (int callback = 0; callback < 2800 && !battle.ResultReady; callback++)
                {
                    battle.Advance(waitForCommand: true);
                    Ff7BattleSoundReceipt[] audio = battle.DrainSoundRequests();
                    if (cold is not null)
                    {
                        cold.Battle!.Advance(waitForCommand: true);
                        Check(audio.SequenceEqual(cold.Battle.DrainSoundRequests()) && JsonSerializer.Serialize(battle.Capture()) == JsonSerializer.Serialize(cold.Battle.Capture()), "Choco/Mog battle diverged after a cold cinematic restore.");
                    }
                    if (battle.Action is { Command: { CommandId: 3 }, Summon: { } effect } action)
                    {
                        Check(action.Command.AttackId == attack && action.Command.TargetMask == 0x30, $"Choco/Mog selected an incorrect attack or target group. Command={action.Command}.");
                        Check(battle.Actors[0].Actor.MagicPoints == 14 && battle.Actors[0].SummonUses?[0x38] == 0, "Choco/Mog must spend exactly one stock and 14 MP.");
                        sawFade |= effect.Camera is null;
                        sawModel |= effect.Model?.FatChocobo == (attack == 0x60);
                        sawContact |= battle.DisplayedHit?.AttackId == attack;
                        if (cold is null && effect.Model is not null)
                        {
                            cold = Ff7Session.Restore(installation, JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(battleHost.Capture()))!);
                            sawCold = true;
                        }
                    }
                    else if (cold is not null)
                    {
                        cold.Dispose();
                        cold = null;
                    }
                }
            }
            finally
            {
                cold?.Dispose();
            }
            Check(sawFade && sawModel && sawContact && sawCold && battle.ResultReady && battle.Outcome == Ff7BattleOutcome.Victory, $"Original summon {attack:X2} did not reach both fades, cinematic model and group victory. Fade={sawFade}, Model={sawModel}, contact={sawContact}, cold={sawCold}, outcome={battle.Outcome}.");
            Ff7VictoryProjection preview = battleHost.PreviewBattleVictory();
            Check(preview.Progress.Characters[0].MagicPoints == 14 && battleHost.ConfirmBattleVictory() && !battleHost.ConfirmBattleVictory() && battleHost.Battle is null, "Choco/Mog did not settle its MP and victory exactly once.");
            battleHost.Host.ResumeEncounter();
        }

        void CheckCamera(bool first, string expected, bool fat = false)
        {
            IReadOnlyList<short> script = Ff7PcExecutableData.LoadChocoMogCamera(installation, first, fat);
            Ff7BattleChocoMogCameraState camera = new();
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] packet = new byte[15];
            for (int frame = 0; frame < (fat ? 106 : 150); frame++)
            {
                Ff7BattleChocoMogCameraState next = Ff7BattleChocoMogCamera.Advance(camera, script);
                if (frame is 0 or 24 or 43 or 148)
                {
                    Ff7BattleChocoMogCameraState cold = JsonSerializer.Deserialize<Ff7BattleChocoMogCameraState>(JsonSerializer.Serialize(camera))!;
                    Check(Ff7BattleChocoMogCamera.Advance(cold, script) == next, "Original Choco/Mog camera changed its next callback after JSON restoration.");
                }
                camera = next;
                Ff7BattleCameraView view = Ff7BattleChocoMogCamera.View(camera, 3, (actor, bone) => new((short)(actor * 100 + bone), (short)(-bone * 10), (short)(actor * -200)));
                short[] coordinates = [view.Position.X, view.Position.Y, view.Position.Z, view.Focus.X, view.Focus.Y, view.Focus.Z];
                for (int index = 0; index < coordinates.Length; index++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(index * 2), coordinates[index]);
                }
                BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(12), (ushort)camera.ProgramCounter);
                packet[14] = camera.Retired ? (byte)0 : (byte)1;
                hash.AppendData(packet);
            }
            string actual = Convert.ToHexString(hash.GetHashAndReset());
            Check(camera.Retired && actual == expected, $"Original Choco/Mog camera variant {first} differs from the native callbacks. Actual={actual}.");
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
