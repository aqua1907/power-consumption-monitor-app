namespace PowerMonitor.Core.Games;

public static class GameProfiles
{
    /// <summary>
    /// Games switched with the house power state. To add one: implement <see cref="IGamePowerProfile"/>
    /// and append it here. Give it its own path on AppSettings if the install location varies.
    /// </summary>
    public static IReadOnlyList<IGamePowerProfile> All { get; } =
    [
        new WowForeverProfile()
    ];
}
