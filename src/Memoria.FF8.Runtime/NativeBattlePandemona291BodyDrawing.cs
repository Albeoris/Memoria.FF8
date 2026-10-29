using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePandemona291BodyBounds(NativeBattlePandemona291Frame Frame, uint ModelAddress, int SkeletonOffset, int BoneCount, NativeBattleActionMatrix World, NativeBattleEffectLocalBounds Bounds, short GroundY, uint ShadowColor);
public sealed record NativeBattlePandemona291BodyDraw(NativeBattlePandemona291Frame Frame, NativeBattleGfGeometryBatch Packets, uint? SourceCursor);

public static class NativeBattlePandemona291BodyDrawing
{
    public static NativeBattlePandemona291BodyBounds Prepare6F0AE0(NativeBattlePandemona291Frame frame, uint taskAddress, short ir0)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona body requires its current arena.");
        uint model = unchecked(taskAddress + 0x154);
        uint directory = Dword(unchecked(model + 0x64));
        int skeleton = NativeBattleWaterExecutionFields.ArenaOffset(Dword(directory), 1);
        int count = arena.Read(skeleton, 1)[0];
        int rootX = BinaryPrimitives.ReadInt32LittleEndian(arena.Read(skeleton + 0x34, 4));
        int rootZ = BinaryPrimitives.ReadInt32LittleEndian(arena.Read(skeleton + 0x3C, 4));
        int minX = unchecked(rootX - 500);
        int maxX = unchecked(rootX + 500);
        int minZ = unchecked(rootZ - 500);
        int maxZ = unchecked(rootZ + 500);
        NativeBattleScriptedColorRegisters? registers = frame.Memory.ScriptedColorRegisters;
        for (int index = 0; index < count; index++)
        {
            int offset = checked(skeleton + 0x20 + index * 48);
            NativeBattleActionMatrix bone = NativeBattleArenaPose.ReadMatrix(arena, offset);
            short length = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset - 14, 2));
            NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(bone, new(0, 0, length));
            short x = Scale(mac.X);
            short y = Scale(mac.Y);
            short z = Scale(mac.Z);
            byte[] input = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(input.AsSpan(4), length);
            registers = (registers ?? throw new NotSupportedException("Native Pandemona bounds require its canonical GTE input owner.")) with { NormalInput = input.ToImmutableArray(), NormalInputKnown = ImmutableArray.Create(true, true, true, true, true, true, false, false), Mac = [Product(mac.X), Product(mac.Y), Product(mac.Z)], Ir = [x, y, z], Rgb = [null, null, null], Flags = null };
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minZ = Math.Min(minZ, z);
            maxZ = Math.Max(maxZ, z);
        }
        Store(0x34, unchecked((short)minX));
        Store(0x3A, unchecked((short)maxX));
        Store(0x3C, 0);
        Store(0x36, 0);
        Store(0x38, unchecked((short)minZ));
        Store(0x3E, unchecked((short)maxZ));
        NativeBattleActionMatrix world = NativeBattleArenaPose.ReadMatrix(arena, NativeBattleWaterExecutionFields.ArenaOffset(unchecked(model + 0x40), 32));
        NativeBattleEffectLocalBounds bounds = new(new(unchecked((short)minX), 0, unchecked((short)minZ)), new(unchecked((short)maxX), 0, unchecked((short)maxZ)));
        short ground = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(unchecked(model + 0x24), 2), 2));
        return new(frame with { Memory = frame.Memory with { Arena = arena, ScriptedColorRegisters = registers } }, model, skeleton, count, world, bounds, ground, Dword(unchecked(model + 0x2C)));

        int Product(int value) => NativePrecision53.RoundInteger(Math.Clamp(value, short.MinValue, short.MaxValue) * (ir0 / 4096d));
        short Scale(int value) => NativeBattlePandemona291Layers.DifferenceGpf45E9D0(0, unchecked((ushort)(short)Math.Clamp(value, short.MinValue, short.MaxValue)), ir0);
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, 4), 4));
        void Store(int offset, short value) => arena = arena.Write(NativeBattleWaterExecutionFields.ArenaOffset(unchecked(model + (uint)offset), 2), BitConverter.GetBytes(value));
    }

    public static NativeBattlePandemona291Frame Compose5095B0(NativeBattlePandemona291BodyBounds prepared, NativeBattleActionMatrix view)
    {
        NativeBattleEffectArena arena = prepared.Frame.Memory.Arena!;
        ImmutableArray<NativeBattleActionMatrix>.Builder local = ImmutableArray.CreateBuilder<NativeBattleActionMatrix>(prepared.BoneCount);
        for (int index = 0; index < prepared.BoneCount; index++)
        {
            local.Add(NativeBattleArenaPose.ReadMatrix(arena, prepared.SkeletonOffset + 0x20 + index * 48));
        }
        NativeBattleActionMatrix camera = NativeBattleGfMatrices.Compose56C2F0(view, prepared.World);
        ImmutableArray<NativeBattleActionMatrix> composed = NativeBattlePoseMatrices.ComposeStageCamera(local.MoveToImmutable(), camera, 0);
        for (int index = 0; index < composed.Length; index++)
        {
            arena = NativeBattleArenaPose.WriteMatrix(arena, prepared.SkeletonOffset + 0x20 + index * 48, composed[index], true);
        }
        return prepared.Frame with { Memory = prepared.Frame.Memory with { Arena = arena } };
    }

    public static NativeBattlePandemona291BodyDraw Render5099D0(NativeBattlePandemona291Frame frame, uint taskAddress, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Pandemona body drawing requires its current arena.");
        uint model = unchecked(taskAddress + 0x154);
        uint directory = Dword(unchecked(model + 0x64));
        uint skeletonAddress = Dword(directory);
        int skeleton = NativeBattleWaterExecutionFields.ArenaOffset(skeletonAddress, 1);
        int count = arena.Read(skeleton, 1)[0];
        uint geometry = Dword(unchecked(directory + 4));
        uint mask = Dword(unchecked(model + 0x7C));
        uint? sourceCursor = null;
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read((offset, length) => arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(unchecked(geometry + (uint)offset), length), length), count, (index, counts, _, end) =>
        {
            if (counts[2] != 0 || counts[3] != 0)
            {
                throw new NotSupportedException("Native Pandemona model's colored object groups require its alternate context color contract.");
            }
            if ((mask & (1u << (index & 31))) != 0)
            {
                sourceCursor = unchecked(geometry + (uint)end);
            }
        });
        int length = checked(meshes.Select((mesh, index) => (mask & (1u << (index & 31))) != 0 ? mesh.Vertices.Length * 8 : 0).DefaultIfEmpty().Max());
        int work = NativeBattleWaterExecutionFields.ArenaOffset(unchecked(frame.Memory.Pandemona291!.ModelBuffer25562F4!.Value + 0x31D4), length);
        ImmutableArray<NativeBattleActionMatrix>.Builder bones = ImmutableArray.CreateBuilder<NativeBattleActionMatrix>(count);
        for (int index = 0; index < count; index++)
        {
            bones.Add(NativeBattleArenaPose.ReadMatrix(arena, skeleton + 0x20 + index * 48));
        }
        NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(meshes, bones.MoveToImmutable(), projection, Dword(unchecked(model + 0x28)), mask, arena.ReadSnapshot(work, length).Data.ToImmutableArray(), colors, 4, new(0, 0, 320, 216), 17, 0, pilotIdentity: pilotIdentity);
        if (drawn.ProjectedBytesWritten != 0)
        {
            arena = arena.Write(work, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten));
        }
        return new(frame with { Memory = frame.Memory with { Arena = arena } }, drawn.Packets, sourceCursor);

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, 4), 4));
    }

    public static NativeBattlePandemona291Frame Restore508C90(NativeBattlePandemona291Frame frame, int skeleton, int count)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        NativeBattleDecodedPose pose = NativeBattleArenaPose.ReadCurrentPose(arena, skeleton, count);
        return frame with { Memory = frame.Memory with { Arena = NativeBattleArenaPose.RestoreBoneMatrices(arena, skeleton, pose) } };
    }
}
