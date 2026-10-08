using LiveSplit.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;

namespace LiveSplit.UI.Components;

/// <summary>
/// The Subsplits component never colors a collapsed world gold: it only compares the world against
/// the comparison. This finds the Subsplits components on the layout and paints the delta columns of
/// a collapsed world with the gold color when the current run set a new best world.
/// </summary>
/// <remarks>
/// LiveSplit updates every component before drawing any of them, so recoloring the labels after the
/// Subsplits component has updated (or before it draws) is what ends up on screen. The Subsplits types
/// are reached by reflection so this component doesn't depend on LiveSplit.Subsplits.dll being installed.
/// </remarks>
internal sealed class SubsplitsGoldPainter
{
    private const string SubsplitsAssemblyName = "LiveSplit.Subsplits";

    /// <summary>
    /// The columns the Subsplits component colors by comparison; the same ones LiveSplit colors gold for a best segment.
    /// </summary>
    private static readonly HashSet<string> DeltaColumnTypes = ["Delta", "DeltaorSplitTime", "SegmentDelta", "SegmentDeltaorSegmentTime"];

    private readonly Dictionary<(Type, string), PropertyInfo> properties = [];

    /// <param name="isNewBestWorld">Whether the given world, finished in the current run, is a new best world.</param>
    public void Paint(LiveSplitState state, IList<WorldTimes> rows, Func<WorldTimes, bool> isNewBestWorld, Color gold)
    {
        if (state.Layout == null || state.CurrentPhase == TimerPhase.NotRunning)
        {
            return;
        }

        foreach (IComponent component in state.Layout.Components)
        {
            if (!IsSubsplitsType(component, "SplitsComponent")
                || GetValue(component, "InternalComponent") is not ComponentRendererComponent renderer
                || renderer.VisibleComponents == null)
            {
                continue;
            }

            foreach (IComponent split in renderer.VisibleComponents)
            {
                if (IsSubsplitsType(split, "SplitComponent"))
                {
                    PaintSplit(state, split, rows, isNewBestWorld, gold);
                }
            }
        }
    }

    private void PaintSplit(LiveSplitState state, IComponent split, IList<WorldTimes> rows, Func<WorldTimes, bool> isNewBestWorld, Color gold)
    {
        if (GetValue(split, "CollapsedSplit") is not true
            || GetValue(split, "Split") is not ISegment segment
            || GetValue(split, "LabelsList") is not IList<SimpleLabel> labels
            || GetValue(split, "ColumnsList") is not IEnumerable columns)
        {
            return;
        }

        // A collapsed row stands for the world ending at its split.
        int endIndex = state.Run.IndexOf(segment);
        WorldTimes row = null;
        foreach (WorldTimes candidate in rows)
        {
            if (candidate.World.EndIndex == endIndex)
            {
                row = candidate;
                break;
            }
        }

        if (row == null || !isNewBestWorld(row))
        {
            return;
        }

        int index = 0;
        foreach (object column in columns)
        {
            if (index >= labels.Count)
            {
                break;
            }

            if (DeltaColumnTypes.Contains(GetValue(column, "Type")?.ToString() ?? ""))
            {
                labels[index].ForeColor = gold;

                // The row only redraws when its cache sees a change. Recording the gold here means the
                // Subsplits component sees its own color as a change next frame and redraws the row,
                // which keeps a rainbow color moving.
                if (GetValue(split, "Cache") is GraphicsCache cache)
                {
                    cache["Columns" + index + "Color"] = gold.ToArgb();
                }
            }

            index++;
        }
    }

    private static bool IsSubsplitsType(object value, string typeName)
    {
        Type type = value?.GetType();
        return type != null && type.Name == typeName && type.Assembly.GetName().Name == SubsplitsAssemblyName;
    }

    private object GetValue(object target, string name)
    {
        Type type = target.GetType();
        if (!properties.TryGetValue((type, name), out PropertyInfo property))
        {
            property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            properties[(type, name)] = property;
        }

        return property?.GetValue(target, null);
    }
}
