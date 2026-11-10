using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed record Ff7BattleCameraScriptResources(Ff7BattleCameraArchive Archive, IReadOnlyList<Ff7BattleCameraEntryScript> EntryScripts, Ff7BattleCameraMath Math)
{
    private static readonly byte[] Idle = [0xFF];

    public ReadOnlyMemory<byte> Script(short camera, byte initialCamera, int track, Ff7BattleCameraComponent component)
    {
        if ((uint)track >= 3 || (uint)component > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(track));
        }
        if (camera == -1)
        {
            return Idle;
        }
        if (camera == -2)
        {
            if (initialCamera >= EntryScripts.Count)
            {
                throw new InvalidDataException("FFVII battle initial camera is absent from the original executable tables.");
            }
            return component == Ff7BattleCameraComponent.Position ? EntryScripts[initialCamera].Position : EntryScripts[initialCamera].Focus;
        }
        return camera == -3 ? Archive.SpecialScript(track, component) : Archive.Script(camera, track, component);
    }
}

public static class Ff7BattleCameraResources
{
    public static Ff7BattleCameraScriptResources LoadScripts(Ff7Installation installation, byte formationLayout) => new(Load(installation, formationLayout), Ff7PcExecutableData.LoadBattleCameraEntryScripts(installation), Ff7PcExecutableData.LoadBattleCameraMath(installation));

    public static Ff7BattleCameraArchive Load(Ff7Installation installation, byte formationLayout)
    {
        ArgumentNullException.ThrowIfNull(installation);
        Ff7ResourceRole role = formationLayout switch
        {
            0 or 1 or 8 => Ff7ResourceRole.BattleCamera0,
            2 => Ff7ResourceRole.BattleCamera1,
            3 or 4 or 5 or 6 or 7 => Ff7ResourceRole.BattleCamera2,
            _ => throw new NotSupportedException($"FFVII battle camera layout {formationLayout} is unsupported.")
        };
        return Ff7BattleCameraArchive.Read(File.ReadAllBytes(installation.GetRequired(role).Path));
    }
}
