using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace WorldCheck;

// Prints the best world segments of a .lss file without starting LiveSplit.
// Usage: WorldCheck <splits.lss> [RealTime|GameTime]
//        WorldCheck <splits.lss> --render <out.png> [currentSplitIndex]
//          Draws the component as it would look mid-run, replaying the PB up to that split.
internal static class Program
{
    private static int Main(string[] args)
    {
        string liveSplitDir = Environment.GetEnvironmentVariable("LIVESPLIT_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"Documents\#Stream\Speedrun\LiveSplit");
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
        {
            string path = Path.Combine(liveSplitDir, new AssemblyName(e.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };

        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: WorldCheck <splits.lss> [RealTime|GameTime]");
            return 1;
        }

        if (args.Length > 2 && args[1] == "--render")
        {
            Render(args[0], args[2], args.Length > 3 ? int.Parse(args[3]) : -1);
        }
        else
        {
            Run(args[0], args.Length > 1 ? args[1] : "RealTime");
        }

        return 0;
    }

    private static void Run(string path, string methodName)
    {
        var method = (TimingMethod)Enum.Parse(typeof(TimingMethod), methodName);
        IRun run = Load(path);

        Console.WriteLine($"{"World",-18} {"Segs",4} {"Best World",11} {"Attempt",7} {"PB",11} {"Sum of Golds",12} {"Save vs PB",11}");
        TimeSpan bestTotal = TimeSpan.Zero;
        foreach (World world in WorldCalculator.GetWorlds(run))
        {
            TimeSpan? best = WorldCalculator.GetBestWorldTime(run, world, method, out int? attempt);
            TimeSpan? pb = WorldCalculator.GetPersonalBestWorldTime(run, world, method);
            TimeSpan? sog = WorldCalculator.GetSumOfGolds(run, world, method);
            bestTotal += best ?? TimeSpan.Zero;
            Console.WriteLine($"{world.Name,-18} {world.SegmentCount,4} {Format(best),11} {(attempt?.ToString() ?? "gold"),7} {Format(pb),11} {Format(sog),12} {Format(pb - best),11}");
        }

        Console.WriteLine($"Sum of best worlds: {Format(bestTotal)}");
    }

    private static void Render(string path, string output, int currentSplitIndex)
    {
        IRun run = Load(path);
        var state = new LiveSplitState(run, null, null, new StandardLayoutSettingsFactory().Create(), null);
        if (currentSplitIndex >= 0)
        {
            // Replay the PB up to the given split, then pause partway into that segment.
            for (int i = 0; i < currentSplitIndex && i < run.Count; i++)
            {
                run[i].SplitTime = run[i].PersonalBestSplitTime;
            }

            state.CurrentSplitIndex = currentSplitIndex;
            state.CurrentPhase = currentSplitIndex >= run.Count ? TimerPhase.Ended : TimerPhase.Paused;
            TimeSpan previous = currentSplitIndex == 0 ? TimeSpan.Zero : run[currentSplitIndex - 1].SplitTime.RealTime ?? TimeSpan.Zero;
            state.TimePausedAt = previous + TimeSpan.FromSeconds(83.4);
        }

        var component = new BestWorldSegmentsComponent(state);
        const int width = 340;
        component.Update(null, state, width, 0, LayoutMode.Vertical);
        int height = (int)component.VerticalHeight;
        using var bitmap = new Bitmap(width, height);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.FromArgb(24, 24, 24));
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            component.DrawVertical(g, state, width, new Region());
        }

        bitmap.Save(output, ImageFormat.Png);
        Console.WriteLine($"Wrote {output} ({width}x{height})");
    }

    private static IRun Load(string path)
    {
        var doc = new XmlDocument();
        using (var file = System.IO.File.OpenRead(path)) doc.Load(file);
        var run = new Run(new StandardComparisonGeneratorsFactory());
        foreach (XmlElement element in doc.SelectNodes("/Run/Segments/Segment"))
        {
            var pb = element.SelectSingleNode("SplitTimes/SplitTime[@name='Personal Best']") as XmlElement;
            var segment = new Segment(
                element["Name"].InnerText,
                pb != null ? Time.FromXml(pb) : default,
                element["BestSegmentTime"] != null ? Time.FromXml(element["BestSegmentTime"]) : default);
            foreach (XmlElement time in element.SelectNodes("SegmentHistory/Time"))
            {
                segment.SegmentHistory[int.Parse(time.GetAttribute("id"))] = Time.FromXml(time);
            }

            run.Add(segment);
        }

        return run;
    }

    private static string Format(TimeSpan? time)
    {
        if (time == null)
        {
            return "-";
        }

        TimeSpan t = time.Value;
        string sign = t < TimeSpan.Zero ? "-" : "";
        t = t.Duration();
        return t.TotalHours >= 1
            ? $"{sign}{(int)t.TotalHours}:{t:mm\\:ss\\.ff}"
            : $"{sign}{(int)t.TotalMinutes}:{t:ss\\.ff}";
    }
}
