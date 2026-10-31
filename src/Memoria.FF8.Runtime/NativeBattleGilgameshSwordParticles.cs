using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshSwordParticleInitialization(NativeBattleEffectTaskPool Children, NativeBattleEffectTaskPool Particles);
public sealed record NativeBattleGilgameshSwordParticleStep(NativeBattleModulePoolTask UpdatedTask, NativeBattleEffectArena Arena, NativeCrtRandomState Random, ImmutableArray<NativeBattleGfGeometryBatch> MeshPackets, ImmutableArray<NativeBattleModulePacketBatch> SpritePackets, int Survivors, int Result);

/// <summary>5947F0's ordered tasks and5948A0/594CA0's shared physical sword-particle banks.</summary>
public static partial class NativeBattleGilgameshSwordParticles
{
    public static NativeBattleGilgameshSwordParticleInitialization Initialize5947F0(NativeBattleEffectTaskPool children, NativeBattleEffectTaskPool particles, short variant, NativeBattleGilgameshSwordParticleResources resources)
    {
        resources.Validate();
        if (children.DescriptorAddress != 0x21FFD58 || children.BankAddress != 0x21FFD68 || children.Stride != 36 || children.Slots.Length != 100 || particles.DescriptorAddress != 0x21FF458 || particles.BankAddress != 0x21FF3C8 || particles.Stride != 36 || particles.Slots.Length != 4)
        {
            throw new InvalidDataException("Native5947F0 requires its actual raw child and particle task banks.");
        }
        children.Validate();
        particles.Validate();
        int slot;
        (children, slot) = children.Append(0x594FD0);
        children = children.WriteSlot(slot, children.Slots[slot].WriteWord(12, 0).WriteWord(14, unchecked((ushort)variant)));
        (particles, slot) = particles.Append(0x595D50);
        particles = particles.WriteSlot(slot, particles.Slots[slot].WriteWord(12, 0).WriteWord(14, 105));
        for (int index = 0; index < 4; index++)
        {
            (children, slot) = children.Append(0x594CA0);
            children = children.WriteSlot(slot, children.Slots[slot].WriteWord(12, 0).WriteWord(14, (ushort)index).WriteWord(28, (ushort)(1 << index)).WriteWord(30, unchecked((ushort)resources.SwordOrder[index])));
            (children, slot) = children.Append(0x5948A0);
            children = children.WriteSlot(slot, children.Slots[slot].WriteWord(12, 0).WriteWord(28, (ushort)(1 << index)).WriteWord(30, unchecked((ushort)resources.SwordOrder[index])));
        }
        return new(children, particles);
    }

    public static NativeBattleGilgameshSwordParticleStep Step(NativeBattleModulePoolTask task, NativeBattleEffectArena arena, NativeCrtRandomState random, NativeBattleGilgameshSwordParticleResources resources, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, uint graphicsFlags, Func<int, byte, short, NativeBattleVector> readSwordBone, NativeBattleSummonColors? colors = null)
    {
        return StepWithPresentation(task, arena, random, resources, worldView, projection, graphicsFlags, readSwordBone, colors, null);
    }

    public static NativeBattleGilgameshSwordParticleStep StepWithPresentation(NativeBattleModulePoolTask task, NativeBattleEffectArena arena, NativeCrtRandomState random, NativeBattleGilgameshSwordParticleResources resources, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, uint graphicsFlags, Func<int, byte, short, NativeBattleVector> readSwordBone, NativeBattleSummonColors? colors = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (task is null || task.PayloadLength != 24 || task.Callback is not (0x5948A0 or 0x594CA0))
        {
            throw new InvalidDataException("Native sword particles require their selected raw0x24-byte callback task.");
        }
        bool mesh = task.Callback == 0x5948A0;
        bool paused = (graphicsFlags & 0x201) != 0;
        if (!mesh && Signed(task, 14) > 0)
        {
            return new(paused ? task : task.WriteWord(14, unchecked((ushort)(task.ReadWord(14) - 1))), arena, random, [], [], 0, 0);
        }
        resources.Validate();
        arena.Validate();
        ArgumentNullException.ThrowIfNull(readSwordBone);
        int bank = mesh ? 0x2F800 : 0x2E000;
        int stride = mesh ? 32 : 24;
        int mask = Signed(task, 28);
        if (!paused && Signed(task, 12) <= 1)
        {
            for (int spawn = 0; spawn < (mesh ? 6 : 4); spawn++)
            {
                int record = -1;
                for (int index = 0; index < 200; index++)
                {
                    int current = bank + index * stride;
                    if (Dword(arena, current) == 0)
                    {
                        record = current;
                        break;
                    }
                }
                if (record < 0)
                {
                    break;
                }
                (arena, random) = Spawn(arena, record, mask, mesh, random);
                if (pilotIdentity.HasValue)
                {
                    NativeBattlePilotVisualLifetimes.Allocated(arena, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank), (record - bank) / stride);
                }
            }
        }
        int sword = Signed(task, 30);
        if (sword is < 0 or >= 4)
        {
            throw new NotSupportedException("Native sword particle bone request leaves its selected four-model bank.");
        }
        NativeBattleVector bone = readSwordBone(sword, 1, 4096);
        NativeBattleActionMatrix localBase = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = bone.X, Y = 0, Z = bone.Z };
        NativeBattleActionMatrix baseView = NativeBattleGfMatrices.Compose56C2F0(worldView, localBase);
        ImmutableArray<NativeBattleGfGeometryBatch>.Builder meshes = ImmutableArray.CreateBuilder<NativeBattleGfGeometryBatch>();
        ImmutableArray<NativeBattleModulePacketBatch>.Builder sprites = ImmutableArray.CreateBuilder<NativeBattleModulePacketBatch>();
        int survivors = 0;
        for (int index = 0; index < 200; index++)
        {
            int record = bank + index * stride;
            if ((unchecked((int)Dword(arena, record)) & mask) == 0)
            {
                continue;
            }
            int age = Word(arena, record + 4);
            int size = Word(arena, record + 6);
            bool dead;
            if (mesh)
            {
                NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(Word(arena, record + 24), 0, Word(arena, record + 28))), size, size, size) with { X = Word(arena, record + 8), Y = Word(arena, record + 10), Z = Word(arena, record + 12) };
                NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(baseView, local);
                if (age >= 8)
                {
                    arena = PutWord(arena, record + 6, unchecked((16 - age) * Word(arena, record + 14) << 9) >> 12);
                }
                meshes.Add(NativeBattleModulePackets.EmitBatch572200(resources.Mesh.Bytes, 0, default, matrix, projection, new(0, 0, null, null, null, null, 17, 2), colors ?? NativeBattleGteColorInterpolation.CodeConstants45F270, pilotIdentity: NativeBattleGilgameshVisuals.Row(pilotIdentity, arena, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank), index, resources.Mesh, resources.Mesh.Address)));
                dead = age >= 16;
            }
            else
            {
                NativeBattleModuleSpriteFrame frame = resources.Sprite.ProjectedFrame571C80(age);
                NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
                {
                    Resource = resources.Sprite,
                    PilotProjection = pilotIdentity.HasValue ? projection : null,
                    PilotIdentity = NativeBattleGilgameshVisuals.Row(pilotIdentity, arena, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank), index, resources.Sprite, resources.Sprite.Address),
                    SpriteFrame = frame,
                    Phase = unchecked((ushort)age),
                    Anchor = new(Word(arena, record + 8), Word(arena, record + 10), Word(arena, record + 12), 0),
                    Scale = size,
                    DepthOffset = -(size >> 2),
                    SpriteFlags = 8
                };
                sprites.Add(NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = baseView }, 0, 17, 2));
                dead = frame.NextOffset < 0;
            }
            if (paused)
            {
                continue;
            }
            if (dead)
            {
                arena = PutDword(arena, record, 0);
                continue;
            }
            arena = PutWord(arena, record + 4, age + 1);
            if (mesh)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    int velocity = Word(arena, record + 16 + axis * 2);
                    arena = PutWord(arena, record + 8 + axis * 2, Word(arena, record + 8 + axis * 2) + velocity);
                    arena = PutWord(arena, record + 16 + axis * 2, velocity - (velocity >> 3));
                }
                arena = PutWord(arena, record + 24, Word(arena, record + 24) + Word(arena, record + 26));
                arena = PutWord(arena, record + 28, Word(arena, record + 28) + Word(arena, record + 30));
            }
            else
            {
                arena = PutWord(arena, record + 8, Word(arena, record + 8) + Word(arena, record + 16));
                arena = PutWord(arena, record + 12, Word(arena, record + 12) + Word(arena, record + 20));
            }
            survivors++;
        }
        if (!paused)
        {
            task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        }
        return new(task, arena, random, meshes.ToImmutable(), sprites.ToImmutable(), survivors, !paused && survivors == 0 ? 2 : 0);
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int offset, int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        return arena.Write(offset, bytes);
    }
    private static NativeBattleEffectArena PutDword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
}
