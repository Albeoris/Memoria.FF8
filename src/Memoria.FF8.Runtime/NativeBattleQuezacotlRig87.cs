using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleQuezacotlRig86
{
    public static NativeBattleQuezacotlSkinBatch Emit6C6770(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, int fadeOriginY)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6C6770 requires its current rig arena.");
        int geometry = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(Directory + 4, 4)) - ArenaAddress));
        int animations = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(Directory + 8, 4)) - ArenaAddress));
        uint mask = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(Model + 0x7C, 4));
        uint rgb = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(Model + 0x28, 4));
        int counter = BinaryPrimitives.ReadInt32LittleEndian(arena.Read(0x14, 4));
        NativeBattleQuezacotlSkinContext context = new(mask, rgb, 0x808080, 0, 0, 320, 216, 0x3F54, 0xB7, (counter >> 6) & 127, (counter >> 5) & 127, 128, 128, fadeOriginY, [0x02000000, 0xE2000000, 0], [0x02000000, 0xE2080210, 0], 17, 2);
        return NativeBattleQuezacotlSkinning.Emit6C68F0(arena.Read(geometry, animations - geometry).ToImmutableArray(), selector => ReadBoneMatrix(memory, selector), projection.View ?? throw new NotSupportedException("Native6C6770 requires its prior view matrix."), projection, context, colors);
    }

    public static NativeBattleQuezacotlRigWrite AddFrameCounter(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, int amount)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6C3940 requires its actual child counter.");
        int counter = BinaryPrimitives.ReadInt32LittleEndian(arena.Read(0x14, 4));
        byte[] raw = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(raw, unchecked(counter + amount));
        return Synchronize(memory with { Arena = arena.Write(0x14, raw) }, effect);
    }

    public static (NativeBattleQuezacotlRigWrite Rig, NativeBattleCameraState Camera) UpdateFlightCamera6C3DDF(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleCameraState camera, int localFrame)
    {
        if (localFrame is < 1 or >= 76)
        {
            throw new ArgumentOutOfRangeException(nameof(localFrame));
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6C3DDF requires its saved camera aliases.");
        int z = effect.Quezacotl?.TargetZ1778 ?? throw new InvalidDataException("Native6C3DDF lost its target Z.");
        NativeBattleVector target = ReadVector(arena, 0x18);
        NativeBattleVector eye = ReadVector(arena, 0x20);
        target = target with { Y = unchecked((short)(target.Y - 17)) };
        eye = eye with { Y = unchecked((short)(eye.Y - 5)), Z = unchecked((short)(eye.Z - z)) };
        int fraction = localFrame * 4096 / 76;
        short angle = unchecked((short)(NativeBattleCameraMath.Sine(fraction >> 1) / 90));
        eye = Rotate(eye, angle);
        eye = eye with { Z = unchecked((short)(eye.Z + z)) };
        arena = WriteVector(arena, 0x18, target);
        arena = WriteVector(arena, 0x20, eye);
        NativeBattleVector currentEye;
        NativeBattleVector currentTarget;
        if (localFrame == 31)
        {
            currentEye = new(1531, -2459, unchecked((short)(z - 518)));
            currentTarget = new(-5127, -1606, unchecked((short)(z + 1542)));
        }
        else if (localFrame is > 31 and <= 40)
        {
            NativeBattleCameraPose pose = camera.CurrentPose ?? throw new NotSupportedException("Native6C3EBB requires its retained current camera coordinates.");
            currentEye = Rotate(new(pose.Eye.X, pose.Eye.Y, unchecked((short)(pose.Eye.Z - z))), 64);
            currentEye = currentEye with { Z = unchecked((short)(currentEye.Z + z)) };
            currentTarget = Rotate(new(pose.Target.X, pose.Target.Y, unchecked((short)(pose.Target.Z - z))), 64);
            currentTarget = currentTarget with { Z = unchecked((short)(currentTarget.Z + z)) };
        }
        else if (fraction < 2048)
        {
            currentEye = eye;
            currentTarget = target;
            camera = camera with { EyePaddingF6 = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(0x26, 2)), TargetPaddingFE = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(0x1E, 2)) };
        }
        else
        {
            int progress = 2 * fraction - 4096;
            currentTarget = NativeBattleQuezacotlRibbonMath.Blend(target, new(-663, -2748, unchecked((short)(z + 1783))), 4096 - progress, progress);
            currentEye = NativeBattleQuezacotlRibbonMath.Blend(eye, new(1351, -1548, unchecked((short)(z - 2580))), 4096 - progress, progress);
        }
        camera = camera.WriteEffectGlobals(new(new(currentEye.X, currentEye.Y, currentEye.Z), new(currentTarget.X, currentTarget.Y, currentTarget.Z), camera.FocalLength, camera.Roll));
        return (Synchronize(memory with { Arena = arena }, effect), camera);

        static NativeBattleVector Rotate(NativeBattleVector vector, short angle)
        {
            (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(NativeBattleGfMatrices.Euler701310(new(0, angle, 0)), vector);
            return new(unchecked((short)x), unchecked((short)y), unchecked((short)z));
        }
    }

    public static NativeBattleQuezacotlRigWrite PreparePose6C3A1E(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, ushort phase)
    {
        if (phase is 118 or 238)
        {
            NativeBattleQuezacotlRigWrite selected = Select6574D0(memory, effect, phase == 118 ? (byte)1 : (byte)2);
            memory = selected.Memory;
            effect = selected.Effect;
        }
        int paused = memory.QuezacotlPause ?? throw new NotSupportedException("Native6FBDB0 requires its retained pause owner.");
        return paused == 0 ? Advance6FBDB0(memory, effect) : new(memory, effect);
    }

    public static NativeBattleQuezacotlRigWrite Select6574D0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, byte selector)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6574D0 requires its current rig memory.");
        int skeleton = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(Directory, 4)) - ArenaAddress));
        int animations = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(Directory + 8, 4)) - ArenaAddress));
        int end = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(Directory + 12, 4)) - ArenaAddress));
        byte[] control = arena.Read(0x94, 8);
        if ((control[1] & 3) != 0)
        {
            throw new NotSupportedException("Native6574D0 requires its reached pose-speed selector contract.");
        }
        ImmutableArray<byte> section = arena.Read(animations, end - animations).ToImmutableArray();
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(section.AsSpan(), arena.Read(skeleton, 1)[0], selector);
        control[0] = selector;
        control[1] = (byte)((control[1] & 0xF3) | 4);
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(2), first.ByteOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(4), first.BitOffset);
        control[6] = 1;
        control[7] = NativeBattlePoseDecoder.ReadFrameCounts(section.AsSpan())[selector];
        arena = arena.Write(0x94, control);
        arena = WritePose(arena, skeleton, first.Pose);
        arena = RestoreBoneMatrices(arena, skeleton, first.Pose);
        return Synchronize(memory with { Arena = arena }, effect);
    }
    public static NativeBattleQuezacotlRigWrite Advance6FBDB0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6FBDB0 requires its current arena rig.");
        int skeleton = Pointer(Directory);
        int animations = Pointer(Directory + 8);
        int end = Pointer(Directory + 12);
        int count = arena.Read(skeleton, 1)[0];
        byte[] control = arena.Read(0x94, 8);
        if ((control[1] & 3) != 0)
        {
            throw new NotSupportedException("Native Quezacotl pose speed flags require their current508F90 cursor contract.");
        }
        ImmutableArray<byte> section = arena.Read(animations, end - animations).ToImmutableArray();
        NativeBattlePoseFirstFrame frame;
        if (control[6] >= control[7])
        {
            frame = NativeBattlePoseDecoder.DecodeFirst509440(section.AsSpan(), count, control[0]);
            control[1] = (byte)((control[1] & 0xF3) | 4);
            control[6] = 1;
            control[7] = NativeBattlePoseDecoder.ReadFrameCounts(section.AsSpan())[control[0]];
        }
        else
        {
            NativeBattleDecodedPose current = ReadCurrentPose(arena, skeleton, count);
            frame = NativeBattlePoseDecoder.AdvanceRegular508F90(section.AsSpan(), count, control[0], current, BinaryPrimitives.ReadUInt16LittleEndian(control.AsSpan(2)), BinaryPrimitives.ReadUInt16LittleEndian(control.AsSpan(4)));
            control[6]++;
            control[1] = (byte)((control[1] & 0xF3) | ((((control[1] >> 2) + 1) & 1) << 2));
        }
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(2), frame.ByteOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(control.AsSpan(4), frame.BitOffset);
        arena = arena.Write(0x94, control);
        arena = WritePose(arena, skeleton, frame.Pose);
        arena = RestoreBoneMatrices(arena, skeleton, frame.Pose);
        return Synchronize(memory with { Arena = arena }, effect);

        int Pointer(int address) => checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(address, 4)) - ArenaAddress));
    }

    public static NativeBattleQuezacotlRigWrite Compose5095B0(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native5095B0 requires its current rig.");
        int skeleton = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(Directory, 4)) - ArenaAddress));
        int count = arena.Read(skeleton, 1)[0];
        NativeBattleActionMatrix model = ReadMatrix(arena, 0x68);
        for (int index = 0; index < count; index++)
        {
            NativeBattleActionMatrix bone = ReadBoneMatrix(memory with { Arena = arena }, index);
            arena = WriteMatrix(arena, skeleton + 32 + index * 48, NativeBattlePoseMatrices.ComposeStageCamera([bone], model, 0)[0], true);
        }
        return new(memory with { Arena = arena }, effect);
    }

    public static NativeBattleQuezacotlRigWrite Restore508C90(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native508C90 requires its current rig.");
        int skeleton = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(Directory, 4)) - ArenaAddress));
        NativeBattleDecodedPose pose = ReadCurrentPose(arena, skeleton, arena.Read(skeleton, 1)[0]);
        return Synchronize(memory with { Arena = RestoreBoneMatrices(arena, skeleton, pose) }, effect);
    }

    private static NativeBattleDecodedPose ReadCurrentPose(NativeBattleEffectArena arena, int skeleton, int count)
    {
        bool hasScale = (arena.Read(skeleton + 1, 1)[0] & 1) != 0;
        ImmutableArray<NativeBattleBonePose>.Builder bones = ImmutableArray.CreateBuilder<NativeBattleBonePose>(count);
        for (int index = 0; index < count; index++)
        {
            int offset = skeleton + 16 + index * 48;
            bones.Add(new(ReadVector(arena, offset + 4), hasScale ? ReadVector(arena, offset + 10) : new(1024, 1024, 1024)));
        }
        return new(ReadVector(arena, skeleton + 8), bones.MoveToImmutable(), hasScale);
    }

    private static NativeBattleEffectArena WritePose(NativeBattleEffectArena arena, int skeleton, NativeBattleDecodedPose pose)
    {
        arena = arena.Write(skeleton + 1, [(byte)((arena.Read(skeleton + 1, 1)[0] & ~1) | (pose.HasScale ? 1 : 0))]);
        arena = WriteVector(arena, skeleton + 8, pose.Translation);
        for (int index = 0; index < pose.Bones.Length; index++)
        {
            int offset = skeleton + 16 + index * 48;
            arena = WriteVector(arena, offset + 4, pose.Bones[index].Rotation);
            if (pose.HasScale)
            {
                arena = WriteVector(arena, offset + 10, pose.Bones[index].Scale);
            }
        }
        return arena;
    }

    private static NativeBattleEffectArena RestoreBoneMatrices(NativeBattleEffectArena arena, int skeleton, NativeBattleDecodedPose pose)
    {
        short scale = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(skeleton + 2, 2));
        ImmutableArray<NativeBattleRigBone>.Builder definition = ImmutableArray.CreateBuilder<NativeBattleRigBone>(pose.Bones.Length);
        for (int index = 0; index < pose.Bones.Length; index++)
        {
            byte[] raw = arena.Read(skeleton + 16 + index * 48, 4);
            definition.Add(new(BinaryPrimitives.ReadInt16LittleEndian(raw), BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(2))));
        }
        ImmutableArray<NativeBattleRigBone> bones = definition.MoveToImmutable();
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.RigBoneMatrices(scale, bones, pose);
        for (int index = 0; index < matrices.Length; index++)
        {
            arena = WriteMatrix(arena, skeleton + 32 + index * 48, matrices[index], bones[index].Parent >= 0 || pose.HasScale);
        }
        return arena;
    }

    private static NativeBattleActionMatrix ReadMatrix(NativeBattleEffectArena arena, int offset)
    {
        byte[] rotation = arena.Read(offset, 18);
        byte[] translation = arena.Read(offset + 20, 12);
        return new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(index * 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(translation), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(4)), BinaryPrimitives.ReadInt32LittleEndian(translation.AsSpan(8)), 4096);
    }

    private static NativeBattleEffectArena WriteMatrix(NativeBattleEffectArena arena, int offset, NativeBattleActionMatrix matrix, bool unknownPadding)
    {
        byte[] raw = new byte[32];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(index * 2), matrix.Rotation[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(20), matrix.X);
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(24), matrix.Y);
        BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(28), matrix.Z);
        arena = arena.Write(offset, raw);
        return unknownPadding ? arena.MarkUnknown(offset + 18, 2) : arena;
    }

    private static NativeBattleVector ReadVector(NativeBattleEffectArena arena, int offset)
    {
        byte[] raw = arena.Read(offset, 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(raw), BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(4)));
    }

    private static NativeBattleEffectArena WriteVector(NativeBattleEffectArena arena, int offset, NativeBattleVector vector)
    {
        byte[] raw = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(raw, vector.X);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(2), vector.Y);
        BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(4), vector.Z);
        return arena.Write(offset, raw);
    }
}
