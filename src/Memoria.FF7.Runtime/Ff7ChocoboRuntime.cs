using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7ChocoboRuntime
{
    private readonly byte[] parameters;
    private readonly Ff7ChocoboSimulation simulation;
    private readonly Ff7ChocoboNativeData native;
    private readonly int[] riderAnimations = new int[6];
    private int fadeStep = -16;
    private bool automaticFadeRequested;
    private ushort input;
    private ushort held;
    private ushort previousInput;

    internal Ff7ChocoboRuntime(FieldHostRequest request, byte[] parameters, PcChocoboCourse course, Ff7ChocoboNativeData native, uint seed, Ff7ChocoboSnapshot? saved = null, Ff7ChocoboCarryState? carry = null)
    {
        if (request.Command is not FieldMinigameCommand { Game: 1 } || parameters.Length != 29)
        {
            throw new InvalidDataException("The FFVII Chocobo module requires its original departure and complete parameters.");
        }
        if (parameters[8] == 0)
        {
            throw new NotSupportedException("FFVII Chocobo betting requires its native ticket and prize lifecycle.");
        }
        Request = request;
        this.parameters = parameters.ToArray();
        Course = course;
        this.native = native;
        carry?.Validate();
        simulation = new(course, native, parameters, seed, saved?.Simulation, carry?.Simulation.Racers);
        RecoverySide = carry?.RecoverySide ?? false;
        RecoveryCount = carry?.RecoveryCount ?? 0;
        Camera = new(simulation, native, saved?.Camera, carry?.Camera);
        simulation.CameraEvent += (_, _) => Camera.NativeEvent();
        simulation.CollisionVolume = Camera.CollisionVolume;
        simulation.RacerAdvanced += Camera.UpdateVisibility;
        InitializeScenery(saved?.Scenery);
        simulation.Audio += (opcode, first, second) => Audio?.Invoke(new(opcode, first, second));
        Camera.SoundEvent += index =>
        {
            IReadOnlyList<Ff7ChocoboSoundEvent> sounds = parameters[7] == 0 ? native.LongSounds : native.ShortSounds;
            if ((uint)index >= sounds.Count)
            {
                throw new InvalidDataException("The FFVII Chocobo track references an absent native sound event.");
            }
            Ff7ChocoboSoundEvent sound = sounds[index];
            if (sound.Parameters[5] != 0)
            {
                throw new NotSupportedException("FFVII Chocobo sound events require their original zero sixth operand.");
            }
            Audio?.Invoke(new(sound.Operation, sound.Parameters[0], sound.Parameters[1], sound.Parameters[2], sound.Parameters[3], sound.Parameters[4]));
        };
        if (saved is not null)
        {
            saved.Validate();
            if (saved.Request != request || !saved.Parameters.SequenceEqual(parameters))
            {
                throw new InvalidDataException("The FFVII Chocobo snapshot no longer matches its field departure.");
            }
            Tick = saved.Tick;
            Countdown = saved.Countdown;
            Started = saved.Started;
            FadeAlpha = saved.FadeAlpha;
            DisplayedFadeAlpha = saved.DisplayedFadeAlpha ?? saved.FadeAlpha;
            fadeStep = saved.FadeStep;
            RecoverySide = saved.RecoverySide;
            RecoveryCount = saved.RecoveryCount;
            held = saved.Held;
            previousInput = saved.PreviousInput;
            input = saved.Input;
            Phase = saved.Phase;
            automaticFadeRequested = saved.AutomaticFadeRequested;
            FrameCounter = saved.FrameCounter;
            saved.RiderAnimations?.CopyTo(riderAnimations, 0);
            if (saved.RiderAnimations is null)
            {
                for (int index = 0; index < simulation.Racers.Length; index++)
                {
                    Ff7ChocoboRacerState racer = simulation.Racers[index];
                    int count = Presentation.Models[racer.Model].Animations.Count;
                    riderAnimations[index] = racer.Animation < count ? racer.Animation : racer.PreviousAnimation < count ? racer.PreviousAnimation : 0;
                }
            }
        }
    }

    public long Tick { get; private set; }
    public int Countdown { get; private set; }
    public bool Started { get; private set; }
    public int FadeAlpha { get; private set; } = 255;
    public int DisplayedFadeAlpha { get; private set; } = 255;
    public Ff7ChocoboPhase Phase { get; private set; }
    public bool RecoverySide { get; private set; }
    public int RecoveryCount { get; private set; }
    public int FrameCounter { get; private set; }
    public int FinishTimer => simulation.FinishTimer;
    public int FirstFinisher => simulation.FirstFinisher;
    public bool SecondFinished => simulation.SecondFinished;
    public bool Finished => Phase == Ff7ChocoboPhase.Completed;
    public PcChocoboCourse Course { get; }
    public Ff7ChocoboCamera Camera { get; }
    public int PlayerColor => parameters[17];
    public IReadOnlyList<int> RiderAnimations => Array.AsReadOnly(riderAnimations);
    public IReadOnlyList<Ff7ChocoboRacerState> Racers => Array.AsReadOnly(simulation.Racers.Select(racer => racer with { Name = racer.Name.ToArray() }).ToArray());
    public Ff7ChocoboRacerState Player => simulation.Racers[0] with { Name = simulation.Racers[0].Name.ToArray() };
    internal FieldHostRequest Request { get; }
    internal uint RandomSeed => simulation.RandomSeed;
    internal event Action<FieldAudioCommand>? Audio;

    internal void SetInput(ushort value)
    {
        input = value;
    }

    public Ff7ChocoboSnapshot Capture()
    {
        return new(Request, parameters.ToArray(), simulation.Capture(), Tick, Countdown, Started, FadeAlpha, fadeStep, RecoverySide, RecoveryCount, held, previousInput, input, Phase, automaticFadeRequested, FrameCounter) { Camera = Camera.Capture(), RiderAnimations = riderAnimations.ToArray(), Scenery = CaptureScenery(), DisplayedFadeAlpha = DisplayedFadeAlpha };
    }

    internal void Advance()
    {
        if (Finished)
        {
            throw new InvalidOperationException("The FFVII Chocobo race has already completed.");
        }
        Tick++;
        if (Phase == Ff7ChocoboPhase.Preparing)
        {
            foreach (Ff7ChocoboRacerState racer in simulation.Racers.Where(racer => racer.Jockey != 0))
            {
                racer.Stamina = unchecked(racer.Stamina * 2);
                racer.MaxStamina = unchecked(racer.MaxStamina * 2);
            }
            Audio?.Invoke(new(0x10, 8));
            Phase = Ff7ChocoboPhase.Racing;
        }
        if (Phase == Ff7ChocoboPhase.Results)
        {
            CompletePlacements();
            Audio?.Invoke(new(0xE4, 64));
            Phase = Ff7ChocoboPhase.Completed;
            return;
        }
        if (simulation.FinishTimer > 3000)
        {
            fadeStep = 16;
            if (!automaticFadeRequested)
            {
                Audio?.Invoke(new(0xC1, 60));
                automaticFadeRequested = true;
            }
        }
        Camera.BeforeMotion(FrameCounter);
        simulation.AdvanceRacers(Started, held, Countdown);
        Camera.AfterMotion(Started);
        AdvanceScenery();
        IReadOnlyList<Ff7ChocoboPresentationModel> models = parameters[7] == 0 ? native.LongPresentation.Models : native.ShortPresentation.Models;
        for (int index = 0; index < simulation.Racers.Length; index++)
        {
            Ff7ChocoboRacerState racer = simulation.Racers[index];
            if (Camera.IsVisible(index) && racer.Animation < models[racer.Model].Animations.Count)
            {
                riderAnimations[index] = racer.Animation;
            }
        }
        Countdown++;
        if (Countdown == 60)
        {
            Started = true;
            foreach (Ff7ChocoboRacerState racer in simulation.Racers)
            {
                racer.Dash = (short)(racer.EarlySprint != 0 ? 60 : 10);
            }
        }
        previousInput = held;
        if ((held & 0x800) != 0 && simulation.SecondFinished)
        {
            fadeStep = 16;
            Audio?.Invoke(new(0xC1, 60));
        }
        held = input;
        if (previousInput != held && (held & 0x100) != 0)
        {
            simulation.Racers[0].Automatic = (short)(simulation.Racers[0].Automatic == 0 ? 1 : 0);
        }
        if ((held & (RecoverySide ? 8 : 2)) != 0)
        {
            RecoverySide = !RecoverySide;
            RecoveryCount++;
        }
        if (RecoveryCount > 10)
        {
            Ff7ChocoboRacerState player = simulation.Racers[0];
            player.Stamina = Math.Min(player.MaxStamina, player.Stamina + player.MaxStamina / 150);
            RecoveryCount = 0;
        }
        if (FadeAlpha > 255 && fadeStep > 0)
        {
            fadeStep = 0;
            Phase = Ff7ChocoboPhase.Results;
            return;
        }
        simulation.Racers[0].SoundCounter--;
        FrameCounter++;
        DisplayedFadeAlpha = FadeAlpha;
        FadeAlpha += fadeStep;
        if (FadeAlpha < 0)
        {
            FadeAlpha = 0;
            fadeStep = 0;
        }
        else if (FadeAlpha > 256)
        {
            FadeAlpha = 256;
        }
    }

    private void CompletePlacements()
    {
        int placement = simulation.Racers.Max(racer => racer.Placement) + 1;
        int[] unplaced = Enumerable.Range(0, 6).Where(index => simulation.Racers[index].Placement == 0).ToArray();
        for (int first = 0; first < unplaced.Length; first++)
        {
            for (int second = 0; second < unplaced.Length - 1; second++)
            {
                if (simulation.Racers[unplaced[second]].Path < simulation.Racers[unplaced[first]].Path)
                {
                    (unplaced[first], unplaced[second]) = (unplaced[second], unplaced[first]);
                }
            }
        }
        foreach (int index in unplaced)
        {
            simulation.Racers[index].Placement = checked((short)placement++);
        }
    }
}
