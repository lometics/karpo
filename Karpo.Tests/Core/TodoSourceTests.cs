// Where a to-do line's tasks come from (TodoOptions, docs\accounts.md "To Do"): the notebook's list on
// this PC unless a Microsoft account is named, a To Do list only with its account, and a line of
// before — no account in the file — read and written as it was.

using System.Text.Json;
using Karpo.Core;

namespace Karpo.Tests.Core;

public sealed class TodoSourceTests
{
    [Fact]
    public void ALineOfBeforeIsTheNotebooksAndWritesNoAccount()
    {
        var line = ListKind.Todo.New();
        line.Todo!.List = "groceries";
        var json = JsonSerializer.Serialize(line, ConfigJson.Default.CellItem);
        Assert.DoesNotContain("account", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("taskList", json, StringComparison.OrdinalIgnoreCase);

        var back = JsonSerializer.Deserialize(json, ConfigJson.Default.CellItem)!;
        back.Normalize();
        Assert.False(back.Todo!.IsToDo);
        Assert.Equal("groceries", back.Todo.List);
    }

    [Fact]
    public void AToDoListIsItsAccountAndListAndComesBack()
    {
        var line = ListKind.Todo.New();
        line.Todo!.Account = " me@outlook.com ";
        line.Todo.TaskList = "AAMkAD=";
        line.Normalize();
        Assert.True(line.Todo.IsToDo);
        Assert.Equal("me@outlook.com", line.Todo.Account);

        var back = JsonSerializer.Deserialize(JsonSerializer.Serialize(line, ConfigJson.Default.CellItem), ConfigJson.Default.CellItem)!;
        back.Normalize();
        Assert.Equal(("me@outlook.com", "AAMkAD="), (back.Todo!.Account, back.Todo.TaskList));
    }

    [Fact]
    public void AListWithoutItsAccountIsNoneAndABlankAccountIsThisPc()
    {
        var line = ListKind.Todo.New();
        line.Todo!.Account = "  ";
        line.Todo.TaskList = "AAMkAD=";
        line.Normalize();
        Assert.False(line.Todo.IsToDo);
        Assert.Null(line.Todo.Account);
        Assert.Null(line.Todo.TaskList);
    }
}
