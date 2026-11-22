// The harness's own windows, each in a child process of its own so a scenario can end it as an app
// ends (Kill): a borderless window covering a display (a video player's or a borderless game's full
// screen), a Direct3D swap chain in exclusive full screen at a lower mode (an older game), and a
// hidden window for messages such as SC_MONITORPOWER.

using System.Diagnostics;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.DirectX.D3D11;
using static TerraFX.Interop.DirectX.DXGI;
using static TerraFX.Interop.Windows.Windows;
using ERROR = TerraFX.Interop.Windows.ERROR;

namespace Karpo.Resilience;

internal static unsafe class TestWindow
{
    private const string ClassName = "resilience.fullscreen-test";

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static LRESULT Proc(HWND window, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == WM.WM_DESTROY)
        {
            PostQuitMessage(0);
            return 0;
        }
        if (msg == WM.WM_PAINT)
        {
            PAINTSTRUCT paint;
            var dc = BeginPaint(window, &paint);
            var brush = CreateSolidBrush(0x00402010);
            FillRect(dc, &paint.rcPaint, brush);
            DeleteObject(brush);
            EndPaint(window, &paint);
            return 0;
        }
        return DefWindowProcW(window, msg, wParam, lParam);
    }

    private static void Register()
    {
        fixed (char* name = ClassName)
        {
            WNDCLASSEXW wc = default;
            wc.cbSize = (uint)sizeof(WNDCLASSEXW);
            wc.lpfnWndProc = &Proc;
            wc.hInstance = GetModuleHandleW(null);
            wc.lpszClassName = name;
            wc.hCursor = LoadCursorW(HINSTANCE.NULL, IDC.IDC_ARROW);
            RegisterClassExW(&wc);
        }
    }

    private static HWND Make(Box at, uint style, uint exStyle)
    {
        Register();
        fixed (char* name = ClassName)
        fixed (char* title = "Karpo resilience test window")
        {
            return CreateWindowExW(exStyle, name, title, style, at.Left, at.Top, at.Width, at.Height, HWND.NULL, HMENU.NULL, GetModuleHandleW(null), null);
        }
    }

    /// <summary>A window nobody sees, for messages that DefWindowProc acts on.</summary>
    public static nint Hidden() => Make(new Box(0, 0, 1, 1), WS.WS_POPUP, WS.WS_EX_TOOLWINDOW);

    /// <summary>Brings <paramref name="window"/> to the front from a process the person is not using: an Alt press first, which Windows lets unlock the foreground.</summary>
    public static void Front(HWND window)
    {
        var inputs = stackalloc INPUT[2];
        inputs[0].type = INPUT.INPUT_KEYBOARD;
        inputs[0].Anonymous.ki.wVk = VK.VK_MENU;
        inputs[1].type = INPUT.INPUT_KEYBOARD;
        inputs[1].Anonymous.ki.wVk = VK.VK_MENU;
        inputs[1].Anonymous.ki.dwFlags = KEYEVENTF_KEYUP;
        SendInput(2, inputs, sizeof(INPUT));
        // And joined to the input of the thread in front, which may hand the foreground over.
        var front = GetForegroundWindow();
        var frontThread = front != HWND.NULL ? GetWindowThreadProcessId(front, null) : 0;
        var own = GetCurrentThreadId();
        var joined = frontThread != 0 && frontThread != own && AttachThreadInput(own, frontThread, true);
        SetForegroundWindow(window);
        BringWindowToTop(window);
        SetFocus(window);
        if (joined)
        {
            AttachThreadInput(own, frontThread, false);
        }
        if (GetForegroundWindow() != window)
        {
            // Windows kept the foreground where it was: a click is what a person does, and it always takes it.
            RECT rect;
            GetWindowRect(window, &rect);
            POINT was;
            GetCursorPos(&was);
            SetCursorPos((rect.left + rect.right) / 2, (rect.top + rect.bottom) / 2);
            var click = stackalloc INPUT[2];
            click[0].type = INPUT.INPUT_MOUSE;
            click[0].Anonymous.mi.dwFlags = 0x0002; // MOUSEEVENTF_LEFTDOWN
            click[1].type = INPUT.INPUT_MOUSE;
            click[1].Anonymous.mi.dwFlags = 0x0004; // MOUSEEVENTF_LEFTUP
            SendInput(2, click, sizeof(INPUT));
            Thread.Sleep(200);
            SetCursorPos(was.x, was.y);
        }
    }

    /// <summary>The child process's side: <c>window borderless|exclusive|maximized \\.\DISPLAYn</c>, until it is ended.</summary>
    public static int Run(string[] args)
    {
        var kind = args[0];
        var device = args[1];
        var display = Probe.Displays().Find(d => d.Device == device) ?? throw new ArgumentException($"no display {device}");
        HWND window;
        switch (kind)
        {
            case "borderless":
                window = Make(display.Bounds, WS.WS_POPUP | WS.WS_VISIBLE, 0);
                break;
            case "maximized":
                window = Make(new Box(display.Work.Left + 100, display.Work.Top + 100, display.Work.Left + 900, display.Work.Top + 700), WS.WS_OVERLAPPEDWINDOW | WS.WS_VISIBLE, 0);
                ShowWindow(window, SW.SW_MAXIMIZE);
                break;
            case "exclusive":
                return Exclusive(display);
            case "hung":
                // An app that stops answering: shown, a taskbar button, then its thread never pumps
                // again (Windows ghosts it after 5 s). Karpo reads its title and icon and must not hang.
                window = Plain(display, "Resilience hung app");
                Pump(TimeSpan.FromSeconds(2));
                Thread.Sleep(Timeout.Infinite);
                return 0;
            case "titles":
                // An app that renames its window a hundred times a second (a player's clock, a progress in the title).
                window = Plain(display, "Resilience titles");
                for (var n = 0; ; n++)
                {
                    fixed (char* title = $"Resilience titles {n}")
                    {
                        SetWindowTextW(window, title);
                    }
                    Pump(TimeSpan.FromMilliseconds(10));
                }
            case "progress":
                return Progress(Plain(display, "Resilience progress"));
            case "flood":
            {
                // A hundred and fifty windows, a taskbar button each, made at once and gone at once.
                var made = new List<HWND>();
                for (var n = 0; n < 150; n++)
                {
                    made.Add(Plain(display, $"Resilience flood {n}", front: false));
                }
                Pump(TimeSpan.FromSeconds(4));
                foreach (var one in made)
                {
                    DestroyWindow(one);
                }
                return 0;
            }
            default:
                throw new ArgumentException($"unknown window {kind}");
        }
        Front(window);
        MSG msg;
        while (GetMessageW(&msg, HWND.NULL, 0, 0) > 0)
        {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        return 0;
    }

    /// <summary>An ordinary app window with a taskbar button, on the display's work area, not in front unless asked.</summary>
    private static HWND Plain(DisplayFacts display, string title, bool front = true)
    {
        var at = new Box(display.Work.Left + 200, display.Work.Top + 200, display.Work.Left + 900, display.Work.Top + 700);
        var window = Make(at, WS.WS_OVERLAPPEDWINDOW | WS.WS_VISIBLE, WS.WS_EX_APPWINDOW);
        fixed (char* text = title)
        {
            SetWindowTextW(window, text);
        }
        if (front)
        {
            Front(window);
        }
        return window;
    }

    private static void Pump(TimeSpan span)
    {
        var end = Environment.TickCount64 + (long)span.TotalMilliseconds;
        MSG msg;
        do
        {
            while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
            {
                TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
            Thread.Sleep(5);
        }
        while (Environment.TickCount64 < end);
    }

    /// <summary>
    /// An app whose button shows progress (ITaskbarList3) that moves twenty times a second, pausing
    /// and erring now and then, with a badge-like overlay that comes and goes: the signals Karpo
    /// draws on the button, made anew at every change of display (Scene.Signals.cs).
    /// </summary>
    private static int Progress(HWND window)
    {
        _ = CoInitializeEx(null, (uint)COINIT.COINIT_APARTMENTTHREADED);
        ITaskbarList3* list = null;
        var clsid = CLSID.CLSID_TaskbarList;
        var iid = IID.IID_ITaskbarList3;
        var made = CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, &iid, (void**)&list);
        if (made.FAILED)
        {
            Console.Error.WriteLine($"ITaskbarList3: 0x{made.Value:X8}");
            return 1;
        }
        list->HrInit();
        var icon = LoadIconW(HINSTANCE.NULL, IDI.IDI_WARNING);
        for (var n = 0; ; n++)
        {
            var state = (n / 100 % 4) switch
            {
                0 => TBPFLAG.TBPF_NORMAL,
                1 => TBPFLAG.TBPF_PAUSED,
                2 => TBPFLAG.TBPF_ERROR,
                _ => TBPFLAG.TBPF_INDETERMINATE,
            };
            list->SetProgressState(window, state);
            list->SetProgressValue(window, (ulong)(n % 100), 100);
            if (n % 40 == 0)
            {
                fixed (char* text = "Resilience")
                {
                    list->SetOverlayIcon(window, n % 80 == 0 ? icon : HICON.NULL, text);
                }
            }
            Pump(TimeSpan.FromMilliseconds(50));
        }
    }

    /// <summary>A blt-model swap chain in exclusive full screen at 1280x720 on the display: the display's mode changes for it.</summary>
    private static int Exclusive(DisplayFacts display)
    {
        var window = Make(display.Bounds, WS.WS_POPUP | WS.WS_VISIBLE, 0);
        Front(window);
        var description = new DXGI_SWAP_CHAIN_DESC
        {
            BufferDesc = new DXGI_MODE_DESC { Width = 1280, Height = 720, Format = DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM },
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1 },
            BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
            BufferCount = 1,
            OutputWindow = window,
            Windowed = true,
            SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_DISCARD,
            Flags = (uint)DXGI_SWAP_CHAIN_FLAG.DXGI_SWAP_CHAIN_FLAG_ALLOW_MODE_SWITCH,
        };
        IDXGISwapChain* chain = null;
        ID3D11Device* device = null;
        ID3D11DeviceContext* context = null;
        var made = D3D11CreateDeviceAndSwapChain(null, D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE, HMODULE.NULL, 0, null, 0, D3D11_SDK_VERSION, &description, &chain, &device, null, &context);
        if (made.FAILED)
        {
            Console.Error.WriteLine($"D3D11CreateDeviceAndSwapChain: 0x{made.Value:X8}");
            return 1;
        }
        // The output (display) the window is on.
        IDXGIOutput* output = null;
        chain->GetContainingOutput(&output);
        var set = chain->SetFullscreenState(true, output);
        if (set.FAILED)
        {
            Console.Error.WriteLine($"SetFullscreenState: 0x{set.Value:X8}");
        }
        chain->ResizeBuffers(1, 1280, 720, DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM, (uint)DXGI_SWAP_CHAIN_FLAG.DXGI_SWAP_CHAIN_FLAG_ALLOW_MODE_SWITCH);
        ID3D11Texture2D* buffer = null;
        var uuid = __uuidof<ID3D11Texture2D>();
        chain->GetBuffer(0, uuid, (void**)&buffer);
        ID3D11RenderTargetView* view = null;
        device->CreateRenderTargetView((ID3D11Resource*)buffer, null, &view);
        var colour = stackalloc float[4] { 0.1f, 0.2f, 0.4f, 1 };
        var watch = Stopwatch.StartNew();
        MSG msg;
        while (true)
        {
            while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
            {
                if (msg.message == WM.WM_QUIT)
                {
                    goto done;
                }
                TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
            context->ClearRenderTargetView(view, colour);
            chain->Present(1, 0);
        }
    done:
        chain->SetFullscreenState(false, null);
        return 0;
    }

    /// <summary>Starts a test window in a child process; the process, to end when the scenario is done.</summary>
    public static Process Start(string kind, string device)
    {
        var self = Environment.ProcessPath!;
        var info = new ProcessStartInfo(self) { UseShellExecute = false };
        info.ArgumentList.Add("window");
        info.ArgumentList.Add(kind);
        info.ArgumentList.Add(device);
        return Process.Start(info)!;
    }
}
