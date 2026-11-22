// Arabic, Urdu and Persian on the dock (Mirror.cs, docs\widgets.md §36 "Right to left"): a line with an
// Arabic letter is laid out right to left, and the menus and a folder's lines are mirrored, their
// words upright. Karpo speaks Arabic, Urdu or Persian from their files (InLanguage.Use). Pictures to
// KARPO_PNG_DIR as rtl-<what>-<language>-<look>-<scale>.png: a menu with a submenu, its icon
// buttons and a tooltip, a folder of lines, and Hindi's, Thai's and Tamil's taller
// letters in a menu or a folder, for clipping.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Localization;

[Collection(InLanguage.Collection)]
public sealed unsafe class RightToLeftPictures
{
    private const string Owner = "layout:default";

    [Fact]
    public void AMirrorTurnsAcrossItsSpanAndBack()
    {
        // A 20-px thing 10 px from the left of a 100-px panel stands 10 px from its right.
        Assert.Equal(70, Mirror.Across(10, 20, 100));
        Assert.Equal(90, Mirror.Back(10, 100));
        var box = new D2D_RECT_F(10, 5, 30, 25);
        var back = Mirror.Back(box, 100);
        Assert.Equal((70f, 5f, 90f, 25f), (back.left, back.top, back.right, back.bottom));
        var again = Mirror.Back(back, 100);
        Assert.Equal((10f, 30f), (again.left, again.right));
        // A part kept upright, 40 to 70 across: mirrored to 30 to 60 and the right way round in it,
        // so a point 5 px into it from the left on the screen is 5 px into it from its own left.
        (float, float)[] upright = [(40, 70)];
        Assert.Equal(45, Mirror.Back(35, 100, upright));
        Assert.Equal(90, Mirror.Back(10, 100, upright));
        var button = Mirror.Back(new D2D_RECT_F(40, 0, 50, 10), 100, upright);
        Assert.Equal((30f, 40f), (button.left, button.right));
        // About x = 50 on top of a shift of 7: x → 100 − x, then + 7.
        var turned = Mirror.Turned(new D2D_MATRIX_3X2_F(1, 0, 0, 1, 7, 3), 100);
        Assert.Equal((-1f, 0f, 0f, 1f, 107f, 3f), (turned._11, turned._12, turned._21, turned._22, turned._31, turned._32));
    }

    [Fact]
    public void AGlyphThatPointsAlongTheLineTurnsWithIt()
    {
        Assert.True(Mirror.Turns(char.ConvertFromUtf32(0xE974)));
        Assert.True(Mirror.Turns(char.ConvertFromUtf32(0xE72B)));
        // Play, the speaker, a letter: as they are.
        Assert.False(Mirror.Turns(char.ConvertFromUtf32(0xE768)));
        Assert.False(Mirror.Turns(char.ConvertFromUtf32(0xE767)));
        Assert.False(Mirror.Turns("A"));
        Assert.False(Mirror.Turns(""));
        Assert.False(Mirror.Turns(null));
    }

    [UiFact]
    public void ALineOfArabicIsLaidRightToLeftWhereALeftToRightOneWouldBe()
    {
        WindowShots.OnHiddenDesktop("rtl-text", (gfx, _) =>
        {
            foreach (var (text, rightToLeft) in new[] { ("الإعدادات", true), ("Wi-Fi متصل", false), ("Settings", false), ("12:45", false) })
            {
                var layout = gfx.TextLayout(text, 14, Weight.Regular, null);
                try
                {
                    Assert.Equal(
                        rightToLeft ? DWRITE_READING_DIRECTION.DWRITE_READING_DIRECTION_RIGHT_TO_LEFT : DWRITE_READING_DIRECTION.DWRITE_READING_DIRECTION_LEFT_TO_RIGHT,
                        layout->GetReadingDirection());
                    DWRITE_TEXT_METRICS metrics;
                    ComUtil.Check(layout->GetMetrics(&metrics));
                    // At the box's left either way, so every caller's box and place stay as they are.
                    Assert.InRange(metrics.left, -0.5f, 0.5f);
                    Assert.True(metrics.width > 10, text);
                }
                finally
                {
                    layout->Release();
                }
            }
            // Karpo in Arabic: "Wi-Fi" and the Arabic after it read in Arabic's order, and a cut line's
            // ellipsis stands at its left — still within the box.
            using (InLanguage.Use("ar"))
            {
                var layout = gfx.TextLayout("Wi-Fi متصل بالشبكة اللاسلكية المنزلية", 14, Weight.Regular, 80);
                try
                {
                    Assert.Equal(DWRITE_READING_DIRECTION.DWRITE_READING_DIRECTION_RIGHT_TO_LEFT, layout->GetReadingDirection());
                    DWRITE_TEXT_METRICS metrics;
                    ComUtil.Check(layout->GetMetrics(&metrics));
                    Assert.InRange(metrics.left, -0.5f, 0.5f);
                    Assert.True(metrics.width <= 80.5f, $"{metrics.width}");
                }
                finally
                {
                    layout->Release();
                }
            }
        });
    }

    /// <summary>A menu of Arabic words: the icon buttons, a header, rows with icons, a check, a submenu, a shortcut and a detail.</summary>
    private static List<FlyoutItem> ArabicMenu() =>
    [
        new(1, "نسخ", new FlyoutIcon.Glyph(char.ConvertFromUtf32(0xE8C8)), Primary: true),
        new(2, "الإعدادات", new FlyoutIcon.Glyph(char.ConvertFromUtf32(0xE713)), Primary: true),
        new(3, "إزالة", new FlyoutIcon.Glyph(char.ConvertFromUtf32(0xE74D)), Primary: true),
        new(0, "", Separator: true),
        new(0, "حديثًا", Header: true),
        new(10, "ملاحظات.txt", new FlyoutIcon.Glyph(char.ConvertFromUtf32(0xE8A5))),
        new(11, "الميزانية الشهرية للأسرة 2026.xlsx", new FlyoutIcon.Glyph(char.ConvertFromUtf32(0xE8A5)), Detail: "المستندات"),
        new(0, "", Separator: true),
        new(12, "إظهار الثواني", Checked: true),
        new(13, "التخطيطات", new FlyoutIcon.Glyph(char.ConvertFromUtf32(0xF0E2)), Children: [new(20, "افتراضي", Checked: true), new(21, "العمل")]),
        new(14, "إدارة المهام", new FlyoutIcon.Glyph(char.ConvertFromUtf32(0xE9D9)), Accelerator: "Ctrl+Shift+Esc"),
        new(15, "إعدادات شريط المهام", new FlyoutIcon.Glyph(char.ConvertFromUtf32(0xE713)), Enabled: false, Detail: "غير متاح الآن"),
    ];

    /// <summary>Hindi's taller letters, with their marks above and below the line, in the same menu.</summary>
    private static List<FlyoutItem> HindiMenu() =>
    [
        new(1, "कॉपी करें", new FlyoutIcon.Glyph(char.ConvertFromUtf32(0xE8C8)), Primary: true),
        new(2, "सेटिंग्स", new FlyoutIcon.Glyph(char.ConvertFromUtf32(0xE713)), Primary: true),
        new(0, "", Separator: true),
        new(0, "हाल ही में", Header: true),
        new(10, "टास्कबार सेटिंग्स", new FlyoutIcon.Glyph(char.ConvertFromUtf32(0xE713)), Detail: "प्रदर्शित करें, छिपाएँ, व्यवस्थित करें"),
        new(11, "कार्य प्रबंधक", new FlyoutIcon.Glyph(char.ConvertFromUtf32(0xE9D9)), Accelerator: "Ctrl+Shift+Esc"),
        new(12, "सेकंड दिखाएँ", Checked: true),
        new(13, "लेआउट", Children: [new(20, "डिफ़ॉल्ट")]),
        new(14, "क्षेत्रीय प्रारूप — ग्रीष्मकालीन", new FlyoutIcon.Glyph(char.ConvertFromUtf32(0xE909))),
        new(15, "স্ক্রিনশট নিন — বাংলা", new FlyoutIcon.Glyph(char.ConvertFromUtf32(0xE722))),
    ];

    [UiFact]
    public void MenuSheets()
    {
        WindowShots.OnHiddenDesktop("rtl-menu", (gfx, instance) =>
        {
            foreach (var (dark, scale) in new[] { (true, 1f), (false, 1.5f) })
            {
                var look = AddListPictures.LookFor(dark);
                var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                var anchor = new POINT { x = work.right / 2, y = work.bottom };
                var suffix = $"{(dark ? "dark" : "light")}-{(int)(scale * 100)}";
                using (InLanguage.Use("ar"))
                {
                    using var menu = new Flyout(gfx, instance, 0) { SpokenName = "شريط المهام" };
                    menu.Show(gfx, look, scale, ArabicMenu(), FlyoutLayout.List, anchor, work, Edge.Bottom);
                    WindowShots.Settle();
                    // The icon buttons from the right: the first at the right, the next to its left.
                    var first = menu.ItemOnScreen(1)!.Value;
                    var second = menu.ItemOnScreen(2)!.Value;
                    Assert.True(first.left > second.left, $"{first.left} {second.left}");
                    // All three together at the row's far end, its left in a mirrored menu, Remove last (§350).
                    var remove = menu.ItemOnScreen(3)!.Value;
                    var panel = menu.OpenPanels[0].Panel;
                    Assert.True(remove.left - panel.left <= (int)MathF.Ceiling(8 * scale), $"{remove.left} {panel.left}");
                    Assert.Equal(first.left - second.right, second.left - remove.right);
                    Assert.True(WindowShots.Save(menu.Hwnd, $"rtl-menu-ar-{suffix}.png"));
                    // The keys on the first button, with its tooltip in Arabic.
                    _ = menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_HOME, 0);
                    menu.TipDue();
                    Assert.Equal("نسخ", menu.TipShown);
                    WindowShots.Settle();
                    Assert.True(WindowShots.Save(menu.TipHwnd, $"rtl-tip-ar-{suffix}.png"));
                    menu.Hide();
                }
                using (InLanguage.Use("hi"))
                {
                    using var menu = new Flyout(gfx, instance, 0) { SpokenName = "टास्कबार" };
                    menu.Show(gfx, look, scale, HindiMenu(), FlyoutLayout.List, anchor, work, Edge.Bottom);
                    WindowShots.Settle();
                    Assert.True(WindowShots.Save(menu.Hwnd, $"rtl-menu-hi-{suffix}.png"));
                    menu.Hide();
                }
            }
        });
    }

    /// <summary>A folder of lines: a heading, two switches, the brightness, a clock, the battery, the processor and the media.</summary>
    private static BarItem Folder(string heading)
    {
        var island = BarKind.FolderList.New();
        island.Id = "rtl";
        island.Folder!.Lists = [];
        foreach (var kind in new[] { ListKind.Heading, ListKind.AirplaneMode, ListKind.NightLight, ListKind.Brightness, ListKind.Clock, ListKind.Battery, ListKind.Cpu, ListKind.Media })
        {
            var line = kind.New();
            if (line.Heading is { } named)
            {
                named.Text = heading;
            }
            island.Folder.Put(line, 0);
        }
        return island;
    }

    [UiFact]
    public void FolderSheets()
    {
        WindowShots.OnHiddenDesktop("rtl-folder", (gfx, instance) =>
        {
            foreach (var (dark, scale) in new[] { (true, 1f), (false, 1.5f) })
            {
                var work = new RECT { right = 3200, bottom = 1600 };
                var anchor = new POINT { x = 1600, y = 1600 };
                var suffix = $"{(dark ? "dark" : "light")}-{(int)(scale * 100)}";
                foreach (var (name, heading) in new[] { ("ar", "المفاتيح والشاشة"), ("ur", "سوئچز اور ڈسپلے"), ("en", "Switches and display"), ("hi", "स्विच और डिस्प्ले"),
                    ("fa", "کلیدها و نمایشگر"), ("th", "สวิตช์และจอแสดงผล"), ("ta", "சுவிட்சுகள் மற்றும் காட்சி") })
                {
                    using var speaking = InLanguage.Use(name);
                    using var folder = new FolderIsland($"{Owner}/rtl-{name}", Folder(heading), gfx, new Config());
                    using var flyout = new FolderFlyout(gfx, instance, 0);
                    flyout.Show(gfx, AddListPictures.LookFor(dark), scale, anchor, work, Edge.Bottom, folder);
                    Assert.True(flyout.IsOpen);
                    WindowShots.Settle(200);
                    Assert.True(WindowShots.Save(flyout.Hwnd, $"rtl-folder-{name}-{suffix}.png"));
                    flyout.Hide();
                }
            }
        });
    }
}
