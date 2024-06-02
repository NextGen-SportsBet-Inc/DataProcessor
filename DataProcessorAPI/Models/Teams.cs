namespace DataProcessorAPI.Models;

public abstract class Teams(Team home, Team away)
{
    public Team Home { get; set; } = home;
    public Team Away { get; set; } = away;
}