using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshModelBankStep(NativeBattleGilgameshModelMemory ModelMemory, NativeBattleEffectArena Arena, NativeBattleModulePoolTask UpdatedTask, ImmutableArray<NativeBattleGfGeometryBatch> Packets, int Result);

/// <summary>595D50/5966B0/596710/596800/596970/596A60 retain caller-owned raw model banks and reuse the current shared core.</summary>
public static class NativeBattleGilgameshModelBanks
{
    private const uint A = 0x21FF760;
    private const uint AControl = 0x21FF4B8;
    private const uint B = 0x2200F00;
    private const uint BControl = 0x21FF2B0;
    private const uint C = 0x2200FC0;
    private const uint CControl = 0x2200EC8;

    public static NativeBattleGilgameshModelBankStep Step(NativeBattleGilgameshModelMemory modelMemory, NativeBattleEffectArena arena, NativeBattleModulePoolTask task, NativeBattleGilgameshModelResources resources, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint graphicsFlags)
    {
        if (task.Payload.IsDefault || task.PayloadLength != 24 || task.Callback is not (0x595D50 or 0x5966B0 or 0x596710 or 0x596800 or 0x596970 or 0x596A60) || resources.Stream749Models.IsDefault || resources.Stream749Models.Length != 5 || resources.StaticModels.IsDefault || resources.StaticModels.Length != 4)
        {
            throw new InvalidDataException("Native Gilgamesh model bank callback requires its raw0x24-byte task and all actual model resource owners.");
        }
        bool paused = (graphicsFlags & 0x201) != 0;
        int counter = unchecked((short)task.ReadWord(12));
        ImmutableArray<NativeBattleGfGeometryBatch>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleGfGeometryBatch>();
        int duration;
        switch (task.Callback)
        {
            case 0x595D50:
                if ((Read(Sword(0), SwordControl(0)).Model[0] & 4) == 0)
                {
                    for (int index = 0; index < 4; index++)
                    {
                        if (index > 0)
                        {
                            Copy(Sword(0), SwordControl(0), Sword(index), SwordControl(index), false, 0);
                        }
                        Draw(Sword(index), SwordControl(index), resources.StaticModels[index]);
                    }
                }
                if (!paused && (Read(Sword(0), SwordControl(0)).Model[0] & 8) == 0)
                {
                    for (int index = 0; index < 4; index++)
                    {
                        Advance(Sword(index), SwordControl(index), resources.StaticModels[index]);
                    }
                }
                duration = unchecked((short)task.ReadWord(14));
                break;
            case 0x5966B0:
                if ((Read(A, AControl).Model[0] & 4) == 0)
                {
                    Draw(A, AControl, resources.StreamModel746);
                }
                if (!paused && (Read(A, AControl).Model[0] & 8) == 0)
                {
                    Advance(A, AControl, resources.StreamModel746);
                }
                duration = 105;
                break;
            case 0x596710:
                if (counter == 0)
                {
                    Identity(B, BControl, -1000);
                    Initialize(B, BControl, resources.Stream749Models[0], 128, 0);
                    for (int index = 0; index < 4; index++)
                    {
                        Initialize(Clone(index), CloneControl(index), resources.Stream749Models[index + 1], 64, (short)index);
                    }
                }
                duration = 42;
                break;
            case 0x596800:
                if ((Read(B, BControl).Model[0] & 4) == 0)
                {
                    Draw(B, BControl, resources.Stream749Models[0], 3);
                    for (int index = 0; index < 4; index++)
                    {
                        Copy(B, BControl, Clone(index), CloneControl(index), true, 80);
                        Draw(Clone(index), CloneControl(index), resources.Stream749Models[index + 1]);
                    }
                }
                if (!paused && (Read(B, BControl).Model[0] & 8) == 0)
                {
                    Advance(B, BControl, resources.Stream749Models[0]);
                    for (int index = 0; index < 4; index++)
                    {
                        Advance(Clone(index), CloneControl(index), resources.Stream749Models[index + 1]);
                    }
                }
                duration = 42;
                break;
            case 0x596970:
                if (counter == 0)
                {
                    Identity(C, CControl, 0);
                    Initialize(C, CControl, resources.StreamModel751, 15, 0);
                    for (int index = 0; index < 4; index++)
                    {
                        Initialize(Sword(index), SwordControl(index), resources.StaticModels[index], 16, 7);
                    }
                }
                duration = 50;
                break;
            case 0x596A60:
                if ((Read(C, CControl).Model[0] & 4) == 0)
                {
                    Draw(C, CControl, resources.StreamModel751);
                    for (int index = 0; index < 4; index++)
                    {
                        Copy(C, CControl, Sword(index), SwordControl(index), true, 0);
                        Draw(Sword(index), SwordControl(index), resources.StaticModels[index]);
                    }
                }
                if (!paused && (Read(B, BControl).Model[0] & 8) == 0)
                {
                    Advance(C, CControl, resources.StreamModel751);
                    for (int index = 0; index < 4; index++)
                    {
                        Advance(Sword(index), SwordControl(index), resources.StaticModels[index]);
                    }
                }
                duration = 50;
                break;
            default:
                throw new InvalidDataException("Native Gilgamesh model bank callback left its finite dispatch.");
        }
        if (paused)
        {
            return new(modelMemory, arena, task, packets.ToImmutable(), 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return new(modelMemory, arena, task, packets.ToImmutable(), unchecked((short)task.ReadWord(12)) >= duration ? 2 : 0);

        NativeBattleCactuarRawModelState Read(uint address, uint controlAddress)
        {
            NativeBattleGilgameshModelOwner owner = modelMemory.Read(address);
            if (owner.ControlAddress != controlAddress)
            {
                throw new InvalidDataException("Native Gilgamesh model bank changed its actual control owner.");
            }
            NativeBattleCactuarRawModelState model = owner.State;
            if (model.Model.IsDefault || model.Model.Length != 0x90 || model.Control.IsDefault || model.Control.Length != 0x34 || model.ProjectedScratch is not null)
            {
                throw new InvalidDataException("Native Gilgamesh model bank requires actual retained raw model/control bytes and one separate projected scratch owner.");
            }
            return model;
        }

        void Publish(uint address, uint controlAddress, NativeBattleCactuarRawModelState model)
        {
            modelMemory = modelMemory.Write(address, model);
        }

        void Initialize(uint address, uint controlAddress, NativeBattleEffectOwnedModelResource resource, ushort argument, short animation)
        {
            NativeBattleRawModelBinding binding = new(controlAddress, 4);
            NativeBattleCactuarRawModelState model = NativeBattleCactuarRawModel.Initialize5A8F40(Read(address, controlAddress), resource, resource.Address, argument, binding);
            Publish(address, controlAddress, NativeBattleCactuarRawModel.Select5A8FB0(model, resource, animation, binding));
        }

        void Draw(uint address, uint controlAddress, NativeBattleEffectOwnedModelResource resource, byte depthShift = 4)
        {
            NativeBattleGilgameshRawModelDraw draw = NativeBattleGilgameshRawModel.Draw595E40(Read(address, controlAddress), resource, controlAddress, arena, worldView, projection, colors, depthShift);
            Publish(address, controlAddress, draw.State);
            arena = draw.Arena;
            packets.Add(draw.Packets);
        }

        void Advance(uint address, uint controlAddress, NativeBattleEffectOwnedModelResource resource)
        {
            Publish(address, controlAddress, NativeBattleCactuarRawModel.Advance5A8FD0(Read(address, controlAddress), resource, new(controlAddress, 4)).State);
        }

        void Identity(uint address, uint controlAddress, int y)
        {
            NativeBattleCactuarRawModelState original = Read(address, controlAddress);
            byte[] bytes = original.Model.ToArray();
            bytes.AsSpan(0x40, 16).Clear();
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x40), 4096);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x48), 4096);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0x50), 4096);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x54), 0);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x58), y);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x5C), 0);
            Publish(address, controlAddress, original with { Model = bytes.ToImmutableArray() });
        }

        void Copy(uint sourceAddress, uint sourceControl, uint destinationAddress, uint destinationControl, bool flags, int zOffset)
        {
            NativeBattleCactuarRawModelState source = Read(sourceAddress, sourceControl);
            NativeBattleCactuarRawModelState destination = Read(destinationAddress, destinationControl);
            byte[] bytes = destination.Model.ToArray();
            source.Model.AsSpan().Slice(0x40, 32).CopyTo(bytes.AsSpan(0x40));
            source.Model.AsSpan().Slice(0x28, 4).CopyTo(bytes.AsSpan(0x28));
            if (flags)
            {
                source.Model.AsSpan().Slice(0, 2).CopyTo(bytes);
            }
            if (zOffset != 0)
            {
                int z = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0x5C));
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x5C), unchecked(z + zOffset));
            }
            Publish(destinationAddress, destinationControl, destination with { Model = bytes.ToImmutableArray() });
        }
    }

    private static uint Sword(int index) => checked(0x21FF4F0u + (uint)index * 0x9C);
    private static uint SwordControl(int index) => checked(0x2200D20u + (uint)index * 0x34);
    private static uint Clone(int index) => checked(0x21FF800u + (uint)index * 0x9C);
    private static uint CloneControl(int index) => checked(0x21FFC78u + (uint)index * 0x34);
}
