using System;
using System.Collections.Generic;

namespace Darya.Server.Models
{
    public enum CyclePhase
    {
        Menstrual = 1,
        Follicular = 2,
        Ovulatory = 3,
        Luteal = 4
    }

    public enum FlowIntensity
    {
        None = 0,
        Light = 1,
        Medium = 2,
        Heavy = 3
    }

    public enum Mood
    {
        Calm = 1,
        Happy = 2,
        Sensitive = 3,
        LowEnergy = 4,
        Anxious = 5,
        Irritable = 6
    }

    public enum Role
    {
        PrimaryUser = 1,
        Partner = 2
    }

    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Role Role { get; set; } = Role.PrimaryUser;
        
        // Cycle settings (mainly relevant for PrimaryUser, but can be configured)
        public int AverageCycleLength { get; set; } = 28;
        public int AveragePeriodLength { get; set; } = 5;
        public DateTime LastPeriodStartDate { get; set; }
        public long? PartnerTelegramChatId { get; set; }
        public string Language { get; set; } = "fa";

        // Partner Linking
        public string? PartnerCode { get; set; } // 6-character linking code
        public Guid? PartnerUserId { get; set; } // Reference to the linked user
        public DateTime? LinkedAt { get; set; }

        // Navigation
        public ICollection<DailyLog> DailyLogs { get; set; } = new List<DailyLog>();
    }

    public class DailyLog
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime Date { get; set; }
        public FlowIntensity FlowIntensity { get; set; }
        public Mood Mood { get; set; }
        public int PainLevel { get; set; } // 0 to 5
        public string Note { get; set; } = string.Empty;

        // Navigation
        public User User { get; set; } = null!;
    }
}
