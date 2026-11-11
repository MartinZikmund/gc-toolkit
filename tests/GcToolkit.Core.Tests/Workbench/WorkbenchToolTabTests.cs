using CommunityToolkit.Mvvm.ComponentModel;
using GcToolkit.Core.ViewModels;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

[TestClass]
public partial class WorkbenchToolTabTests
{
    private static ToolInputState StateWith(string text) => FakeStatefulTool.StateWith(text);

    [TestMethod]
    public void Attach_WithSavedState_RestoresItIntoTheTool()
    {
        WorkbenchToolTab tab = new("Fake", "Fake", null, typeof(FakeStatefulTool), StateWith("saved"));
        FakeStatefulTool tool = new();

        tab.Attach(tool);

        Assert.AreEqual("saved", tool.Text);
    }

    [TestMethod]
    public void Attach_ToolEdited_CapturesAndRaisesStateChanged()
    {
        WorkbenchToolTab tab = new("Fake", "Fake", null, typeof(FakeStatefulTool));
        FakeStatefulTool tool = new();
        tab.Attach(tool);
        var raised = 0;
        tab.StateChanged += (_, _) => raised++;

        tool.Text = "typed";

        Assert.AreEqual(1, raised);
        Assert.AreEqual("typed", tab.State!.GetString("text"));
    }

    [TestMethod]
    public void Attach_OutputOnlyChange_DoesNotRaise()
    {
        WorkbenchToolTab tab = new("Fake", "Fake", null, typeof(FakeStatefulTool), StateWith("saved"));
        FakeStatefulTool tool = new();
        tab.Attach(tool);
        var raised = 0;
        tab.StateChanged += (_, _) => raised++;

        tool.Output = "derived";

        Assert.AreEqual(0, raised);
    }

    [TestMethod]
    public void Attach_SameToolTwice_SubscribesOnce()
    {
        WorkbenchToolTab tab = new("Fake", "Fake", null, typeof(FakeStatefulTool));
        FakeStatefulTool tool = new();
        tab.Attach(tool);
        tab.Attach(tool);
        var raised = 0;
        tab.StateChanged += (_, _) => raised++;

        tool.Text = "typed";

        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public void Attach_ToolWithoutState_KeepsNoState()
    {
        WorkbenchToolTab tab = new("Fake", "Fake", null, typeof(PlainTool));
        PlainTool tool = new();
        tab.Attach(tool);

        tool.Text = "typed";

        Assert.IsNull(tab.State);
    }

    [TestMethod]
    public void Detach_ToolEdited_NoLongerRaises()
    {
        WorkbenchToolTab tab = new("Fake", "Fake", null, typeof(FakeStatefulTool));
        FakeStatefulTool tool = new();
        tab.Attach(tool);
        var raised = 0;
        tab.StateChanged += (_, _) => raised++;

        tab.Detach();
        tool.Text = "typed";

        Assert.AreEqual(0, raised);
    }

    private sealed partial class PlainTool : ObservableObject
    {
        [ObservableProperty]
        public partial string Text { get; set; } = string.Empty;
    }
}
