using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace KamuiT;

/// <summary>
/// Janela separada (não overlay WPF) porque o terminal usa HwndHost:
/// conteúdo WPF não renderiza por cima dele (airspace).
/// </summary>
public class LimboWindow : Window
{
    private static readonly Brush Bg = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x13));
    private static readonly Brush CardBorder = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x33));
    private static readonly Brush TextPrimary = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
    private static readonly Brush TextMuted = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x93));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0xB0, 0x30, 0x30));
    private static readonly Brush ItemHover = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x26));
    private static readonly Brush ItemSelected = new SolidColorBrush(Color.FromRgb(0x2A, 0x1A, 0x1A));

    private readonly ListBox _list;
    private readonly Action<TermTab> _restore;

    public LimboWindow(IReadOnlyList<TermTab> limboTabs, Action<TermTab> restore)
    {
        _restore = restore;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.Height;
        Width = 460;
        Topmost = true;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
            else if (e.Key == Key.Enter) RestoreSelected();
        };

        var card = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = Bg,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20, 18, 20, 16),
            Margin = new Thickness(8),
            Effect = new DropShadowEffect
            {
                Color = Colors.Black, BlurRadius = 24, ShadowDepth = 4, Opacity = 0.6,
            },
        };

        var stack = new StackPanel();

        // Header
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock
        {
            Text = "\u25CF", // ●
            Foreground = Accent,
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });
        header.Children.Add(new TextBlock
        {
            Text = "Limbo",
            Foreground = TextPrimary,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
        });
        header.Children.Add(new TextBlock
        {
            Text = $"   {limboTabs.Count} sessão(ões) em espera",
            Foreground = TextMuted,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 2),
        });
        stack.Children.Add(header);

        stack.Children.Add(new TextBlock
        {
            Text = "Enter restaura · Esc fecha",
            Foreground = TextMuted,
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 12),
        });

        // Lista de abas
        _list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);

        var itemTemplate = new DataTemplate(typeof(TermTab));
        var itemBorder = new FrameworkElementFactory(typeof(Border));
        itemBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        itemBorder.SetValue(Border.PaddingProperty, new Thickness(10, 8, 10, 8));
        var title = new FrameworkElementFactory(typeof(TextBlock));
        title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(TermTab.Title)));
        title.SetValue(TextBlock.ForegroundProperty, TextPrimary);
        title.SetValue(TextBlock.FontSizeProperty, 13.0);
        title.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        itemBorder.AppendChild(title);
        itemTemplate.VisualTree = itemBorder;
        _list.ItemTemplate = itemTemplate;

        var itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
        itemStyle.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0, 1, 0, 1)));
        itemStyle.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        var template = new ControlTemplate(typeof(ListBoxItem));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        var bd = new FrameworkElementFactory(typeof(Border));
        bd.Name = "Bd"; // nome pra os triggers acharem (factory usa .Name, não SetValue(NameProperty))
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        bd.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        bd.AppendChild(presenter);
        template.VisualTree = bd;
        var hoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, ItemHover, "Bd"));
        var selTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selTrigger.Setters.Add(new Setter(Border.BackgroundProperty, ItemSelected, "Bd"));
        template.Triggers.Add(hoverTrigger);
        template.Triggers.Add(selTrigger);
        itemStyle.Setters.Add(new Setter(ListBoxItem.TemplateProperty, template));
        _list.ItemContainerStyle = itemStyle;

        foreach (var tab in limboTabs)
            _list.Items.Add(tab);
        if (_list.Items.Count > 0)
            _list.SelectedIndex = 0;
        _list.MouseDoubleClick += (_, _) => RestoreSelected();

        stack.Children.Add(_list);
        card.Child = stack;
        Content = card;
        // Sem auto-close por Deactivated: o foco de ativação fica no HWND do terminal
        // (não-WPF), o que disparava Deactivated e fechava o popup no mesmo instante.
        // Fecha via Esc, Enter ou duplo-clique.
    }

    private void RestoreSelected()
    {
        if (_list.SelectedItem is TermTab tab)
        {
            _restore(tab);
            Close();
        }
    }
}
