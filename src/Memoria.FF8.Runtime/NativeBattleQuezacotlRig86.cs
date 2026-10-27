using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlRigWrite(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);

/// <summary>Draft86 producers; canonical model, directory, pose and skeleton bytes stay in their actual effect arena.</summary>
public static partial class NativeBattleQuezacotlRig86
{
    public const int Model = 0x28;
    public const int Directory = 0xC4;
    public const int Resource = 0x3169C;
    private const uint ArenaAddress = 0x20DFAB8;

    public static NativeBattleActionMatrix ReadBoneMatrix(NativeBattleEffectMemory memory, int boneIndex)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native rig bone read requires its actual effect arena.");
        int skeleton = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(Directory, 4)) - ArenaAddress));
        int offset = checked(skeleton + 32 + boneIndex * 48);
        //The native signed selector is not clamped to a guessed bone range. Each actual read must remain owned.
        short[] rotation = new short[9];
        byte[] words = arena.Read(offset, 18);
        for (int index = 0; index < rotation.Length; index++)
        {
            rotation[index] = BinaryPrimitives.ReadInt16LittleEndian(words.AsSpan(index * 2));
        }
        byte[] translation = arena.Read(offset + 20, 12);
        return new(rotation.ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(translation), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(8)), 4096);
    }

    public static NativeBattleQuezacotlRigWrite InitializeBeforeSecondWindow(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native86 requires its actual effect arena.");
        int z = effect.Quezacotl?.TargetZ1778 ?? throw new InvalidDataException("Native86 lost its entry target Z.");
        //701270(0) clears20 rotation bytes only. Translation and all unwritten object fields retain their current bytes.
        byte[] identity = new byte[20];
        foreach (int offset in new[] { 0, 8, 16 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(identity.AsSpan(offset), 4096);
        }
        arena = arena.Write(0x68, identity);
        Dword(0x80, 0);
        Word(0x46, 0);
        Dword(0x7C, 0);
        Word(0x44, 0);
        Dword(0x84, z);
        Word(0x48, unchecked((short)z));
        Word(0x36, 0);
        Word(0x38, 0);
        Word(0x34, 0);
        Dword(0x14, 0);

        //6EC060(model, directory, current arena resource), exactly its written fields.
        Word(Model, 2);
        Dword(Model + 0x7C, -1);
        Dword(Model + 0x8C, 0);
        Dword(Model + 0x78, 0);
        Byte(Model + 0x61, 0);
        Byte(Model + 0x60, 0);
        Dword(Model + 0x68, unchecked((int)(ArenaAddress + Directory)));
        Dword(Model + 0x64, unchecked((int)(ArenaAddress + Directory)));
        Byte(Model + 0x6D, 0);
        Dword(Model + 0x28, 0x808080);
        int[] sections = Enumerable.Range(0, 4).Select(index => checked(Resource + BinaryPrimitives.ReadInt32LittleEndian(arena.Read(Resource + 4 + index * 4, 4)))).ToArray();
        if (sections[0] < Resource + 20 || !sections.SequenceEqual(sections.Order()) || sections[3] > NativeBattleEffectArena.Length)
        {
            throw new InvalidDataException("Native6EC060 section pointers leave their current effect resource.");
        }
        for (int index = 0; index < 4; index++)
        {
            Dword(Directory + index * 4, unchecked((int)(ArenaAddress + sections[index])));
        }

        NativeBattlePoseSource source = new(arena.Read(sections[0], sections[1] - sections[0]).ToImmutableArray(), arena.Read(sections[2], sections[3] - sections[2]).ToImmutableArray());
        source.Validate();
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(source.Animations.AsSpan(), source.BoneCount, 0);
        int control = Model + 0x6C;
        Byte(control, 0);
        Byte(control + 1, 4);
        Word(control + 2, unchecked((short)first.ByteOffset));
        Word(control + 4, unchecked((short)first.BitOffset));
        Byte(control + 6, 1);
        Byte(control + 7, NativeBattlePoseDecoder.ReadFrameCounts(source.Animations.AsSpan())[0]);
        int skeleton = sections[0];
        NativeBattleDecodedPose pose = first.Pose;
        Byte(skeleton + 1, (byte)((arena.Read(skeleton + 1, 1)[0] & ~1) | (pose.HasScale ? 1 : 0)));
        Vector(skeleton + 8, pose.Translation);
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.StageBoneMatrices(source, pose);
        for (int index = 0; index < source.BoneCount; index++)
        {
            int bone = skeleton + 16 + index * 48;
            Vector(bone + 4, pose.Bones[index].Rotation);
            if (pose.HasScale)
            {
                Vector(bone + 10, pose.Bones[index].Scale);
            }
            NativeBattleActionMatrix matrix = matrices[index];
            byte[] raw = new byte[32];
            for (int word = 0; word < 9; word++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(word * 2), matrix.Rotation[word]);
            }
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(20), matrix.X);
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(24), matrix.Y);
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(28), matrix.Z);
            arena = arena.Write(bone + 16, raw);
            short parent = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(16 + index * 48));
            if (parent >= 0 || pose.HasScale)
            {
                //56C2F0/56C090 copy uninitialized stack padding with their otherwise known matrix.
                arena = arena.MarkUnknown(bone + 16 + 18, 2);
            }
        }
        return Synchronize(memory with { Arena = arena }, effect);

        void Byte(int offset, byte value) => arena = arena.Write(offset, [value]);
        void Word(int offset, short value)
        {
            byte[] raw = new byte[2];
            BinaryPrimitives.WriteInt16LittleEndian(raw, value);
            arena = arena.Write(offset, raw);
        }
        void Dword(int offset, int value)
        {
            byte[] raw = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(raw, value);
            arena = arena.Write(offset, raw);
        }
        void Vector(int offset, NativeBattleVector vector)
        {
            Word(offset, vector.X);
            Word(offset + 2, vector.Y);
            Word(offset + 4, vector.Z);
        }
    }

    public static NativeBattleQuezacotlRigWrite InitializeAfterSecondWindow(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleCameraState cameraState)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native86 requires its actual effect arena.");
        NativeBattleCameraPose camera = cameraState.CurrentPose ?? throw new NotSupportedException("Native86 requires its actual current eye/target globals.");
        short eyePadding = cameraState.EyePaddingF6 ?? throw new NotSupportedException("Native86 copies current camera WORD B8B7F6; explicitly migrate this standalone's proved padding owner.");
        short targetPadding = cameraState.TargetPaddingFE ?? throw new NotSupportedException("Native86 copies current camera WORD B8B7FE; explicitly migrate this standalone's proved padding owner.");
        byte[] sine = new byte[32];
        for (int index = 0; index < 16; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(sine.AsSpan(index * 2), unchecked((short)NativeBattleCameraMath.Sine(index * 2048 / 15)));
        }
        arena = arena.Write(0x3C25C, sine);
        ImmutableArray<NativeBattleEffectTaskPool> pools = effect.Pools;
        AddPool(0x2521728, 0x3BA5C, 0x80, 16);
        AddPool(0x2521718, 0x3C27C, 0x3C, 32);
        AddPool(0x2521708, 0x3F200, 0x20, 128);
        using NativeBattleEffectArenaWriter writer = new(arena);
        for (int index = 0; index < 256; index++)
        {
            writer.Write(0x3CA02 + index * 0x28, [0xFF, 0xFF]);
        }
        writer.Write(0x3F1FC, [0, 0, 0, 0]);
        for (int index = 0; index < 512; index++)
        {
            writer.Write(0x40200 + index * 0x1C, [0xFF, 0xFF]);
        }
        writer.Write(0x43A00, [0, 0, 0, 0]);
        arena = writer.Freeze();
        byte[] savedCamera = new byte[16];
        BinaryPrimitives.WriteInt16LittleEndian(savedCamera, camera.Target.X);
        BinaryPrimitives.WriteInt16LittleEndian(savedCamera.AsSpan(2), camera.Target.Y);
        BinaryPrimitives.WriteInt16LittleEndian(savedCamera.AsSpan(4), camera.Target.Z);
        BinaryPrimitives.WriteInt16LittleEndian(savedCamera.AsSpan(6), targetPadding);
        BinaryPrimitives.WriteInt16LittleEndian(savedCamera.AsSpan(8), camera.Eye.X);
        BinaryPrimitives.WriteInt16LittleEndian(savedCamera.AsSpan(10), camera.Eye.Y);
        BinaryPrimitives.WriteInt16LittleEndian(savedCamera.AsSpan(12), camera.Eye.Z);
        BinaryPrimitives.WriteInt16LittleEndian(savedCamera.AsSpan(14), eyePadding);
        arena = arena.Write(0x18, savedCamera);
        ImmutableArray<uint> heads = memory.QuezacotlSecondaryHeads ?? throw new NotSupportedException("Native86 requires the retained secondary descriptors not reset by this producer.");
        heads = heads.SetItem(0, 0).SetItem(1, 0).SetItem(2, 0);
        return Synchronize(memory with { Arena = arena, QuezacotlSecondaryHeads = heads }, effect with { Pools = pools });

        void AddPool(uint descriptor, int offset, ushort stride, ushort count)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(descriptor, ArenaAddress + (uint)offset, stride, count);
            pools = pools.Where(existing => existing.DescriptorAddress != descriptor).Append(pool).ToImmutableArray();
            //508300 clears each entire slot, not only the linked-list header.
            arena = arena.Write(offset, new byte[stride * count]);
        }
    }

    private static NativeBattleQuezacotlRigWrite Synchronize(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        NativeBattleEffectTaskPool child = effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738);
        child = child.WriteSlot(0, child.Slots[0].WriteRetainedBytes(12, memory.Arena!.Read(12, child.Stride - 12)));
        return new(memory, effect with { Pools = effect.Pools.Select(pool => pool.DescriptorAddress == child.DescriptorAddress ? child : pool).ToImmutableArray() });
    }
}
