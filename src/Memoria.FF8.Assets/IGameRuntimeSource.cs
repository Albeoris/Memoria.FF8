using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Edition-owned authentication and runtime contracts for original resources.</summary>
public interface IGameRuntimeSource
{
    string? NativeExecutableHash { get; }
    NativeFieldRuntimeProfile? NativeRuntimeProfile { get; }
    NativeFieldRuntimeProfile RequireNativeRuntimeProfile();
}
