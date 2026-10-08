using System;
using System.Collections.Generic;

namespace LiveSplit.Model;

/// <summary>
/// A group of consecutive segments that LiveSplit's Subsplits component treats as one section:
/// any number of subsplits (names starting with "-") followed by the segment that ends the section.
/// </summary>
public sealed class World
{
    public string Name { get; }
    public int StartIndex { get; }
    public int EndIndex { get; }

    public int SegmentCount => EndIndex - StartIndex + 1;
    public bool HasSubsplits => EndIndex > StartIndex;

    public World(string name, int startIndex, int endIndex)
    {
        Name = name;
        StartIndex = startIndex;
        EndIndex = endIndex;
    }

    public bool Contains(int segmentIndex)
    {
        return segmentIndex >= StartIndex && segmentIndex <= EndIndex;
    }
}

public sealed class WorldTimes
{
    public World World { get; set; }
    public TimeSpan? Best { get; set; }
    public TimeSpan? PersonalBest { get; set; }
    public TimeSpan? SumOfGolds { get; set; }
}

public static class WorldCalculator
{
    public static List<WorldTimes> GetWorldTimes(IList<ISegment> run, TimingMethod method)
    {
        var times = new List<WorldTimes>();
        foreach (World world in GetWorlds(run))
        {
            times.Add(new WorldTimes
            {
                World = world,
                Best = GetBestWorldTime(run, world, method, out _),
                PersonalBest = GetPersonalBestWorldTime(run, world, method),
                SumOfGolds = GetSumOfGolds(run, world, method),
            });
        }

        return times;
    }

    /// <summary>
    /// Splits the run into worlds using the same naming convention as the Subsplits component.
    /// </summary>
    public static List<World> GetWorlds(IList<ISegment> run)
    {
        var worlds = new List<World>();
        int start = 0;
        for (int i = 0; i < run.Count; i++)
        {
            string name = run[i].Name ?? "";
            bool isSubsplit = name.StartsWith("-");
            bool isLast = i == run.Count - 1;
            if (!isSubsplit || isLast)
            {
                worlds.Add(new World(GetWorldName(name), start, i));
                start = i + 1;
            }
        }

        return worlds;
    }

    /// <summary>
    /// "{Bianco Hills} 5 - Hey Beter" -> "Bianco Hills", "Bowser" -> "Bowser", "-Last" -> "Last".
    /// </summary>
    public static string GetWorldName(string segmentName)
    {
        segmentName ??= "";
        if (segmentName.StartsWith("{"))
        {
            int close = segmentName.IndexOf('}');
            if (close > 0)
            {
                return segmentName.Substring(1, close - 1).Trim();
            }
        }

        return segmentName.TrimStart('-').Trim();
    }

    /// <summary>
    /// The fastest the world has been completed within a single attempt in the segment history,
    /// i.e. what therun.gg shows as the gold when subsplits are merged.
    /// </summary>
    public static TimeSpan? GetBestWorldTime(IList<ISegment> run, World world, TimingMethod method, out int? attemptId)
    {
        attemptId = null;
        TimeSpan? best = null;

        ISegment last = run[world.EndIndex];
        foreach (KeyValuePair<int, Time> entry in last.SegmentHistory)
        {
            if (entry.Value[method] == null)
            {
                // The world's final split was skipped, so the attempt never cleanly finished the world.
                continue;
            }

            TimeSpan? time = GetAttemptWorldTime(run, world, entry.Key, method);
            if (time != null && (best == null || time < best))
            {
                best = time;
                attemptId = entry.Key;
            }
        }

        // A world with a single segment is just that segment, so its gold always counts
        // (history may have been cleaned up, while the best segment is kept).
        if (!world.HasSubsplits)
        {
            TimeSpan? gold = last.BestSegmentTime[method];
            if (gold != null && (best == null || gold < best))
            {
                best = gold;
                attemptId = null;
            }
        }

        return best;
    }

    /// <summary>
    /// The world time of one attempt, or null if that attempt didn't play through the whole world
    /// with a known start and end. Skipped splits inside the world are fine, because LiveSplit
    /// stores their time in the next segment.
    /// </summary>
    public static TimeSpan? GetAttemptWorldTime(IList<ISegment> run, World world, int attemptId, TimingMethod method)
    {
        if (world.StartIndex > 0)
        {
            // If the split before the world was skipped, the first segment's time would include
            // part of the previous world.
            if (!run[world.StartIndex - 1].SegmentHistory.TryGetValue(attemptId, out Time previous)
                || previous[method] == null)
            {
                return null;
            }
        }

        TimeSpan total = TimeSpan.Zero;
        for (int i = world.StartIndex; i <= world.EndIndex; i++)
        {
            if (!run[i].SegmentHistory.TryGetValue(attemptId, out Time segment))
            {
                return null;
            }

            TimeSpan? time = segment[method];
            if (time != null)
            {
                total += time.Value;
            }
            else if (i == world.EndIndex)
            {
                return null;
            }
        }

        return total;
    }

    /// <summary>
    /// The sum of the best segments (golds) of every segment in the world.
    /// </summary>
    public static TimeSpan? GetSumOfGolds(IList<ISegment> run, World world, TimingMethod method)
    {
        TimeSpan total = TimeSpan.Zero;
        for (int i = world.StartIndex; i <= world.EndIndex; i++)
        {
            TimeSpan? gold = run[i].BestSegmentTime[method];
            if (gold == null)
            {
                return null;
            }

            total += gold.Value;
        }

        return total;
    }

    /// <summary>
    /// How long the world took in the personal best.
    /// </summary>
    public static TimeSpan? GetPersonalBestWorldTime(IList<ISegment> run, World world, TimingMethod method)
    {
        return GetSplitDifference(run, world, method, x => x.PersonalBestSplitTime);
    }

    /// <summary>
    /// How long the world took in the attempt that is currently running. Null if the world isn't finished yet.
    /// </summary>
    public static TimeSpan? GetCurrentRunWorldTime(IList<ISegment> run, World world, TimingMethod method, int currentSplitIndex)
    {
        if (currentSplitIndex <= world.EndIndex)
        {
            return null;
        }

        return GetSplitDifference(run, world, method, x => x.SplitTime);
    }

    /// <summary>
    /// How far into the world the runner currently is. Null unless the current split is inside the world.
    /// </summary>
    public static TimeSpan? GetCurrentRunWorldProgress(IList<ISegment> run, World world, TimingMethod method, int currentSplitIndex, TimeSpan? currentTime)
    {
        if (!world.Contains(currentSplitIndex) || currentTime == null)
        {
            return null;
        }

        TimeSpan? start = world.StartIndex == 0 ? TimeSpan.Zero : run[world.StartIndex - 1].SplitTime[method];
        return currentTime - start;
    }

    private static TimeSpan? GetSplitDifference(IList<ISegment> run, World world, TimingMethod method, Func<ISegment, Time> getSplit)
    {
        TimeSpan? end = getSplit(run[world.EndIndex])[method];
        TimeSpan? start = world.StartIndex == 0 ? TimeSpan.Zero : getSplit(run[world.StartIndex - 1])[method];
        return end - start;
    }
}
