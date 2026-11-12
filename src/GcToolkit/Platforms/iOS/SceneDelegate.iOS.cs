using Foundation;
using Uno.UI.Runtime.AppleUIKit;

namespace GcToolkit.iOS;

// iOS 27 SDK apps must use the UIKit scene lifecycle; registered by name in Info.plist.
[Register("SceneDelegate")]
public class SceneDelegate : UnoUISceneDelegate;
