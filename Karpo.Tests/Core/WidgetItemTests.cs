using static Karpo.Core.BarKind;

namespace Karpo.Tests.Core;

/// <summary>
/// A placed widget is a taskbar's entry (<see cref="BarItem"/>) or a folder's widget
/// (<see cref="CellItem"/>), both a <see cref="WidgetItem"/> (S5 step 6a). The file does not change
/// with the split, and that a folder's widget stands in its folder's view, the list, is filled in
/// where the file is read, since the file does not say.
/// </summary>
public sealed class WidgetItemTests
{
    /// <summary>
    /// A file as this build writes it: a taskbar with the records only an entry has and a list folder,
    /// its widgets with options and heights, each in its own words.
    /// </summary>
    // Today's shape, so the file is never repaired up to it.
    private static readonly string TheFile = $$"""
        {
          "version": {{Config.Shape}},
          "formats": {
            "temperature": 0,
            "sizes": 0
          },
          "onlineUpdateMinutes": 15,
          "screens": [],
          "startWithWindows": true,
          "layouts": [
            {
              "id": "L",
              "name": "Default",
              "icon": "\uE8A9",
              "type": "taskbar",
              "size": 48,
              "show": "reserve",
              "spacing": 6,
              "previews": true,
              "islands": [
                {
                  "id": "start",
                  "kind": "start",
                  "section": "start"
                },
                {
                  "id": "search",
                  "kind": "search",
                  "section": "start"
                },
                {
                  "id": "list",
                  "kind": "folderList",
                  "section": "start",
                  "folder": {
                    "face": {
                      "kind": "icon",
                      "options": {}
                    },
                    "lists": [
                      [
                        {
                          "island": {
                            "id": "now",
                            "kind": "weatherNow",
                            "size": "full",
                            "weather": {
                              "place": "Oslo",
                              "latitude": null,
                              "longitude": null,
                              "useDeviceLocation": false,
                              "isSet": false
                            }
                          }
                        },
                        {
                          "island": {
                            "id": "home",
                            "kind": "clock",
                            "size": "full",
                            "clock": {
                              "seconds": false,
                              "timeZone": "",
                              "label": "Home",
                              "view": "month"
                            }
                          }
                        },
                        {
                          "island": {
                            "id": "input",
                            "kind": "soundInput",
                            "height": {
                              "fixed": false,
                              "cells": 6
                            }
                          }
                        },
                        {
                          "island": {
                            "id": "mixer",
                            "kind": "soundMixer",
                            "height": {
                              "fixed": false,
                              "cells": 24
                            }
                          }
                        },
                        {
                          "island": {
                            "id": "gpu",
                            "kind": "performance",
                            "reading": "gpu",
                            "style": "graph",
                            "size": "full",
                            "performance": {
                              "showGraph": true,
                              "seconds": 1,
                              "gpuAdapter": ""
                            }
                          }
                        },
                        {
                          "island": {
                            "id": "saver",
                            "kind": "energySaver",
                            "size": "compact",
                            "switch": {
                              "showLabel": false
                            }
                          }
                        }
                      ]
                    ]
                  }
                }
              ]
            }
          ],
          "defaultLayoutId": "L",
          "lockTaskbar": true
        }
        """;

    /// <summary>
    /// Read and written back, byte for byte (the line ends are the writer's): the two records write
    /// the keys the one record always did, in the same order, and nothing is repaired.
    /// </summary>
    [Fact]
    public void TheFileReadsAndWritesBackByteForByte()
    {
        var config = Config.Parse(TheFile);

        Assert.False(config.Repair());
        // The file ends with a line end, which a raw string cannot end with.
        Assert.Equal(TheFile.ReplaceLineEndings("\n") + "\n", config.ToJson().ReplaceLineEndings("\n"));
        _ = Configs.RoundTrip(config, out var again);
        Assert.False(again);
    }

    /// <summary>
    /// Where the file is read, a folder's widgets stand in its view, the list: each is a line and
    /// knows its own kind as a line.
    /// </summary>
    [Fact]
    public void AFoldersWidgetsStandInItsViewOnceRead()
    {
        var islands = Configs.Repaired(TheFile).Layouts[0].Islands;
        var list = islands.Single(island => island.Id == "list").Folder!.Cells.Select(cell => cell.Island).ToList();

        Assert.All(list, line => Assert.Equal(FolderView.List, line.View));
        Assert.All(list, line => Assert.Equal(ListWidgets.Find(line.Kind)!.Kind, line.List));
        Assert.Equal(ListKind.Clock, list.Single(line => line.Id == "home").List);
    }

    /// <summary>
    /// A widget its folder's view has no form of goes where the file is read, and the file is
    /// written back without it: the volume slider (its list form is the sound's volume), the widgets
    /// board, the date (the person took it out of lists, 2026-09-26). What the view has stays, standing
    /// in it. "sound" and "weather" were such words once; they are the widgets with styles now (§209).
    /// </summary>
    [Fact]
    public void AWidgetItsViewHasNoFormOfIsDroppedWhereTheFileIsRead()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "L", "islands": [
              { "id": "l", "kind": "folderList", "folder": { "lists": [ [
                { "island": { "id": "wifi", "kind": "volume" } },
                { "island": { "id": "w", "kind": "widgetsBoard" } },
                { "island": { "id": "t2", "kind": "date" } },
                { "island": { "id": "n", "kind": "weatherNow" } } ] ] } } ] } ] }
            """, out var changed);

        Assert.True(changed);
        var list = config.Layouts[0].Islands.Single(island => island.Id == "l").Folder!.Cells;
        Assert.Equal(["n"], list.Select(cell => cell.Island.Id));
        Assert.Equal(ListKind.WeatherNow, list[0].Island.List);
    }

    /// <summary>
    /// A copy stands where the original does: a folder's copy stands its widgets in its view again,
    /// since the copy goes through the file's own form, and a widget's copy keeps its view.
    /// </summary>
    [Fact]
    public void ACopyStandsWhereTheOriginalDoes()
    {
        var folder = Configs.Repaired(TheFile).Layouts[0].Islands.Single(island => island.Id == "list");

        var clone = folder.Clone();
        var copy = folder.Copy();

        Assert.All(clone.Folder!.Cells, cell => Assert.Equal(FolderView.List, cell.Island.View));
        Assert.All(copy.Folder!.Cells, cell => Assert.Equal(FolderView.List, cell.Island.View));
        var line = folder.Folder!.Cells[0].Island;
        var lineClone = line.Clone();
        var lineCopy = line.Copy();
        Assert.Equal((line.Id, (FolderView?)FolderView.List), (lineClone.Id, lineClone.View));
        Assert.NotEqual(line.Id, lineCopy.Id);
        Assert.Equal(FolderView.List, lineCopy.View);
        Assert.Equal(line.Weather!.Place, lineCopy.Weather!.Place);
        Assert.NotSame(line.Weather, lineCopy.Weather);
    }

    /// <summary>
    /// A new line stands in a list as the list's own, and starts at its line's measurement, which
    /// for a taskbar entry with a list form is not the width it starts at on the bar.
    /// </summary>
    [Fact]
    public void ANewLineStandsInTheList()
    {
        var line = ListKind.Clock.New();

        Assert.Equal(("clock", (FolderView?)FolderView.List, (ListKind?)ListKind.Clock), (line.Kind, line.View, line.List));
        Assert.NotEqual(WidgetBounds.DefaultFor(BarKind.NightLight), ListKind.Calendar.ListRange());
        // A clock is its size's form (§432): a new one starts Full, and has no height.
        Assert.Equal(((LineHeight?)null, (LineSize?)LineSize.Full), (line.Height, line.Size));
    }

    /// <summary>A widget put into a folder stands in the folder's view, whatever it stood in before.</summary>
    [Fact]
    public void AWidgetPutIntoAFolderStandsInItsView()
    {
        var folder = FolderList.New();
        var line = new CellItem { Kind = "drive" };
        Assert.Null(line.View);

        Assert.NotNull(Karpo.Core.Folders.Add(FolderList, folder.Folder!, line));

        Assert.Equal((ListKind?)ListKind.Drive, line.List);
    }
}
