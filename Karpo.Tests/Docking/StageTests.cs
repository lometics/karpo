// The stage's switch (Karpo.Dock\Stage.cs): Karpo.exe --stage x,y,w,h[,dpi][;…], the dock on made-up
// displays for a recording or a test. Read only: nothing here turns a stage on for the rest of the tests.

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Docking;

public sealed class StageTests
{
    [Fact]
    public void NoSwitchIsNoStage()
    {
        Assert.Null(Stage.Read(["--gpu"], isolated: true, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void ARectangleOfTheLeftDisplayAtItsDpi()
    {
        var read = Stage.Read(["--stage", "-2304,72,2048,1280,144"], isolated: true, out var error);
        Assert.Null(error);
        Assert.Equal([(new RectI(-2304, 72, -256, 1352), 144u)], read);
    }

    [Fact]
    public void WithoutADpiItIs96()
    {
        Assert.Equal(96u, Stage.Read(["--stage", "0,0,1366,854"], isolated: true, out _)![0].Dpi);
    }

    /// <summary>Two screens side by side, as a test of edit mode on several displays has them: one a rectangle, after a ';'.</summary>
    [Fact]
    public void ADisplayARectangle()
    {
        var read = Stage.Read(["--stage", "-2560,0,1280,1440;-1280,0,1280,1440,144"], isolated: true, out var error);
        Assert.Null(error);
        Assert.Equal([(new RectI(-2560, 0, -1280, 1440), 96u), (new RectI(-1280, 0, 0, 1440), 144u)], read);
    }

    [Fact]
    public void NeverOnThePersonsOwnConfig()
    {
        Assert.Null(Stage.Read(["--stage", "0,0,1366,854"], isolated: false, out var error));
        Assert.Contains("KARPO_CONFIG_DIR", error);
    }

    [Theory]
    [InlineData("0,0,1366")]
    [InlineData("0,0,0,854")]
    [InlineData("0,0,1366,-1")]
    [InlineData("0,0,1366,854,0")]
    [InlineData("a,0,1366,854")]
    [InlineData("0,0,1366,854,144,1")]
    [InlineData("--gpu")]
    [InlineData("0,0,1366,854;")]
    [InlineData("0,0,1366,854;0,0,1366")]
    public void AWrongRectangleSaysHow(string given)
    {
        Assert.Null(Stage.Read(["--stage", given], isolated: true, out var error));
        Assert.StartsWith("usage:", error);
    }

    [Fact]
    public void ASwitchWithNothingAfterIt()
    {
        Assert.Null(Stage.Read(["--stage"], isolated: true, out var error));
        Assert.StartsWith("usage:", error);
    }
}
