namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHeroFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleHeroState State)
{
    public NativeBattleHeroProfile Profile => NativeBattleHeroProfile.ForEffect(State.Effect);
}

public abstract record NativeBattleHeroOperation;
public sealed record NativeBattleHeroSound(uint Sound, int Channel, int Pan) : NativeBattleHeroOperation;
public sealed record NativeBattleHeroContact(int Index) : NativeBattleHeroOperation;
public sealed record NativeBattleHeroAct(NativeBattleGfAnimationSource Source, NativeBattleGfAnimationNode Node, NativeBattleHeroDrawContext Context) : NativeBattleHeroOperation;
public sealed record NativeBattleHeroFanBegin(int TemporaryBytes) : NativeBattleHeroOperation;
public sealed record NativeBattleHeroFan(NativeBattleVector Angles, NativeBattleModuleAnchor Anchor, int Radius, int Fade, int Depth, uint MeshAddress, int Index) : NativeBattleHeroOperation;
public sealed record NativeBattleHeroFanEnd(int TemporaryBytes) : NativeBattleHeroOperation;

public sealed record NativeBattleHeroDrawContext(NativeBattleVector Anchor, short? AnchorWord6, int ScaleX, int ScaleY, int ScaleZ, bool? ScaleEnabled, int Depth, int ScratchArenaOffset);

/// <summary>An explicitly verified imported CRT stack profile for the observed main thread.</summary>
public sealed record NativeBattleHeroCrtStackContract(string Profile, string ExecutionCaptureOwner, int ThreadId)
{
    public const string Win32X86Profile = "win32-x86-10.0.26100.9444-main-crt";

    public void Validate(NativeBattleExecutionStack execution)
    {
        execution.Validate();
        if (Profile != Win32X86Profile || ExecutionCaptureOwner != execution.CaptureSha256 || ThreadId != execution.CapturedThreadId)
        {
            throw new NotSupportedException("The Hero CRT stack requires its explicitly verified imported-code profile and actual main-thread capture owner.");
        }
    }
}

public sealed record NativeBattleHeroStackInput(NativeBattleExecutionStack? Execution, long Callback, NativeBattleStackBytes? CurrentWord, NativeBattleHeroCrtStackContract? Crt)
{
    public Func<bool, NativeBattleStackBytes?>? ResolveCurrentWord { get; init; }

    public bool RequireScale(bool initializedChild)
    {
        NativeBattleExecutionStack execution = Execution ?? throw new NotSupportedException("The Hero visible draw requires its actual native execution-stack owner.");
        execution.Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(Callback);
        if (initializedChild && Crt is not null)
        {
            Crt.Validate(execution);
            //The ACT length argument and the imported CRT's saved frame pointer are both nonnull.
            return true;
        }
        NativeBattleStackBytes? selected = ResolveCurrentWord is { } resolve ? resolve(initializedChild) : CurrentWord;
        NativeBattleStackBytes word = selected ?? throw new NotSupportedException("The Hero draw requires its current physical C-88 operand at the reached creation phase.");
        if (word.FrameOwner != $"common47CF60:{Callback}")
        {
            throw new InvalidDataException("The Hero C-88 operand belongs to another host frame.");
        }
        return word.IsNonzero(-0x88);
    }
}

public sealed record NativeBattleHeroInputs(NativeBattleActionPacketsState Packets, Func<byte, NativeBattleGraphicsModel> ReadModel, Func<byte, NativeBattleModuleAnchor> ReadAnchor571400, NativeBattleHeroStackInput Stack, Func<NativeBattleHeroFrame, NativeBattleHeroOperation, NativeBattleHeroFrame> Execute)
{
    public Func<NativeBattleHeroFrame, byte, NativeBattleHeroFrame>? ObserveAnchor571400 { get; init; }
}
