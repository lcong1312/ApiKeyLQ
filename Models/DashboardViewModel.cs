using System;
using System.Collections.Generic;

namespace ApiKey.Models
{
    public class DashboardViewModel
    {
        public int TotalKeys { get; set; }
        public int ActiveKeys { get; set; }
        public int ExpiredKeys { get; set; }
        public int DisabledKeys { get; set; }
        public int TotalRequests { get; set; }
        public int TodayRequests { get; set; }

        public List<TopKeyMetric> TopKeys { get; set; } = new List<TopKeyMetric>();
        public List<DailyRequestMetric> Last7DaysRequests { get; set; } = new List<DailyRequestMetric>();
    }

    public class TopKeyMetric
    {
        public string Name { get; set; }
        public string Owner { get; set; }
        public string KeyString { get; set; }
        public int RequestCount { get; set; }
    }

    public class DailyRequestMetric
    {
        public string DateString { get; set; } // "MM-dd" or "dd/MM"
        public int RequestCount { get; set; }
    }
}
