namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlastingZone164Sprite(NativeBattleModulePacketBatch Batch, bool Common) : NativeBattleBlastingZone164Operation;

public static partial class NativeBattleBlastingZone164Controller
{
    public static NativeBattleBlastingZone164ChildStep AdvanceSpriteParticles(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleBlastingZone164Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint callback = task.Callback;
        if (callback is not (0x5F0E80 or 0x5F5120 or 0x5F57C0))
        {
            throw new NotSupportedException("Unsupported native Blasting Zone sprite callback.");
        }
        bool orbit = callback == 0x5F0E80;
        bool final = callback == 0x5F57C0;
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        uint bank = final ? 0x23C72D8u : 0x23C6978u;
        uint end = bank + 2400;
        short counter = Word(task, 12);
        if (!paused && counter <= (orbit ? 48 : 20))
        {
            int count = orbit ? 1 : final ? 5 : 8;
            for (int index = 0; index < count; index++)
            {
                uint row = bank;
                while (row < end && ReadDword(frame.Staging, row) != 0)
                {
                    row += 24;
                }
                if (row == end)
                {
                    break;
                }
                SetDword(row, 1);
                SetWord(row + 4, 0);
                SetWord(row + 6, Rand() % (orbit ? 640 : final ? 3072 : 640) + (orbit ? 768 : final ? 768 : 512));
                if (orbit)
                {
                    int x = Rand() % 4096 - 2048;
                    int y = Rand() % 4096 - 2048;
                    int z = Rand() % 4096 - 2048;
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                    SetWord(row + 8, direction.X);
                    SetWord(row + 10, direction.Y);
                    SetWord(row + 12, direction.Z);
                    SetWord(row + 14, Rand() % 150 + 400);
                    SetWord(row + 16, Rand() % 4096);
                    SetWord(row + 18, Rand() % 65 + 30);
                }
                else if (final)
                {
                    SetWord(row + 8, Rand() % 6000 - 3000);
                    SetWord(row + 10, -(Rand() % 1500));
                    SetWord(row + 12, -1100 - Rand() % 8000);
                }
                else
                {
                    int angle = Rand() % 4096;
                    int distance = Rand() % 600 + 600;
                    int sine = NativeBattleCameraMath.Sine(angle);
                    int cosine = NativeBattleCameraMath.Cosine(angle);
                    SetWord(row + 8, (distance * sine) >> 12);
                    SetWord(row + 10, -900 - Rand() % 6500);
                    SetWord(row + 12, (distance * cosine) >> 12);
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(sine, Rand() % 4096 - 2048, cosine);
                    int speed = Rand() % 75 + 15;
                    SetWord(row + 16, (direction.X * speed) >> 12);
                    SetWord(row + 18, (direction.Y * speed) >> 12);
                    SetWord(row + 20, (direction.Z * speed) >> 12);
                }
            }
        }
        uint spriteAddress = orbit ? 0xD99748u : final ? 0xD9961Cu : 0xD999ACu;
        NativeBattleSearchSpriteResource selected = frame.Memory.BlastingZone164?.RetainedResources?.Sprites.Single(sprite => sprite.Address == spriteAddress) ?? throw new InvalidDataException("Native Blasting Zone requires its entered sprite resources.");
        NativeBattleModuleResource resource = new(0, spriteAddress, 0, 0, selected.Bytes);
        NativeBattleActionMatrix translated = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = ReadWord(frame.Staging, 0x23C9D98), Y = orbit ? ReadWord(frame.Staging, 0x23C9D9A) : 0, Z = ReadWord(frame.Staging, 0x23C9D9C) };
        NativeBattleActionMatrix basis = NativeBattlePoseMatrices.Compose56C2F0(final ? ReadMatrix(frame.Staging, 0x23C9D68) : view, translated);
        int alive = 0;
        for (uint row = bank; row < end; row += 24)
        {
            if ((ReadDword(frame.Staging, row) & 1) == 0)
            {
                continue;
            }
            short age = ReadWord(frame.Staging, row + 4);
            NativeBattleVector point = new(ReadWord(frame.Staging, row + 8), ReadWord(frame.Staging, row + 10), ReadWord(frame.Staging, row + 12));
            if (orbit)
            {
                NativeBattleVector axis = new(unchecked((short)-point.Z), point.Y, unchecked((short)-point.X));
                NativeBattleActionMatrix rotation = new(NativeBattleModuleAlignment.Rotation5714F0(ReadWord(frame.Staging, row + 16), axis), 0, 0, 0, 4096);
                var rotated = NativeBattleGfMatrices.RotateLong56C600(rotation, point.X, point.Y, point.Z);
                short radius = ReadWord(frame.Staging, row + 14);
                point = new(unchecked((short)((rotated.X * radius) >> 12)), unchecked((short)((rotated.Y * radius) >> 12)), unchecked((short)((rotated.Z * radius) >> 12)));
            }
            short scale = ReadWord(frame.Staging, row + 6);
            NativeBattleModuleSpriteFrame animation = resource.ProjectedFrame571C80(unchecked((ushort)age));
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = animation, Phase = unchecked((ushort)age), Anchor = new(point.X, point.Y, point.Z, 0), Scale = scale, SpriteFlags = final ? (ushort)8 : (ushort)0, DepthOffset = final ? -(scale >> 3) : 0 };
            frame = inputs.Execute(frame, new NativeBattleBlastingZone164Sprite(NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = basis }, cameraRoll, 17, 2), orbit));
            if (paused)
            {
                continue;
            }
            if (animation.NextOffset < 0)
            {
                SetDword(row, 0);
            }
            else
            {
                SetWord(row + 4, age + 1);
                if (orbit)
                {
                    SetWord(row + 16, ReadWord(frame.Staging, row + 16) + ReadWord(frame.Staging, row + 18));
                }
                else if (!final)
                {
                    SetWord(row + 8, point.X + ReadWord(frame.Staging, row + 16));
                    SetWord(row + 10, point.Y + ReadWord(frame.Staging, row + 18));
                    SetWord(row + 12, point.Z + ReadWord(frame.Staging, row + 20));
                    alive++;
                }
            }
        }
        if (paused)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        bool released = orbit ? Word(task, 12) >= 54 : final ? Word(task, 12) >= 25 : alive == 0;
        if (released && (orbit || final))
        {
            for (uint row = bank; row < end; row += 24)
            {
                if ((ReadDword(frame.Staging, row) & 1) != 0)
                {
                    SetDword(row, 0);
                }
            }
        }
        return new(frame, pool.WriteSlot(slot, task), released ? 2 : 0);

        void SetWord(uint address, int value)
        {
            frame = frame with { Staging = WriteWord(frame.Staging, address, unchecked((ushort)value)) };
        }
        void SetDword(uint address, uint value)
        {
            frame = frame with { Staging = WriteDword(frame.Staging, address, value) };
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }
}
