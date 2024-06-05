using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace DataProcessorAPI.Models;

public class FootballMatch
{
        [Key]
        public int Id { get; set; } // ID must be a string
        
        public required string Time { get; set; } // Time of the match

        public required int HomeTeamId { get; set; } // ID for the home team
        
        public required string HomeTeamName { get; set; } // Name for the home team
        
        public required int AwayTeamId { get; set; } // ID for the away team
        
        public required string AwayTeamName { get; set; } // Name for the away team
        
        // public required int MatchId { get; set; } // Game current fixture value

        public required DateTime UpdateTimestamp { get; set; } // Date time
        
        public required string Version { get; set; } // Versioning control for the entity
        
        [NotMapped]
        public Dictionary<string, string>? ResultOdds { get; set; } // Dictionary for the values of possible final score and respective odd value

        public string ResultOddsJson
        {
                get => JsonSerializer.Serialize(ResultOdds);
                set => ResultOdds = JsonSerializer.Deserialize<Dictionary<string, string>>(value);
        }
        
}