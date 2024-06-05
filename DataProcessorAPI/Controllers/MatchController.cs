using DataProcessorAPI.Models;
using DataProcessorAPI.Repository;
using DataProcessorAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataProcessorAPI.Controllers;


[ApiController]
[Route("[controller]")]
public class MatchController : ControllerBase
{
    private readonly ILogger<MatchController> _logger;
    private readonly IMatchRepository _matchRepository;

    public MatchController(ILogger<MatchController> logger, IMatchRepository matchRepository)
    {
        _logger = logger;
        _matchRepository = matchRepository;
    }

    // [Authorize]
    [HttpGet("/liveFootballMatches")]
    public async Task<IActionResult> LiveFootballMatches()
    {
        var matches = await _matchRepository.GetAllMatches();
        return Ok(new { matches });
    }
}
