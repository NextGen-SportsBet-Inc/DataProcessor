using MassTransit;
using Shared.Messages;

namespace DataProcessorAPI.Services;

public class MatchService(
    ISendEndpointProvider sendEndpointProvider)
{
    public async Task MatchChanged(int matchId, int teamId, double odd)
    {
        var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:match-change"));
        await endpoint.Send(new ChangeBetStatusRequest { MatchId = matchId, TeamId = teamId, Odd = odd});
    }
}

