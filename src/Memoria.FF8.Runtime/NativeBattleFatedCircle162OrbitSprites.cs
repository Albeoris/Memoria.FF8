using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleFatedCircle162Controller
{
    public static NativeBattleFatedCircle162ChildStep Advance5FA8F0(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        for (int index = 0; index < 40; index++)
        {
            uint address = 0x23CE650u + (uint)index * 24;
            if ((ReadDword(frame.Staging, address) & 1) == 0)
            {
                continue;
            }
            NativeBattleVector axis = new(ReadWord(frame.Staging, address + 8), ReadWord(frame.Staging, address + 10), ReadWord(frame.Staging, address + 12));
            NativeBattleActionMatrix rotation = new(NativeBattleModuleAlignment.Rotation5714F0(ReadWord(frame.Staging, address + 14), axis), 0, 0, 0, 4096);
            NativeBattleVector point = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, ReadWord(frame.Staging, address + 16)));
            point = new(unchecked((short)(point.X + Word(task, 16))), unchecked((short)(point.Y + Word(task, 18))), unchecked((short)(point.Z + Word(task, 20))));
            (frame, bool ended) = DrawSprite(frame, inputs, 0xDA1F20, unchecked((ushort)ReadWord(frame.Staging, address + 4)), point, ReadWord(frame.Staging, address + 6), 0, view, projection, cameraRoll, NativeBattleSquallFinisherVisualIdentity.Particle(162, task, address, 0xDA1F20));
            if (paused)
            {
                continue;
            }
            frame = frame with { Staging = WriteWord(frame.Staging, address + 4, unchecked((ushort)(ReadWord(frame.Staging, address + 4) + 1))) };
            if (ended)
            {
                frame = frame with { Staging = WriteDword(frame.Staging, address, 0) };
            }
            else
            {
                frame = frame with { Staging = WriteWord(WriteWord(frame.Staging, address + 14, unchecked((ushort)(ReadWord(frame.Staging, address + 14) + ReadWord(frame.Staging, address + 22)))), address + 16, unchecked((ushort)(ReadWord(frame.Staging, address + 16) - ReadWord(frame.Staging, address + 18)))) };
            }
        }
        if (paused)
        {
            return new(frame, pool, 0);
        }
        if (task.ReadWord(12) <= 4)
        {
            for (int index = 0; index < 6; index++)
            {
                int free = FreeParticle(frame.Staging, 0x23CE650, 40);
                if (free < 0)
                {
                    break;
                }
                uint address = 0x23CE650u + (uint)free * 24;
                NativeBattleSquallFinisherVisualIdentity.Allocated(162, task, address);
                NativeBattleFileStaging staging = WriteDword(frame.Staging, address, 1);
                staging = WriteWord(staging, address + 4, 0);
                staging = WriteWord(staging, address + 6, unchecked((ushort)(Rand() % 512 + 384)));
                int x = Rand() % 4096 - 2048;
                int y = Rand() % 4096 - 2048;
                int z = Rand() % 4096 - 2048;
                var axis = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                staging = WriteWord(WriteWord(WriteWord(staging, address + 8, unchecked((ushort)axis.X)), address + 10, unchecked((ushort)axis.Y)), address + 12, unchecked((ushort)axis.Z));
                staging = WriteWord(staging, address + 14, unchecked((ushort)(Rand() % 4096)));
                int radius = Rand() % 900 + 600;
                staging = WriteWord(staging, address + 16, unchecked((ushort)radius));
                staging = WriteWord(staging, address + 18, unchecked((ushort)(radius / (Rand() % 10 + 16))));
                staging = WriteWord(staging, address + 22, unchecked((ushort)(Rand() % 50 + 30)));
                frame = frame with { Staging = staging };
            }
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        bool complete = Word(task, 12) >= 23;
        if (complete)
        {
            for (int index = 0; index < 40; index++)
            {
                uint address = 0x23CE650u + (uint)index * 24;
                if ((ReadDword(frame.Staging, address) & 1) != 0)
                {
                    frame = frame with { Staging = WriteDword(frame.Staging, address, 0) };
                }
            }
        }
        return new(frame, pool.WriteSlot(slot, task), complete ? 2 : 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }

    public static NativeBattleFatedCircle162ChildStep Advance5FCD60(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        NativeBattleSearchSpriteResource resource = frame.Memory.FatedCircle162?.RetainedResources?.Sprites.Single(sprite => sprite.Address == 0xDA1B60) ?? throw new NotSupportedException("Native5FCD60 requires its retained planar sprite resource.");
        NativeBattleModuleResource sprite = new(0, resource.Address, 0, 0, resource.Bytes);
        for (int index = 0; index < 80; index++)
        {
            uint address = 0x23CEA40u + (uint)index * 24;
            if ((ReadDword(frame.Staging, address) & 2) == 0)
            {
                continue;
            }
            short age = ReadWord(frame.Staging, address + 4);
            NativeBattleModuleSpriteFrame selected = sprite.ProjectedFrame571C80(unchecked((ushort)age));
            for (int quad = 0; quad < selected.Quads.Length; quad += 20)
            {
                if ((BinaryPrimitives.ReadUInt16LittleEndian(selected.Quads.AsSpan(quad + 14, 2)) & 0xC00) != 0)
                {
                    throw new NotSupportedException("Native5FCD60 does not observe the scratch Z-scale required by a nonplanar sprite frame.");
                }
            }
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(1024, 0, ReadWord(frame.Staging, address + 20)));
            short scale = ReadWord(frame.Staging, address + 6);
            // Only the two scaled planar columns are consumed by the original frame's XY vertices.
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, 0);
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
            {
                PilotIdentity = NativeBattleSquallFinisherVisualIdentity.Particle(162, task, address, sprite.Address),
                Resource = sprite,
                SpriteFrame = selected,
                Phase = unchecked((ushort)age),
                Anchor = new(ReadWord(frame.Staging, address + 8), ReadWord(frame.Staging, address + 10), ReadWord(frame.Staging, address + 12), 0),
                Scale = scale,
                DepthOffset = 0,
                SpriteFlags = 8,
                SpriteWorldRotation = rotation.Rotation
            };
            NativeBattleModulePacketBatch packets = NativeBattleModulePackets.EmitSprite571DC0(draw, projection with { View = view }, cameraRoll, 17, 2);
            frame = inputs.Execute(frame, new NativeBattleFatedCircle162Sprite(packets));
            if (!paused)
            {
                age = unchecked((short)(age - 2));
                frame = frame with { Staging = WriteWord(frame.Staging, address + 4, unchecked((ushort)age)) };
                if (age < 0)
                {
                    frame = frame with { Staging = WriteDword(frame.Staging, address, 0) };
                }
            }
        }
        if (paused)
        {
            return new(frame, pool, 0);
        }
        if (task.ReadWord(12) <= 12)
        {
            int count = Word(task, 12) / 7 + 2;
            for (int index = 0; index < count; index++)
            {
                int free = FreeParticle(frame.Staging, 0x23CEA40, 80);
                if (free < 0)
                {
                    break;
                }
                uint address = 0x23CEA40u + (uint)free * 24;
                NativeBattleSquallFinisherVisualIdentity.Allocated(162, task, address);
                NativeBattleFileStaging staging = WriteDword(frame.Staging, address, 2);
                staging = WriteWord(staging, address + 4, 19);
                staging = WriteWord(staging, address + 6, unchecked((ushort)(Rand() % 5120 + 2304)));
                staging = WriteWord(WriteWord(WriteWord(staging, address + 8, task.ReadWord(16)), address + 10, 0), address + 12, unchecked((ushort)(Word(task, 20) + 2200)));
                int spread = unchecked(8 * Word(task, 12) * (Rand() % 190));
                spread = unchecked(spread + 30 * (Rand() % 70 + 10));
                staging = WriteWord(staging, address + 8, unchecked((ushort)(ReadWord(staging, address + 8) + Rand() % spread - (spread >> 1))));
                staging = WriteWord(staging, address + 12, unchecked((ushort)(ReadWord(staging, address + 12) + Rand() % (spread >> 2) - (spread >> 3))));
                staging = WriteWord(staging, address + 20, unchecked((ushort)(Rand() % 2048)));
                frame = frame with { Staging = staging };
            }
        }
        task = task.WriteWord(20, unchecked((ushort)(Word(task, 20) - 580))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(frame, pool.WriteSlot(slot, task), ReadDword(frame.Staging, 0x23D0D80) == 0 ? 2 : 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }
}
