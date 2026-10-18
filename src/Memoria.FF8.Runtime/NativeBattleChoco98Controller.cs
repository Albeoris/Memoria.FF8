namespace Memoria.FF8.Runtime;

public static class NativeBattleChoco98Controller
{
    public const uint MainDescriptor = 0x256E538;
    public const uint MainBank = 0x256E470;

    public static NativeBattleChoco98Frame Initialize721860(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleActionPacketsState packets, NativeBattleChoco98Resources resources, NativeCrtRandomState random)
    {
        return FromFamily(NativeBattleChoco97Controller.InitializeFamily(memory, effect, packets, NativeBattleChocoControllerResources.From(resources), random, true));
    }

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value) => NativeBattleChoco97Controller.WriteCompletionByteFamily(effect, address, value, true);

    public static NativeBattleChoco98Frame Advance7219D0(NativeBattleChoco98Frame frame, NativeBattleChoco98Inputs inputs, NativeBattleChoco98Resources resources)
    {
        NativeBattleChoco97Inputs adapted = new(inputs.Packets, inputs.View, inputs.ReadModel, inputs.ReadPoseSource, (current, task, model) =>
        {
            NativeBattleChoco98ModelDrawStep step = inputs.DrawModel(FromFamily(current), task, model);
            return new(ToFamily(step.Frame), step.Drawing);
        }, (current, task) =>
        {
            NativeBattleChoco98ProgramStep step = inputs.AdvanceProgram(FromFamily(current), task);
            return new(ToFamily(step.Frame), step.Task, step.Remaining);
        }, (current, address) =>
        {
            NativeBattleChoco98NodesStep step = inputs.AdvanceNodes(FromFamily(current), address);
            return new(ToFamily(step.Frame), step.Complete);
        }, (current, task) =>
        {
            NativeBattleChoco98SpriteDrawStep step = inputs.AdvanceSprite(FromFamily(current), task);
            return new(ToFamily(step.Frame), step.Task, step.Complete);
        }, current =>
        {
            NativeBattleChoco98VibrationStep step = inputs.StartVibration(FromFamily(current));
            return new(ToFamily(step.Frame), step.Handle);
        }, (current, operation) => ToFamily(inputs.Execute(FromFamily(current), operation)));
        return FromFamily(NativeBattleChoco97Controller.AdvanceFamily(ToFamily(frame), adapted, NativeBattleChocoControllerResources.From(resources), true));
    }

    private static NativeBattleChoco97Frame ToFamily(NativeBattleChoco98Frame frame) => new(frame.Memory, frame.Effect, frame.Random);
    private static NativeBattleChoco98Frame FromFamily(NativeBattleChoco97Frame frame) => new(frame.Memory, frame.Effect, frame.Random);
}
