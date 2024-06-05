using DataProcessorAPI.Data;
using DataProcessorAPI.Models;
using DataProcessorAPI.Services;
using Microsoft.EntityFrameworkCore;

namespace DataProcessorAPI.Repository;

public class MatchRepository : IMatchRepository
{
    private readonly MatchDbContext _matchDbContext;
    
    public MatchRepository(MatchDbContext matchDbContext)
    {
        _matchDbContext = matchDbContext;
    }
    
    
    public virtual async Task<List<FootballMatch>> GetByMatchId(int matchId)
    {
        return await _matchDbContext.FootballMatches.Where(b => b.Id == matchId).ToListAsync();
    }


    public virtual async Task<List<FootballMatch>> GetAllMatches()
    {
        return await _matchDbContext.FootballMatches.ToListAsync();
    }
}
