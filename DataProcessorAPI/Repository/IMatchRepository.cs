using DataProcessorAPI.Models;

namespace DataProcessorAPI.Repository;

public interface IMatchRepository
{

    // Task<List<FootballMatch>> GetByMatchId(int matchId);
    Task<List<FootballMatch>> GetAllMatches();

}
