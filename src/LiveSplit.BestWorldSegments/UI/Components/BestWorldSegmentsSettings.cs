using LiveSplit.Model;
using LiveSplit.TimeFormatters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.UI.Components;

public class BestWorldSegmentsSettings : UserControl
{
    public const string CurrentTimingMethodName = "Current Timing Method";
    public const string RealTimeName = "Real Time";
    public const string GameTimeName = "Game Time";

    public bool DisplayOnLayout { get; set; }

    public bool ShowCurrentColumn { get; set; }
    public bool ShowPersonalBestColumn { get; set; }
    public bool ShowSumOfGoldsColumn { get; set; }
    public bool ShowBestColumn { get; set; }
    public bool ShowColumnLabels { get; set; }

    public bool IncludeSingleSegmentWorlds { get; set; }
    public bool HighlightCurrentWorld { get; set; }
    public Color CurrentWorldColor { get; set; }
    public int RowCount { get; set; }
    public int RowHeight { get; set; }

    public string TimingMethodName { get; set; }
    public TimeAccuracy Accuracy { get; set; }
    public string AccuracyName
    {
        get => Accuracy.ToString();
        set => Accuracy = (TimeAccuracy)Enum.Parse(typeof(TimeAccuracy), value);
    }

    public bool OverrideTextColor { get; set; }
    public Color TextColor { get; set; }
    public bool OverrideTimeColor { get; set; }
    public Color TimeColor { get; set; }

    public Color BackgroundColor { get; set; }
    public Color BackgroundColor2 { get; set; }
    public GradientType BackgroundGradient { get; set; }
    public string GradientString
    {
        get => BackgroundGradient.ToString();
        set => BackgroundGradient = (GradientType)Enum.Parse(typeof(GradientType), value);
    }

    public LayoutMode Mode { get; set; }

    /// <summary>
    /// Supplied by the component so the settings page can list the world times of the loaded splits.
    /// </summary>
    public Func<IList<WorldTimes>> GetWorldTimes { get; set; }

    private readonly Button btnColor2;
    private readonly ListView worldList;

    public BestWorldSegmentsSettings()
    {
        DisplayOnLayout = true;
        ShowCurrentColumn = true;
        ShowPersonalBestColumn = true;
        ShowSumOfGoldsColumn = false;
        ShowBestColumn = true;
        ShowColumnLabels = true;
        IncludeSingleSegmentWorlds = true;
        HighlightCurrentWorld = true;
        CurrentWorldColor = Color.FromArgb(90, 51, 115, 244);
        RowCount = 0;
        RowHeight = 24;
        TimingMethodName = CurrentTimingMethodName;
        Accuracy = TimeAccuracy.Hundredths;
        OverrideTextColor = false;
        TextColor = Color.White;
        OverrideTimeColor = false;
        TimeColor = Color.White;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = GradientType.Plain;

        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        Padding = new Padding(7);

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            Location = new Point(7, 7),
        };
        Controls.Add(root);

        var display = new CheckBox { Text = "Display world segments on layout", AutoSize = true, Margin = new Padding(6, 3, 3, 6) };
        display.DataBindings.Add("Checked", this, nameof(DisplayOnLayout), false, DataSourceUpdateMode.OnPropertyChanged);
        root.Controls.Add(display);

        var times = AddGroup(root, "World times");
        worldList = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Width = 430,
            Height = 200,
        };
        worldList.Columns.Add("World", 154);
        worldList.Columns.Add("Best World", 90, HorizontalAlignment.Right);
        worldList.Columns.Add("PB", 90, HorizontalAlignment.Right);
        worldList.Columns.Add("Sum of Golds", 90, HorizontalAlignment.Right);
        times.Controls.Add(worldList);
        times.SetColumnSpan(worldList, 2);
        var explanation = new Label
        {
            Text = "Best World is the fastest you've done the world in a single attempt. Sum of Golds adds up "
                + "the gold of every split in the world, which can come from different attempts.",
            AutoSize = true,
            MaximumSize = new Size(430, 0),
        };
        times.Controls.Add(explanation);
        times.SetColumnSpan(explanation, 2);
        VisibleChanged += (s, e) =>
        {
            if (Visible)
            {
                RefreshWorldList();
            }
        };

        var columns = AddGroup(root, "Columns (left to right)");
        AddCheckBox(columns, "This run (world time of the current attempt)", nameof(ShowCurrentColumn));
        AddCheckBox(columns, "Personal best (world time in your PB)", nameof(ShowPersonalBestColumn));
        AddCheckBox(columns, "Sum of golds (sum of each segment's best)", nameof(ShowSumOfGoldsColumn));
        AddCheckBox(columns, "Best world segment (fastest the world was done in one attempt)", nameof(ShowBestColumn));
        AddCheckBox(columns, "Show column labels", nameof(ShowColumnLabels));

        var worlds = AddGroup(root, "Worlds");
        AddCheckBox(worlds, "Include splits that aren't part of a subsplit group", nameof(IncludeSingleSegmentWorlds));
        AddCheckBox(worlds, "Highlight the current world", nameof(HighlightCurrentWorld));
        AddColorButton(worlds, "Highlight color:", nameof(CurrentWorldColor));
        var rowCount = new NumericUpDown { Minimum = 0, Maximum = 99, Width = 60 };
        rowCount.DataBindings.Add("Value", this, nameof(RowCount), false, DataSourceUpdateMode.OnPropertyChanged);
        AddRow(worlds, "Rows to show (0 = all):", rowCount);
        var rowHeight = new NumericUpDown { Minimum = 10, Maximum = 80, Width = 60 };
        rowHeight.DataBindings.Add("Value", this, nameof(RowHeight), false, DataSourceUpdateMode.OnPropertyChanged);
        AddRow(worlds, "Row height:", rowHeight);

        var timing = AddGroup(root, "Timing");
        var timingMethod = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        timingMethod.Items.AddRange([CurrentTimingMethodName, RealTimeName, GameTimeName]);
        timingMethod.DataBindings.Add("SelectedItem", this, nameof(TimingMethodName), false, DataSourceUpdateMode.OnPropertyChanged);
        AddRow(timing, "Timing method:", timingMethod);
        timingMethod.SelectionChangeCommitted += (s, e) => BeginInvoke(new Action(RefreshWorldList));
        var accuracy = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        accuracy.Items.AddRange(Enum.GetNames(typeof(TimeAccuracy)));
        accuracy.DataBindings.Add("SelectedItem", this, nameof(AccuracyName), false, DataSourceUpdateMode.OnPropertyChanged);
        AddRow(timing, "Accuracy:", accuracy);
        accuracy.SelectionChangeCommitted += (s, e) => BeginInvoke(new Action(RefreshWorldList));

        var colors = AddGroup(root, "Colors");
        AddCheckBox(colors, "Override layout text color", nameof(OverrideTextColor));
        AddColorButton(colors, "Text color:", nameof(TextColor));
        AddCheckBox(colors, "Override layout time color", nameof(OverrideTimeColor));
        AddColorButton(colors, "Time color:", nameof(TimeColor));
        var gradient = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        gradient.Items.AddRange(Enum.GetNames(typeof(GradientType)));
        gradient.DataBindings.Add("SelectedItem", this, nameof(GradientString), false, DataSourceUpdateMode.OnPropertyChanged);
        AddRow(colors, "Background:", gradient);
        AddColorButton(colors, "Background color 1:", nameof(BackgroundColor));
        btnColor2 = AddColorButton(colors, "Background color 2:", nameof(BackgroundColor2));
        gradient.SelectedIndexChanged += (s, e) => btnColor2.Enabled = gradient.SelectedItem?.ToString() != nameof(GradientType.Plain);
    }

    public void RefreshWorldList()
    {
        IList<WorldTimes> worlds = GetWorldTimes?.Invoke();
        if (worlds == null)
        {
            return;
        }

        var formatter = new SplitTimeFormatter(Accuracy);
        worldList.BeginUpdate();
        worldList.Items.Clear();
        TimeSpan? totalBest = TimeSpan.Zero, totalPersonalBest = TimeSpan.Zero, totalSumOfGolds = TimeSpan.Zero;
        foreach (WorldTimes world in worlds)
        {
            worldList.Items.Add(new ListViewItem(
            [
                world.World.Name,
                formatter.Format(world.Best),
                formatter.Format(world.PersonalBest),
                formatter.Format(world.SumOfGolds),
            ]));
            totalBest += world.Best;
            totalPersonalBest += world.PersonalBest;
            totalSumOfGolds += world.SumOfGolds;
        }

        if (worlds.Count > 0)
        {
            var total = new ListViewItem(
            [
                "Total",
                formatter.Format(totalBest),
                formatter.Format(totalPersonalBest),
                formatter.Format(totalSumOfGolds),
            ]);
            total.Font = new Font(worldList.Font, FontStyle.Bold);
            worldList.Items.Add(total);
        }

        worldList.EndUpdate();

        // Fit the list to its rows so the settings page scrolls instead of the list.
        if (worldList.Items.Count > 0 && worldList.IsHandleCreated)
        {
            worldList.Height = worldList.Items[worldList.Items.Count - 1].Bounds.Bottom + 4;
        }
    }

    private static TableLayoutPanel AddGroup(TableLayoutPanel root, string title)
    {
        var group = new GroupBox { Text = title, AutoSize = true, MinimumSize = new Size(448, 0) };
        var table = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        group.Controls.Add(table);
        root.Controls.Add(group);
        return table;
    }

    private void AddCheckBox(TableLayoutPanel table, string text, string property)
    {
        var checkBox = new CheckBox { Text = text, AutoSize = true };
        checkBox.DataBindings.Add("Checked", this, property, false, DataSourceUpdateMode.OnPropertyChanged);
        table.Controls.Add(checkBox);
        table.SetColumnSpan(checkBox, 2);
    }

    private Button AddColorButton(TableLayoutPanel table, string text, string property)
    {
        var button = new Button { Width = 23, Height = 23, FlatStyle = FlatStyle.Popup };
        button.DataBindings.Add("BackColor", this, property, false, DataSourceUpdateMode.OnPropertyChanged);
        button.Click += (s, e) => SettingsHelper.ColorButtonClick(button, this);
        AddRow(table, text, button);
        return button;
    }

    private static void AddRow(TableLayoutPanel table, string text, Control control)
    {
        table.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) });
        table.Controls.Add(control);
    }

    public TimingMethod GetTimingMethod(LiveSplitState state)
    {
        return TimingMethodName switch
        {
            RealTimeName => TimingMethod.RealTime,
            GameTimeName => TimingMethod.GameTime,
            _ => state.CurrentTimingMethod,
        };
    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        DisplayOnLayout = SettingsHelper.ParseBool(element["DisplayOnLayout"], true);
        ShowCurrentColumn = SettingsHelper.ParseBool(element["ShowCurrentColumn"], true);
        ShowPersonalBestColumn = SettingsHelper.ParseBool(element["ShowPersonalBestColumn"], true);
        ShowSumOfGoldsColumn = SettingsHelper.ParseBool(element["ShowSumOfGoldsColumn"], false);
        ShowBestColumn = SettingsHelper.ParseBool(element["ShowBestColumn"], true);
        ShowColumnLabels = SettingsHelper.ParseBool(element["ShowColumnLabels"], true);
        IncludeSingleSegmentWorlds = SettingsHelper.ParseBool(element["IncludeSingleSegmentWorlds"], true);
        HighlightCurrentWorld = SettingsHelper.ParseBool(element["HighlightCurrentWorld"], true);
        CurrentWorldColor = SettingsHelper.ParseColor(element["CurrentWorldColor"], Color.FromArgb(90, 51, 115, 244));
        RowCount = SettingsHelper.ParseInt(element["RowCount"], 0);
        RowHeight = SettingsHelper.ParseInt(element["RowHeight"], 24);
        TimingMethodName = SettingsHelper.ParseString(element["TimingMethod"], CurrentTimingMethodName);
        Accuracy = SettingsHelper.ParseEnum(element["Accuracy"], TimeAccuracy.Hundredths);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        TextColor = SettingsHelper.ParseColor(element["TextColor"], Color.White);
        OverrideTimeColor = SettingsHelper.ParseBool(element["OverrideTimeColor"]);
        TimeColor = SettingsHelper.ParseColor(element["TimeColor"], Color.White);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.Transparent);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.Transparent);
        BackgroundGradient = SettingsHelper.ParseEnum(element["BackgroundGradient"], GradientType.Plain);
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Settings");
        CreateSettingsNode(document, parent);
        return parent;
    }

    public int GetSettingsHashCode()
    {
        return CreateSettingsNode(null, null);
    }

    private int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.0") ^
            SettingsHelper.CreateSetting(document, parent, "DisplayOnLayout", DisplayOnLayout) ^
            SettingsHelper.CreateSetting(document, parent, "ShowCurrentColumn", ShowCurrentColumn) ^
            SettingsHelper.CreateSetting(document, parent, "ShowPersonalBestColumn", ShowPersonalBestColumn) ^
            SettingsHelper.CreateSetting(document, parent, "ShowSumOfGoldsColumn", ShowSumOfGoldsColumn) ^
            SettingsHelper.CreateSetting(document, parent, "ShowBestColumn", ShowBestColumn) ^
            SettingsHelper.CreateSetting(document, parent, "ShowColumnLabels", ShowColumnLabels) ^
            SettingsHelper.CreateSetting(document, parent, "IncludeSingleSegmentWorlds", IncludeSingleSegmentWorlds) ^
            SettingsHelper.CreateSetting(document, parent, "HighlightCurrentWorld", HighlightCurrentWorld) ^
            SettingsHelper.CreateSetting(document, parent, "CurrentWorldColor", CurrentWorldColor) ^
            SettingsHelper.CreateSetting(document, parent, "RowCount", RowCount) ^
            SettingsHelper.CreateSetting(document, parent, "RowHeight", RowHeight) ^
            SettingsHelper.CreateSetting(document, parent, "TimingMethod", TimingMethodName) ^
            SettingsHelper.CreateSetting(document, parent, "Accuracy", Accuracy) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
            SettingsHelper.CreateSetting(document, parent, "TextColor", TextColor) ^
            SettingsHelper.CreateSetting(document, parent, "OverrideTimeColor", OverrideTimeColor) ^
            SettingsHelper.CreateSetting(document, parent, "TimeColor", TimeColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
            SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient);
    }
}
