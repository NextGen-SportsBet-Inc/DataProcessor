namespace DataProcessorAPI.Models;

public abstract class Fixture(Status status)
{
    public Status Status { get; set; } = status;
}