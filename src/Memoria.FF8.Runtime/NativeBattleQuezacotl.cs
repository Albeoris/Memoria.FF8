using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectFileAsset(short FileId, string Name, ImmutableArray<byte> Data);
public sealed partial record NativeBattleQuezacotlResources(NativeBattleEffectInitializerAsset Initializer, NativeBattleEffectHeapAllocation WorkSeed0, NativeBattleEffectHeapAllocation WorkSeed1, NativeVibrationBank Vibration)
{
    public ImmutableArray<NativeBattleEffectFileAsset> Files { get; init; } = [];
    public uint? InitialSound { get; init; }
    public uint? Frame65Sound { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public uint? Frame209Sound { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public uint? Frame277Sound { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public uint? Frame229FlashColor { get; init; }
    public ImmutableArray<uint>? FlashColors { get; init; }
}
public sealed record NativeBattleQuezacotlState(ushort Random17A0, int TargetZ1770, int TargetZ1778)
{
    public NativeBattleActionMatrix? Billboard17B0 { get; init; }
    public int Intensity17A4 { get; init; }
}
public sealed record NativeBattleQuezacotlEntry(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public sealed record NativeBattleQuezacotlPreparation(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeVibrationProcessState? Vibration, bool ChildBoundary);

public static class NativeBattleQuezacotl
{
    public static (NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect) Initialize6C3560(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        NativeBattleQuezacotlResources resources = RequireResources(effect);
        //50AF20 first selects534150, then571870 clears only the separate1MB effect arena.
        memory = memory.BeginLoad(effect.SelectedIndex);
        NativeBattleEffectIoState io = memory.EffectIo ?? throw new NotSupportedException("Native6C3560 requires its retained original effect IO callback owner.");
        int start = memory.Cursor;
        memory = memory.LoadIntoArena(resources.Initializer.Data.AsSpan(), new(resources.Initializer.Name, NativeBattleTextureDomain.Magic));
        memory = memory with { EffectIo = io with { Callback = 0x6C3580 } };
        return (memory, effect with { ResourcePointers = [new(0x25216D8, start)] });
    }

    public static NativeBattleQuezacotlEntry Enter6C3640(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeCrtRandomState random, NativeBattleActionPacketsState packets, Func<int, short> readModelZ20)
    {
        NativeBattleQuezacotlResources resources = RequireResources(effect);
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native6C3640 requires its original50AFC0 action parameters.");
        packets.Validate();
        NativeBattleGuardianWorkBuffer work = memory.GuardianWorkBuffer ?? throw new NotSupportedException("Native6C3640 requires the separately retained571B70 work buffer;571870 does not establish its bytes.");
        NativeBattleQuezacotlExecutableMemory executable = memory.QuezacotlExecutableMemory ?? throw new NotSupportedException("Native6C3640 requires the current retained executable model bytes; immutable catalog seeds cannot establish their runtime state.");
        executable.Validate();
        NativeBattleEffectTextureState textures = memory.Textures ?? throw new NotSupportedException("Native6C3640 requires its actual505E30 texture queue.");
        NativeBattleEffectResourcePointer pointer = effect.ResourcePointers.Single(pointer => pointer.GlobalAddress == 0x25216D8);
        if (pointer.ArenaOffset != 0)
        {
            throw new InvalidDataException("Native6C3560 mag115 source must begin at the just-cleared571B80 arena cursor.");
        }
        if (resources.WorkSeed0.Data.Length != 0x109FC || resources.WorkSeed1.Data.Length != 0x4D9C)
        {
            throw new InvalidDataException("Native6C3640 requires its exact427F/1367 DWORD executable seed copies.");
        }
        random = random.Draw(out ushort seed);
        NativeBattleEffectHeapAllocation tim = new(resources.Initializer.Name, resources.Initializer.Data);
        (textures, _) = textures.EnqueueTim505E30(tim);
        work = work.Write(0, executable.Data.AsSpan());
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x2521748, 0x2521758, 20, 1);
        (main, _) = main.Append(0x6C3760);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int count = header.ReadByte(16);
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native6C3640 requires the actual physical target contact pointer.");
        if (count == 0)
        {
            throw new InvalidDataException("Native6C3640 divides the target-position sum by its original nonzero target count.");
        }
        int sum = 0;
        for (int index = 0; index < count; index++)
        {
            sum = unchecked(sum + readModelZ20(packets.ReadContact(first + index).Primary.ActorIndex));
        }
        int average = sum / count;
        NativeBattleQuezacotlState state = new(seed, average, unchecked(average + 3690));
        memory = memory with { GuardianWorkBuffer = work, Textures = textures, QuezacotlCompletion17A8 = 0 };
        return new(memory, effect with { Pools = [main], Quezacotl = state, ReturnedDescriptor = main.DescriptorAddress, DrawArenaOffset = null }, random);
    }

    /// <summary>Complete the original early callbacks; prepare the exact private child prefix before stopping at unported6C3940.</summary>
    public static NativeBattleQuezacotlPreparation Prepare6C3760(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeVibrationProcessState? vibration, NativeBattleActionMatrix? view)
    {
        NativeBattleQuezacotlResources resources = RequireResources(effect);
        int pause = memory.QuezacotlPause ?? throw new NotSupportedException("Native6C3760 requires its retained DWORD25216DC pause owner.");
        NativeBattleQuezacotlState state = (effect.Quezacotl ?? throw new InvalidDataException("Native6C3760 requires its executed6C3640 entry globals.")) with { Intensity17A4 = 0 };
        NativeBattleEffectTaskPool main = effect.Pools.FirstOrDefault(pool => pool.DescriptorAddress == 0x2521748) ?? throw new InvalidDataException("Native6C3760 requires its original one-slot main descriptor.");
        main.Validate();
        if (main.BankAddress != 0x2521758 || main.Stride != 20 || main.Slots.Length != 1 || main.Head != 0 || main.Slots[0].Callback != 0x6C3760)
        {
            throw new InvalidDataException("Native6C3760 lost its original1x20 task bank.");
        }
        NativeBattleModulePoolTask task = main.Slots[0];
        uint buffer = task.ReadDword(16);
        int draw = buffer != 0 ? 0x2E50 : 0x15E50;
        task.WriteDword(16, buffer != 0 ? 0u : 1u);
        short phase = unchecked((short)task.ReadWord(12));
        ImmutableArray<NativeBattleEffectTaskPool> pools = effect.Pools;
        if (phase == 2 && pause == 0 && task.ReadByte(15) == 0)
        {
            NativeVibrationEnqueue queued = (vibration ?? throw new NotSupportedException("Native6C3760 requires the actual retained4A29A0 vibration queue.")).Enqueue(resources.Vibration, 1, 128);
            vibration = queued.State;
            NativeBattleEffectTaskPool child = NativeBattleEffectTaskPool.Initialize(0x2521738, 0x20DFAB8, 0x8D0, 1);
            (child, int index) = child.Append(0x6C3940);
            child.Slots[index].WriteDword(16, unchecked((uint)queued.Handle));
            byte[] physical = new byte[0x8D0];
            BinaryPrimitives.WriteUInt16LittleEndian(physical, 1);
            BinaryPrimitives.WriteUInt32LittleEndian(physical.AsSpan(8), 0x6C3940);
            child.Slots[index].ReadRawPayloadSpan().CopyTo(physical.AsSpan(12));
            NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6C3760 requires the actual cleared571B50 arena for its8D0 task.");
            memory = memory with { Arena = arena.Write(0, physical).Write(0x28E50, [1, 0, 0, 0]) };
            memory = NativeBattleEffectIo.Initialize534110(memory, 0x28E54);
            pools = pools.Where(pool => pool.DescriptorAddress != child.DescriptorAddress).Append(child).ToImmutableArray();
            task.WriteByte(15, 1);
        }
        bool childBoundary = task.ReadByte(15) != 0;
        if (childBoundary)
        {
            state = state with { Billboard17B0 = NativeBattleGfMatrices.Normalize67AAE0(view ?? throw new NotSupportedException("Native6C3760 requires its prior native camera matrix before private child submission.")) };
        }
        else if (pause == 0)
        {
            task.WriteWord(12, unchecked((ushort)(phase + 1)));
        }
        main = main.WriteSlot(0, task);
        pools = pools.Select(pool => pool.DescriptorAddress == main.DescriptorAddress ? main : pool).ToImmutableArray();
        return new(memory, effect with { Pools = pools, Quezacotl = state, DrawArenaOffset = draw }, vibration, childBoundary);
    }

    private static NativeBattleQuezacotlResources RequireResources(NativeBattleOriginalEffectState effect)
    {
        if (effect.Binding.Effect != 116 || effect.Binding.Entry != 0x6C3550 || effect.Binding.Initializer != 0x6C3560)
        {
            throw new InvalidDataException("Native Quezacotl requires its original effect116 entry6C3550/initializer6C3560 binding.");
        }
        NativeBattleQuezacotlResources resources = effect.Binding.NativeQuezacotlResources ?? throw new NotSupportedException("Native Quezacotl requires its selectively materialized original mag115 and executable resources.");
        if (resources.Initializer.Name != "mag115.tim" || resources.Initializer.Data.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native6C3560 requires its actual mag115.tim allocation.");
        }
        return resources;
    }
}
