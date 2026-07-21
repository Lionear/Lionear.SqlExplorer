using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace SqlExplorer.Tools.CopyTable;

/// <summary>
/// Copy Table's own dialog view (Route B): a From → To header, a destination connection + database picker,
/// segmented "what to copy" and row scope, fidelity switches, and two mode cards (Run the copy / Open as
/// script). It only builds the input area — the host frames it with the Run/Cancel buttons, the progress
/// checklist and the success banner. Values are written back through <see cref="IToolUiContext"/> under the
/// same <c>ToolField.Key</c>s <see cref="CopyTableTool"/> reads. Code-built with themed controls, so it
/// follows the app's light/dark theme.
/// </summary>
internal sealed class CopyTableView : UserControl
{
    private readonly IToolUiContext _ctx;
    private readonly ComboBox _databaseBox;
    private readonly TextBlock _targetChip;
    private readonly NumericUpDown _rowCount;

    public CopyTableView(IToolUiContext ctx, string initialMode, string sourceTable)
    {
        _ctx = ctx;

        // Seed the values the host will collect, so a straight-through "Run" uses the shown defaults.
        ctx.SetValue("what", What.Both);
        ctx.SetValue("rows", "All");
        ctx.SetValue("keepIdentity", "true");
        ctx.SetValue("dropExisting", "false");
        ctx.SetValue("mode", initialMode);

        var root = new StackPanel { Margin = new Thickness(18, 14, 18, 4), Spacing = 16 };

        // ── From → To header ──────────────────────────────────────────────────────────────────────────
        _targetChip = new TextBlock { Text = "—", VerticalAlignment = VerticalAlignment.Center, FontFamily = Mono };
        root.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Chip("FROM", new TextBlock { Text = sourceTable, FontFamily = Mono, VerticalAlignment = VerticalAlignment.Center }),
                new TextBlock { Text = "→", Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center, FontSize = 15 },
                Chip("TO", _targetChip)
            }
        });

        // ── Destination connection + database ─────────────────────────────────────────────────────────
        var connections = ctx.ListConnections();
        var connectionBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = connections,
            PlaceholderText = "Pick a connection…",
            ItemTemplate = new FuncDataTemplate<ToolConnectionInfo>((c, _) =>
                new TextBlock { Text = c?.Name ?? "" }, supportsRecycling: true)
        };
        _databaseBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = "Pick a database…",
            IsEnabled = false
        };

        connectionBox.SelectionChanged += async (_, _) =>
        {
            if (connectionBox.SelectedItem is not ToolConnectionInfo picked)
            {
                return;
            }

            ctx.SetValue("toConnection", picked.Id);
            _databaseBox.ItemsSource = null;
            _databaseBox.IsEnabled = false;
            ctx.SetValue("toDatabase", null);
            UpdateTarget();

            var databases = await ctx.ListDatabasesAsync(picked.Id, CancellationToken.None);
            _databaseBox.ItemsSource = databases;
            _databaseBox.IsEnabled = databases.Count > 0;
        };
        _databaseBox.SelectionChanged += (_, _) =>
        {
            ctx.SetValue("toDatabase", _databaseBox.SelectedItem as string);
            UpdateTarget();
        };

        root.Children.Add(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,12,*"),
            Children =
            {
                Field("Copy to connection", connectionBox, 0),
                Field("Database", _databaseBox, 2)
            }
        });

        // ── What to copy (segmented) ──────────────────────────────────────────────────────────────────
        root.Children.Add(Labeled("What to copy", Segmented("what",
            [(What.Both, "Structure + data"), (What.Structure, "Structure only"), (What.Data, "Data only")],
            What.Both, v => ctx.SetValue("what", v))));

        // ── Rows ──────────────────────────────────────────────────────────────────────────────────────
        _rowCount = new NumericUpDown
        {
            Minimum = 1, Maximum = 10_000_000, Value = 1000, Increment = 100, Width = 110,
            FormatString = "0", IsEnabled = false, VerticalAlignment = VerticalAlignment.Center
        };
        var allRows = new RadioButton { Content = "All rows", GroupName = "rows", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
        var firstRows = new RadioButton { Content = "First", GroupName = "rows", VerticalAlignment = VerticalAlignment.Center };

        void SyncRows()
        {
            _rowCount.IsEnabled = firstRows.IsChecked == true;
            ctx.SetValue("rows", firstRows.IsChecked == true
                ? ((int)(_rowCount.Value ?? 1000)).ToString()
                : "All");
        }

        allRows.IsCheckedChanged += (_, _) => SyncRows();
        firstRows.IsCheckedChanged += (_, _) => SyncRows();
        _rowCount.ValueChanged += (_, _) => SyncRows();

        root.Children.Add(Labeled("Rows", new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center,
            Children = { allRows, firstRows, _rowCount }
        }));

        // ── Fidelity options (switches) ───────────────────────────────────────────────────────────────
        root.Children.Add(SwitchRow("Keep identity / sequence values", true,
            "Preserve the original keys instead of letting the target regenerate them",
            v => ctx.SetValue("keepIdentity", v ? "true" : "false")));
        root.Children.Add(SwitchRow("Drop target table if it exists", false,
            "Off by default — the copy fails safely if the target table already exists",
            v => ctx.SetValue("dropExisting", v ? "true" : "false")));

        // ── How (mode cards) ──────────────────────────────────────────────────────────────────────────
        root.Children.Add(Labeled("How", ModeCards(initialMode, v => ctx.SetValue("mode", v))));

        Content = new ScrollViewer { Content = root };
    }

    private static readonly FontFamily Mono = new("Cascadia Code,Consolas,Menlo,monospace");

    private void UpdateTarget()
    {
        var db = _databaseBox.SelectedItem as string;
        _targetChip.Text = string.IsNullOrWhiteSpace(db) ? "—" : db;
    }

    private static class What
    {
        public const string Both = "Structure + data";
        public const string Structure = "Structure only";
        public const string Data = "Data only";
    }

    // A small label chip ("FROM", "TO") next to a value, in a hairline pill.
    private static Control Chip(string label, Control value)
    {
        var pill = new Border
        {
            CornerRadius = new CornerRadius(6), Padding = new Thickness(9, 5), BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 7,
                Children =
                {
                    new TextBlock { Text = label, FontSize = 10, FontWeight = FontWeight.Bold, Opacity = 0.55,
                        VerticalAlignment = VerticalAlignment.Center },
                    value
                }
            }
        };
        pill[!BackgroundProperty] = new DynamicResourceExtension("SESecondaryBgBrush");
        pill[!BorderBrushProperty] = new DynamicResourceExtension("SEHairlineBrush");
        return pill;
    }

    private static Control Field(string label, Control input, int column)
    {
        var panel = Labeled(label, input);
        Grid.SetColumn(panel, column);
        return panel;
    }

    private static Control Labeled(string label, Control input)
    {
        var caption = new TextBlock { Text = label, FontSize = 11.5, Margin = new Thickness(0, 0, 0, 5) };
        caption[!ForegroundProperty] = new DynamicResourceExtension("SETextSecondaryBrush");
        return new StackPanel { Spacing = 0, Children = { caption, input } };
    }

    // Segmented control: a row of RadioButtons that read as one control (host theme styles them).
    private static Control Segmented(string group, IReadOnlyList<(string Value, string Label)> options,
        string initial, Action<string> onChange)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        foreach (var (value, label) in options)
        {
            var rb = new RadioButton { Content = label, GroupName = group, IsChecked = value == initial };
            rb.IsCheckedChanged += (_, _) => { if (rb.IsChecked == true) onChange(value); };
            row.Children.Add(rb);
        }

        return row;
    }

    private static Control SwitchRow(string label, bool initial, string help, Action<bool> onChange)
    {
        var toggle = new ToggleSwitch { IsChecked = initial, OnContent = "", OffContent = "" };
        toggle.IsCheckedChanged += (_, _) => onChange(toggle.IsChecked == true);

        var text = new StackPanel
        {
            Spacing = 1, VerticalAlignment = VerticalAlignment.Center,
            Children = { new TextBlock { Text = label } }
        };
        var helpText = new TextBlock { Text = help, FontSize = 11, TextWrapping = TextWrapping.Wrap };
        helpText[!ForegroundProperty] = new DynamicResourceExtension("SETextFaintBrush");
        text.Children.Add(helpText);

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { text, Put(toggle, 1) }
        };
    }

    // Two selectable cards; the chosen one gets an accent border.
    private Control ModeCards(string initial, Action<string> onChange)
    {
        var cards = new List<(Border Card, string Value)>();
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*") };

        Border MakeCard(string value, string title, string desc, int column)
        {
            var radio = new RadioButton { GroupName = "mode", IsChecked = value == initial, VerticalAlignment = VerticalAlignment.Top };
            var descText = new TextBlock { Text = desc, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            descText[!ForegroundProperty] = new DynamicResourceExtension("SETextSecondaryBrush");

            var card = new Border
            {
                CornerRadius = new CornerRadius(6), Padding = new Thickness(11, 10), BorderThickness = new Thickness(1.5),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 8,
                    Children =
                    {
                        radio,
                        new StackPanel { Children = { new TextBlock { Text = title, FontWeight = FontWeight.SemiBold }, descText } }
                    }
                }
            };
            card[!BackgroundProperty] = new DynamicResourceExtension("SEPanelBgBrush");

            void Select() { onChange(value); foreach (var (c, v) in cards) Highlight(c, v == value); }
            radio.IsCheckedChanged += (_, _) => { if (radio.IsChecked == true) Select(); };
            card.PointerPressed += (_, _) => radio.IsChecked = true;

            Grid.SetColumn(card, column);
            cards.Add((card, value));
            return card;
        }

        grid.Children.Add(MakeCard("Run the copy", "Run the copy", "Create & fill the table on the target, with progress", 0));
        grid.Children.Add(MakeCard("Open as script", "Open as script", "Review the CREATE + INSERT in a new query tab first", 2));
        foreach (var (c, v) in cards) Highlight(c, v == initial);
        return grid;
    }

    private static void Highlight(Border card, bool on) =>
        card[!BorderBrushProperty] = new DynamicResourceExtension(on ? "SEAccentBrush" : "SEHairlineBrush");

    private static Control Put(Control c, int column)
    {
        Grid.SetColumn(c, column);
        return c;
    }
}
