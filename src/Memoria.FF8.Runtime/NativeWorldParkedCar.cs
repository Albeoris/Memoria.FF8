using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The actual vehicle allocation retained by the ordinary54A7F0/54B460 car-to-foot switch.</summary>
public sealed record NativeWorldParkedCar([property: JsonRequired] NativeCarWorldRuntimeState Car, [property: JsonRequired] NativeWorldPathPosition Position, [property: JsonRequired] short BodyYaw, [property: JsonRequired] long DismountFrame)
{
    public void Validate()
    {
        Car?.Validate();
        if (Car is null || DismountFrame < 0 || BodyYaw is < 0 or >= 4096)
        {
            throw new InvalidDataException("Parked car requires its executed vehicle and dismount pose.");
        }
    }
}
