// The Windows constants TerraFX does not carry, from the SDK headers (wingdi.h, winuser.h).

namespace Karpo.Resilience;

internal static class C
{
    public const uint SDC_TOPOLOGY_CLONE = 0x2;
    public const uint SDC_TOPOLOGY_EXTEND = 0x4;
    public const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x20;
    public const uint SDC_APPLY = 0x80;
    public const uint SDC_ALLOW_CHANGES = 0x400;

    public const uint ENUM_CURRENT_SETTINGS = unchecked((uint)-1);
    public const uint DM_DISPLAYORIENTATION = 0x80;
    public const uint DM_PELSWIDTH = 0x80000;
    public const uint DM_PELSHEIGHT = 0x100000;
    public const int DISP_CHANGE_SUCCESSFUL = 0;

    public const uint MOUSEEVENTF_MOVE = 0x1;
    public const uint GR_GDIOBJECTS = 0;
    public const uint GR_USEROBJECTS = 1;
}
