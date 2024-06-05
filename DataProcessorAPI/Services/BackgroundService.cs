using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataProcessorAPI.Data;
using DataProcessorAPI.Models;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DataProcessorAPI.Services;

public class ProcessorBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly Dictionary<string, string[]> _exchangeQueueMap = new()
    {
        { "football", new[] { "football_live_odds" } }
    };
    private readonly Dictionary<string, string> _footballTeamsDict;

    private static Dictionary<string, string> LoadFootballTeams(string filePath)
    {
        var footballTeamsDict = new Dictionary<string, string>();

        try
        {
            var json = File.ReadAllText(filePath);
            using var document = JsonDocument.Parse(json);
            
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                Console.WriteLine("Invalid JSON format: Expected an array at the root.");
                return footballTeamsDict;
            }

            foreach (var teamElement in document.RootElement.EnumerateArray())
            {
                if (teamElement.TryGetProperty("id", out var idElement) &&
                    teamElement.TryGetProperty("name", out var nameElement))
                {
                    footballTeamsDict[idElement.GetInt32().ToString()] = nameElement.GetString()!;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading football teams: {ex.Message}");
        }

        return footballTeamsDict;
    }


    public ProcessorBackgroundService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        var hostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST");
        var port = Environment.GetEnvironmentVariable("RABBITMQ_PORT");
        var userName = Environment.GetEnvironmentVariable("RABBITMQ_USER");
        var password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD");

        // Log the values to verify they are loaded correctly
        if (string.IsNullOrEmpty(hostName) || string.IsNullOrEmpty(port) || string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("RabbitMQ environment variables are not set");
            throw new Exception("RabbitMQ environment variables are not set");
        }

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = hostName,
                Port = int.Parse(port),
                UserName = userName,
                Password = password
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to RabbitMQ: {ex.Message}");
            throw;
        }

        _footballTeamsDict = LoadFootballTeams("TeamsData/football_teams.json");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        foreach (var (exchange, queues) in _exchangeQueueMap)
        {
            // Declare the exchange with the type 'topic'
            _channel.ExchangeDeclare(exchange: exchange, type: "topic");

            foreach (var queue in queues)
            {
                const int maxMessagesInQueue = 1;
                _channel.QueueDeclare(queue: queue, durable: true, exclusive: false, autoDelete: false, arguments: new Dictionary<string, object>
                {
                    { "x-max-length", maxMessagesInQueue }
                });
                _channel.QueueBind(queue: queue, exchange: exchange, routingKey: queue);

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (_, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ProcessorDbContext>();

                        if (queue == "football_live_odds")
                        {
                            await FootballOddMessage(message, dbContext);
                        }
                        // To add more cases for different queues if necessary
                    }

                    _channel.BasicAck(ea.DeliveryTag, false);
                };

                _channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);
            }
        }

        return Task.CompletedTask;
    }

    private async Task FootballOddMessage(string message, ProcessorDbContext dbContext)
    {
        var processedData = new List<FootballMatch>();


        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(message);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"Error deserializing message: {ex.Message}");
            return;
        }

        if (!doc.RootElement.TryGetProperty("content", out var contentElement) || 
            !contentElement.TryGetProperty("response", out var responseElement))
        {
            Console.WriteLine("Content or Response is null or not found");
            return;
        }

        Console.WriteLine("Received message from football odds");

        foreach (var response in responseElement.EnumerateArray())
        {
            if (!response.TryGetProperty("teams", out var teamsElement) ||
                !teamsElement.TryGetProperty("home", out var homeTeamElement) ||
                !homeTeamElement.TryGetProperty("id", out var homeTeamIdElement) ||
                !teamsElement.TryGetProperty("away", out var awayTeamElement) ||
                !awayTeamElement.TryGetProperty("id", out var awayTeamIdElement))
            {
                Console.WriteLine("Home or Away team ID is null or not found");
                continue;
            }

            if (!_footballTeamsDict.TryGetValue(homeTeamIdElement.ToString(), out var homeTeamName))
            {
                Console.WriteLine($"Home team ID {homeTeamIdElement} not found in footballTeamsDict");
                continue;
            }

            if (!_footballTeamsDict.TryGetValue(awayTeamIdElement.ToString(), out var awayTeamName))
            {
                Console.WriteLine($"Away team ID {awayTeamIdElement} not found in footballTeamsDict");
                continue;
            }

            if (!response.TryGetProperty("odds", out var oddsElement))
            {
                Console.WriteLine("Odds is null or not found");
                continue;
            }

            var finalScoreOdds = new Dictionary<string, double>();
            foreach (var odd in oddsElement.EnumerateArray())
            {
                if (!odd.TryGetProperty("name", out var nameElement) ||
                    nameElement.GetString() != "Final Score") continue;
                foreach (var value in odd.GetProperty("values").EnumerateArray())
                {
                    if (value.TryGetProperty("value", out var valueElement) &&
                        value.TryGetProperty("odd", out var oddElement))
                    {
                        finalScoreOdds[valueElement.GetString()!] = double.Parse(oddElement.GetString()!.Replace(".", ","));
                    }
                }
            }
            
            // If finalScoreOdds is empty, skip the current match
            if (finalScoreOdds.Count == 0)
            {
                continue;
            }
            

            var resultOdds = CalculateMatchOdds(finalScoreOdds);

            var data = new FootballMatch
            {
                Id = response.GetProperty("fixture").GetProperty("id").GetInt32(),
                HomeTeamId = homeTeamIdElement.GetInt32(),
                HomeTeamName = homeTeamName,
                AwayTeamId = awayTeamIdElement.GetInt32(),
                AwayTeamName = awayTeamName,
                ResultOdds = resultOdds.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString("F2")),
                UpdateTimestamp = DateTime.Now,
                Version = "1.0.0"
            };

            Console.WriteLine($"Processing football odds for {data.HomeTeamName} vs {data.AwayTeamName}");
            foreach (var odd in resultOdds)
            {
                Console.WriteLine($"Final score: {odd.Key} - Odd: {odd.Value}");
            }

            processedData.Add(data);
        }

        if (processedData.Count > 0)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT FootballMatches ON");
                dbContext.FootballMatches.AddRange(processedData);
                await dbContext.SaveChangesAsync();
                await dbContext.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT FootballMatches OFF");
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
    
    private static Dictionary<string, double> CalculateMatchOdds(Dictionary<string, double> finalScoreOdds)
    {
        // Convert odds to probabilities
        var probabilities = new Dictionary<string, double>();
        foreach (var (finalScore, odds) in finalScoreOdds)
        {
            probabilities[finalScore] = 1.0 / (odds + 1.0);
        }

        // Sum probabilities for each outcome
        double homeWinProb = 0;
        double awayWinProb = 0;
        double drawProb = 0;

        foreach (var (finalScore, probability) in probabilities)
        {
            var scores = finalScore.Split('-');
            var homeScore = int.Parse(scores[0]);
            var awayScore = int.Parse(scores[1]);

            if (homeScore > awayScore)
            {
                homeWinProb += probability;
            }
            else if (homeScore < awayScore)
            {
                awayWinProb += probability;
            }
            else
            {
                drawProb += probability;
            }
        }

        // Normalize probabilities to ensure they sum to 1
        var totalProb = homeWinProb + awayWinProb + drawProb;
        homeWinProb /= totalProb;
        awayWinProb /= totalProb;
        drawProb /= totalProb;

        // Convert probabilities back to odds
        var homeWinOdd = 1.0 / homeWinProb;
        var awayWinOdd = 1.0 / awayWinProb;
        var drawOdd = 1.0 / drawProb;

        return new Dictionary<string, double>
        {
            { "Home Win", homeWinOdd },
            { "Away Win", awayWinOdd },
            { "Draw", drawOdd }
        };
    }


    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Close();
        _connection.Close();
        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
        base.Dispose();
    }
}