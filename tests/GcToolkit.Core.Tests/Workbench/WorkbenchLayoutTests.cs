using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Tests.Workbench;

[TestClass]
public class WorkbenchLayoutTests
{
    [TestMethod]
    [DataRow(0d, WorkbenchLayoutMode.Compact)]
    [DataRow(360d, WorkbenchLayoutMode.Compact)]
    [DataRow(639d, WorkbenchLayoutMode.Compact)]
    [DataRow(640d, WorkbenchLayoutMode.Medium)]
    [DataRow(839d, WorkbenchLayoutMode.Medium)]
    [DataRow(840d, WorkbenchLayoutMode.Wide)]
    [DataRow(1920d, WorkbenchLayoutMode.Wide)]
    public void ModeFor_Width_ReturnsMode(double width, WorkbenchLayoutMode expected)
        => Assert.AreEqual(expected, WorkbenchLayout.ModeFor(width));

    [TestMethod]
    [DataRow(100d, 1200d, 320d)]
    [DataRow(500d, 1200d, 500d)]
    [DataRow(1000d, 1200d, 800d)]
    [DataRow(500d, 0d, 500d)]
    [DataRow(500d, 600d, 320d)]
    public void ClampToolPaneWidth_Requested_StaysInRange(double requested, double available, double expected)
        => Assert.AreEqual(expected, WorkbenchLayout.ClampToolPaneWidth(requested, available));
}
