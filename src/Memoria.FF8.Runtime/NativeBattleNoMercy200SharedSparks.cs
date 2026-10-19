namespace Memoria.FF8.Runtime;

public sealed record NativeBattleNoMercy200BoneInputs(Func<int, int> ReadBoneCount, Func<int, int, short, NativeBattleVector> ReadBone);

public static partial class NativeBattleNoMercy200Controller
{
    public static NativeBattleNoMercy200ChildStep AdvanceSharedSparks(NativeBattleNoMercy200Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleNoMercy200Inputs inputs, NativeBattleNoMercy200BoneInputs bones)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint callback = task.Callback;
        if (callback is not (0x63CEF0 or 0x63DE90 or 0x63E170))
        {
            throw new InvalidDataException("Native No Mercy spark callback requires its original producer.");
        }
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        bool impact = callback == 0x63CEF0;
        bool moving = callback == 0x63E170;
        short tick = Word(task, 12);
        int marker = impact ? 2 : Word(task, 14) + (moving ? 128 : 64);
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 72);
        frame = frame with { Staging = temporary.Staging };
        if (!paused && tick <= (impact ? 19 : moving ? 6 : 7))
        {
            int count = impact ? tick / 12 + 1 : moving ? 3 : 2;
            int actor = impact ? -1 : Word(task, 26);
            int boneCount = impact ? 0 : bones.ReadBoneCount(actor);
            if (moving)
            {
                frame = frame with { Staging = WriteDword(WriteDword(frame.Staging, temporary.Address, TaskDword(task, 16)), temporary.Address + 4, TaskDword(task, 20)) };
            }
            for (int index = 0; index < count; index++)
            {
                uint row = FreeParticle(frame.Staging);
                if (row == 0)
                {
                    break;
                }
                SetDword(row, checked((uint)marker));
                SetWord(row + 4, 0);
                SetWord(row + 6, Rand() % (impact ? 896 : moving ? 1024 : 3840) + (impact ? 6 * tick + 256 : 768));
                if (impact)
                {
                    var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(Rand() % 8192 - 4096, Rand() % 8192 - 4096, Rand() % 8192 - 4096);
                    SetDword(temporary.Address + 40, unchecked((uint)direction.X));
                    SetDword(temporary.Address + 44, unchecked((uint)direction.Y));
                    SetDword(temporary.Address + 48, unchecked((uint)direction.Z));
                    int distance = Rand() % 40 + 10 * tick + 20;
                    frame = frame with { Staging = CopyEight(frame.Staging, 0x24FBCD8, row + 8) };
                    SetWord(row + 8, ReadWord(frame.Staging, row + 8) + (unchecked(distance * direction.X) >> 12));
                    SetWord(row + 10, ReadWord(frame.Staging, row + 10) + (unchecked(distance * direction.Y) >> 12));
                    SetWord(row + 12, ReadWord(frame.Staging, row + 12) + (unchecked(distance * direction.Z) >> 12));
                }
                else
                {
                    int selector = boneCount == 0 ? 0 : Rand() % boneCount;
                    short fraction = (short)(Rand() % 4096);
                    NativeBattleVector point = bones.ReadBone(actor, selector, fraction);
                    frame = frame with { Staging = WritePoint(frame.Staging, row + 8, point) };
                    if (moving)
                    {
                        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(point.X - Word(task, 16), point.Y - Word(task, 18), point.Z - Word(task, 20));
                        SetDword(temporary.Address + 40, unchecked((uint)direction.X));
                        SetDword(temporary.Address + 44, unchecked((uint)direction.Y));
                        SetDword(temporary.Address + 48, unchecked((uint)direction.Z));
                        int speed = Rand() % 280 + 220;
                        SetWord(row + 16, unchecked(speed * direction.X) >> 12);
                        SetWord(row + 18, unchecked(speed * direction.Y) >> 12);
                        SetWord(row + 20, unchecked(speed * direction.Z) >> 12);
                    }
                    else
                    {
                        SetWord(row + 8, point.X + Rand() % 800 - 400);
                        SetWord(row + 10, point.Y + Rand() % 800 - 400);
                        SetWord(row + 12, point.Z + Rand() % 800 - 400);
                    }
                }
            }
        }
        NativeBattleTemporaryAllocation sprite = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 180);
        uint sourceAddress = impact ? 0xE2DB40u : moving ? 0xE2DFE4u : 0xE2DC84u;
        NativeBattleSearchSpriteResource selected = frame.Memory.NoMercy200?.Resources?.Sprites.Single(resource => resource.Address == sourceAddress) ?? throw new NotSupportedException("Native No Mercy sparks require their original entered sprite.");
        NativeBattleModuleResource resource = new(0, sourceAddress, 0, 0, selected.Bytes);
        ushort flags = moving ? (ushort)0 : (ushort)8;
        frame = frame with { Staging = NativeBattleModuleSpriteTemporary.Begin571C80(sprite.Staging, sprite.Address, sourceAddress, flags) };
        frame = frame with { Staging = StoreMatrix(frame.Staging, temporary.Address + 8, inputs.View).MarkUnknown(temporary.Address + 26, 2) };
        int alive = 0;
        for (uint row = 0x24F8DB0; row < 0x24F9710; row += 24)
        {
            int actual = ReadDword(frame.Staging, row);
            if (impact ? (actual & 2) == 0 : actual != marker)
            {
                continue;
            }
            short age = ReadWord(frame.Staging, row + 4);
            NativeBattleModuleSpriteFrame animation = resource.ProjectedFrame571C80(unchecked((ushort)age));
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = animation, Phase = unchecked((ushort)age), Anchor = new(ReadWord(frame.Staging, row + 8), ReadWord(frame.Staging, row + 10), ReadWord(frame.Staging, row + 12), 0), Scale = ReadWord(frame.Staging, row + 6), SpriteFlags = flags, DepthOffset = impact ? -(ReadWord(frame.Staging, row + 6) >> 3) : -Word(task, 24) };
            frame = inputs.Execute(frame, new NativeBattleNoMercy200SpriteDraw(draw) { TemporaryAddress = sprite.Address });
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
                if (moving)
                {
                    for (uint axis = 0; axis < 3; axis++)
                    {
                        short speed = ReadWord(frame.Staging, row + 16 + axis * 2);
                        SetWord(row + 8 + axis * 2, ReadWord(frame.Staging, row + 8 + axis * 2) + speed);
                        SetWord(row + 16 + axis * 2, speed - (speed >> 2));
                    }
                }
                alive++;
            }
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(NativeBattleTemporaryMemory.Release5082D0(frame.Staging, 180), 72) };
        if (paused)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, alive == 0 ? 2 : 0);

        void SetWord(uint address, int value)
        {
            frame = frame with { Staging = WriteWord(frame.Staging, address, value) };
        }
        void SetDword(uint address, uint value)
        {
            frame = frame with { Staging = WriteDword(frame.Staging, address, value) };
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort random) };
            return random;
        }
    }
}
