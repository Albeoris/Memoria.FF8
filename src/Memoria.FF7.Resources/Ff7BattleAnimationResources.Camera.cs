using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed partial class Ff7BattleAnimationResources
{
    private readonly Dictionary<string, byte> deathTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Ff7BattleCameraModelParameters> cameraModelParameters = new(StringComparer.OrdinalIgnoreCase);

    public byte DeathType(string prefix)
    {
        if (!deathTypes.TryGetValue(prefix, out byte kind))
        {
            kind = ReadDeathType(archive, prefix);
            deathTypes.Add(prefix, kind);
        }
        return kind;
    }

    public static bool HasBossDeath(SceneFormation formation, LgpArchive archive)
    {
        return formation.Enemies.Any(enemy => enemy.EnemyId >= 0 && Ff7BattleActorDeath.IsBossKind(ReadDeathType(archive, ModelPrefix(enemy.EnemyId, true))));
    }

    private static byte ReadDeathType(LgpArchive archive, string prefix)
    {
        byte[] action = archive.ReadEntry(archive.Find(prefix + "ab").Single());
        return (byte)(action[2] & 0x3F);
    }

    public Ff7BattleCameraModelParameters CameraModelParameters(string prefix)
    {
        if (!cameraModelParameters.TryGetValue(prefix, out Ff7BattleCameraModelParameters parameters))
        {
            parameters = Ff7BattleCameraModelParameters.Read(Read(prefix + "ab"));
            cameraModelParameters.Add(prefix, parameters);
        }
        return parameters;
    }
}
