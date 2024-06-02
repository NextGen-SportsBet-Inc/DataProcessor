namespace DataProcessorAPI.Models;


public abstract class FootballTeam(string id, string name)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
}