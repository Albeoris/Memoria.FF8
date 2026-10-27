using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeTornado146Runner
    {
        private void InitializeCloudDeltas()
        {
            using NativeBattleTornado146Work work = new(state);
            for (int vertex = 0; vertex < 120; vertex++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    int offset = vertex * 8 + axis * 2;
                    int first = BinaryPrimitives.ReadInt16LittleEndian(resources.Cloud.Bytes.AsSpan().Slice(8 + offset));
                    int second = BinaryPrimitives.ReadInt16LittleEndian(resources.CloudTarget.AsSpan().Slice(offset));
                    work.WriteWord(0x24946E0u + (uint)offset, unchecked((ushort)((second - first) / 12)));
                }
            }
            state = work.Freeze();
        }

        private int Cloud(ref NativeBattleModulePoolTask task)
        {
            int age = Word(task, 12);
            int buffer = Word(task, 14);
            if ((uint)buffer > 1)
            {
                throw new InvalidDataException("Native Tornado cloud requires one of its two actual morph buffers.");
            }
            uint selected = age >= 12 ? 0xDD181Cu : 0x2494AA8u + (uint)buffer * 960;
            ImmutableArray<byte> vertices = resources.CloudTarget;
            if (age < 12)
            {
                using NativeBattleTornado146Work work = new(state);
                for (int vertex = 0; vertex < 120; vertex++)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        uint offset = (uint)(vertex * 8 + axis * 2);
                        short first = BinaryPrimitives.ReadInt16LittleEndian(resources.Cloud.Bytes.AsSpan().Slice(8 + (int)offset));
                        work.WriteWord(selected + offset, unchecked((ushort)(first + unchecked((short)(work.SignedWord(0x24946E0 + offset) * age)))));
                    }
                }
                vertices = work.Read(selected, 960).ToImmutableArray();
                state = work.Freeze();
            }
            NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleCuragaMatrices.Identity8DD770() with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) }, Word(task, 28), Word(task, 28), Word(task, 28));
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(owner.NativeFileStaging!, 88);
            owner.NativeFileStaging = temporary.Staging.Write(temporary.Address, BitConverter.GetBytes(resources.Cloud.Address)).Write(temporary.Address + 4, BitConverter.GetBytes(selected)).Write(temporary.Address + 8, [0, 0, 0]).Write(temporary.Address + 12, BitConverter.GetBytes(341 * age)).Write(temporary.Address + 28, BitConverter.GetBytes(0x20C0u));
            owner.NativeFileStaging = owner.NativeFileStaging.Write(temporary.Address + 24, BitConverter.GetBytes(0u)).Write(temporary.Address + 32, BitConverter.GetBytes(resources.Cloud.Address + BinaryPrimitives.ReadUInt32LittleEndian(resources.Cloud.Bytes.AsSpan())));
            NativeBattleMeshPacketVariant observations = new(null, null, false, false)
            {
                ObserveFlags = flags => owner.NativeFileStaging = owner.NativeFileStaging!.Write(temporary.Address + 48, BitConverter.GetBytes(flags)),
                ObserveArea = area => owner.NativeFileStaging = owner.NativeFileStaging!.Write(temporary.Address + 36, BitConverter.GetBytes(area)),
                ObserveAverageDepth = depth => owner.NativeFileStaging = owner.NativeFileStaging!.Write(temporary.Address + 44, BitConverter.GetBytes(depth))
            };
            Append(NativeBattleGfGeometryPackets.EmitBatch(resources.Cloud.Bytes, 0, vertices, NativeBattleGfMatrices.Compose56C2F0(View, world), Projection, new(0x20C0, 0, 341 * age, null, null, 0, 0, 17, 2), Colors, observeSourceCursor: cursor => owner.NativeFileStaging = owner.NativeFileStaging!.Write(temporary.Address + 32, BitConverter.GetBytes(resources.Cloud.Address + (uint)cursor)), variant: observations));
            owner.NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(owner.NativeFileStaging!, 88);
            if (DrawOnly)
            {
                return 0;
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= 13 ? 2 : 0;
        }

        private int Sparks(ref NativeBattleModulePoolTask task)
        {
            using NativeBattleTornado146Work work = new(state);
            List<(NativeBattleVector Point, int Size, int Frame)> draws = [];
            for (int index = 0; index < 60; index++)
            {
                uint row = 0x249A230u + (uint)index * 16;
                int frame = work.SignedDword(row);
                if (frame < 0)
                {
                    continue;
                }
                draws.Add((WorkVector(work, row + 8), work.SignedDword(row + 4), frame));
                if (!DrawOnly)
                {
                    work.WriteDword(row, unchecked((uint)(frame + 1)));
                    if (frame + 1 >= 16)
                    {
                        work.WriteDword(row, uint.MaxValue);
                    }
                    else
                    {
                        work.WriteDword(row + 4, unchecked((uint)(work.SignedDword(row + 4) - 32)));
                        work.WriteWord(row + 10, unchecked((ushort)(work.SignedWord(row + 10) + work.SignedWord(row + 14))));
                        short velocity = work.SignedWord(row + 14);
                        work.WriteWord(row + 14, unchecked((ushort)(velocity + (velocity >> 3))));
                    }
                }
            }
            int age = Word(task, 12);
            if (!DrawOnly && age is >= 1 and <= 50)
            {
                int births = age < 25 ? 3 : age < 38 ? 2 : 1;
                for (int born = 0; born < births; born++)
                {
                    int free = Enumerable.Range(0, 60).FirstOrDefault(index => work.SignedDword(0x249A230u + (uint)index * 16) < 0, -1);
                    if (free < 0)
                    {
                        break;
                    }
                    uint row = 0x249A230u + (uint)free * 16;
                    work.WriteDword(row, 0);
                    work.WriteDword(row + 4, (uint)(Random() % 6144 + 2304));
                    int angle = Random() % 4096;
                    int radius = Random() % 1100 + 1900;
                    work.WriteWord(row + 8, unchecked((ushort)(Word(task, 16) + (NativeBattleCameraMath.Cosine(angle) * radius >> 12))));
                    work.WriteWord(row + 10, unchecked((ushort)(-40 - Random() % 140)));
                    work.WriteWord(row + 12, unchecked((ushort)(Word(task, 20) + (NativeBattleCameraMath.Sine(angle) * radius >> 12))));
                    work.WriteWord(row + 14, unchecked((ushort)(-20 - Random() % 50)));
                }
            }
            state = work.Freeze();
            DrawSprites(draws);
            if (DrawOnly)
            {
                return 0;
            }
            task = task.WriteWord(12, unchecked((ushort)(age + 1)));
            return age + 1 >= 70 ? 2 : 0;
        }

        private int Debris(ref NativeBattleModulePoolTask task)
        {
            using NativeBattleTornado146Work work = new(state);
            List<(NativeBattleVector Point, int Size, int Frame)> draws = [];
            int start = Word(task, 14);
            for (int index = start; index < start + 20; index++)
            {
                uint life = 0x24946E0u + (uint)index * 8;
                uint row = 0x2494AA8u + (uint)index * 16;
                int frame = work.SignedWord(life + 6);
                if (frame < 0)
                {
                    continue;
                }
                draws.Add((WorkVector(work, row), work.SignedWord(life), frame));
                if (!DrawOnly)
                {
                    work.WriteWord(life + 6, unchecked((ushort)(frame + 1)));
                    if (frame + 1 >= 16)
                    {
                        work.WriteWord(life + 6, ushort.MaxValue);
                    }
                    else
                    {
                        work.WriteWord(row, unchecked((ushort)(work.SignedWord(row) + work.SignedWord(row + 8))));
                        work.WriteWord(row + 4, unchecked((ushort)(work.SignedWord(row + 4) + work.SignedWord(row + 12))));
                        short x = work.SignedWord(row + 8);
                        short z = work.SignedWord(row + 12);
                        work.WriteWord(row + 8, unchecked((ushort)(x - (x >> 4))));
                        work.WriteWord(row + 12, unchecked((ushort)(z - (z >> 4))));
                    }
                }
            }
            if (!DrawOnly && Word(task, 12) == 0)
            {
                for (int index = start; index < start + 20; index++)
                {
                    uint life = 0x24946E0u + (uint)index * 8;
                    uint row = 0x2494AA8u + (uint)index * 16;
                    work.WriteWord(life + 6, 0);
                    work.WriteWord(life, (ushort)(Random() % 6144 + 2304));
                    work.WriteWord(row, task.ReadWord(16));
                    work.WriteWord(row + 2, 0);
                    work.WriteWord(row + 4, task.ReadWord(20));
                    int angle = Random() % 4096;
                    int speed = Random() % 160 + 90;
                    work.WriteWord(row + 8, unchecked((ushort)(NativeBattleCameraMath.Cosine(angle) * speed >> 12)));
                    work.WriteWord(row + 12, unchecked((ushort)(NativeBattleCameraMath.Sine(angle) * speed >> 12)));
                }
            }
            state = work.Freeze();
            DrawSprites(draws);
            if (DrawOnly)
            {
                return 0;
            }
            task = task.WriteWord(12, unchecked((ushort)(Word(task, 12) + 1)));
            return Word(task, 12) >= 17 ? 2 : 0;
        }

        private void DrawSprites(List<(NativeBattleVector Point, int Size, int Frame)> draws)
        {
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(owner.NativeFileStaging!, 180);
            owner.NativeFileStaging = NativeBattleModuleSpriteTemporary.Begin571C80(temporary.Staging, temporary.Address, resources.Spark.Address, 0);
            foreach (var draw in draws)
            {
                NativeBattleModuleDraw sprite = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resources.Spark, SpriteFrame = resources.Spark.ProjectedFrame571C80(draw.Frame), Phase = unchecked((ushort)draw.Frame), Anchor = new(draw.Point.X, draw.Point.Y, draw.Point.Z, 0), Scale = draw.Size, DepthOffset = -(draw.Size >> 3), SpriteFlags = 0 };
                NativeBattleModuleSpriteTemporaryResult rendered = NativeBattleModuleSpriteTemporary.Emit571C80(owner.NativeFileStaging!, temporary.Address, sprite, Projection, owner.NativeCamera!.Roll, 17, 2);
                owner.NativeFileStaging = rendered.Staging;
                Append(rendered.Batch.CapturedWrites ?? throw new InvalidDataException("Native Tornado requires its captured sprite stores."));
            }
            owner.NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(owner.NativeFileStaging!, 180);
        }

        private static NativeBattleVector WorkVector(NativeBattleTornado146Work work, uint address) => new(work.SignedWord(address), work.SignedWord(address + 2), work.SignedWord(address + 4));
    }
}
