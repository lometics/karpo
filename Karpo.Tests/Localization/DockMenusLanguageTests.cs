// The dock's menus in other languages: the bar's own menu, an app's, the Win+X menu, the add list,
// the desktops, and what a screen reader hears. Each reads its words when it is made, so the same
// call answers in the language Karpo speaks now and in English again afterwards.

using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Localization;

[Collection(InLanguage.Collection)]
public sealed class DockMenusLanguageTests
{
    private static string[] Texts(IEnumerable<FlyoutItem> rows) =>
        [.. rows.Where(row => !row.Separator && !row.Header).Select(row => row.Text)];

    [Fact]
    public void TheBarsMenuSpeaksWindowsGerman()
    {
        using (InLanguage.Use("de"))
        {
            // §663: Windows' own German for its lock, "Taskleiste fixieren".
            Assert.Equal(["Task-Manager", "Taskleiste fixieren", "Taskleisteneinstellungen"], Texts(TheDock.BarRows([])));
        }
        Assert.Equal(["Task Manager", "Lock the taskbar", "Taskbar settings"], Texts(TheDock.BarRows([])));
    }

    [Fact]
    public void AnAppsMenuClosesOneWindowOrAllInFrench()
    {
        using (InLanguage.Use("fr"))
        {
            var one = Texts(TheDock.AppRows([], "Notepad", new FlyoutIcon.Glyph("x"), pinned: false, open: 1, buttons: null, Edge.Bottom));
            Assert.Equal(["Notepad", "Épingler à la barre des tâches", "Fermer la fenêtre"], one);
            var all = Texts(TheDock.AppRows([], "Notepad", new FlyoutIcon.Glyph("x"), pinned: true, open: 3, TheDock.WidgetButtons(), Edge.Bottom, endTask: true));
            Assert.Equal(["Couper", "Copier", "Supprimer", "Notepad", "Fin de tâche", "Fermer toutes les fenêtres"], all);
        }
    }

    [Fact]
    public void TheWinXMenuHasWindowsChineseWords()
    {
        var machine = new QuickLinkMachine(Battery: false, WindowsTerminal: true, Sleep: true, Hibernate: false);
        using (InLanguage.Use("zh-Hans"))
        {
            var items = QuickLinks.Items(machine);
            Assert.Contains("任务管理器", Texts(items));
            Assert.Contains("终端（管理员）", Texts(items));
            var power = items.Single(item => item.Children is { Count: > 0 });
            Assert.Equal("关机或注销", power.Text);
            Assert.Equal(["注销", "睡眠", "关机", "重启"], Texts(power.Children!));
        }
    }

    [Fact]
    public void ACountPicksItsLanguagesForm()
    {
        using (InLanguage.Use("de"))
        {
            Assert.Equal("1 ausgeführtes Fenster", S.DockUiaRunningWindows(1));
            Assert.Equal("3 ausgeführte Fenster", S.DockUiaRunningWindows(3));
        }
        using (InLanguage.Use("fr"))
        {
            // French takes the singular for nought as well as one.
            Assert.Equal("0 fenêtre ouverte", S.DockUiaRunningWindows(0));
            Assert.Equal("2 fenêtres ouvertes", S.DockUiaRunningWindows(2));
        }
        using (InLanguage.Use("zh-Hans"))
        {
            Assert.Equal("后退 10 秒", S.DockWidgetMenuSeekBack(10));
        }
        Assert.Equal("1 running window", S.DockUiaRunningWindows(1));
    }

    [Fact]
    public void AnAddButtonNamesItsSectionInSpanish()
    {
        using (InLanguage.Use("es"))
        {
            Assert.Equal("Agregar un widget a la izquierda", AddList.HereIn(BarSection.Start, vertical: false));
            Assert.Equal("Agregar un widget abajo", AddList.HereIn(BarSection.End, vertical: true));
            Assert.Equal("Agregar un widget aquí", AddList.Here);
        }
        Assert.Equal("Add a widget to the middle", AddList.HereIn(BarSection.Middle, vertical: false));
    }

    [Fact]
    public void DesktopsAreNumberedAndRoomIsKeptInTheLanguage()
    {
        using (InLanguage.Use("es"))
        {
            Assert.Equal("Escritorio 1", Switchers.Numbered(0));
            // The room the switcher keeps is Windows' "Desktop 88" in the language it is shown in.
            Assert.Equal("Escritorio 88", Switchers.NameRoom);
            Assert.Equal("Trabajo\nEscritorio 2 de 3", Switchers.DesktopTooltip("Trabajo", 1, 3));
        }
        Assert.Equal("Desktop 88", Switchers.NameRoom);
    }
}
