namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    public uint? ReturnedTaskPointer1D96AAC { get; init; }
    public NativeBattleOriginalEffectState? DetachedReturnedTask { get; init; }

    public NativeBattleEffectMemory InitializeReturnedTask500870() => this with { ReturnedTaskPointer1D96AAC = 0, DetachedReturnedTask = null };

    public int ReadReturnedTask5044B0() => (ReturnedTaskPointer1D96AAC ?? throw new NotSupportedException("Native read40 requires its retained1D96AAC pointer; an unentered selected module cannot establish NULL.")) == 0 ? 0 : 1;

    public NativeBattleEffectMemory RetainReturnedTaskOnModuleLoad(NativeBattleOriginalEffectState? selected)
    {
        if (ReturnedTaskPointer1D96AAC is not > 0 || DetachedReturnedTask is not null)
        {
            return this;
        }
        if (selected?.ReturnedDescriptor != ReturnedTaskPointer1D96AAC)
        {
            throw new InvalidDataException("Native retained task pointer lost its actual attached pool owner before module selection.");
        }
        return this with { DetachedReturnedTask = selected };
    }

    public NativeBattleEffectMemory AssignReturnedTask(NativeBattleOriginalEffectState entered)
    {
        uint descriptor = entered.ReturnedDescriptor ?? throw new InvalidDataException("Native assigned entry return has no actual task descriptor result.");
        return this with { ReturnedTaskPointer1D96AAC = descriptor, DetachedReturnedTask = null };
    }

    public void ValidateReturnedTaskOwner(NativeBattleOriginalEffectState? selected)
    {
        if (ReturnedTaskPointer1D96AAC is > 0 && (DetachedReturnedTask ?? selected)?.ReturnedDescriptor != ReturnedTaskPointer1D96AAC)
        {
            throw new InvalidDataException("Native canonical returned task has no matching physical pool owner.");
        }
    }
}
