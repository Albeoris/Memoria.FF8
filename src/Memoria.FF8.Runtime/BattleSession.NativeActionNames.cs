namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private string? ResolveNativeActionHeaderName(NativeBattleActionDataReference data, BattleNativeAction native)
    {
        //The logical pointer retains its original producer family: a GF attack name and its saved GF name have different owners.
        return data.ProducerAddress switch
        {
            0x47EBD0 => data.Argument < definition.NativeCommandNames.Length ? definition.NativeCommandNames[data.Argument] : null,
            0x47EA30 => data.Argument < definition.NativeItemNames.Length ? definition.NativeItemNames[data.Argument] : native.KernelSection == 7 && native.KernelIndex == data.Argument ? native.Name : null,
            0x47E970 when data.Argument >= 64 => definition.ActionDefinitions.SingleOrDefault(action => action.Ability == BattleAbility.GuardianForce && action.Id == data.Argument - 64)?.DisplayName,
            0x47E970 => native.KernelSection == 1 && native.KernelIndex == data.Argument ? native.Name : null,
            0x495070 => native.KernelSection == 2 && native.KernelIndex == data.Argument - 64 ? native.Name : null,
            0x4950A0 => native.KernelSection == 3 && native.KernelIndex == data.Argument ? native.Name : null,
            0x47E5F0 => native.KernelSection == 5 && native.KernelIndex == data.Argument ? native.Name : null,
            0x495050 => native.KernelSection == 9 && native.KernelIndex == data.Argument ? native.Name : null,
            0x47E6B0 => native.KernelSection == 18 && native.KernelIndex == data.Argument ? native.Name : null,
            0x47E650 => native.KernelSection == 19 && native.KernelIndex == data.Argument ? native.Name : null,
            0x47E590 => native.KernelSection == 21 && native.KernelIndex == data.Argument ? native.Name : null,
            0x47E530 => native.KernelSection == 22 && native.KernelIndex == data.Argument ? native.Name : null,
            0x47E4C0 => native.KernelSection == 25 && native.KernelIndex == data.Argument ? native.Name : null,
            _ => null
        };
    }
}
