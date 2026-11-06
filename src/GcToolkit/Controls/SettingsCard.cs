using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace GcToolkit.Controls;

/// <summary>
/// A minimal settings row (icon, header, description, trailing content). Replaces the
/// CommunityToolkit SettingsCard, whose Uno build is not yet binary-compatible with Uno 7.
/// </summary>
public sealed partial class SettingsCard : ContentControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(object), typeof(SettingsCard),
        new PropertyMetadata(null, (d, _) => ((SettingsCard)d).SetAccessibleContentName()));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(object), typeof(SettingsCard), new PropertyMetadata(null));

    public static readonly DependencyProperty HeaderIconProperty = DependencyProperty.Register(
        nameof(HeaderIcon), typeof(IconElement), typeof(SettingsCard), new PropertyMetadata(null));

    public SettingsCard() => IsTabStop = false;

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public object? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public IconElement? HeaderIcon
    {
        get => (IconElement?)GetValue(HeaderIconProperty);
        set => SetValue(HeaderIconProperty, value);
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        SetAccessibleContentName();
    }

    // As the toolkit card did: name the trailing control after the header so a screen reader
    // announces which setting it changes. Buttons and text already announce their own content.
    private void SetAccessibleContentName()
    {
        if (Header is string { Length: > 0 } header
            && Content is UIElement element and not ButtonBase and not TextBlock
            && string.IsNullOrEmpty(AutomationProperties.GetName(element)))
        {
            AutomationProperties.SetName(element, header);
        }
    }
}
