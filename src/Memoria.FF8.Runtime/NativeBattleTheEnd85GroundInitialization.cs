using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTheEnd85NodeServices(NativeBattleTheEnd85Resources Resources, NativeBattleActionPacketsState Packets, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, NativeBattleActionMatrix CopiedCamera, Func<NativeBattleTheEnd85Frame, uint, uint, uint, NativeBattleTheEnd85Frame> DrawNode)
{
    public NativeBattleEffectNodeDogOrigins? Mode2Origins { get; init; }
}
public sealed record NativeBattleTheEnd85NodeStep(NativeBattleTheEnd85Frame Frame, bool Complete);

public static class NativeBattleTheEnd85GroundInitialization
{
    public const uint ModelBankAddress = 0x17151EC;
    public const int ModelBankLength = 0xA0C4;

    public static NativeBattleTheEnd85Frame Initialize788C20(NativeBattleTheEnd85Frame frame, uint taskAddress, NativeBattleTheEnd85NodeServices services)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(taskAddress);
        uint source = task.ReadDword(48);
        NativeBattleTheEnd85State state = State(frame);
        ImmutableArray<byte> data = state.ModelData ?? services.Resources.Read(ModelBankAddress, ModelBankLength).ToImmutableArray();
        int directory = checked((int)(source - ModelBankAddress));
        if (directory < 0 || directory > data.Length - 0x104)
        {
            throw new InvalidDataException("Native The End grounded source leaves its retained executable model bank.");
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(directory)) == 0)
        {
            byte[] relocated = NativeBattleEffectNodeBankFixups.Relocate(data, ModelBankAddress, directory);
            BinaryPrimitives.WriteUInt32LittleEndian(relocated.AsSpan(directory), 1);
            data = relocated.ToImmutableArray();
        }
        task = NativeBattleEffectNodeContext.Initialize(task, hierarchy.Read(task.ReadDword(16)), frame.Effect.Parameters ?? throw new NotSupportedException("Native The End grounded initializer requires its current action parameters."), services.Packets, services.ReadModel, services.ReadPoseSource, source, dogOrigins: services.Mode2Origins);
        state = (state with { ModelData = data }).WriteDword(0x25C21DC, checked(taskAddress + 52));
        return frame with { Memory = frame.Memory with { TheEnd85 = state }, Effect = frame.Effect with { Pools = hierarchy.Write(taskAddress, task).Pools } };
    }

    public static NativeBattleTheEnd85NodeStep Advance789700(NativeBattleTheEnd85Frame frame, uint taskAddress, NativeBattleTheEnd85NodeServices services)
    {
        NativeBattleTheEnd85State state = State(frame).WriteDword(0x25C21DC, checked(taskAddress + 52));
        frame = frame with { Memory = frame.Memory with { TheEnd85 = state } };
        var context = NativeBattleEffectNodeLifecycle.AdvanceContext(ReadTask, WriteTask, () =>
        {
            state = State(frame);
            int first = ArenaOffset(state.Dword(0x25C1CB8));
            int second = ArenaOffset(state.Dword(0x25C21D0));
            NativeBattleTheEnd85SpawnScratchObserver scratch = new(NativeBattleEffectNodeContextMemory.Capture(frame.Memory));
            using NativeBattleEffectNodeMemory memory = new(frame.Memory, frame.Effect, taskAddress, first, 1944, second, 40320, useSharedScratch: true) { SpawnScratchObserver = scratch };
            NativeBattleEffectNodeBank bank = new(ModelBankAddress, state.ModelData ?? throw new NotSupportedException("Native The End grounded nodes require their initialized mutable model bank."), services.Resources.Read);
            ushort emitterCursor = state.Word(0x25BC890);
            ushort geometryCursor = state.Word(0x25C1CC4);
            NativeBattleEffectNodeInputs inputs = new(services.CopiedCamera, services.ReadModel, (parent, source, target) => NativeBattleEffectNodeAllocation.AllocateEmitter(memory, first, 18, ref emitterCursor, parent, source, target), (emitter, source) => NativeBattleEffectNodeAllocation.AllocateGeometry(memory, second, 60, ref geometryCursor, emitter, source));
            NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, inputs);
            NativeCrtRandomState random = frame.Random;
            lifecycle.Birth();
            lifecycle.AdvanceList(ref random);
            NativeBattleEffectNodeMemoryWrite written = memory.FlushStorage();
            state = (state with { ModelData = bank.Data }).Write(0x25BC890, BitConverter.GetBytes(emitterCursor)).Write(0x25C1CC4, BitConverter.GetBytes(geometryCursor));
            frame = new(NativeBattleEffectNodeContextMemory.Publish(written.Memory, scratch.View) with { TheEnd85 = state }, written.Effect, random);
            Draw(false);
            Draw(true);
        });
        state = State(frame);
        state = state.Write(0x25C21B0, BitConverter.GetBytes(unchecked((ushort)(state.Word(0x25C21B0) + context.EmitterCount)))).Write(0x25C1CB4, BitConverter.GetBytes(unchecked((ushort)(state.Word(0x25C1CB4) + context.GeometryCount))));
        frame = frame with { Memory = frame.Memory with { TheEnd85 = state } };
        return new(frame, context.Complete);

        void Draw(bool meshes)
        {
            uint head = ReadTask().ReadDword(52 + 44);
            for (uint node = head; node != 0; node = D(node + 4))
            {
                if (W(node + 8) != 1)
                {
                    continue;
                }
                int sourceIndex = unchecked((sbyte)Read(node + 470, 1)[0]);
                uint directory = ReadTask().ReadDword(600);
                uint model = BinaryPrimitives.ReadUInt32LittleEndian(ReadModel(directory + unchecked((uint)(sourceIndex * 4)), 4));
                byte kind = ReadModel(model + 22, 1)[0];
                if (!(meshes ? kind is 4 or 5 : kind <= 2) || D(node + 368) == 0)
                {
                    continue;
                }
                if (meshes)
                {
                    frame = services.DrawNode(frame, node, model, 0x7897F0);
                    continue;
                }
                int count = unchecked((sbyte)Read(node + 472, 1)[0]);
                if (count == 1)
                {
                    frame = services.DrawNode(frame, node, model, 0x789A10);
                }
                else
                {
                    for (int index = 0; index < count; index++)
                    {
                        for (int axis = 0; axis < 3; axis++)
                        {
                            Write(node + 192 + (uint)(axis * 4), BitConverter.GetBytes(unchecked((int)(short)W(node + 412 + (uint)(index * 8 + axis * 2)))));
                        }
                        frame = services.DrawNode(frame, node, model, 0x789A10);
                    }
                }
            }
        }
        byte[] ReadModel(uint address, int length)
        {
            ImmutableArray<byte> data = State(frame).ModelData!.Value;
            if (address >= ModelBankAddress && (ulong)address - ModelBankAddress + (uint)length <= (uint)data.Length)
            {
                return data.AsSpan().Slice(checked((int)(address - ModelBankAddress)), length).ToArray();
            }
            return services.Resources.Read(address, length);
        }
        byte[] Read(uint pointer, int length) => (frame.Memory.Arena ?? throw new NotSupportedException("Native The End node arena is missing.")).Read(ArenaOffset(pointer), length);
        uint D(uint pointer) => BinaryPrimitives.ReadUInt32LittleEndian(Read(pointer, 4));
        ushort W(uint pointer) => BinaryPrimitives.ReadUInt16LittleEndian(Read(pointer, 2));
        void Write(uint pointer, byte[] data) => frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(ArenaOffset(pointer), data) } };
        NativeBattleModulePoolTask ReadTask() => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(taskAddress);
        void WriteTask(NativeBattleModulePoolTask task) => frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(taskAddress, task).Pools } };
    }

    private static NativeBattleTheEnd85State State(NativeBattleTheEnd85Frame frame) => frame.Memory.TheEnd85 ?? throw new NotSupportedException("Native The End requires its retained module owner.");
    private static int ArenaOffset(uint pointer)
    {
        if (pointer < 0x20DFAB8 || pointer >= 0x21DFAB8)
        {
            throw new InvalidDataException("Native The End node pointer leaves its actual arena.");
        }
        return checked((int)(pointer - 0x20DFAB8));
    }
}
