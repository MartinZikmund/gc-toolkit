using System.Text.Json.Serialization;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.ViewModels.Tools;
using GcToolkit.Core.Workbench;

namespace GcToolkit.Core.Serialization;

/// <summary>
/// Source-generated JSON contracts for everything persisted through <c>IPreferences</c>. Trimmed
/// heads (the WASM release publish) disable reflection-based System.Text.Json, so <c>Preferences</c>
/// is registered with this context as its resolver — a type stored via <c>SetComplex</c> that is
/// missing here throws at runtime, on those heads only.
/// </summary>
[JsonSerializable(typeof(List<RecentEntry>))]
[JsonSerializable(typeof(List<FavoriteToolEntry>))]
[JsonSerializable(typeof(ElementTheme))]
[JsonSerializable(typeof(SavedVariableSheet))]
[JsonSerializable(typeof(RhumbLineState))]
[JsonSerializable(typeof(WorkbenchSessionState))]
public sealed partial class PreferencesJsonContext : JsonSerializerContext
{
}
