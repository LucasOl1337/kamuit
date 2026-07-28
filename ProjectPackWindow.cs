using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace KamuiT;

/// <summary>
/// Abre N terminais já no diretório do projeto (sem cd em cada aba).
/// Janela separada por airspace do HwndHost (igual Limbo).
/// </summary>
public class ProjectPackWindow : Window
{
    private static readonly Brush Bg = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x13));
    private static readonly Brush CardBorder = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x33));
    private static readonly Brush TextPrimary = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
    private static readonly Brush TextMuted = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x93));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
    private static readonly Brush ItemHover = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x26));
    private static readonly Brush ItemSelected = new SolidColorBrush(Color.FromRgb(0x1A, 0x2A, 0x3A));

    private readonly string _projectsRoot;
    private readonly ListBox _list;
    private readonly TextBox _countBox;
    private readonly Action<string, int> _open;

    public ProjectPackWindow(string projectsRoot, Action<string, int> open)
    {
        _projectsRoot = projectsRoot;
        _open = open;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.Height;
        Width = 480;
        Topmost = true;
        KeyDown += OnKeyDown;

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

        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock
        {
            Text = "\u25CF",
            Foreground = Accent,
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });
        header.Children.Add(new TextBlock
        {
            Text = "Abrir projeto",
            Foreground = TextPrimary,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
        });
        header.Children.Add(new TextBlock
        {
            Text = "   N abas já no folder",
            Foreground = TextMuted,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        });
        stack.Children.Add(header);

        stack.Children.Add(new TextBlock
        {
            Text = projectsRoot,
            Foreground = TextMuted,
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 10),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        _list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MaxHeight = 320,
            Padding = new Thickness(0),
            FocusVisualStyle = null,
        };
        _list.Resources.Add(SystemColors.HighlightBrushKey, ItemSelected);
        _list.Resources.Add(SystemColors.InactiveSelectionHighlightBrushKey, ItemSelected);
        _list.Resources.Add(SystemColors.ControlBrushKey, Brushes.Transparent);
        _list.MouseDoubleClick += (_, _) => Confirm();

        var itemTemplate = new DataTemplate(typeof(string));
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding());
        factory.SetValue(TextBlock.ForegroundProperty, TextPrimary);
        factory.SetValue(TextBlock.PaddingProperty, new Thickness(10, 8, 10, 8));
        factory.SetValue(TextBlock.FontSizeProperty, 13.0);
        itemTemplate.VisualTree = factory;
        _list.ItemTemplate = itemTemplate;

        var containerStyle = new Style(typeof(ListBoxItem));
        containerStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        containerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        containerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        containerStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, ItemHover));
        containerStyle.Triggers.Add(hover);
        _list.ItemContainerStyle = containerStyle;

        foreach (var name in ListProjectFolders(projectsRoot))
            _list.Items.Add(name);

        if (_list.Items.Count > 0)
            _list.SelectedIndex = 0;

        stack.Children.Add(_list);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 14, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(new TextBlock
        {
            Text = "Abas",
            Foreground = TextMuted,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        });

        _countBox = new TextBox
        {
            Text = "3",
            Width = 40,
            FontSize = 14,
            Foreground = TextPrimary,
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22)),
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 4, 8, 4),
            CaretBrush = TextPrimary,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(_countBox);

        row.Children.Add(new TextBlock
        {
            Text = "  (1–9)   Enter = abrir · Esc = fechar · 1–9 = qtd",
            Foreground = TextMuted,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var openBtn = new Button
        {
            Content = "Abrir",
            Margin = new Thickness(16, 0, 0, 0),
            Padding = new Thickness(14, 6, 14, 6),
            Foreground = TextPrimary,
            Background = Accent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Focusable = false,
        };
        openBtn.Click += (_, _) => Confirm();
        row.Children.Add(openBtn);

        stack.Children.Add(row);
        card.Child = stack;
        Content = card;

        Loaded += (_, _) =>
        {
            _list.Focus();
            if (_list.SelectedItem is not null)
                _list.ScrollIntoView(_list.SelectedItem);
        };
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Enter)
        {
            Confirm();
            e.Handled = true;
            return;
        }
        if (e.Key is >= Key.D1 and <= Key.D9 && Keyboard.Modifiers == ModifierKeys.None
            && !ReferenceEquals(Keyboard.FocusedElement, _countBox))
        {
            _countBox.Text = ((int)(e.Key - Key.D0)).ToString();
            e.Handled = true;
        }
    }

    private void Confirm()
    {
        if (_list.SelectedItem is not string name || string.IsNullOrWhiteSpace(name))
            return;

        if (!int.TryParse(_countBox.Text.Trim(), out var n) || n < 1)
            n = 3;
        if (n > 9)
            n = 9;

        var path = Path.Combine(_projectsRoot, name);
        if (!Directory.Exists(path))
            return;

        _open(path, n);
        Close();
    }

    private static IEnumerable<string> ListProjectFolders(string root)
    {
        if (!Directory.Exists(root))
            yield break;

        IEnumerable<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(root)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)!;
        }
        catch
        {
            yield break;
        }

        foreach (var d in dirs)
        {
            if (d!.StartsWith('.') || d.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
                continue;
            yield return d;
        }
    }
}
