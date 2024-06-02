namespace DataProcessorAPI.Models;

public abstract class ResponseM(List<Odd> odds, Fixture fixture, Teams teams)
{
    public Teams Teams { get; set; } = teams;
    public Fixture Fixture { get; set; } = fixture;
    public List<Odd> Odds { get; set; } = odds;
}