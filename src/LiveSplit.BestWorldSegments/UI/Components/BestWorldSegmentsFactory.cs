using LiveSplit.Model;
using LiveSplit.UI.Components;
using System;

[assembly: ComponentFactory(typeof(BestWorldSegmentsFactory))]

namespace LiveSplit.UI.Components;

public class BestWorldSegmentsFactory : IComponentFactory
{
    public string ComponentName => "Best World Segments";

    public string Description => "Merges subsplits into worlds and shows the best time you've done each world in a single attempt.";

    public ComponentCategory Category => ComponentCategory.List;

    public IComponent Create(LiveSplitState state)
    {
        return new BestWorldSegmentsComponent(state);
    }

    public string UpdateName => ComponentName;

    // No update server; LiveSplit ignores update checks that fail.
    public string XMLURL => "";

    public string UpdateURL => "";

    public Version Version => Version.Parse("1.0.0");
}
