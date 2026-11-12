namespace Memoria.FF8.Runtime;

/// <summary>Owns Steam callbacks for a product without a verified achievement catalog.</summary>
public sealed class SteamPlatformSession : IDisposable
{
    private readonly ISteamAchievementApi? api;
    private readonly int owningThread = Environment.CurrentManagedThreadId;
    private readonly uint product;
    private bool disposed;
    public bool Ready { get; private set; }
    public string? Diagnostic { get; private set; }

    public SteamPlatformSession(string nativePath, string productId)
    {
        product = RequireProduct(productId);
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                Diagnostic = "Steam integration is unavailable on this host.";
                return;
            }
            api = new SteamAchievementNativeApi(nativePath);
            Initialize(productId);
        }
        catch (Exception exception) when (IsNativeFailure(exception))
        {
            Stop("Steam SDK unavailable: " + exception.Message);
        }
    }

    internal SteamPlatformSession(ISteamAchievementApi api, string productId)
    {
        product = RequireProduct(productId);
        this.api = api;
        try
        {
            Initialize(productId);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private static uint RequireProduct(string productId)
    {
        return productId switch
        {
            "3837340" => 3837340,
            "39140" => 39140,
            _ => throw new ArgumentException("No verified Steam platform identity exists for this product.", nameof(productId))
        };
    }

    private void Initialize(string productId)
    {
        if (!api!.Initialize(productId, out string? diagnostic))
        {
            Stop(diagnostic ?? "Steam initialization failed.");
            return;
        }
        if (api.AppId != product)
        {
            Stop("Steam runtime AppID does not match the selected product.");
            return;
        }
        Ready = true;
        Diagnostic = "Steam product verified; achievement delivery is unavailable for this product.";
    }

    public void Pump()
    {
        CheckThread();
        if (disposed || !Ready)
        {
            return;
        }
        try
        {
            if (api!.AppId != product)
            {
                Stop("Steam runtime AppID changed.");
                return;
            }
            api.Pump();
        }
        catch (Exception exception) when (IsNativeFailure(exception))
        {
            Stop("Steam callback processing failed: " + exception.Message);
        }
    }

    private void Stop(string diagnostic)
    {
        Diagnostic = diagnostic;
        Dispose();
    }

    private static bool IsNativeFailure(Exception exception) => exception is IOException or UnauthorizedAccessException or ArgumentException or BadImageFormatException or DllNotFoundException or EntryPointNotFoundException or InvalidOperationException;

    private void CheckThread()
    {
        if (Environment.CurrentManagedThreadId != owningThread)
        {
            throw new InvalidOperationException("Steam SDK operations must remain on their owning application thread.");
        }
    }

    public void Dispose()
    {
        CheckThread();
        if (disposed)
        {
            return;
        }
        disposed = true;
        Ready = false;
        try
        {
            api?.Dispose();
        }
        catch (Exception exception) when (IsNativeFailure(exception))
        {
            Diagnostic = (Diagnostic ?? "Steam SDK stopped.") + " Shutdown: " + exception.Message;
        }
    }
}
