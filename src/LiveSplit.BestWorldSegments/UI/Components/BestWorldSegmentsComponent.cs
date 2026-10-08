using LiveSplit.Model;
using LiveSplit.TimeFormatters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.UI.Components;

public class BestWorldSegmentsComponent : IComponent
{
    private enum Column
    {
        Current,
        PersonalBest,
        SumOfGolds,
        Best,
    }

    private const float CellPadding = 6f;

    public BestWorldSegmentsSettings Settings { get; set; }
    protected LiveSplitState State { get; set; }

    private readonly SplitTimeFormatter formatter = new();
    private readonly SimpleLabel nameLabel = new();
    private readonly SimpleLabel timeLabel = new() { IsMonospaced = true };
    private readonly SubsplitsGoldPainter subsplitsGoldPainter = new();

    private List<WorldTimes> rows = [];
    private List<WorldTimes> visibleRows = [];
    private List<Column> visibleColumns = [];
    private IRun cachedRun;
    private int cachedSegmentCount;
    private TimingMethod cachedMethod;
    private bool isDirty = true;
    private int lastDrawHash;
    private float horizontalWidth = 100f;

    public string ComponentName => "Best World Segments";

    public float PaddingTop => 0f;
    public float PaddingBottom => 0f;
    public float PaddingLeft => 0f;
    public float PaddingRight => 0f;

    public float VerticalHeight => Settings.DisplayOnLayout
        ? Settings.RowHeight * (visibleRows.Count + (Settings.ShowColumnLabels ? 1 : 0))
        : 0f;
    public float MinimumWidth => Settings.DisplayOnLayout ? 100f : 0f;
    public float HorizontalWidth => Settings.DisplayOnLayout ? horizontalWidth : 0f;
    public float MinimumHeight => Settings.DisplayOnLayout ? 2 * Settings.RowHeight : 0f;

    public IDictionary<string, Action> ContextMenuControls => null;

    public BestWorldSegmentsComponent(LiveSplitState state)
    {
        Settings = new BestWorldSegmentsSettings();
        State = state;
        Settings.GetWorldTimes = () =>
        {
            UpdateRows(State);
            return rows;
        };
        State.OnReset += State_OnReset;
        State.RunManuallyModified += State_RunManuallyModified;
        UpdateRows(state);
    }

    private void State_OnReset(object sender, TimerPhase e)
    {
        // Resetting is when LiveSplit adds the attempt to the segment history.
        isDirty = true;
    }

    private void State_RunManuallyModified(object sender, EventArgs e)
    {
        isDirty = true;
    }

    private void UpdateRows(LiveSplitState state)
    {
        TimingMethod method = Settings.GetTimingMethod(state);
        IRun run = state.Run;
        if (!isDirty && run == cachedRun && run.Count == cachedSegmentCount && method == cachedMethod)
        {
            return;
        }

        rows = WorldCalculator.GetWorldTimes(run, method);

        cachedRun = run;
        cachedSegmentCount = run.Count;
        cachedMethod = method;
        isDirty = false;
    }

    private void UpdateVisibility(LiveSplitState state)
    {
        visibleRows = rows.Where(x => Settings.IncludeSingleSegmentWorlds || x.World.HasSubsplits).ToList();

        int count = Settings.RowCount;
        if (count > 0 && count < visibleRows.Count)
        {
            // Keep the current world in view, centered when possible. Before the run starts that's
            // the first world, and after it ends it's the last.
            int current = visibleRows.FindIndex(x => x.World.EndIndex >= state.CurrentSplitIndex);
            if (current < 0)
            {
                current = visibleRows.Count - 1;
            }

            int start = Math.Max(0, Math.Min(current - ((count - 1) / 2), visibleRows.Count - count));
            visibleRows = visibleRows.GetRange(start, count);
        }

        visibleColumns = [];
        if (Settings.ShowCurrentColumn)
        {
            visibleColumns.Add(Column.Current);
        }

        if (Settings.ShowPersonalBestColumn)
        {
            visibleColumns.Add(Column.PersonalBest);
        }

        if (Settings.ShowSumOfGoldsColumn)
        {
            visibleColumns.Add(Column.SumOfGolds);
        }

        if (Settings.ShowBestColumn)
        {
            visibleColumns.Add(Column.Best);
        }
    }

    private static string GetColumnLabel(Column column)
    {
        return column switch
        {
            Column.Current => "This Run",
            Column.PersonalBest => "PB",
            Column.SumOfGolds => "SoG",
            _ => "Best",
        };
    }

    private Color GetTextColor(LiveSplitState state)
    {
        return Settings.OverrideTextColor ? Settings.TextColor : state.LayoutSettings.TextColor;
    }

    private Color GetTimeColor(LiveSplitState state)
    {
        return Settings.OverrideTimeColor ? Settings.TimeColor : state.LayoutSettings.TextColor;
    }

    private string GetCell(LiveSplitState state, WorldTimes row, Column column, out Color color)
    {
        color = GetTimeColor(state);
        switch (column)
        {
            case Column.PersonalBest:
                return formatter.Format(row.PersonalBest);
            case Column.SumOfGolds:
                return formatter.Format(row.SumOfGolds);
            case Column.Best:
                return formatter.Format(row.Best);
        }

        TimingMethod method = Settings.GetTimingMethod(state);
        TimeSpan? finished = WorldCalculator.GetCurrentRunWorldTime(state.Run, row.World, method, state.CurrentSplitIndex);
        if (finished != null)
        {
            if (IsNewBestWorld(finished, row))
            {
                color = state.LayoutSettings.BestSegmentColor;
            }
            else if (row.PersonalBest != null)
            {
                color = finished <= row.PersonalBest
                    ? state.LayoutSettings.AheadGainingTimeColor
                    : state.LayoutSettings.BehindLosingTimeColor;
            }

            return formatter.Format(finished);
        }

        TimeSpan? progress = WorldCalculator.GetCurrentRunWorldProgress(state.Run, row.World, method, state.CurrentSplitIndex, state.CurrentTime[method]);
        return progress == null ? "" : formatter.Format(progress);
    }

    private static bool IsNewBestWorld(TimeSpan? finished, WorldTimes row)
    {
        return finished != null && (row.Best == null || finished < row.Best);
    }

    private bool IsNewBestWorld(LiveSplitState state, WorldTimes row)
    {
        TimingMethod method = Settings.GetTimingMethod(state);
        return IsNewBestWorld(WorldCalculator.GetCurrentRunWorldTime(state.Run, row.World, method, state.CurrentSplitIndex), row);
    }

    private void PaintSubsplitsGold(LiveSplitState state)
    {
        if (Settings.SubsplitsWorldGold)
        {
            subsplitsGoldPainter.Paint(state, rows, row => IsNewBestWorld(state, row));
        }
    }

    private void PrepareLabel(SimpleLabel label, LiveSplitState state, Font font, string text, Color color, StringAlignment alignment)
    {
        label.Text = text;
        label.Font = font;
        label.ForeColor = color;
        label.HorizontalAlignment = alignment;
        label.VerticalAlignment = StringAlignment.Center;
        label.HasShadow = state.LayoutSettings.DropShadows;
        label.ShadowColor = state.LayoutSettings.ShadowsColor;
        label.OutlineColor = state.LayoutSettings.TextOutlineColor;
    }

    private void DrawLabel(SimpleLabel label, Graphics g, float x, float y, float width, float height)
    {
        label.X = x;
        label.Y = y;
        label.Width = width;
        label.Height = height;
        label.Draw(g);
    }

    private void DrawBackground(Graphics g, float width, float height)
    {
        if (Settings.BackgroundColor.A > 0
            || (Settings.BackgroundGradient != GradientType.Plain && Settings.BackgroundColor2.A > 0))
        {
            using var brush = new LinearGradientBrush(
                new PointF(0, 0),
                Settings.BackgroundGradient == GradientType.Horizontal ? new PointF(width, 0) : new PointF(0, height),
                Settings.BackgroundColor,
                Settings.BackgroundGradient == GradientType.Plain ? Settings.BackgroundColor : Settings.BackgroundColor2);
            g.FillRectangle(brush, 0, 0, width, height);
        }
    }

    private void DrawHighlight(Graphics g, LiveSplitState state, WorldTimes row, float x, float y, float width, float height)
    {
        if (Settings.HighlightCurrentWorld
            && state.CurrentPhase != TimerPhase.NotRunning
            && row.World.Contains(state.CurrentSplitIndex))
        {
            using var brush = new SolidBrush(Settings.CurrentWorldColor);
            g.FillRectangle(brush, x, y, width, height);
        }
    }

    public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
    {
        // Drawing starts after every component has updated, so this covers being above the Subsplits component.
        PaintSubsplitsGold(state);
        if (!Settings.DisplayOnLayout)
        {
            return;
        }

        float rowHeight = Settings.RowHeight;
        DrawBackground(g, width, VerticalHeight);

        Font textFont = state.LayoutSettings.TextFont;
        Font timesFont = state.LayoutSettings.TimesFont;
        Color textColor = GetTextColor(state);

        string[,] cells = new string[visibleRows.Count, visibleColumns.Count];
        Color[,] colors = new Color[visibleRows.Count, visibleColumns.Count];
        float[] columnWidths = new float[visibleColumns.Count];
        for (int c = 0; c < visibleColumns.Count; c++)
        {
            columnWidths[c] = Settings.ShowColumnLabels
                ? g.MeasureString(GetColumnLabel(visibleColumns[c]), textFont).Width
                : 0f;
            for (int r = 0; r < visibleRows.Count; r++)
            {
                cells[r, c] = GetCell(state, visibleRows[r], visibleColumns[c], out colors[r, c]);
                columnWidths[c] = Math.Max(columnWidths[c], g.MeasureString(cells[r, c], timesFont).Width);
            }

            columnWidths[c] += CellPadding;
        }

        float timesWidth = columnWidths.Sum();
        float nameWidth = Math.Max(0f, width - timesWidth - (2 * CellPadding));
        float y = 0f;

        if (Settings.ShowColumnLabels)
        {
            float x = width - CellPadding - timesWidth;
            for (int c = 0; c < visibleColumns.Count; c++)
            {
                PrepareLabel(nameLabel, state, textFont, GetColumnLabel(visibleColumns[c]), textColor, StringAlignment.Far);
                DrawLabel(nameLabel, g, x, y, columnWidths[c], rowHeight);
                x += columnWidths[c];
            }

            y += rowHeight;
        }

        for (int r = 0; r < visibleRows.Count; r++)
        {
            DrawHighlight(g, state, visibleRows[r], 0f, y, width, rowHeight);

            PrepareLabel(nameLabel, state, textFont, visibleRows[r].World.Name, textColor, StringAlignment.Near);
            DrawLabel(nameLabel, g, CellPadding, y, nameWidth, rowHeight);

            float x = width - CellPadding - timesWidth;
            for (int c = 0; c < visibleColumns.Count; c++)
            {
                PrepareLabel(timeLabel, state, timesFont, cells[r, c], colors[r, c], StringAlignment.Far);
                DrawLabel(timeLabel, g, x, y, columnWidths[c], rowHeight);
                x += columnWidths[c];
            }

            y += rowHeight;
        }
    }

    public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
    {
        PaintSubsplitsGold(state);
        if (!Settings.DisplayOnLayout)
        {
            return;
        }

        DrawBackground(g, HorizontalWidth, height);

        Font textFont = state.LayoutSettings.TextFont;
        Font timesFont = state.LayoutSettings.TimesFont;
        Color textColor = GetTextColor(state);
        Column column = visibleColumns.Count > 0 ? visibleColumns[visibleColumns.Count - 1] : Column.Best;

        float x = 0f;
        foreach (WorldTimes row in visibleRows)
        {
            string time = GetCell(state, row, column, out Color timeColor);
            float cellWidth = Math.Max(g.MeasureString(row.World.Name, textFont).Width, g.MeasureString(time, timesFont).Width) + (2 * CellPadding);

            DrawHighlight(g, state, row, x, 0f, cellWidth, height);

            PrepareLabel(nameLabel, state, textFont, row.World.Name, textColor, StringAlignment.Center);
            DrawLabel(nameLabel, g, x, 0f, cellWidth, height / 2f);
            PrepareLabel(timeLabel, state, timesFont, time, timeColor, StringAlignment.Center);
            DrawLabel(timeLabel, g, x, height / 2f, cellWidth, height / 2f);

            x += cellWidth;
        }

        horizontalWidth = Math.Max(MinimumWidth, x);
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        formatter.Accuracy = Settings.Accuracy;
        UpdateRows(state);
        UpdateVisibility(state);

        // Runs after the Subsplits component's update when this component is below it on the layout.
        PaintSubsplitsGold(state);

        if (invalidator == null)
        {
            return;
        }

        // Only redraw when something visible changed.
        int hash = Settings.GetSettingsHashCode() ^ width.GetHashCode() ^ height.GetHashCode() ^ rows.GetHashCode();
        hash = (hash * 31) + state.CurrentSplitIndex;
        hash = (hash * 31) + state.CurrentPhase.GetHashCode();
        if (Settings.DisplayOnLayout && visibleColumns.Contains(Column.Current))
        {
            foreach (WorldTimes row in visibleRows)
            {
                hash = (hash * 31) + GetCell(state, row, Column.Current, out Color color).GetHashCode();
                hash = (hash * 31) + color.GetHashCode();
            }
        }

        if (hash != lastDrawHash)
        {
            lastDrawHash = hash;
            invalidator.Invalidate(0, 0, width, height);
        }
    }

    public Control GetSettingsControl(LayoutMode mode)
    {
        Settings.Mode = mode;
        return Settings;
    }

    public void SetSettings(XmlNode settings)
    {
        Settings.SetSettings(settings);
        isDirty = true;
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        return Settings.GetSettings(document);
    }

    public int GetSettingsHashCode()
    {
        return Settings.GetSettingsHashCode();
    }

    public void Dispose()
    {
        State.OnReset -= State_OnReset;
        State.RunManuallyModified -= State_RunManuallyModified;
    }
}
