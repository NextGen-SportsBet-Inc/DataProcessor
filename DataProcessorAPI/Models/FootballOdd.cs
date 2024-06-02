using System.ComponentModel.DataAnnotations;

namespace DataProcessorAPI.Models;

public class FootballOdd
{
        [Key]
        public string Id { get; set; } = null!; // ID must be a string

        public required string HomeTeamId { get; set; } // ID for the home team
        
        public required string HomeTeamName { get; set; } // Name for the home team
        
        public required string AwayTeamId { get; set; } // ID for the away team
        
        public required string AwayTeamName { get; set; } // Name for the away team
        
        public required string FixtureId { get; set; } // Game current fixture value

        public required Dictionary<string, string> ResultOdds { get; set; } // Dictionary for the values of possible final score and respective odd value

        public required DateTime OddTimestamp { get; set; } // Date time
        
        public required string Version { get; set; } // Versioning control for the entity
        
}