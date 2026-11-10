using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources;

public sealed partial class Ff7BattleAnimationResources
{
    private readonly Dictionary<string, Ff7BattleCameraModelParameters> cameraModelParameters = new(StringComparer.OrdinalIgnoreCase);

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
