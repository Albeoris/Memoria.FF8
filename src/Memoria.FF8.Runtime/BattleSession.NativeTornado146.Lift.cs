using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeTornado146Runner
    {
        private int Lift(ref NativeBattleModulePoolTask task)
        {
            using NativeBattleTornado146Work work = new(state);
            int age = Word(task, 12);
            NativeBattleModelAliases aliases = owner.nativeActionPackets.ModelAliases;
            ImmutableArray<BattleModelOwner> recipients;
            var rows = new Dictionary<BattleModelOwner, NativeBattleTornado146LiftRowWriter>();
            if (age == 0)
            {
                for (byte cell = 0; cell < NativeBattleModelBoundary.CellCount; cell++)
                {
                    work.WriteDword(OriginalLiftRow(cell), 1);
                }
                NativeBattleActionHeaderSlot header = owner.nativeActionPackets.ReadHeader(effect.Parameters!.Header.Advance(Word(task, 14)));
                BattleContactCursor start = header.FirstContact;
                int count = header.ReadContactCount();
                work.WriteDword(0x249A604, (uint)count);
                var ordered = ImmutableArray.CreateBuilder<BattleModelOwner>(count);
                for (int index = 0; index < count; index++)
                {
                    BattleModelOwner actor = owner.nativeActionPackets.ReadContact(start.Advance(index)).Primary.Recipient;
                    ordered.Add(actor);
                    if (index < NativeBattleModelBoundary.CellCount)
                    {
                        work.WriteModelOperand(0x249A608u + (uint)index * 4, actor, aliases);
                    }
                    if (!rows.TryGetValue(actor, out NativeBattleTornado146LiftRowWriter? row))
                    {
                        row = CreateRow(actor, null);
                        rows.Add(actor, row);
                    }
                    NativeBattleGraphicsModel model = graphics.ActorModels.Get(actor);
                    NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Tornado lift requires its actual target transform.");
                    row.WriteDword(0, 0);
                    row.WriteWord(4, (ushort)(model.Flags0 & 0x1020));
                    row.WriteVector(8, transform.Position28);
                    row.WriteWord(14, unchecked((ushort)transform.Vertical34));
                    row.WriteVector(24, transform.Position28);
                    row.WriteWord(30, unchecked((ushort)transform.Vertical34));
                    row.WriteVector(16, new(model.Rotation12 ?? throw new NotSupportedException("Native Tornado lift requires target rotation X."), model.Rotation14 ?? throw new NotSupportedException("Native Tornado lift requires target rotation Y."), model.Rotation16 ?? throw new NotSupportedException("Native Tornado lift requires target rotation Z.")));
                    row.WriteVector(32, row.Vector(16));
                    if (model.RotationPadding18 is ushort padding)
                    {
                        row.WriteWord(22, padding);
                        row.WriteWord(38, padding);
                    }
                    else
                    {
                        row.MarkUnknown(22, 2);
                        row.MarkUnknown(38, 2);
                    }
                    row.WriteWord(24, unchecked((ushort)(row.SignedWord(24) - Word(task, 16))));
                    row.WriteWord(28, unchecked((ushort)(row.SignedWord(28) - Word(task, 20))));
                    row.WriteWord(30, unchecked((ushort)(-60 - Random() % 110)));
                    row.WriteWord(38, (ushort)(Random() % 40 + 15));
                    row.WriteDword(40, 0);
                    row.WriteDword(44, (uint)(Random() % 60 + 20));
                    row.WriteDword(48, unchecked((uint)owner.ReadNativeGraphicsBone(graphics, actor, 240, 4096).Y));
                    graphics = graphics with { ActorModels = graphics.ActorModels.Set(actor, model with { Flags0 = (ushort)(model.Flags0 | 0x1000) }) };
                }
                recipients = ordered.MoveToImmutable();
            }
            else
            {
                NativeBattleTornado146LiftRows saved = effect.TornadoLiftRows ?? throw new InvalidDataException("Native Tornado lift requires its captured target work rows.");
                recipients = saved.Recipients;
                foreach ((BattleModelOwner actor, NativeBattleTornado146LiftRow row) in saved.Rows)
                {
                    rows.Add(actor, CreateRow(actor, row));
                }
            }
            foreach (BattleModelOwner actor in graphics.ActorModels.Owners)
            {
                if (!rows.TryGetValue(actor, out NativeBattleTornado146LiftRowWriter? row))
                {
                    continue;
                }
                NativeBattleGraphicsModel model = graphics.ActorModels.Get(actor);
                NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Tornado target lost its position owner.");
                if (age < 72)
                {
                    NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleGfMatrices.Euler56CD50(new(0, row.SignedWord(40), 0)), row.Vector(24));
                    NativeBattleVector point = new(unchecked((short)(rotated.X + Word(task, 16))), row.SignedWord(26), unchecked((short)(rotated.Z + Word(task, 20))));
                    model = model with { Transform = transform with { Position28 = point, Vertical34 = row.SignedWord(14) }, Rotation14 = row.SignedWord(34) };
                }
                else
                {
                    NativeBattleVector point = row.Vector(8);
                    NativeBattleVector rotation = row.Vector(16);
                    if (age < 82)
                    {
                        point = point with { Y = unchecked((short)(row.SignedWord(26) + row.SignedWord(48))) };
                        rotation = rotation with { Z = unchecked((short)(rotation.Z + 2048)) };
                    }
                    ushort? padding = row.OptionalWord(22);
                    model = model with { Transform = transform with { Position28 = point, Vertical34 = row.SignedWord(14) }, Rotation12 = rotation.X, Rotation14 = rotation.Y, Rotation16 = rotation.Z, RotationPadding18 = padding };
                }
                graphics = graphics with { ActorModels = graphics.ActorModels.Set(actor, model) };
            }
            if (DrawOnly)
            {
                PublishWork();
                return 0;
            }
            foreach (BattleModelOwner actor in graphics.ActorModels.Owners)
            {
                if (!rows.TryGetValue(actor, out NativeBattleTornado146LiftRowWriter? row))
                {
                    continue;
                }
                if (age < 72)
                {
                    short y = row.SignedWord(26);
                    if (age >= 26 && y > -12000)
                    {
                        short velocity = row.SignedWord(30);
                        row.WriteWord(26, unchecked((ushort)(y + velocity)));
                        row.WriteWord(30, unchecked((ushort)(velocity + (velocity >> 5))));
                    }
                    short spin = row.SignedWord(38);
                    row.WriteWord(34, unchecked((ushort)(row.SignedWord(34) + spin)));
                    row.WriteWord(38, unchecked((ushort)(spin + (spin >> 6))));
                    int turn = row.SignedDword(44);
                    row.WriteDword(40, unchecked(row.Dword(40) + (uint)turn));
                    row.WriteDword(44, unchecked((uint)(turn + (turn >> 5))));
                    if (row.SignedWord(26) < -200)
                    {
                        NativeBattleGraphicsModel model = graphics.ActorModels.Get(actor);
                        graphics = graphics with { ActorModels = graphics.ActorModels.Set(actor, model with { Flags0 = (ushort)(model.Flags0 | 0x20) }) };
                    }
                }
                else if (age == 72)
                {
                    short velocity = unchecked((short)(-row.SignedWord(26) / 8));
                    row.WriteWord(30, unchecked((ushort)velocity));
                    row.WriteWord(24, row.Word(8));
                    row.WriteWord(26, unchecked((ushort)(row.SignedWord(26) + velocity)));
                    row.WriteWord(28, row.Word(12));
                }
                else if (age < 80)
                {
                    row.WriteWord(26, unchecked((ushort)(row.SignedWord(26) + row.SignedWord(30))));
                }
                else if (age == 80)
                {
                    int child = Allocate(1, 0x618280);
                    int first = Word(task, 24);
                    Put(1, child, pools[1].Slots[child].WriteWord(12, 0).WriteWord(14, unchecked((ushort)first)).WriteDword(16, row.Dword(24)).WriteDword(20, row.Dword(28)));
                    task = task.WriteWord(24, unchecked((ushort)(first + 20)));
                    for (int index = first; index < first + 20; index++)
                    {
                        work.WriteWord(0x24946E6u + (uint)index * 8, ushort.MaxValue);
                    }
                    owner.PublishNativeModuleSound(new(resources.LandSound, 0x8000, new(row.SignedWord(24), row.SignedWord(26), row.SignedWord(28), row.SignedWord(30))));
                }
                else
                {
                    owner.NativeCamera = owner.NativeCamera!.AddViewTranslationY61B8D5((short)((age & 1) == 0 ? 12 : -12));
                }
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            if (age + 1 > 83)
            {
                foreach (BattleModelOwner actor in graphics.ActorModels.Owners)
                {
                    if (rows.TryGetValue(actor, out NativeBattleTornado146LiftRowWriter? row))
                    {
                        NativeBattleGraphicsModel model = graphics.ActorModels.Get(actor);
                        graphics = graphics with { ActorModels = graphics.ActorModels.Set(actor, model with { Flags0 = (ushort)((model.Flags0 & 0xEFDF) | row.Word(4)) }) };
                    }
                }
            }
            PublishWork();
            return age + 1 > 83 ? 2 : 0;

            NativeBattleTornado146LiftRowWriter CreateRow(BattleModelOwner actor, NativeBattleTornado146LiftRow? saved)
            {
                uint? original = aliases.TryEncodeExact(actor, out byte cell) ? OriginalLiftRow(cell) : null;
                return new(work, original, saved);
            }

            void PublishWork()
            {
                state = work.Freeze();
                effect = effect with { TornadoLiftRows = new(recipients, rows.ToImmutableDictionary(pair => pair.Key, pair => pair.Value.Freeze())) };
            }
        }

        private static uint OriginalLiftRow(byte cell) => 0x2494570u + (uint)cell * NativeBattleTornado146LiftRowWriter.Length;
    }
}
