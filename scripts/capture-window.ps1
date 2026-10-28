param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$OutPath
)

# Run with Windows PowerShell 5.1 (System.Drawing is built in there).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies 'System.Drawing.dll' -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class PclWindowCapture
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);

    public static string Capture(IntPtr hWnd, string path)
    {
        RECT rect;
        if (!GetWindowRect(hWnd, out rect)) return "GetWindowRect failed";
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0) return "bad window rect";

        using (var bitmap = new Bitmap(width, height))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            var hdc = graphics.GetHdc();
            // PW_RENDERFULLCONTENT (2) asks the window to render its GPU-composited content.
            PrintWindow(hWnd, hdc, 2);
            graphics.ReleaseHdc(hdc);
            bitmap.Save(path, ImageFormat.Png);
        }
        return width + "x" + height;
    }
}
'@

$handle = (Get-Process -Id $ProcessId).MainWindowHandle
if ($handle -eq 0) { throw "process $ProcessId has no main window" }
$size = [PclWindowCapture]::Capture($handle, $OutPath)
Write-Host "captured $OutPath ($size)"
