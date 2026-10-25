using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicActorAnimation2CSource(NativeBattleEffectArena Input, NativeBattleEffectArena Resources, byte[] Skeleton, uint Color, uint ObjectMask);
public sealed record NativeBattleCinematicActorAnimation2CServices(Func<uint, NativeBattleCinematicActorAnimation2CSource> CaptureActor, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareBasis, NativeBattleCinematicModelServices Model, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleCinematicActorCloneDraw> DrawBody);

/// <summary>One actor's independent animation copy over current physical resource owners.</summary>
public static class NativeBattleCinematicActorAnimation2C
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicActorAnimation2CServices services)
    {
        uint clone = SlotDword(0xBC);
        if (clone == 0)
        {
            uint header = CoreDword(frame, 0x279744C);
            byte actorIndex = ReadSlot(frame, 0x1B, 1)[0];
            uint actor = CoreDword(frame, unchecked(header + 0x60u + actorIndex * 4u));
            NativeBattleCinematicActorAnimation2CSource source = services.CaptureActor(actor);
            if (source.Skeleton.Length == 0 || source.Skeleton.Length != checked(16 + source.Skeleton[0] * 48))
            {
                throw new InvalidDataException("Native actor animation requires the complete current variable skeleton records.");
            }
            (frame, clone) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 0x80);
            StoreDword(clone + 0xC, actor);
            CopySnapshot(source.Input, 0, clone + 0x10, 12);
            CopySnapshot(source.Resources, 0, clone + 0x30, 16);
            StoreDword(clone + 0x54, 0);
            StoreDword(clone + 0x58, 0x00D00140);
            StoreDword(clone + 0x78, source.Color);
            StoreDword(clone + 0x5C, source.Color);
            StoreDword(clone + 0x60, source.ObjectMask);
            StoreWord(clone + 0x64, 0);
            StoreDword(clone + 0x68, 0);
            StoreDword(clone + 0x50, uint.MaxValue);
            StoreWord(clone, 0);
            StoreWord(clone + 2, 0);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(clone + 0x21, 1), [0]) };
            frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0xBC, 4), BitConverter.GetBytes(clone)) };
            uint skeleton;
            (frame, skeleton) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, checked((uint)source.Skeleton.Length));
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(skeleton, source.Skeleton.Length), source.Skeleton) };
            StoreDword(clone + 0x30, skeleton);
            StoreDword(clone + 0x14, clone + 0x30);
            uint payload = SlotDword(0xB8);
            short animation = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(payload, 2), 2));
            if (animation is < 0 or > byte.MaxValue)
            {
                throw new NotSupportedException("Native actor animation requires its selected BYTE animation index.");
            }
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(clone + 9, 1), [(byte)animation]) };
            frame = NativeBattleCinematicModel.Select509440(frame, clone, (byte)animation, services.Model);
        }
        if (CoreDword(frame, 0x27973B8) != unchecked(frame.TemporaryFrame.Address + 0x100))
        {
            throw new NotSupportedException("Native actor animation requires its actual callback pause/control binding.");
        }
        frame = services.PrepareBasis(frame);
        frame = NativeBattleCinematicModel.AdvanceAndCompose(frame, clone, services.Model);
        StoreDword(clone + 0x5C, BlendColor(ReadDword(clone + 0x78), SlotDword(0xCC)));
        uint manager = CoreDword(frame, 0x27973EC);
        StoreDword(clone + 0x44, CoreDword(frame, manager + 0x74));
        NativeBattleCinematicActorCloneDraw drawn = services.DrawBody(frame, clone);
        return WriteCoreDword(drawn.Frame, CoreDword(drawn.Frame, 0x27973EC) + 0x7C, drawn.Cursor);

        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        void StoreDword(uint address, uint value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(value)) };
        void StoreWord(uint address, ushort value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
        void CopySnapshot(NativeBattleEffectArena source, int offset, uint destination, int length)
        {
            (byte[] data, bool[] known) = source.ReadSnapshot(offset, length);
            int target = ArenaOffset(destination, length);
            NativeBattleEffectArena arena = frame.Arena.Write(target, data);
            for (int index = 0; index < length; index++)
            {
                if (!known[index])
                {
                    arena = arena.MarkUnknown(target + index, 1);
                }
            }
            frame = frame with { Arena = arena };
        }
    }

    public static uint BlendColor(uint original, uint control)
    {
        uint result = control & 0x02000000;
        for (int shift = 0; shift < 24; shift += 8)
        {
            int value = (int)((original >> shift) & 255) + (int)((control >> shift) & 255) - 128;
            result |= (uint)Math.Clamp(value, 0, 255) << shift;
        }
        return result;
    }

    public static NativeBattleEffectMemory PublishProjectionRegisters(NativeBattleEffectMemory memory, NativeBattleEffectArena arena, uint skeleton, ImmutableArray<NativeBattleRawMesh> meshes, uint objectMask)
    {
        NativeBattleRawVertexGroup? last = null;
        for (int index = 0; index < meshes.Length; index++)
        {
            if ((objectMask & (1u << (index & 31))) == 0)
            {
                continue;
            }
            ImmutableArray<NativeBattleRawVertexGroup> groups = meshes[index].PackedVertexGroups ?? throw new NotSupportedException("Native actor projection requires its actual packed vertex-group order.");
            if (groups.IsDefault)
            {
                throw new NotSupportedException("Native actor projection requires its observed packed vertex groups.");
            }
            foreach (NativeBattleRawVertexGroup group in groups)
            {
                last = group;
            }
        }
        if (last is null)
        {
            return memory;
        }
        int boneCount = arena.Read(ArenaOffset(skeleton, 1), 1)[0];
        if (last.Bone >= boneCount)
        {
            throw new InvalidDataException("Native actor projection selected an absent copied bone.");
        }
        int offset = ArenaOffset(skeleton + 0x20u + 0x30u * last.Bone, 32);
        NativeBattleActionMatrix matrix = NativeBattleArenaPose.ReadMatrix(arena, offset);
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 32);
        short? padding = known[18] && known[19] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(18, 2)) : null;
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(memory.GeometryRegisters ?? throw new NotSupportedException("Native actor projection requires its retained geometry registers."), matrix, true, true, padding);
        geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => known[index] ? (byte?)bytes[index] : null).ToImmutableArray() };
        return memory with { GeometryRegisters = geometry };
    }

    public static void RequireSelectedPrimitiveGroups(ImmutableArray<ushort> counts)
    {
        if (counts.IsDefault || counts.Length != 4)
        {
            throw new InvalidDataException("Native actor animation requires all four actual primitive group counts.");
        }
        if (counts[2] != 0 || counts[3] != 0)
        {
            throw new NotSupportedException("Native actor animation colored primitives require their current color-operation feedback contract.");
        }
    }
}
