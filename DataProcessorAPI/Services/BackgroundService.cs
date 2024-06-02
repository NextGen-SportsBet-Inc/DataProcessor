using System.Text;
using System.Text.Json;
using DataProcessorAPI.Data;
using DataProcessorAPI.Models;
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
        // { "exchange", ["queue1", "queue2"] },
        { "football", ["football_live_odds"] }
    };
    private readonly Dictionary<string, string> _footballTeamsDict = LoadFootballTeams("../../marosca/football_teams.json");


    private static Dictionary<string, string> LoadFootballTeams(string filePath)
    {
        var footballTeamsDict = new Dictionary<string, string>();
        var json = File.ReadAllText(filePath);
        var footballTeamsList = JsonSerializer.Deserialize<List<FootballTeam>>(json);

        if (footballTeamsList == null) return footballTeamsDict;
        foreach (var team in footballTeamsList!)
        {
            footballTeamsDict[team.Id] = team.Name;
        }

        return footballTeamsDict;
    }

    public ProcessorBackgroundService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        var factory = new ConnectionFactory
        {
            HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST"),
            UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER"),
            Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD")
        };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        foreach (var (exchange, queues) in _exchangeQueueMap)
        {
            _channel.ExchangeDeclare(exchange: exchange, type: ExchangeType.Direct);

            foreach (var queue in queues)
            {
                const int maxMessagesInQueue = 1;
                _channel.QueueDeclare(queue: queue, durable: true, arguments: new Dictionary<string, object>
                {
                    { "x-max-length", maxMessagesInQueue }
                });
                // _channel.QueueBind(queue: queue, exchange: exchange, routingKey: queue);

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
        var processedData = new List<FootballOdd>();
        
        var responseMessage = JsonSerializer.Deserialize<ResponseMessage>(message);

        if (responseMessage?.Response != null)
        {
            foreach (var response in responseMessage.Response)
            {
                var data = new FootballOdd
                {
                    HomeTeamId = response.Teams.Home.Id,
                    HomeTeamName = _footballTeamsDict[response.Teams.Home.Id],
                    AwayTeamId = response.Teams.Away.Id,
                    AwayTeamName = _footballTeamsDict[response.Teams.Away.Id],
                    FixtureId = response.Fixture.Status.Long,
                    ResultOdds = new Dictionary<string, string>(),
                    OddTimestamp = DateTime.Now,
                    Version = "1.0.0"
                };

                foreach (var value in response.Odds.Where(odd => odd.Name == "Final Score").SelectMany(odd => odd.Values))
                {
                    data.ResultOdds[value.ValueV] = value.Odd;
                }

                processedData.Add(data);
            }

            dbContext.FootballOdds.AddRange(processedData);
            await dbContext.SaveChangesAsync();
        }
        
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