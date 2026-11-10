using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public static class Ff7BattleCameraResources
{
    public static Ff7BattleCameraArchive Load(Ff7Installation installation, byte formationLayout)
    {
        ArgumentNullException.ThrowIfNull(installation);
        Ff7ResourceRole role = formationLayout switch
        {
            0 or 1 => Ff7ResourceRole.BattleCamera0,
            2 => Ff7ResourceRole.BattleCamera1,
            3 or 4 => Ff7ResourceRole.BattleCamera2,
            _ => throw new NotSupportedException($"FFVII battle camera layout {formationLayout} is unsupported.")
        };
        return Ff7BattleCameraArchive.Read(File.ReadAllBytes(installation.GetRequired(role).Path));
    }
}
