using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldEntity(int Model, int X = 0, int Y = 0, int Z = 0, int Direction = 0, int TargetDirection = 0, byte Speed = 0, int Flags = 0, int Animation = 0, int AnimationFrame = 0, int Elevation = 0, int VerticalSpeed = 0, int VerticalVelocity = 0, int Terrain = 0);
public sealed record Ff7WorldLight(int Index, int X = 0, int Z = 0, int Enabled = 0, int AmbientRed = 0, int AmbientGreen = 0, int AmbientBlue = 0, int Radius = 0, int Radius2 = 0, int Red = 0, int Green = 0, int Blue = 0, int Red2 = 0, int Green2 = 0, int Blue2 = 0);
public sealed record Ff7WorldMarsh(bool Initialized = false, int X = 0, int Z = 0, int Direction = 0x400);
public sealed record Ff7WorldProcess(int WorldId, long Tick, int EntrySelector, WorldSnapshot Vm, Ff7WorldEntity[] Entities, int ControlledModel, bool MovementEnabled, int ViewMode, int RadarMode, Ff7WorldCameraState Camera, Ff7WorldLight[] Lights, int SelectedLight, Ff7WorldMarsh Marsh, int PlayerModel, int EncounterRate, Ff7Window? Window = null, Ff7Dialogue? Dialogue = null, short LastSelection = 0)
{
    public IReadOnlyList<Ff7Window> Windows => Window is null ? [] : [Window];
    public IReadOnlyList<Ff7Dialogue> Dialogues => Dialogue is null ? [] : [Dialogue];
}
