namespace DataProcessorAPI.Models;

public abstract class Odd(string name, List<Value> values)
{
    public string Name { get; set; } = name;
    public List<Value> Values { get; set; } = values;
}