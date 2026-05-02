using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Manifold;
using DalamudMCP.Plugin.Readers;
using DalamudMCP.Protocol;
using MemoryPack;

namespace DalamudMCP.Plugin.Operations;

[Operation(
    "game.screenshot",
    Description = "Captures a screenshot of the game client or window.",
    Summary = "Captures a game screenshot.")]
[ResultFormatter(typeof(GameScreenshotOperation.TextFormatter))]
[CliCommand("game", "screenshot")]
[McpTool("capture_game_screenshot")]
public sealed partial class GameScreenshotOperation
    : IOperation<GameScreenshotOperation.Request, GameScreenshotSnapshot>, IPluginReaderStatus
{
    private readonly Func<Request, CancellationToken, ValueTask<GameScreenshotSnapshot>> executor;
    private readonly Func<bool>? isReadyProvider;
    private readonly Func<string>? detailProvider;
    private readonly string unavailableDetail;

    public GameScreenshotOperation(PluginRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        executor = CreateCaptureExecutor(options.CaptureDirectoryPath);
        isReadyProvider = static () => WindowBitmapCaptureHelper.HasCapturableWindow();
        detailProvider = () => isReadyProvider() ? "ready" : "window_unavailable";
        unavailableDetail = "window_unavailable";
    }

    internal GameScreenshotOperation(
        Func<Request, CancellationToken, ValueTask<GameScreenshotSnapshot>> executor,
        bool isReady = true,
        string detail = "ready")
    {
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        isReadyProvider = () => isReady;
        detailProvider = () => string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
        unavailableDetail = string.IsNullOrWhiteSpace(detail) ? "ready" : detail;
    }

    public string ReaderKey => "game.screenshot";

    public bool IsReady => isReadyProvider?.Invoke() ?? false;

    public string Detail => detailProvider?.Invoke() ?? unavailableDetail;

    public ValueTask<GameScreenshotSnapshot> ExecuteAsync(Request request, OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        return executor(request, context.CancellationToken);
    }

    [MemoryPackable]
    [ProtocolOperation("game.screenshot")]
    [LegacyBridgeRequest("CaptureGameScreenshot")]
    public sealed partial class Request
    {
        [Option("capture-area", Description = "Capture area to use: client or window.", Required = false)]
        public string? CaptureArea { get; init; }
    }

    public sealed class TextFormatter : IResultFormatter<GameScreenshotSnapshot>
    {
        public string? FormatText(GameScreenshotSnapshot result, OperationContext context)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(context);

            return result.SummaryText;
        }
    }

    private static Func<Request, CancellationToken, ValueTask<GameScreenshotSnapshot>> CreateCaptureExecutor(string captureDirectoryPath)
    {
        return (request, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string captureArea = NormalizeCaptureArea(request.CaptureArea);
            if (!WindowBitmapCaptureHelper.TryCaptureToBitmapFile(captureDirectoryPath, captureArea, out WindowBitmapCaptureHelper.BitmapCaptureResult result))
                throw new InvalidOperationException("A capturable game window is not available.");

            return ValueTask.FromResult(
                new GameScreenshotSnapshot(
                    result.CapturedAt,
                    captureArea,
                    result.FilePath,
                    result.Width,
                    result.Height,
                    result.FileSizeBytes,
                    $"Captured {captureArea} screenshot to {Path.GetFileName(result.FilePath)} ({result.Width}x{result.Height})."));
        };
    }

    private static string NormalizeCaptureArea(string? captureArea)
    {
        if (string.IsNullOrWhiteSpace(captureArea))
            return ScreenshotCaptureArea.Client;

        string normalized = captureArea.Trim();
        if (string.Equals(normalized, ScreenshotCaptureArea.Client, StringComparison.OrdinalIgnoreCase))
            return ScreenshotCaptureArea.Client;

        if (string.Equals(normalized, ScreenshotCaptureArea.Window, StringComparison.OrdinalIgnoreCase))
            return ScreenshotCaptureArea.Window;

        throw new ArgumentOutOfRangeException(nameof(captureArea), "Capture area must be either 'client' or 'window'.");
    }

    private static class ScreenshotCaptureArea
    {
        public const string Client = "client";
        public const string Window = "window";
    }

    [SupportedOSPlatform("windows")]
    private static class WindowBitmapCaptureHelper
    {
        private const int BitsPerPixel = 32;
        private const int BytesPerPixel = BitsPerPixel / 8;
        private const uint BiRgb = 0;
        private const uint DibRgbColors = 0;
        private const uint GwOwner = 4;
        private const uint PwClientOnly = 0x00000001;
        private const uint PwRenderFullContent = 0x00000002;
        private const uint SrcCopy = 0x00CC0020;
        private const uint CaptureBlt = 0x40000000;

        internal static bool HasCapturableWindow()
        {
            IntPtr windowHandle = TryResolveGameWindowHandle();
            return windowHandle != nint.Zero && !IsIconic(windowHandle);
        }

        internal static bool TryCaptureToBitmapFile(
            string captureDirectoryPath,
            string captureArea,
            out BitmapCaptureResult result)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(captureDirectoryPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(captureArea);

            result = default;
            IntPtr windowHandle = TryResolveGameWindowHandle();
            if (windowHandle == nint.Zero || IsIconic(windowHandle))
                return false;

            if (!TryGetCaptureBounds(windowHandle, captureArea, out CaptureBounds bounds))
                return false;

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return false;

            Directory.CreateDirectory(captureDirectoryPath);

            DateTimeOffset capturedAt = DateTimeOffset.UtcNow;
            string filePath = Path.Combine(captureDirectoryPath, CreateFileName(captureArea, capturedAt));
            if (!TryCaptureWindowToBitmapFile(windowHandle, filePath, captureArea, bounds.Width, bounds.Height)
                && !TryCaptureBoundsToBitmapFile(filePath, bounds))
            {
                return false;
            }

            FileInfo fileInfo = new(filePath);
            if (!fileInfo.Exists || fileInfo.Length <= 0)
            {
                TryDelete(filePath);
                return false;
            }

            result = new BitmapCaptureResult(capturedAt, filePath, bounds.Width, bounds.Height, fileInfo.Length);
            return true;
        }

        private static string CreateFileName(string captureArea, DateTimeOffset capturedAt)
        {
            return $"ffxiv-{captureArea}-{capturedAt:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.bmp";
        }

        private static bool TryGetCaptureBounds(nint windowHandle, string captureArea, out CaptureBounds bounds)
        {
            if (string.Equals(captureArea, ScreenshotCaptureArea.Window, StringComparison.Ordinal))
            {
                if (!GetWindowRect(windowHandle, out NativeRect windowRect))
                {
                    bounds = default;
                    return false;
                }

                bounds = new CaptureBounds(
                    windowRect.Left,
                    windowRect.Top,
                    windowRect.Right - windowRect.Left,
                    windowRect.Bottom - windowRect.Top);
                return true;
            }

            if (!GetClientRect(windowHandle, out NativeRect clientRect))
            {
                bounds = default;
                return false;
            }

            NativePoint topLeft = new(clientRect.Left, clientRect.Top);
            NativePoint bottomRight = new(clientRect.Right, clientRect.Bottom);
            if (!ClientToScreen(windowHandle, ref topLeft) || !ClientToScreen(windowHandle, ref bottomRight))
            {
                bounds = default;
                return false;
            }

            bounds = new CaptureBounds(
                topLeft.X,
                topLeft.Y,
                bottomRight.X - topLeft.X,
                bottomRight.Y - topLeft.Y);
            return true;
        }

        private static bool TryCaptureBoundsToBitmapFile(string filePath, CaptureBounds bounds)
        {
            IntPtr screenDc = GetDC(nint.Zero);
            if (screenDc == nint.Zero)
                return false;

            IntPtr memoryDc = nint.Zero;
            IntPtr bitmap = nint.Zero;
            IntPtr originalBitmap = nint.Zero;

            try
            {
                memoryDc = CreateCompatibleDC(screenDc);
                if (memoryDc == nint.Zero)
                    return false;

                bitmap = CreateCompatibleBitmap(screenDc, bounds.Width, bounds.Height);
                if (bitmap == nint.Zero)
                    return false;

                originalBitmap = SelectObject(memoryDc, bitmap);
                if (originalBitmap == nint.Zero)
                    return false;

                if (!BitBlt(memoryDc, 0, 0, bounds.Width, bounds.Height, screenDc, bounds.Left, bounds.Top, SrcCopy | CaptureBlt))
                    return false;

                return TryPersistBitmapFile(filePath, bounds.Width, bounds.Height, memoryDc, bitmap);
            }
            catch (IOException)
            {
                TryDelete(filePath);
                return false;
            }
            finally
            {
                if (originalBitmap != nint.Zero)
                    _ = SelectObject(memoryDc, originalBitmap);

                if (bitmap != nint.Zero)
                    _ = DeleteObject(bitmap);

                if (memoryDc != nint.Zero)
                    _ = DeleteDC(memoryDc);

                _ = ReleaseDC(nint.Zero, screenDc);
            }
        }

        private static bool TryCaptureWindowToBitmapFile(
            nint windowHandle,
            string filePath,
            string captureArea,
            int width,
            int height)
        {
            IntPtr screenDc = GetDC(nint.Zero);
            if (screenDc == nint.Zero)
                return false;

            IntPtr memoryDc = nint.Zero;
            IntPtr bitmap = nint.Zero;
            IntPtr originalBitmap = nint.Zero;

            try
            {
                memoryDc = CreateCompatibleDC(screenDc);
                if (memoryDc == nint.Zero)
                    return false;

                bitmap = CreateCompatibleBitmap(screenDc, width, height);
                if (bitmap == nint.Zero)
                    return false;

                originalBitmap = SelectObject(memoryDc, bitmap);
                if (originalBitmap == nint.Zero)
                    return false;

                _ = DwmFlush();
                if (!PrintWindow(windowHandle, memoryDc, GetPrintWindowFlags(captureArea)))
                    return false;

                return TryPersistBitmapFile(filePath, width, height, memoryDc, bitmap);
            }
            catch (IOException)
            {
                TryDelete(filePath);
                return false;
            }
            finally
            {
                if (originalBitmap != nint.Zero)
                    _ = SelectObject(memoryDc, originalBitmap);

                if (bitmap != nint.Zero)
                    _ = DeleteObject(bitmap);

                if (memoryDc != nint.Zero)
                    _ = DeleteDC(memoryDc);

                _ = ReleaseDC(nint.Zero, screenDc);
            }
        }

        private static uint GetPrintWindowFlags(string captureArea)
        {
            return string.Equals(captureArea, ScreenshotCaptureArea.Client, StringComparison.Ordinal)
                ? PwClientOnly | PwRenderFullContent
                : PwRenderFullContent;
        }

        private static bool TryPersistBitmapFile(string filePath, int width, int height, nint memoryDc, nint bitmap)
        {
            byte[] pixels = new byte[checked(width * height * BytesPerPixel)];
            BitmapInfo bitmapInfo = new()
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = width,
                    Height = height,
                    Planes = 1,
                    BitCount = BitsPerPixel,
                    Compression = BiRgb,
                    SizeImage = (uint)pixels.Length
                }
            };

            int scanLines = GetDIBits(memoryDc, bitmap, 0, (uint)height, pixels, ref bitmapInfo, DibRgbColors);
            if (scanLines == 0)
                return false;

            NormalizeAlpha(pixels);
            WriteBitmapFile(filePath, width, height, pixels);
            return true;
        }

        private static void NormalizeAlpha(byte[] pixels)
        {
            for (int index = 3; index < pixels.Length; index += BytesPerPixel)
                pixels[index] = byte.MaxValue;
        }

        private static void WriteBitmapFile(string filePath, int width, int height, byte[] pixels)
        {
            using FileStream stream = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new(stream);

            int headerSize = 14 + Marshal.SizeOf<BitmapInfoHeader>();
            int fileSize = headerSize + pixels.Length;

            writer.Write((ushort)0x4D42);
            writer.Write(fileSize);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write(headerSize);
            writer.Write((uint)Marshal.SizeOf<BitmapInfoHeader>());
            writer.Write(width);
            writer.Write(height);
            writer.Write((ushort)1);
            writer.Write((ushort)BitsPerPixel);
            writer.Write((uint)BiRgb);
            writer.Write(pixels.Length);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(pixels);
        }

        private static void TryDelete(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static nint TryResolveGameWindowHandle()
        {
            using Process process = Process.GetCurrentProcess();
            process.Refresh();
            if (process.MainWindowHandle != nint.Zero)
                return process.MainWindowHandle;

            uint processId = (uint)process.Id;
            IntPtr discoveredHandle = nint.Zero;
            _ = EnumWindows(
                (windowHandle, lParam) =>
                {
                    _ = lParam;
                    if (!IsWindowVisible(windowHandle) || GetWindow(windowHandle, GwOwner) != nint.Zero)
                        return true;

                    if (GetWindowThreadProcessId(windowHandle, out uint windowProcessId) == 0 || windowProcessId != processId)
                        return true;

                    discoveredHandle = windowHandle;
                    return false;
                },
                nint.Zero);

            return discoveredHandle;
        }

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BitBlt(nint hdc, int x, int y, int cx, int cy, nint hdcSrc, int x1, int y1, uint rop);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(nint hWnd, ref NativePoint point);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern nint CreateCompatibleBitmap(nint hdc, int cx, int cy);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern nint CreateCompatibleDC(nint hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteDC(nint hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(nint hObject);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmFlush();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint GetDC(nint hWnd);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetDIBits(nint hdc, nint hbm, uint start, uint cLines, [Out] byte[] lpvBits, ref BitmapInfo lpbmi, uint usage);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(nint hWnd, out NativeRect lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint GetWindow(nint hWnd, uint uCmd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(nint hWnd, out NativeRect lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PrintWindow(nint hWnd, nint hdcBlt, uint nFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(nint hWnd, nint hDc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern nint SelectObject(nint hdc, nint h);

        private delegate bool EnumWindowsProc(nint windowHandle, nint lParam);

        internal readonly record struct BitmapCaptureResult(
            DateTimeOffset CapturedAt,
            string FilePath,
            int Width,
            int Height,
            long FileSizeBytes);

        private readonly record struct CaptureBounds(int Left, int Top, int Width, int Height);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;

            public NativePoint(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public BitmapInfoHeader Header;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ClrUsed;
            public uint ClrImportant;
        }
    }
}

[MemoryPackable]
public sealed partial record GameScreenshotSnapshot(
    DateTimeOffset CapturedAt,
    string CaptureArea,
    string FilePath,
    int Width,
    int Height,
    long FileSizeBytes,
    string SummaryText);



