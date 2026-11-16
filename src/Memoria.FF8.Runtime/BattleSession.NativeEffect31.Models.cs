using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattlePoseSource ReadNativeEffect31Pose(NativeBattleGraphicsState graphics, NativeBattleEffect31Frame frame, BattleModelOwner actor)
    {
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect31 requires its current addressed pose owner.");
        return NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect31 requires its current physical skeleton.");
    }

    private NativeBattleEffectArena ReadNativeEffect31Physical(NativeBattleGraphicsState graphics, NativeBattleEffect31Frame frame, uint address, int length, NativeBattleHostedGuestModel? hosted = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (NativeBattleHostedGuestModel.TryResolveBank(address, out int bankId))
        {
            if (hosted is not null)
            {
                return hosted.Read(address, length);
            }
            NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new InvalidDataException("Effect31 hosted guest read requires the live CPU owner.");
            BattlePartyModelOwner owner = cpu.Cpu.PartyBankOwners.Where(pair => pair.Value == bankId).Select(pair => pair.Key).SingleOrDefault()
                ?? throw new InvalidDataException("Effect31 hosted guest address has no live party owner.");
            NativeBattlePoseSource pose = ReadNativeEffect31Pose(graphics, frame, owner);
            return ReadNativeEffect31GuestModel(graphics, frame, owner, pose).Read(address, length);
        }
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, length);
        if (TryResolveNativeModelRead(address, length, out int modelOffset))
        {
            if (length > NativeBattleGraphicsModelAddress.RowSize - modelOffset)
            {
                throw new InvalidDataException("Effect31 physical read crosses a current model row.");
            }
            (NativeBattleModelSlot slot, int offset) = NativeBattleGraphicsModelAddress.ResolveOriginalField(address, length);
            return NativeBattleEffectNodeContextMemory.Copy(CaptureNativeEffect301Model(graphics.NativeBindings.ReadOriginalCell(slot, graphics.ActorModels)), offset, result, 0, length);
        }
        if (address >= 0x1D99A78 && (ulong)address + (uint)length <= 0x1D99A80)
        {
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect31 requires its current action parameters.");
            return result.Write(0, NativeBattleCinematicParameterBytes.Read(parameters, checked((int)(address - 0x1D99A78)), length));
        }
        if (TryReadOriginalHeaderBytes(address, length, out byte[] header))
        {
            return result.Write(0, header);
        }
        if (TryResolveNativeContactRead(address, length, out BattleContactOccurrence row, out int contactOffset))
        {
            if (length > NativeBattleContactAddress.RowSize - contactOffset)
            {
                throw new InvalidDataException("Effect31 physical read crosses a current contact row.");
            }
            return result.Write(0, ReadOriginalContactBytes(row, contactOffset, length));
        }
        for (int bank = 0; bank < 3; bank++)
        {
            uint first = 0x196B018u + (uint)bank * 0x10000;
            if (address >= first && (ulong)address + (uint)length <= (ulong)first + 0x10000)
            {
                NativeBattleModelCpuBank cpu = (NativeModelCpuResources ?? throw new NotSupportedException("Effect31 requires its current CPU banks.")).Cpu.Banks[bank];
                int relative = checked((int)(address - first));
                foreach (NativeBattleEffectMemoryRegion region in cpu.KnownRanges)
                {
                    int begin = Math.Max(relative, region.Offset);
                    int end = Math.Min(relative + length, region.Offset + region.Data.Length);
                    if (begin < end)
                    {
                        result = result.Write(begin - relative, region.Data.AsSpan(begin - region.Offset, end - begin));
                    }
                }
                return result;
            }
        }
        return NativeBattleEffect31Controller.ReadBytes(frame, address, length);
    }

    private NativeBattleHostedGuestModel ReadNativeEffect31GuestModel(NativeBattleGraphicsState graphics, NativeBattleEffect31Frame frame, BattlePartyModelOwner owner, NativeBattlePoseSource pose)
    {
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new InvalidDataException("Effect31 hosted guest placement requires the live CPU owner.");
        NativeBattleModelTextureCache cache = NativeModelTextureCache ?? throw new InvalidDataException("Effect31 hosted guest placement requires the live body row owner.");
        NativeBattleFileStaging staging = frame.Staging ?? throw new InvalidDataException("Effect31 hosted guest placement requires the current staging owner.");
        return NativeBattleHostedGuestModel.Open(cpu, cache, staging, graphics, owner, pose, CaptureNativeEffect301Model);
    }

    private NativeBattleResourcePointer ReadNativeEffect31SkeletonPointer(NativeBattleGraphicsState graphics, NativeBattleEffect31Frame frame, BattleModelOwner actor)
    {
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(actor);
        if (model.ActiveStagedResourceRow100 is uint staged)
        {
            NativeBattleResourcePointer? pointer;
            if (!NativeBattleEffectTaskMemory.TryReadPointer(frame.Effect.Pools, staged, out pointer))
            {
                pointer = IsNativeEffectArenaAddress(staged) ? (frame.Memory.Arena ?? throw new NotSupportedException("Effect31 requires its current resource arena.")).ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(staged, 4)) : (frame.Staging ?? throw new NotSupportedException("Effect31 requires its current skeleton directory.")).ReadResourcePointer(staged);
            }
            return pointer ?? throw new NotSupportedException("Effect31 requires its actual staged skeleton pointer.");
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect31 requires its current CPU resources.");
        return new(cpu.Rows[model.ActiveResourceRow100 ?? throw new NotSupportedException("Effect31 requires its current CPU resource row.")].Require(12), null);
    }

    private NativeBattleResourcePointer ReadNativeEffect31BonePointer(NativeBattleGraphicsState graphics, NativeBattleEffect31Frame frame, BattleModelOwner actor, int bone)
    {
        NativeBattleResourcePointer pointer = ReadNativeEffect31SkeletonPointer(graphics, frame, actor);
        int offset = checked(32 + bone * 48);
        if (!pointer.HasValidOwner() || pointer.CpuRow is not null || bone < 0 || pointer.Length is int extent && offset > extent - 32)
        {
            throw new InvalidDataException("Effect31 selected bone matrix leaves its skeleton section.");
        }
        return pointer with { Cpu = pointer.Cpu is { } reference ? reference.Advance(offset) : null, StagingAddress = pointer.StagingAddress is uint address ? checked(address + (uint)offset) : null, Length = 32 };
    }

    private ImmutableArray<NativeBattlePackedMatrix32> ReadNativeEffect31BodyBones(NativeBattleGraphicsState graphics, NativeBattleEffect31Frame frame, BattleModelOwner actor, NativeBattlePoseSource source)
    {
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(graphics.ActorModels.Get(actor), source);
        ImmutableArray<NativeBattlePackedMatrix32>.Builder result = ImmutableArray.CreateBuilder<NativeBattlePackedMatrix32>(matrices.Length);
        NativeBattleHostedGuestModel? hosted = null;
        for (int bone = 0; bone < matrices.Length; bone++)
        {
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrices[bone], null);
            NativeBattleResourcePointer pointer = ReadNativeEffect31BonePointer(graphics, frame, actor, bone);
            (byte[] bytes, bool[] known) = pointer.Cpu is { IsPresentationResource: true } && actor is BattlePartyModelOwner party
                ? ReadHostedPadding(party, pointer)
                : NativeBattleModelSectionSnapshot.Read(NativeModelCpuResources, frame.Staging!, pointer, 18, 2, (address, count) => ReadNativeEffect31Physical(graphics, frame, address, count).ReadSnapshot(0, count));
            result.Add(packed with { Bytes = packed.Bytes.SetItem(18, bytes[0]).SetItem(19, bytes[1]), Known = packed.Known.SetItem(18, known[0]).SetItem(19, known[1]) });
        }
        return result.ToImmutable();

        (byte[] Bytes, bool[] Known) ReadHostedPadding(BattlePartyModelOwner party, NativeBattleResourcePointer pointer)
        {
            NativeBattleHostedGuestModel placement = hosted ??= ReadNativeEffect31GuestModel(graphics, frame, party, source);
            uint address = placement.RequireBoneMatrixAddress(pointer);
            return ReadNativeEffect31Physical(graphics, frame, checked(address + 18), 2, placement).ReadSnapshot(0, 2);
        }
    }

    private NativeBattleEffect31Frame ObserveNativeEffect31Point(NativeBattleEffect31Frame frame, NativeBattleBoneAnchorProducer producer)
    {
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect31 requires its current temporary owner.");
        NativeBattleEffectMemory memory = ObserveNativeBoneAnchor(frame.Memory, producer);
        return frame with { Memory = memory, Staging = NativeFileStaging };
    }

    private NativeBattleEffect31PointStep ReadNativeEffect31Point(NativeBattleGraphicsState graphics, NativeBattleEffect31Frame frame, BattleModelOwner actor, int selector, int fraction, NativeBattleEffectArena retained)
    {
        NativeBattleGraphicsModel model = graphics.ActorModels.Get(actor);
        NativeBattlePoseSource? source = (model.Flags0 & 2) == 0 ? null : ReadNativeEffect31Pose(graphics, frame, actor);
        NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source!, checked((byte)selector), checked((short)fraction));
        frame = ObserveNativeEffect31Point(frame, producer);
        NativeBattleVector point = producer.Enabled ? new(unchecked((short)frame.Memory.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)frame.Memory.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)frame.Memory.ScriptedColorRegisters.Mac[2]!.Value)) : producer.DisabledPosition;
        return new(frame, retained.Write(0, BitConverter.GetBytes(unchecked((short)point.X))).Write(2, BitConverter.GetBytes(unchecked((short)point.Y))).Write(4, BitConverter.GetBytes(unchecked((short)point.Z))));
    }
}
