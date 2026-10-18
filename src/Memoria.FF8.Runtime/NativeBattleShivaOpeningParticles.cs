using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShivaOpeningMeshDraw(NativeBattleStaticMeshResource Resource, int VertexArenaOffset, NativeBattleActionMatrix Matrix, NativeBattleModuleMeshContext Context, int MaximumCount, int WindowCount);
public sealed record NativeBattleShivaOpeningParticleInputs(NativeBattleActionMatrix EntryWorld, NativeBattleActionMatrix ViewWorld, NativeBattleStaticMeshResource Mesh, NativeBattleModuleResource FirstSprite, NativeBattleModuleResource ParticleSprite, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaOpeningMeshDraw, NativeBattleShivaEarlyTaskFrame> DrawMesh, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleModuleDraw, NativeBattleShivaEarlyTaskFrame> DrawSprite);

public static class NativeBattleShivaOpeningParticles
{
    public static NativeBattleShivaEarlyTaskStep Advance5C16B0(NativeBattleShivaEarlyTaskFrame frame, int slot, uint graphicsFlags, NativeBattleShivaOpeningParticleInputs inputs)
    {
        frame.Pool.Validate();
        if (frame.Pool.DescriptorAddress != 0x22BC198 || frame.Pool.BankAddress != 0x22BC1A8 || frame.Pool.Stride != 0x24 || frame.Pool.Slots.Length != 100 || (uint)slot >= frame.Pool.Slots.Length || frame.Pool.Slots[slot] is not { Callback: 0x5C16B0 } task || (task.Flags & 1) == 0)
        {
            throw new InvalidDataException("Native Shiva opening particles require their allocated5C16B0 task.");
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Shiva opening particles require their retained geometry and particle arena.");
        int counter = TaskWord(0x0C);
        bool paused = (graphicsFlags & 0x201) != 0;
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, TaskWord(0x18), 0));
        local = NativeBattlePoseMatrices.Scale56BEF0(local, TaskWord(0x1C), TaskWord(0x20), TaskWord(0x1C)) with { X = TaskWord(0x10), Y = TaskWord(0x12), Z = TaskWord(0x14) };
        int active = 0;
        if (counter < 60)
        {
            ArgumentNullException.ThrowIfNull(inputs.DrawMesh);
            if (inputs.Mesh.Address != 0xD215C4)
            {
                throw new InvalidDataException("Native Shiva opening body requires its originalD215C4 mesh.");
            }
            int maximum = Math.Min((int)TaskWord(0x22), 36);
            int window = counter < 18 ? 36 : Math.Max(72 - 2 * counter, 0);
            NativeBattleModuleMeshContext context = new(counter < 18 ? 0x2030u : 0x20F0u, 0, counter < 18 ? null : 4096 - (window << 12) / 36, null, null, null, 17, 2);
            NativeBattleActionMatrix meshMatrix = NativeBattlePoseMatrices.Compose56C2F0(inputs.ViewWorld, local);
            frame = inputs.DrawMesh(frame, new(inputs.Mesh, 0x33000, meshMatrix, context, maximum, window));
            arena = frame.Memory.Arena ?? throw new InvalidDataException("Native Shiva body drawing lost its retained arena.");
            active = 1;
        }
        NativeBattleActionMatrix worldLocal = NativeBattlePoseMatrices.Compose56C2F0(inputs.EntryWorld, local);
        if (counter < 29)
        {
            NativeBattleShivaRetainedVector current = frame.Geometry.Current;
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(worldLocal, new(current.X, current.Y, current.Z));
            NativeBattleModuleAnchor anchor = new(unchecked((short)(rotated.X + worldLocal.X)), unchecked((short)(rotated.Y + worldLocal.Y)), unchecked((short)(rotated.Z + worldLocal.Z)), 0);
            DrawSprite(inputs.FirstSprite, 0xD209F4, (ushort)(counter & 7), anchor, 2304, -144);
            active++;
        }
        uint mask = unchecked((uint)(int)TaskWord(0x0E));
        for (int index = 0; index < 150; index++)
        {
            int address = 0x2C000 + index * 24;
            if ((Dword(address) & mask) == 0)
            {
                continue;
            }
            ushort phase = Uword(address + 4);
            int size = Word(address + 6);
            short next = DrawSprite(inputs.ParticleSprite, 0xD20C7C, phase, new(Word(address + 8), Word(address + 10), Word(address + 12), 0), size, -(size >> 4));
            if (paused)
            {
                continue;
            }
            PutWord(address + 4, phase + 1);
            if (next < 0)
            {
                PutDword(address, 0);
                continue;
            }
            PutWord(address + 8, Word(address + 8) + Word(address + 16));
            PutWord(address + 10, Word(address + 10) + Word(address + 18));
            PutWord(address + 12, Word(address + 12) + Word(address + 20));
            active++;
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        if (paused)
        {
            return new(frame, 0);
        }
        task = frame.Pool.Slots[slot];
        task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + task.ReadWord(0x1A)))).WriteWord(0x22, unchecked((ushort)(task.ReadWord(0x22) + 2)));
        frame = frame with { Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(slot, task) } };
        if (counter is >= 0 and <= 30)
        {
            int start = frame.Geometry.Current.W;
            if (start is < 0 or > 72)
            {
                throw new InvalidDataException("Native Shiva particle spawning requires its actual0..72 retained start vertex.");
            }
            for (int emitted = 0; emitted < 3; emitted++)
            {
                int free = 0;
                while (free < 150 && Dword(0x2C000 + free * 24) != 0)
                {
                    free++;
                }
                if (free == 150)
                {
                    break;
                }
                int address = 0x2C000 + free * 24;
                PutDword(address, mask);
                PutWord(address + 4, 0);
                PutWord(address + 6, Draw() % 768 + 1024);
                int vertex = Draw() % (74 - start) + start;
                int source = 0x33000 + vertex * 8;
                NativeBattleVector position = NativeBattleGfMatrices.RotateShort56C4F0(worldLocal, new(Word(source), Word(source + 2), Word(source + 4)));
                PutWord(address + 8, position.X + worldLocal.X);
                PutWord(address + 10, position.Y + worldLocal.Y);
                PutWord(address + 12, position.Z + worldLocal.Z);
                int dx = Draw() % 4096 - 2048;
                int dy = Draw() % 4096 - 2048;
                int dz = Draw() % 4096 - 2048;
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, dy, dz);
                int speed = Draw() % 40 + 30;
                PutWord(address + 16, unchecked(direction.X * speed) >> 12);
                PutWord(address + 18, unchecked(direction.Y * speed) >> 12);
                PutWord(address + 20, unchecked(direction.Z * speed) >> 12);
            }
        }
        task = frame.Pool.Slots[slot].WriteWord(0x0C, unchecked((ushort)(frame.Pool.Slots[slot].ReadWord(0x0C) + 1)));
        frame = frame with { Memory = frame.Memory with { Arena = arena }, Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(slot, task) } };
        return new(frame, unchecked((short)task.ReadWord(0x0C)) >= 60 && active == 0 ? 2 : 0);

        short TaskWord(int offset) => unchecked((short)task.ReadWord(offset));
        ushort Uword(int address) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(address, 2));
        short Word(int address) => unchecked((short)Uword(address));
        uint Dword(int address) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(address, 4));

        short DrawSprite(NativeBattleModuleResource resource, uint expectedAddress, ushort phase, NativeBattleModuleAnchor anchor, int size, int depth)
        {
            ArgumentNullException.ThrowIfNull(inputs.DrawSprite);
            if (resource.Address != expectedAddress)
            {
                throw new InvalidDataException("Native Shiva opening particle sprite differs from its original resource.");
            }
            NativeBattleModuleSpriteFrame selected = resource.ProjectedFrame571C80(unchecked((short)phase));
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Resource = resource, SpriteFrame = selected, Phase = phase, Anchor = anchor, Scale = size, DepthOffset = depth, SpriteFlags = 0 };
            frame = inputs.DrawSprite(frame with { Memory = frame.Memory with { Arena = arena } }, draw);
            arena = frame.Memory.Arena ?? throw new InvalidDataException("Native Shiva sprite drawing lost its retained arena.");
            return selected.NextOffset;
        }

        ushort Draw()
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            return value;
        }

        void PutWord(int address, int value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
            arena = arena.Write(address, bytes);
        }

        void PutDword(int address, uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            arena = arena.Write(address, bytes);
        }
    }
}
