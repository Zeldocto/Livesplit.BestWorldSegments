# LiveSplit.BestWorldSegments

A LiveSplit component that merges subsplits into worlds and shows your **best world segment**: the fastest you've played each world within a single attempt. It's the same number therun.gg shows after "Merge subsplits".

Worlds follow the Subsplits component's naming: segments starting with `-` are subsplits, and the next segment ends the world (`{Bianco Hills} Last Split` → "Bianco Hills"). A segment that isn't part of a group (e.g. `Bowser`) is its own world.

## Columns

| Column | Meaning |
| --- | --- |
| This Run | World time in the current attempt. Live while you're in the world; once finished it's gold for a new best world, green if at or ahead of your PB's world time, red if behind. |
| PB | World time in your personal best. |
| SoG | Sum of golds of the world's segments (off by default). This is ≤ Best, since golds can come from different attempts. |
| Best | Best world segment from your segment history. |

## Settings page

The top of the settings page lists every world's Best World, PB and Sum of Golds, with totals, for the loaded splits. Uncheck **Display world segments on layout** to hide the component from the layout while keeping that table available.

**Rows to show** limits how many worlds are drawn (0 = all). The window follows the current world and keeps it centered when it can, so 1 shows only the world you're in.

## Notes

The Best column comes from segment history, so a new best world from the current run shows up there after you reset and save times, the same way golds work in LiveSplit.

### How skipped splits are handled

An attempt counts for a world only if the split before the world and the world's last split both have times. Skipped splits inside the world are fine, because LiveSplit stores their time in the next segment.

## Install

Copy `LiveSplit.BestWorldSegments.dll` into LiveSplit's `Components` folder, restart LiveSplit, then go to Edit Layout → + → List → Best World Segments.

## Build

Requires the .NET SDK (8 or newer). It references the `LiveSplit.Core.dll` and `UpdateManager.dll` from your LiveSplit install, set in `LiveSplitDir.props`:

```
dotnet build src/LiveSplit.BestWorldSegments -c Release -p:LiveSplitDir="C:\path\to\LiveSplit"
```

## WorldCheck

`tools/WorldCheck` prints the numbers for a splits file without opening LiveSplit, and can render a preview image of the component:

```
dotnet run --project tools/WorldCheck -c Release -- "splits.lss"
dotnet run --project tools/WorldCheck -c Release -- "splits.lss" --render preview.png 9
```
