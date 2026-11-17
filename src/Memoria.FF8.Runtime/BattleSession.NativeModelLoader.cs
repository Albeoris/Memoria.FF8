namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public NativeBattleModelLoader? NativeModelLoader => Presentation?.NativeModelLoader;
    public NativeBattleModelCpuResources? NativeModelCpuResources => Presentation?.NativeModelCpuResources;
    public NativeBattleFileStaging? NativeFileStaging => Presentation?.NativeFileStaging;
}
