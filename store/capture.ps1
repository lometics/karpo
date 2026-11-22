# Captures the real screen in physical pixels: a whole display, or a rectangle.
#   ./capture.ps1 -Out shot.png                      # the primary display
#   ./capture.ps1 -Out shot.png -Display 1           # the second display
#   ./capture.ps1 -Out bar.png -Rect 0,2080,3840,80  # x,y,w,h on the virtual screen
param([Parameter(Mandatory)][string]$Out, [int]$Display = 0, [int[]]$Rect)

Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type -Namespace Karpo -Name Dpi -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(System.IntPtr value);'
[Karpo.Dpi]::SetProcessDpiAwarenessContext([IntPtr]-4) | Out-Null   # per-monitor v2: real pixels

if ($Rect) {
    $x, $y, $w, $h = $Rect
} else {
    $screens = [System.Windows.Forms.Screen]::AllScreens | Sort-Object { -not $_.Primary }
    $b = $screens[$Display].Bounds
    $x, $y, $w, $h = $b.X, $b.Y, $b.Width, $b.Height
}
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size $w, $h))
$g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"$Out ($w x $h at $x,$y)"
