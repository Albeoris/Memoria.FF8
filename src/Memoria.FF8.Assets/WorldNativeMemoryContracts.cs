using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

internal static class WorldNativeMemoryContracts
{
    public static void RequireSupported(IGameNativeMemorySource memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        if (memory.SourceHash is not (NativeFieldRuntimeProfile.FF8EnExecutableHash or NativeFieldRuntimeProfile.RemasteredEnImageHash))
        {
            throw new NotSupportedException("Native world tables require an authenticated supported English source image.");
        }
    }
}
