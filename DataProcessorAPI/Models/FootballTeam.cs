namespace DataProcessorAPI.Models;


// ReSharper disable once ClassNeverInstantiated.Global
public class FootballTeam(int id, string name)
{
    public int Id { get; set; } = id;
    public string Name { get; set; } = name;
}
