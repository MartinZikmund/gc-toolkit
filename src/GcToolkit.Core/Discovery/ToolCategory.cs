namespace GcToolkit.Core.Discovery;

/// <summary>
/// The closed set of tool categories — the required level that directly holds tools.
/// Per-member metadata is derived <b>by convention</b> (research R6), with no per-member
/// attributes or tables:
/// <list type="bullet">
///   <item><description><c>Id</c> = member name (e.g. <c>Coordinates</c>).</description></item>
///   <item><description><c>NameKey</c> = <c>Category_&lt;Member&gt;</c> (e.g. <c>Category_Coordinates</c>).</description></item>
///   <item><description><c>Order</c> = declaration index (so the order below is the display order).</description></item>
///   <item><description>Icon = <c>Assets/Icons/Categories/&lt;Member&gt;.png</c> in the app head (runtime fallback, R7).</description></item>
/// </list>
/// Adding a category = adding a member here, its <c>Category_&lt;Member&gt;</c> resw string, and a bitmap.
/// </summary>
public enum ToolCategory
{
    Coordinates,
    Ciphers,
    Numbers,
    Alphabets,
    Text,
}
