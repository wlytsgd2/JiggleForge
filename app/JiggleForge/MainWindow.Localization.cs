using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace JiggleForge;

public static class Localization
{
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key",
        typeof(string),
        typeof(Localization),
        new PropertyMetadata(null, OnKeyChanged));

    public static string GetKey(DependencyObject element) =>
        (string)element.GetValue(KeyProperty);

    public static void SetKey(DependencyObject element, string value) =>
        element.SetValue(KeyProperty, value);

    private static void OnKeyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not FrameworkElement element || args.NewValue is not string key)
        {
            return;
        }

        element.Loaded -= LocalizedElement_Loaded;
        element.Loaded += LocalizedElement_Loaded;
        Apply(element, key);
    }

    private static void LocalizedElement_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            Apply(element, GetKey(element));
        }
    }

    private static void Apply(FrameworkElement element, string key)
    {
        switch (element)
        {
            case TextBlock textBlock:
                SetProperty(key, "Text", value => textBlock.Text = value);
                break;
            case ToggleSwitch toggleSwitch:
                SetProperty(key, "Header", value => toggleSwitch.Header = value);
                SetProperty(key, "OnContent", value => toggleSwitch.OnContent = value);
                SetProperty(key, "OffContent", value => toggleSwitch.OffContent = value);
                break;
            case TextBox textBox:
                SetProperty(key, "Header", value => textBox.Header = value);
                SetProperty(key, "PlaceholderText", value => textBox.PlaceholderText = value);
                break;
            case NumberBox numberBox:
                SetProperty(key, "Header", value => numberBox.Header = value);
                break;
            case ComboBox comboBox:
                SetProperty(key, "Header", value => comboBox.Header = value);
                SetProperty(key, "PlaceholderText", value => comboBox.PlaceholderText = value);
                break;
            case ContentControl contentControl:
                SetProperty(key, "Content", value => contentControl.Content = value);
                break;
        }

        SetProperty(key, "ToolTip", value => ToolTipService.SetToolTip(element, value));
    }

    private static void SetProperty(string key, string propertyName, Action<string> setter)
    {
        if (AppLanguageService.TryGetProperty(key, propertyName, out string value))
        {
            setter(value);
        }
    }
}
