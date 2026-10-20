using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeTornado146Runner
    {
        private int Lift(ref NativeBattleModulePoolTask task)
        {
            NativeBattleTornado146Work work = new(state);
            int age = Word(task, 12);
            if (age == 0)
            {
                for (int actor = 0; actor < 7; actor++)
                {
                    work.WriteDword(0x2494570u + (uint)actor * 52, 1);
                }
                NativeBattleActionHeaderSlot header = owner.nativeActionPackets.Headers[checked(effect.Parameters!.HeaderIndex7C + Word(task, 14))];
                int start = header.FirstContactIndex ?? throw new NotSupportedException("Native Tornado lift requires the actual target contacts.");
                int count = header.ReadByte(16);
                work.WriteDword(0x249A604, (uint)count);
                for (int index = 0; index < count; index++)
                {
                    int actor = owner.nativeActionPackets.ReadContact(start + index).Primary.ActorIndex;
                    work.WriteDword(0x249A608u + (uint)index * 4, (uint)actor);
                    uint row = 0x2494570u + (uint)actor * 52;
                    NativeBattleGraphicsModel model = graphics.Models[actor];
                    NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Tornado lift requires its actual target transform.");
                    work.WriteDword(row, 0);
                    work.WriteWord(row + 4, (ushort)(model.Flags0 & 0x1020));
                    WorkVector(work, row + 8, transform.Position28);
                    work.WriteWord(row + 14, unchecked((ushort)transform.Vertical34));
                    WorkVector(work, row + 24, transform.Position28);
                    work.WriteWord(row + 30, unchecked((ushort)transform.Vertical34));
                    WorkVector(work, row + 16, new(model.Rotation12 ?? throw new NotSupportedException("Native Tornado lift requires target rotation X."), model.Rotation14 ?? throw new NotSupportedException("Native Tornado lift requires target rotation Y."), model.Rotation16 ?? throw new NotSupportedException("Native Tornado lift requires target rotation Z.")));
                    WorkVector(work, row + 32, WorkVector(work, row + 16));
                    if (model.RotationPadding18 is ushort padding)
                    {
                        work.WriteWord(row + 22, padding);
                        work.WriteWord(row + 38, padding);
                    }
                    else
                    {
                        work.MarkUnknown(row + 22, 2);
                        work.MarkUnknown(row + 38, 2);
                    }
                    work.WriteWord(row + 24, unchecked((ushort)(work.SignedWord(row + 24) - Word(task, 16))));
                    work.WriteWord(row + 28, unchecked((ushort)(work.SignedWord(row + 28) - Word(task, 20))));
                    work.WriteWord(row + 30, unchecked((ushort)(-60 - Random() % 110)));
                    work.WriteWord(row + 38, (ushort)(Random() % 40 + 15));
                    work.WriteDword(row + 40, 0);
                    work.WriteDword(row + 44, (uint)(Random() % 60 + 20));
                    work.WriteDword(row + 48, unchecked((uint)NativeBattlePoseMatrices.ReadBone(model, PoseSource(actor), 240, 4096).Y));
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = (ushort)(model.Flags0 | 0x1000) }) };
                }
            }
            for (int actor = 0; actor < 7; actor++)
            {
                uint row = 0x2494570u + (uint)actor * 52;
                if (work.Dword(row) != 0)
                {
                    continue;
                }
                NativeBattleGraphicsModel model = graphics.Models[actor];
                NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Tornado target lost its position owner.");
                if (age < 72)
                {
                    NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleGfMatrices.Euler56CD50(new(0, work.SignedWord(row + 40), 0)), WorkVector(work, row + 24));
                    NativeBattleVector point = new(unchecked((short)(rotated.X + Word(task, 16))), work.SignedWord(row + 26), unchecked((short)(rotated.Z + Word(task, 20))));
                    model = model with { Transform = transform with { Position28 = point, Vertical34 = work.SignedWord(row + 14) }, Rotation14 = work.SignedWord(row + 34) };
                }
                else
                {
                    NativeBattleVector point = WorkVector(work, row + 8);
                    NativeBattleVector rotation = WorkVector(work, row + 16);
                    if (age < 82)
                    {
                        point = point with { Y = unchecked((short)(work.SignedWord(row + 26) + work.SignedWord(row + 48))) };
                        rotation = rotation with { Z = unchecked((short)(rotation.Z + 2048)) };
                    }
                    (byte[] bytes, bool[] known) = work.ReadSnapshot(row + 22, 2);
                    ushort? padding = known.All(value => value) ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes) : null;
                    model = model with { Transform = transform with { Position28 = point, Vertical34 = work.SignedWord(row + 14) }, Rotation12 = rotation.X, Rotation14 = rotation.Y, Rotation16 = rotation.Z, RotationPadding18 = padding };
                }
                graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
            }
            if (DrawOnly)
            {
                state = work.Freeze();
                return 0;
            }
            for (int actor = 0; actor < 7; actor++)
            {
                uint row = 0x2494570u + (uint)actor * 52;
                if (work.Dword(row) != 0)
                {
                    continue;
                }
                if (age < 72)
                {
                    short y = work.SignedWord(row + 26);
                    if (age >= 26 && y > -12000)
                    {
                        short velocity = work.SignedWord(row + 30);
                        work.WriteWord(row + 26, unchecked((ushort)(y + velocity)));
                        work.WriteWord(row + 30, unchecked((ushort)(velocity + (velocity >> 5))));
                    }
                    short spin = work.SignedWord(row + 38);
                    work.WriteWord(row + 34, unchecked((ushort)(work.SignedWord(row + 34) + spin)));
                    work.WriteWord(row + 38, unchecked((ushort)(spin + (spin >> 6))));
                    int turn = work.SignedDword(row + 44);
                    work.WriteDword(row + 40, unchecked(work.Dword(row + 40) + (uint)turn));
                    work.WriteDword(row + 44, unchecked((uint)(turn + (turn >> 5))));
                    if (work.SignedWord(row + 26) < -200)
                    {
                        NativeBattleGraphicsModel model = graphics.Models[actor];
                        graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = (ushort)(model.Flags0 | 0x20) }) };
                    }
                }
                else if (age == 72)
                {
                    short velocity = unchecked((short)(-work.SignedWord(row + 26) / 8));
                    work.WriteWord(row + 30, unchecked((ushort)velocity));
                    work.WriteWord(row + 24, work.Word(row + 8));
                    work.WriteWord(row + 26, unchecked((ushort)(work.SignedWord(row + 26) + velocity)));
                    work.WriteWord(row + 28, work.Word(row + 12));
                }
                else if (age < 80)
                {
                    work.WriteWord(row + 26, unchecked((ushort)(work.SignedWord(row + 26) + work.SignedWord(row + 30))));
                }
                else if (age == 80)
                {
                    int child = Allocate(1, 0x618280);
                    int first = Word(task, 24);
                    Put(1, child, pools[1].Slots[child].WriteWord(12, 0).WriteWord(14, unchecked((ushort)first)).WriteDword(16, work.Dword(row + 24)).WriteDword(20, work.Dword(row + 28)));
                    task = task.WriteWord(24, unchecked((ushort)(first + 20)));
                    for (int index = first; index < first + 20; index++)
                    {
                        work.WriteWord(0x24946E6u + (uint)index * 8, ushort.MaxValue);
                    }
                    owner.PublishNativeModuleSound(new(resources.LandSound, 0x8000, new(work.SignedWord(row + 24), work.SignedWord(row + 26), work.SignedWord(row + 28), work.SignedWord(row + 30))));
                }
                else
                {
                    owner.NativeCamera = owner.NativeCamera!.AddViewTranslationY61B8D5((short)((age & 1) == 0 ? 12 : -12));
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            if (age + 1 > 83)
            {
                for (int actor = 0; actor < 7; actor++)
                {
                    uint row = 0x2494570u + (uint)actor * 52;
                    if (work.Dword(row) == 0)
                    {
                        NativeBattleGraphicsModel model = graphics.Models[actor];
                        graphics = graphics with { Models = graphics.Models.SetItem(actor, model with { Flags0 = (ushort)((model.Flags0 & 0xEFDF) | work.Word(row + 4)) }) };
                    }
                }
            }
            state = work.Freeze();
            return age + 1 > 83 ? 2 : 0;
        }

        private static void WorkVector(NativeBattleTornado146Work work, uint address, NativeBattleVector value)
        {
            work.WriteWord(address, unchecked((ushort)value.X));
            work.WriteWord(address + 2, unchecked((ushort)value.Y));
            work.WriteWord(address + 4, unchecked((ushort)value.Z));
        }
    }
}
