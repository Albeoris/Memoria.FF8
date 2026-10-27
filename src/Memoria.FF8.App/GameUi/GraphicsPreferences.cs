namespace Memoria.FF8.GameUi;

internal sealed record GraphicsPreferences
{
    public Memoria.FF8.Platform.GraphicsBackend Backend { get; init; } = Memoria.FF8.Platform.GraphicsBackend.Auto;

    public int MsaaSamples { get; init; } = 8;

    [System.Text.Json.Serialization.JsonIgnore]
    public string AntiAliasingLabel => MsaaSamples == 0 ? "Off" : $"MSAA {MsaaSamples}x";

    public void Validate()
    {
        if (!Enum.IsDefined(Backend))
        {
            throw new InvalidDataException("Graphics.Backend must be Auto, Direct3D11, Direct3D12 or Vulkan.");
        }
        if (MsaaSamples is not (0 or 2 or 4 or 8))
        {
            throw new InvalidDataException("Graphics.MsaaSamples must be 0, 2, 4 or 8.");
        }
    }
}
