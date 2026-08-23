using System;
using Darya.Server.Models;

namespace Darya.Server.Services
{
    public class CycleStatus
    {
        public int CurrentDay { get; set; }
        public CyclePhase Phase { get; set; }
        public string PhaseName { get; set; } = string.Empty;
        public int DaysUntilNextPeriod { get; set; }
        public string PartnerAdvice { get; set; } = string.Empty;
    }

    public interface ICycleEngine
    {
        CycleStatus CalculateStatus(User user, DateTime today);
    }

    public class CycleEngine : ICycleEngine
    {
        public CycleStatus CalculateStatus(User user, DateTime today)
        {
            int cycleLength = user.AverageCycleLength > 0 ? user.AverageCycleLength : 28;
            int periodLength = user.AveragePeriodLength > 0 ? user.AveragePeriodLength : 5;
            
            // Calculate days difference
            int daysDiff = (today.Date - user.LastPeriodStartDate.Date).Days;
            
            // Adjust for negative days (e.g. last period in the future, handle gracefully)
            int currentDay = ((daysDiff % cycleLength) + cycleLength) % cycleLength + 1;
            
            CyclePhase phase;
            string advice;

            // Phase thresholds
            int menstrualEnd = periodLength;
            int follicularEnd = Math.Max(periodLength, cycleLength / 2 - 2);
            int ovulatoryEnd = Math.Min(cycleLength - 2, cycleLength / 2 + 2);

            bool isPersian = (user.Language == "fa");
            if (currentDay <= menstrualEnd)
            {
                phase = CyclePhase.Menstrual;
                advice = isPersian 
                    ? "احتمالاً خسته است، دل‌درد داره یا انرژی‌ش کمه. با درست کردن چای نبات یا بابونه، آوردن کیسه آب گرم و انجام دادن کارهای خونه ازش حمایت کن. سعی کن آروم و باحوصله باهاش صحبت کنی."
                    : "She might feel fatigued, have cramps, or experience low energy. Support her by preparing warm herbal tea, offering a hot water bottle, and taking care of extra chores. Keep communication low-pressure and gentle.";
            }
            else if (currentDay <= follicularEnd)
            {
                phase = CyclePhase.Follicular;
                advice = isPersian 
                    ? "با بالا رفتن استروژن، انرژی و خلاقیتش در حال افزایشه. الان بهترین موقع برای برنامه‌ریزی یه قرار دونفره، امتحان کردن کارهای جدید یا پیش بردن پروژه‌های مشترکه. با انرژی خوبش همراه شو!"
                    : "Her energy, optimism, and creativity are rising as estrogen climbs. This is a great time to plan date nights, try new activities together, or tackle collaborative projects. Share in her positive vibe!";
            }
            else if (currentDay <= ovulatoryEnd)
            {
                phase = CyclePhase.Ovulatory;
                advice = isPersian 
                    ? "امروز در اوج انرژی و شادابیه. احتمالاً دوست داره بیشتر معاشرت کنه و احساس اعتماد به نفس بالایی داره. یه برنامه بیرون رفتن جذاب بچین، گفتگوهای عمیق داشته باش و حسابی بهش توجه کن."
                    : "Estrogen and testosterone peak, making this her highest-energy phase. She is likely feeling social, confident, and expressive. Plan a fun night out, engage in stimulating conversations, and show verbal appreciation.";
            }
            else
            {
                phase = CyclePhase.Luteal;
                advice = isPersian 
                    ? "احتمالاً علائم پی‌ام‌اس (PMS)، بی‌حوصلگی یا حساسیت داره. یه محیط دنج و آروم براش فراهم کن، ماساژش بده، بدون قضاوت و تلاش برای حل مسئله به حرفاش گوش کن و بهش اطمینان بده که دوسش داری."
                    : "Progesterone is high, which can bring PMS, low energy, and a desire to nest. She might feel sensitive or anxious. Provide a comforting, cozy environment at home, offer back rubs, listen actively without trying to fix everything, and reassure her of your love.";
            }

            // Calculate days until next period starts
            int daysUntilNextPeriod = cycleLength - currentDay + 1;

            return new CycleStatus
            {
                CurrentDay = currentDay,
                Phase = phase,
                PhaseName = phase.ToString(),
                DaysUntilNextPeriod = daysUntilNextPeriod,
                PartnerAdvice = advice
            };
        }
    }
}
