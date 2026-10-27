using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect338Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect338Controller
{
    public static NativeBattleEffect338Frame Initialize(NativeBattleEffect338Frame frame, NativeBattleEffect338Resources resources, NativeBattleEffect338Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Effect338 requires its exclusive inactive root bank.");
        }
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        int header = Parameters(frame).HeaderIndex7C;
        int first = packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect338 requires its actual first action contact.");
        uint target = ModelAddress(packets.ReadContact(first).Primary.ActorIndex);
        frame = frame with { Random = frame.Random.Draw(out ushort seed), State = frame.State.InitializeSource(resources) };
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Effect338 requires its current texture process.");
        foreach (NativeBattleEffectHeapAllocation texture in resources.Textures)
        {
            (textures, _) = textures.EnqueueTim505E30(texture);
        }
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        frame = frame with { Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) }, State = frame.State.WriteDword(0x25561C8, seed).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)).WriteDword(TargetGlobal, target).WriteDword(ArenaGlobal, ArenaAddress) with { RootBankStorage = null }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
        return ClearDwords701200(frame, RootBank + 12, 2);
    }

    public static NativeBattleEffect338Frame Advance(NativeBattleEffect338Frame frame, NativeBattleEffect338Resources resources, NativeBattleEffect338Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 4) || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Effect338 requires its exclusive active task banks.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Effect338 task pool leaves its physical descriptor contract.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleEffect338Frame Frame, int Survivors) AdvancePool(NativeBattleEffect338Frame frame, int index, NativeBattleEffect338Resources resources, NativeBattleEffect338Inputs inputs)
    {
        NativeBattleEffect338Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            (current, int result) = AdvanceTask(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static (NativeBattleEffect338Frame Frame, int Result) AdvanceTask(NativeBattleEffect338Frame frame, uint address, NativeBattleEffect338Resources resources, NativeBattleEffect338Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot(frame, address, resources, inputs),
        MainCallback => AdvanceMain(frame, address, resources, inputs),
        0x6ECC50 => AdvanceProgramme(frame, address, resources, inputs),
        0x6ED0C0 => AdvanceFragment(frame, address, resources, inputs),
        _ => throw new NotSupportedException("Effect338 reached an unsupported native task callback.")
    };

    public static NativeBattleEffect338Frame ClearDwords701200(NativeBattleEffect338Frame frame, uint address, int count) => WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, new byte[checked(count * 4)]), checked(count * 4));

    private static NativeBattleEffect338Frame InitializeSecondaryBanks(NativeBattleEffect338Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Effect338 secondary reset requires its inactive COMMON banks.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect338 reset requires its current COMMON owner.");
        ImmutableArray<NativeBattleEffectTaskPool>.Builder pools = frame.Effect.Pools.ToBuilder();
        foreach (NativeBattleCureFamilyPool profile in Pools.Skip(1))
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
            for (int slot = 0; slot < pool.Slots.Length; slot++)
            {
                CopyTask(pool.Slots[slot], 12, NativeBattleEffectNodeContextMemory.Copy(arena, checked((int)(profile.Bank - ArenaAddress)) + slot * profile.Stride + 12, NativeBattleEffectArena.Clear().MarkUnknown(0, profile.Stride - 12), 0, profile.Stride - 12), profile.Stride - 12);
            }
            pools.Add(pool);
        }
        return frame with { Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, pools.ToImmutable().RemoveAt(0)), Effect = frame.Effect with { Pools = pools.ToImmutable() } };
    }

    private static (NativeBattleEffect338Frame Frame, int Result) AdvanceRoot(NativeBattleEffect338Frame frame, uint address, NativeBattleEffect338Resources resources, NativeBattleEffect338Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        uint arena = frame.State.ReadDword(ArenaGlobal);
        bool alternate = root.ReadDword(16) != 0;
        frame = Dword(frame, CursorGlobal, unchecked(arena + (uint)(alternate ? 0x5CD8 : 0x15CD8)));
        frame = Dword(frame, EndGlobal, unchecked(arena + (uint)(alternate ? 0x15CD8 : 0x25CD8)));
        frame = frame with { Effect = frame.Effect with { DrawArenaOffset = NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(CursorGlobal), 0) } };
        frame = Dword(frame, 0x255623C, 0);
        frame = Write(frame, address, root.WriteDword(16, alternate ? 0u : 1u));
        if (Signed(root, 12) == 2 && root.ReadByte(15) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(15, 1));
            frame = InitializeParticleBank(frame);
            frame = InitializeSecondaryBanks(frame);
            (frame, uint main) = Append(frame, Pools[1].Descriptor, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect338 mandatory main allocation failed after pool reset.");
            }
            frame = ClearDwords701200(frame, main + 12, 0x31);
            uint target = frame.State.ReadDword(TargetGlobal);
            frame = Write(frame, main, Read(frame, main).WriteDword(24, frame.State.ReadDword(CasterGlobal)));
            frame = inputs.Execute(frame, new NativeBattleEffect338CaptureBounds6DFD90(target, main + 20));
            NativeBattleEffect338VibrationStep vibration = inputs.StartVibration(frame);
            frame = vibration.Frame;
            NativeBattleModulePoolTask task = NativeBattleEffect338Model.Initialize6EC060(Read(frame, main).WriteDword(16, unchecked((uint)vibration.Handle)), main, resources.Model);
            NativeBattleEffect338PoseStep selected = NativeBattleEffect338Model.Select6574D0(frame.State.Skeleton!, task, resources.Model, 0);
            frame = frame with { State = frame.State with { Skeleton = selected.Skeleton } };
            task = CopyTask(selected.Task, 0x64, PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattlePandemona291Restoration.Rotation701270(0), 0)), 32);
            NativeBattleModelTransform transform = inputs.ReadModel(frame, target).Transform ?? throw new NotSupportedException("Effect338 requires its actual recipient placement and height fields.");
            short z = unchecked((short)(transform.Position28.Z + transform.Scale38 / 4));
            frame = Word(Word(Word(frame, 0x2556240, transform.Position28.X), 0x2556242, transform.Position28.Y), 0x2556244, unchecked((short)(z + 1024)));
            frame = Word(Word(Word(frame, 0x2556248, transform.Position28.X), 0x255624A, unchecked((short)(transform.Position28.Y + Signed(task, 22)))), 0x255624C, z);
            task = task.WriteDword(0x78, unchecked((uint)(int)transform.Position28.X)).WriteDword(0x7C, 0).WriteDword(0x80, unchecked((uint)(int)z)).WriteWord(0x40, unchecked((ushort)transform.Position28.X)).WriteWord(0x42, 0).WriteWord(0x44, unchecked((ushort)z));
            frame = Write(frame, main, task);
            frame = inputs.Execute(frame, new NativeBattleEffect338InitializeIo534110(IoOffset));
        }
        int survivors = 1;
        if (Read(frame, address).ReadByte(15) != 0)
        {
            (frame, survivors) = AdvancePool(frame, 1, resources, inputs);
            (frame, _) = AdvancePool(frame, 2, resources, inputs);
            (frame, _) = AdvancePool(frame, 3, resources, inputs);
        }
        if (Read(frame, address).ReadByte(15) != 0 && survivors == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect338AdvanceIo534210());
            NativeBattleEffect338IoPoll poll = inputs.PollIo5342C0(frame, 0);
            return (poll.Frame, poll.Busy ? 0 : 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)))), 0);
    }
}
