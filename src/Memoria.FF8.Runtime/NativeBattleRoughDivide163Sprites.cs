namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRoughDivide163Sprite(NativeBattleModulePacketBatch Batch) : NativeBattleRoughDivide163Operation;

public static partial class NativeBattleRoughDivide163Controller
{
    public static NativeBattleRoughDivide163ChildStep AdvanceSpriteParticles(NativeBattleRoughDivide163Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRoughDivide163Inputs inputs, NativeBattleSearchSpriteResource sprite, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint flag = task.Callback switch
        {
            0x5F67A0 => 1,
            0x5F6A30 => 2,
            0x5F7A80 => 4,
            _ => throw new NotSupportedException("Native Rough Divide sprite callback is unsupported.")
        };
        if (sprite.Address != 0xD9B458)
        {
            throw new InvalidDataException("Native Rough Divide particles require their actual sprite directory.");
        }
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        int counter = Word(task, 12);
        if (!paused && counter <= (flag == 1 ? 20 : flag == 2 ? 10 : 3))
        {
            int count = flag == 4 ? 8 : 2;
            for (int index = 0; index < count; index++)
            {
                NativeBattleModuleAnchor bone = default!;
                if (flag == 1)
                {
                    bone = inputs.ReadBone(frame.Parameters.Actor78, (byte)(20 - (index != 0 ? 1 : 0)), 4096);
                    if (bone.Y <= -40)
                    {
                        continue;
                    }
                }
                uint record = FreeParticle(frame.Staging, 0x23CA1D8, 100, 24);
                if (record == 0)
                {
                    break;
                }
                NativeBattleFileStaging staging = WriteWord(WriteDword(frame.Staging, record, flag), record + 4, 0);
                int scale = flag == 1 ? Rand() % 512 + 1280 : flag == 2 ? Rand() % 768 + 1024 : Rand() % 1408 + 1408;
                staging = WriteWord(staging, record + 6, unchecked((ushort)scale));
                if (flag == 1)
                {
                    staging = Point(staging, record + 8, new(bone.X, bone.Y, bone.Z), null);
                }
                else if (flag == 2)
                {
                    staging = Copy(staging, 0x23CD318, record + 8, 8);
                }
                else
                {
                    staging = staging.Write(record + 8, task.ReadRawPayloadSpan().Slice(4, 8));
                }
                if (flag is 1 or 2)
                {
                    int range = flag == 1 ? 100 : 80;
                    for (uint axis = 0; axis < 6; axis += 2)
                    {
                        staging = WriteWord(staging, record + 8 + axis, unchecked((ushort)(ReadWord(staging, record + 8 + axis) + Rand() % range - range / 2)));
                    }
                }
                else
                {
                    int angle = Rand() % 4096;
                    int distance = Rand() % 100 + 300;
                    staging = WriteWord(staging, record + 8, unchecked((ushort)(ReadWord(staging, record + 8) + ((distance * NativeBattleCameraMath.Sine(angle)) >> 12))));
                    staging = WriteWord(staging, record + 10, unchecked((ushort)(ReadWord(staging, record + 10) - Rand() % 100)));
                    staging = WriteWord(staging, record + 12, unchecked((ushort)(ReadWord(staging, record + 12) + ((distance * NativeBattleCameraMath.Cosine(angle)) >> 12))));
                    int speed = Rand() % 80 + 40;
                    staging = WriteWord(staging, record + 16, unchecked((ushort)((speed * NativeBattleCameraMath.Sine(angle)) >> 12)));
                    staging = WriteWord(staging, record + 20, unchecked((ushort)((speed * NativeBattleCameraMath.Cosine(angle)) >> 12)));
                }
                frame = frame with { Staging = staging };
            }
        }
        int alive = 0;
        for (int index = 0; index < 100; index++)
        {
            uint record = 0x23CA1D8u + (uint)index * 24;
            if ((ReadDword(frame.Staging, record) & flag) == 0)
            {
                continue;
            }
            short age = ReadWord(frame.Staging, record + 4);
            short scale = ReadWord(frame.Staging, record + 6);
            NativeBattleModuleResource resource = new(0, sprite.Address, 0, 0, sprite.Bytes);
            NativeBattleModuleSpriteFrame animation = resource.ProjectedFrame571C80(unchecked((ushort)age));
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
            {
                Resource = resource,
                SpriteFrame = animation,
                Phase = unchecked((ushort)age),
                Anchor = new(ReadWord(frame.Staging, record + 8), ReadWord(frame.Staging, record + 10), ReadWord(frame.Staging, record + 12), 0),
                Scale = scale,
                DepthOffset = -(scale >> 3),
                SpriteFlags = 8
            };
            frame = inputs.Execute(frame, new NativeBattleRoughDivide163Sprite(NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = view }, cameraRoll, 17, 2)));
            if (paused)
            {
                continue;
            }
            if (animation.NextOffset < 0)
            {
                frame = frame with { Staging = WriteDword(frame.Staging, record, 0) };
                continue;
            }
            NativeBattleFileStaging current = WriteWord(frame.Staging, record + 4, unchecked((ushort)(age + 1)));
            if (flag == 4)
            {
                for (uint axis = 0; axis < 6; axis += 2)
                {
                    short velocity = ReadWord(current, record + 16 + axis);
                    current = WriteWord(WriteWord(current, record + 8 + axis, unchecked((ushort)(ReadWord(current, record + 8 + axis) + velocity))), record + 16 + axis, unchecked((ushort)(velocity - (velocity >> 3))));
                }
            }
            frame = frame with { Staging = current };
            alive++;
        }
        if (paused)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool.WriteSlot(slot, task), alive == 0 ? 2 : 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    private static uint FreeParticle(NativeBattleFileStaging staging, uint bank, int capacity, int stride)
    {
        for (int index = 0; index < capacity; index++)
        {
            uint address = bank + (uint)(index * stride);
            if (ReadDword(staging, address) == 0)
            {
                return address;
            }
        }
        return 0;
    }
}
