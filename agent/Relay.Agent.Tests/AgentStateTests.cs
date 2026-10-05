using System.IO;

namespace Relay.Agent.Tests;

public class AgentStateTests
{
    [Fact]
    public void TheWindowSpotSurvivesTheStateFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"relay-state-{Guid.NewGuid():n}.json");
        File.WriteAllText(path, """{"AgentId":"a","Token":"t","Port":8731,"Window":{"Left":3330,"Top":346,"Width":1020,"Height":700,"Maximized":false}}""");
        try
        {
            var state = AgentState.LoadOrCreate(path);
            Assert.Equal(new WindowPlacement(3330, 346, 1020, 700, false), state.Window);
        }
        finally { File.Delete(path); }
    }
}
