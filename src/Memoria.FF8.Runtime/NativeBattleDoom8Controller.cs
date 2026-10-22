namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoom8Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleDoom8State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}
public sealed record NativeBattleDoom8Step(NativeBattleDoom8Frame Frame, int Result);
public abstract record NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8UploadTim(uint Address) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8Sound(NativeBattleModuleSound Sound) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8ModeChanged(uint DirectorAddress, short Mode) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8UpdateCaster(uint Address) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8UpdateTargetBounds(uint Address) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8InitializeTarget(uint Address) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8TextureLoad(uint RootAddress, uint SourceAddress) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8Contact(int Index) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8QueueFile(ushort Opcode, ushort FileId, ushort Index, uint DestinationAddress, int DeclaredLength, uint CompletionCallback) : NativeBattleDoom8Operation;
public sealed record NativeBattleDoom8Inputs(NativeBattleActionPacketsState Packets, Func<NativeBattleDoom8Frame, NativeBattlePackedMatrix32> ReadCamera, Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> Execute, Func<NativeBattleDoom8Frame, uint, NativeBattleDoom8Step> AdvanceLeaf, uint DirectorSound);
public sealed record NativeBattleDoom8DirectorInputs(uint SoundValue, Func<NativeBattleDoom8Frame, NativeBattleDoom8Operation, NativeBattleDoom8Frame> Execute);

public static partial class NativeBattleDoom8Controller
{
    public const uint Entry = 0x8C4980;
    public const uint DirectorCallback = 0x8C5350;

    public static NativeBattleModulePoolTask Read(NativeBattleDoom8Frame frame, uint address)
    {
        return new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    }

    public static NativeBattleDoom8Frame Write(NativeBattleDoom8Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(address, task with { Next = hierarchy.Read(address).Next });
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
    }

    internal static NativeBattleDoom8Step Finish(NativeBattleDoom8Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
        task = Read(frame, address);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            NativeBattleEffectHierarchy hierarchy = new NativeBattleEffectHierarchy(frame.Effect.Pools).Complete8DC530(address);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        }
        return new(frame, complete ? 2 : 0);
    }

    internal static void RequireDirector(NativeBattleDoom8Frame frame, uint address)
    {
        frame.State.Validate();
        if (frame.Effect.Binding.Effect != 8 || frame.Effect.Binding.Entry != Entry || Read(frame, address).Callback != DirectorCallback)
        {
            throw new InvalidDataException("Doom director requires its actual module and callback.");
        }
        NativeBattleCureFamilyPool expected = NativeBattleDoom8State.Pools[2];
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == expected.Descriptor);
        if (pool.BankAddress != expected.Bank || pool.Stride != expected.Stride || pool.Slots.Length != expected.Capacity || address < pool.BankAddress || address >= pool.BankAddress + pool.Stride * pool.Slots.Length)
        {
            throw new InvalidDataException("Doom director requires its physical task bank.");
        }
    }
}
