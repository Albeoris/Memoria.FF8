namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRoughDivide163Controller
{
    public static NativeBattleRoughDivide163ChildStep Advance5F7E00(NativeBattleRoughDivide163Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRoughDivide163Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, short cameraRoll)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        short counter = Word(task, 12);
        if (!paused && counter <= 10)
        {
            short heightIndex = unchecked((short)(counter + 1));
            int radius = ((1500 * NativeBattleCameraMath.Sine((counter << 11) / 10)) >> 12) + 100;
            if (radius <= 0)
            {
                throw new NotSupportedException("Native5F7E00 requires its reached positive random radius.");
            }
            for (int index = 0; index < 2; index++)
            {
                uint record = FreeParticle(frame.Staging, 0x23CAB38, 150, 32);
                if (record == 0)
                {
                    break;
                }
                NativeBattleFileStaging staging = WriteWord(WriteDword(frame.Staging, record, 4), record + 4, 0);
                short scale = unchecked((short)(Rand() % 2560 + 1280));
                staging = WriteWord(staging, record + 6, unchecked((ushort)scale));
                staging = staging.Write(record + 8, task.ReadRawPayloadSpan().Slice(4, 8));
                staging = WriteWord(staging, record + 8, unchecked((ushort)(ReadWord(staging, record + 8) + Rand() % (2 * radius) - radius)));
                staging = WriteWord(staging, record + 10, unchecked((ushort)(-350 * heightIndex - Rand() % 800 + 400)));
                staging = WriteWord(staging, record + 12, unchecked((ushort)(ReadWord(staging, record + 12) + Rand() % (2 * radius) - radius)));
                staging = WriteWord(staging, record + 16, unchecked((ushort)(scale >> 1)));
                staging = WriteWord(staging, record + 18, unchecked((ushort)(scale / 3)));
                staging = WriteWord(staging, record + 20, unchecked((ushort)(Rand() % 4096)));
                staging = WriteWord(staging, record + 22, unchecked((ushort)(Rand() % 70 + 50)));
                frame = frame with { Staging = staging };
            }
        }
        NativeBattleSearchSpriteResource sprite = frame.Memory.RoughDivide163?.RetainedResources?.Sprites.Single(resource => resource.Address == 0xD9B2AC) ?? throw new NotSupportedException("Native5F7E00 requires its actually entered sprite resource.");
        int alive = 0;
        for (int index = 0; index < 150; index++)
        {
            uint record = 0x23CAB38u + (uint)index * 32;
            NativeBattleFileStaging staging = frame.Staging;
            if ((ReadDword(staging, record) & 4) == 0)
            {
                continue;
            }
            short age = ReadWord(staging, record + 4);
            short scale = ReadWord(staging, record + 6);
            NativeBattleModuleResource resource = new(0, sprite.Address, 0, 0, sprite.Bytes);
            NativeBattleModuleSpriteFrame animation = resource.ProjectedFrame571C80(unchecked((ushort)age));
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
            {
                Resource = resource,
                SpriteFrame = animation,
                Phase = unchecked((ushort)age),
                Anchor = new(ReadWord(staging, record + 8), ReadWord(staging, record + 10), ReadWord(staging, record + 12), 0),
                Scale = scale,
                DepthOffset = -(scale >> 3),
                SpriteFlags = 8
            };
            NativeBattleActionMatrix spriteCamera = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, view, cameraRoll);
            frame = inputs.Execute(frame, new NativeBattleRoughDivide163Sprite(NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = view }, cameraRoll, 17, 2)));
            if (age < 6)
            {
                int meshScale = ReadWord(staging, record + 16);
                NativeBattleActionMatrix rotation = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(512, -512, ReadWord(staging, record + 20))), meshScale, meshScale, meshScale);
                NativeBattleActionMatrix matrix = rotation with { X = spriteCamera.X, Y = spriteCamera.Y, Z = spriteCamera.Z };
                frame = DrawParticleMesh(frame, inputs, 0xD9CE04, matrix, projection, colors, new(age >= 0 ? 0xF3u : 0x33u, 0, age >= 0 ? 682 * age : null, null, null, null, 17, 2));
            }
            if (paused)
            {
                continue;
            }
            if (animation.NextOffset < 0)
            {
                frame = frame with { Staging = WriteDword(frame.Staging, record, 0) };
                continue;
            }
            staging = WriteWord(frame.Staging, record + 4, unchecked((ushort)(age + 1)));
            short speed = ReadWord(staging, record + 18);
            staging = WriteWord(staging, record + 16, unchecked((ushort)(ReadWord(staging, record + 16) + speed)));
            staging = WriteWord(staging, record + 18, unchecked((ushort)(speed - speed / 3)));
            staging = WriteWord(staging, record + 20, unchecked((ushort)(ReadWord(staging, record + 20) + ReadWord(staging, record + 22))));
            frame = frame with { Staging = staging };
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
}
