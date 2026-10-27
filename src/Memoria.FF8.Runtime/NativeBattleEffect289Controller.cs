using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect289Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect289Controller
{
    public static NativeBattleEffect289Frame Initialize(NativeBattleEffect289Frame frame, NativeBattleEffect289Resources resources, NativeBattleEffect289Inputs inputs)
    {
        RequireBinding(frame);
        resources.Validate();
        frame.State.Validate();
        if (!frame.Effect.Pools.IsEmpty || frame.State.RootBankStorage is null)
        {
            throw new InvalidDataException("Effect289 requires its exclusive inactive root bank.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect289 requires its actual COMMON arena.");
        int tim = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect289 requires its executed texture initializer.");
        NativeBattleEffectTextureState textures = frame.Memory.Textures ?? throw new NotSupportedException("Effect289 requires its current texture process.");
        (textures, _) = textures.EnqueueArenaTim505E30(arena, tim);
        frame = frame with { Memory = frame.Memory with { Textures = textures }, State = frame.State.WriteDword(InitializerGlobal, checked(ArenaAddress + (uint)tim)).WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, ModelAddress(Parameters(frame).Actor78)).WriteDword(ArenaGlobal, ArenaAddress) };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, _) = root.Append(RootCallback);
        frame = frame with { State = frame.State with { RootBankStorage = null }, Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor, DrawArenaOffset = null } };
        frame = ClearDwords701200(frame, RootBank + 12, 2);
        return frame with { Memory = frame.Memory with { Textures = textures.Register5099A0(resources.Camera) } };
    }

    public static NativeBattleEffect289Frame Advance(NativeBattleEffect289Frame frame, NativeBattleEffect289Resources resources, NativeBattleEffect289Inputs inputs)
    {
        RequireBinding(frame);
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length is not (1 or 4) || frame.State.RootBankStorage is not null)
        {
            throw new InvalidDataException("Effect289 requires its exclusive active task banks.");
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Effect289 task banks leave their physical descriptor contract.");
            }
        }
        (frame, int survivors) = AdvancePool(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleEffect289Frame Frame, int Survivors) AdvancePool(NativeBattleEffect289Frame frame, int index, NativeBattleEffect289Resources resources, NativeBattleEffect289Inputs inputs)
    {
        NativeBattleEffect289Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            (current, int result) = AdvanceTask(current, pool.BankAddress + (uint)(slot * pool.Stride), resources, inputs);
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static (NativeBattleEffect289Frame Frame, int Result) AdvanceTask(NativeBattleEffect289Frame frame, uint address, NativeBattleEffect289Resources resources, NativeBattleEffect289Inputs inputs) => Read(frame, address).Callback switch
    {
        RootCallback => AdvanceRoot(frame, address, resources, inputs),
        MainCallback => AdvanceMain(frame, address, resources, inputs),
        0x6783F0 => AdvanceParticle6783F0(frame, address, inputs),
        0x678490 => AdvanceChildProgramme678490(frame, address, resources, inputs),
        _ => throw new NotSupportedException("Effect289 reached an unsupported native task callback.")
    };

    private static (NativeBattleEffect289Frame Frame, int Result) AdvanceRoot(NativeBattleEffect289Frame frame, uint address, NativeBattleEffect289Resources resources, NativeBattleEffect289Inputs inputs)
    {
        NativeBattleModulePoolTask root = Read(frame, address);
        int age = Signed(root, 0xC);
        bool parity = root.ReadDword(0x10) != 0;
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(frame.State.ReadDword(ArenaGlobal) + (uint)(parity ? FirstPacketOffset : SecondPacketOffset))) };
        frame = Write(frame, address, Read(frame, address).WriteDword(0x10, parity ? 0u : 1u));
        if (age == 1 && root.ReadByte(0xF) == 0)
        {
            frame = Write(frame, address, Read(frame, address).WriteByte(0xF, 1));
            frame = InitializeSecondaryBanks(frame);
            (frame, uint main) = MandatoryAppend(frame, MainDescriptor, MainCallback);
            frame = ClearDwords701200(frame, main + 0xC, 0x65);
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            int header = Parameters(frame).HeaderIndex7C;
            int first = packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect289 requires its current first contact pointer.");
            uint target = ModelAddress(packets.ReadContact(first).Primary.ActorIndex);
            frame = Write(frame, main, Read(frame, main).WriteDword(0x10, target));
            NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, main + MainProgrammeWorkspaceOffset, MainProgrammeWorkspaceLength).Initialize7016B0(resources.MainProgramme, MainProgrammeWorkspaceLength);
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, main + MainProgrammeWorkspaceOffset, workspace);
            NativeBattleEffect289PointStep anchor = inputs.ReadAnchor571400(frame, target);
            frame = Write(anchor.Frame, main, CopyTask(Read(anchor.Frame, main), 0x14, anchor.Point8, 8));
            frame = InitializeMeshW677A10(frame, 0x106EDE4, resources);
            frame = InitializeMeshW677A10(frame, 0x106FAF4, resources);
        }
        if (Read(frame, address).ReadByte(0xF) != 0)
        {
            (frame, int main) = AdvancePool(frame, 1, resources, inputs);
            (frame, _) = AdvancePool(frame, 2, resources, inputs);
            (frame, int programmes) = AdvancePool(frame, 3, resources, inputs);
            if (main == 0 && programmes == 0)
            {
                return (frame, 2);
            }
        }
        return (Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)(age + 1)))), 0);
    }

    private static NativeBattleEffect289Frame InitializeSecondaryBanks(NativeBattleEffect289Frame frame)
    {
        if (frame.Effect.Pools.Length != 1)
        {
            throw new InvalidDataException("Effect289 bank reset requires its actual inactive COMMON banks.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect289 requires its retained COMMON owner.");
        List<NativeBattleEffectTaskPool> reset = [];
        for (int index = 1; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(expected.Descriptor, expected.Bank, expected.Stride, expected.Capacity);
            for (int slot = 0; slot < pool.Slots.Length; slot++)
            {
                int offset = checked((int)(pool.BankAddress - ArenaAddress) + slot * pool.Stride + 12);
                NativeBattleEffectArena retained = NativeBattleEffectNodeContextMemory.Copy(arena, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, pool.Stride - 12), 0, pool.Stride - 12);
                pool = pool.WriteSlot(slot, CopyTask(pool.Slots[slot], 12, retained, pool.Stride - 12));
            }
            reset.Add(pool);
        }
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.AddRange(reset) }, Memory = frame.Memory with { Arena = arena.MarkUnknown(0, ScratchOffset) } };
    }

    public static NativeBattleEffect289Frame InitializeMeshW677A10(NativeBattleEffect289Frame frame, uint source, NativeBattleEffect289Resources resources)
    {
        NativeBattleStaticMeshResource mesh = resources.ReadMesh(source);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4, 4));
        for (int vertex = 0; vertex < count; vertex++)
        {
            short z = BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(12 + vertex * 8, 2));
            frame = frame with { State = frame.State.WriteMeshW(source, vertex, unchecked((short)-z)) };
        }
        return frame;
    }

    private static (NativeBattleEffect289Frame Frame, uint Address) MandatoryAppend(NativeBattleEffect289Frame frame, uint descriptor, uint callback)
    {
        (frame, uint address) = Append(frame, descriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect289 exhausted its mandatory native task bank.");
        }
        return (frame, address);
    }

    private static NativeBattleEffect289Frame Random289(NativeBattleEffect289Frame frame, out int value)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort sample);
        value = sample;
        return frame with { Random = random };
    }
    public static NativeBattleGeometryRegisterStep Registers289(NativeBattleEffect289Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect289 requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect289 requires its current arithmetic registers."));
    public static NativeBattleEffect289Frame Publish289(NativeBattleEffect289Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    public static NativeBattleVector Vector289(NativeBattleEffectArena point) => new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
    public static short? W289(NativeBattleEffectArena point)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
}
