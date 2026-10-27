using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeFieldEyeProcess([property: JsonRequired] int Version, [property: JsonRequired] NativeBattleRetainedMemory Memory, [property: JsonRequired] NativeCrtRandomState? CrtRandom)
{
    public void Validate(NativeFieldRegistrationState registration)
    {
        if (Version != 1)
        {
            throw new InvalidDataException("Native eye process has an unsupported state version.");
        }
        Memory.Validate();
        registration.Validate(Memory);
        NativeBattleFileStaging storage = Memory.FileStaging!;
        foreach (NativeFieldRegisteredInstance instance in registration.Instances.Where(instance => instance.ModelId >= 0))
        {
            byte mode = storage.Read(instance.Address + 114, 1)[0];
            if ((mode & 0xF0) is not (0 or 0x10 or 0x20 or 0x40 or 0x80))
            {
                throw new InvalidDataException("Native eye backend has an invalid high-nibble mode.");
            }
        }
    }
}
