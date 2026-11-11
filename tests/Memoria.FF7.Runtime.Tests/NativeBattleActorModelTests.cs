using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

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
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
