namespace Memoria.FF8;

internal sealed record WindowTransferRectangle(int X, int Y, int Width, int Height);

/// <summary>Portable window placement plus the native restore placement used by Windows.</summary>
internal sealed record WindowTransferPlacement(WindowTransferRectangle NormalBounds, bool Maximized, int Flags = 0, int MinimumX = 0, int MinimumY = 0, int MaximumX = 0, int MaximumY = 0);

/// <summary>Presentation state passed to a replacement game process without changing gameplay state.</summary>
internal sealed record WindowTransferState(bool Borderless, WindowTransferRectangle Bounds, WindowTransferPlacement Placement, WindowTransferPlacement? WindowedPlacement, int ClientWidth, int ClientHeight, int BackBufferWidth, int BackBufferHeight);
