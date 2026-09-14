using WpfBorder = System.Windows.Controls.Border;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfContentPresenter = System.Windows.Controls.ContentPresenter;
using WpfControl = System.Windows.Controls.Control;
using WpfFrameworkElementFactory = System.Windows.FrameworkElementFactory;
using WpfGrid = System.Windows.Controls.Grid;
using WpfItemsPresenter = System.Windows.Controls.ItemsPresenter;
using WpfPopup = System.Windows.Controls.Primitives.Popup;
using WpfScrollViewer = System.Windows.Controls.ScrollViewer;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfToggleButton = System.Windows.Controls.Primitives.ToggleButton;

namespace QS3D.AutoCAD.UI;

/// <summary>
/// Owns QS3D WPF control templates whose hover/pressed chrome must not fall back to
/// the host Windows theme. This keeps light foreground text on dark QS3D surfaces.
/// </summary>
internal static class Qs3dControlChrome
{
    internal static void ApplyWpfControlChrome(System.Windows.DependencyObject root, Qs3dThemePalette palette)
    {
        ApplyOne(root, palette);
        foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(root))
        {
            if (child is System.Windows.DependencyObject dependencyChild)
                ApplyWpfControlChrome(dependencyChild, palette);
        }
    }

    internal static void ApplyButtonChrome(WpfButton button, Qs3dThemePalette palette) =>
        button.Template = CreateButtonTemplate(palette);

    internal static void ApplyComboBoxChrome(WpfComboBox comboBox, Qs3dThemePalette palette) =>
        comboBox.Template = CreateComboBoxTemplate(palette);

    internal static void ApplyComboBoxItemChrome(WpfComboBoxItem item, Qs3dThemePalette palette) =>
        item.Template = CreateComboBoxItemTemplate(palette);

    private static void ApplyOne(System.Windows.DependencyObject item, Qs3dThemePalette palette)
    {
        if (item is WpfComboBoxItem comboBoxItem)
            ApplyComboBoxItemChrome(comboBoxItem, palette);
        else if (item is WpfComboBox comboBox)
            ApplyComboBoxChrome(comboBox, palette);
        else if (item is WpfButton button)
            ApplyButtonChrome(button, palette);
    }

    private static System.Windows.Controls.ControlTemplate CreateButtonTemplate(Qs3dThemePalette palette)
    {
        var template = new System.Windows.Controls.ControlTemplate(typeof(WpfButton));
        var chrome = new WpfFrameworkElementFactory(typeof(WpfBorder), "Chrome");
        chrome.SetValue(WpfBorder.BackgroundProperty, new System.Windows.TemplateBindingExtension(WpfControl.BackgroundProperty));
        chrome.SetValue(WpfBorder.BorderBrushProperty, new System.Windows.TemplateBindingExtension(WpfControl.BorderBrushProperty));
        chrome.SetValue(WpfBorder.BorderThicknessProperty, new System.Windows.TemplateBindingExtension(WpfControl.BorderThicknessProperty));
        chrome.SetValue(WpfBorder.CornerRadiusProperty, new System.Windows.CornerRadius(4));
        chrome.SetValue(System.Windows.UIElement.SnapsToDevicePixelsProperty, true);

        var presenter = new WpfFrameworkElementFactory(typeof(WpfContentPresenter));
        presenter.SetValue(WpfContentPresenter.ContentProperty, new System.Windows.TemplateBindingExtension(System.Windows.Controls.ContentControl.ContentProperty));
        presenter.SetValue(WpfContentPresenter.ContentTemplateProperty, new System.Windows.TemplateBindingExtension(System.Windows.Controls.ContentControl.ContentTemplateProperty));
        presenter.SetValue(WpfContentPresenter.MarginProperty, new System.Windows.TemplateBindingExtension(WpfControl.PaddingProperty));
        presenter.SetValue(WpfContentPresenter.HorizontalAlignmentProperty, new System.Windows.TemplateBindingExtension(WpfControl.HorizontalContentAlignmentProperty));
        presenter.SetValue(WpfContentPresenter.VerticalAlignmentProperty, new System.Windows.TemplateBindingExtension(WpfControl.VerticalContentAlignmentProperty));
        presenter.SetValue(WpfContentPresenter.RecognizesAccessKeyProperty, true);
        chrome.AppendChild(presenter);
        template.VisualTree = chrome;

        var hover = new System.Windows.Trigger { Property = System.Windows.UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new System.Windows.Setter(WpfBorder.BorderBrushProperty, palette.Accent, "Chrome"));
        template.Triggers.Add(hover);

        var pressed = new System.Windows.Trigger { Property = WpfButton.IsPressedProperty, Value = true };
        pressed.Setters.Add(new System.Windows.Setter(WpfBorder.BackgroundProperty, palette.Selection, "Chrome"));
        pressed.Setters.Add(new System.Windows.Setter(WpfBorder.BorderBrushProperty, palette.Accent, "Chrome"));
        template.Triggers.Add(pressed);

        var focused = new System.Windows.Trigger { Property = System.Windows.UIElement.IsKeyboardFocusWithinProperty, Value = true };
        focused.Setters.Add(new System.Windows.Setter(WpfBorder.BorderBrushProperty, palette.Accent, "Chrome"));
        template.Triggers.Add(focused);
        return template;
    }

    private static System.Windows.Controls.ControlTemplate CreateComboBoxTemplate(Qs3dThemePalette palette)
    {
        var template = new System.Windows.Controls.ControlTemplate(typeof(WpfComboBox));
        var root = new WpfFrameworkElementFactory(typeof(WpfGrid));

        var chrome = new WpfFrameworkElementFactory(typeof(WpfBorder), "Chrome");
        chrome.SetValue(WpfBorder.BackgroundProperty, new System.Windows.TemplateBindingExtension(WpfControl.BackgroundProperty));
        chrome.SetValue(WpfBorder.BorderBrushProperty, new System.Windows.TemplateBindingExtension(WpfControl.BorderBrushProperty));
        chrome.SetValue(WpfBorder.BorderThicknessProperty, new System.Windows.TemplateBindingExtension(WpfControl.BorderThicknessProperty));
        chrome.SetValue(WpfBorder.CornerRadiusProperty, new System.Windows.CornerRadius(4));
        root.AppendChild(chrome);

        var selected = new WpfFrameworkElementFactory(typeof(WpfContentPresenter));
        selected.SetValue(WpfContentPresenter.ContentProperty, new System.Windows.TemplateBindingExtension(WpfComboBox.SelectionBoxItemProperty));
        selected.SetValue(WpfContentPresenter.ContentTemplateProperty, new System.Windows.TemplateBindingExtension(WpfComboBox.SelectionBoxItemTemplateProperty));
        selected.SetValue(WpfContentPresenter.ContentStringFormatProperty, new System.Windows.TemplateBindingExtension(WpfComboBox.SelectionBoxItemStringFormatProperty));
        selected.SetValue(WpfContentPresenter.MarginProperty, new System.Windows.Thickness(8, 0, 28, 0));
        selected.SetValue(WpfContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
        selected.SetValue(System.Windows.UIElement.IsHitTestVisibleProperty, false);
        root.AppendChild(selected);

        var arrow = new WpfFrameworkElementFactory(typeof(WpfTextBlock));
        arrow.SetValue(WpfTextBlock.TextProperty, "▾");
        arrow.SetValue(WpfTextBlock.ForegroundProperty, palette.Muted);
        arrow.SetValue(WpfTextBlock.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Right);
        arrow.SetValue(WpfTextBlock.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
        arrow.SetValue(WpfTextBlock.MarginProperty, new System.Windows.Thickness(0, 0, 8, 0));
        arrow.SetValue(System.Windows.UIElement.IsHitTestVisibleProperty, false);
        root.AppendChild(arrow);

        var toggle = new WpfFrameworkElementFactory(typeof(WpfToggleButton));
        toggle.SetValue(WpfToggleButton.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        toggle.SetValue(WpfToggleButton.BorderThicknessProperty, new System.Windows.Thickness(0));
        toggle.SetValue(WpfToggleButton.FocusableProperty, false);
        toggle.SetValue(WpfToggleButton.TemplateProperty, CreateTransparentToggleTemplate());
        toggle.SetBinding(WpfToggleButton.IsCheckedProperty, new System.Windows.Data.Binding("IsDropDownOpen")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
            Mode = System.Windows.Data.BindingMode.TwoWay
        });
        root.AppendChild(toggle);

        var popup = new WpfFrameworkElementFactory(typeof(WpfPopup), "PART_Popup");
        popup.SetValue(WpfPopup.PlacementProperty, System.Windows.Controls.Primitives.PlacementMode.Bottom);
        popup.SetValue(WpfPopup.AllowsTransparencyProperty, true);
        popup.SetValue(WpfPopup.StaysOpenProperty, false);
        popup.SetBinding(WpfPopup.IsOpenProperty, new System.Windows.Data.Binding("IsDropDownOpen")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
            Mode = System.Windows.Data.BindingMode.TwoWay
        });
        popup.SetBinding(WpfPopup.PlacementTargetProperty, new System.Windows.Data.Binding
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });
        popup.SetBinding(System.Windows.FrameworkElement.WidthProperty, new System.Windows.Data.Binding("ActualWidth")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
        });

        var popupBorder = new WpfFrameworkElementFactory(typeof(WpfBorder));
        popupBorder.SetValue(WpfBorder.BackgroundProperty, palette.Card);
        popupBorder.SetValue(WpfBorder.BorderBrushProperty, palette.Border);
        popupBorder.SetValue(WpfBorder.BorderThicknessProperty, new System.Windows.Thickness(1));
        popupBorder.SetValue(WpfBorder.PaddingProperty, new System.Windows.Thickness(2));

        var scroll = new WpfFrameworkElementFactory(typeof(WpfScrollViewer));
        scroll.SetValue(WpfScrollViewer.CanContentScrollProperty, true);
        scroll.SetValue(WpfScrollViewer.VerticalScrollBarVisibilityProperty, System.Windows.Controls.ScrollBarVisibility.Auto);
        scroll.SetValue(WpfScrollViewer.HorizontalScrollBarVisibilityProperty, System.Windows.Controls.ScrollBarVisibility.Disabled);
        scroll.AppendChild(new WpfFrameworkElementFactory(typeof(WpfItemsPresenter)));
        popupBorder.AppendChild(scroll);
        popup.AppendChild(popupBorder);
        root.AppendChild(popup);
        template.VisualTree = root;

        var hover = new System.Windows.Trigger { Property = System.Windows.UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new System.Windows.Setter(WpfBorder.BackgroundProperty, palette.CardHover, "Chrome"));
        hover.Setters.Add(new System.Windows.Setter(WpfBorder.BorderBrushProperty, palette.Accent, "Chrome"));
        template.Triggers.Add(hover);

        var open = new System.Windows.Trigger { Property = WpfComboBox.IsDropDownOpenProperty, Value = true };
        open.Setters.Add(new System.Windows.Setter(WpfBorder.BackgroundProperty, palette.Selection, "Chrome"));
        open.Setters.Add(new System.Windows.Setter(WpfBorder.BorderBrushProperty, palette.Accent, "Chrome"));
        template.Triggers.Add(open);

        var focused = new System.Windows.Trigger { Property = System.Windows.UIElement.IsKeyboardFocusWithinProperty, Value = true };
        focused.Setters.Add(new System.Windows.Setter(WpfBorder.BorderBrushProperty, palette.Accent, "Chrome"));
        template.Triggers.Add(focused);
        return template;
    }

    private static System.Windows.Controls.ControlTemplate CreateTransparentToggleTemplate()
    {
        var template = new System.Windows.Controls.ControlTemplate(typeof(WpfToggleButton));
        var border = new WpfFrameworkElementFactory(typeof(WpfBorder));
        border.SetValue(WpfBorder.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        template.VisualTree = border;
        return template;
    }

    private static System.Windows.Controls.ControlTemplate CreateComboBoxItemTemplate(Qs3dThemePalette palette)
    {
        var template = new System.Windows.Controls.ControlTemplate(typeof(WpfComboBoxItem));
        var chrome = new WpfFrameworkElementFactory(typeof(WpfBorder), "ItemChrome");
        chrome.SetValue(WpfBorder.BackgroundProperty, new System.Windows.TemplateBindingExtension(WpfControl.BackgroundProperty));
        chrome.SetValue(WpfBorder.PaddingProperty, new System.Windows.TemplateBindingExtension(WpfControl.PaddingProperty));

        var presenter = new WpfFrameworkElementFactory(typeof(WpfContentPresenter));
        presenter.SetValue(WpfContentPresenter.ContentProperty, new System.Windows.TemplateBindingExtension(System.Windows.Controls.ContentControl.ContentProperty));
        presenter.SetValue(WpfContentPresenter.ContentTemplateProperty, new System.Windows.TemplateBindingExtension(System.Windows.Controls.ContentControl.ContentTemplateProperty));
        presenter.SetValue(WpfContentPresenter.HorizontalAlignmentProperty, new System.Windows.TemplateBindingExtension(WpfControl.HorizontalContentAlignmentProperty));
        presenter.SetValue(WpfContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
        chrome.AppendChild(presenter);
        template.VisualTree = chrome;

        var highlighted = new System.Windows.Trigger { Property = WpfComboBoxItem.IsHighlightedProperty, Value = true };
        highlighted.Setters.Add(new System.Windows.Setter(WpfBorder.BackgroundProperty, palette.CardHover, "ItemChrome"));
        template.Triggers.Add(highlighted);

        var selected = new System.Windows.Trigger { Property = WpfComboBoxItem.IsSelectedProperty, Value = true };
        selected.Setters.Add(new System.Windows.Setter(WpfBorder.BackgroundProperty, palette.Selection, "ItemChrome"));
        template.Triggers.Add(selected);
        return template;
    }
}