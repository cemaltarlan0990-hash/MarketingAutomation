using System;
using System.Collections.Generic;

namespace RelatedEntegrasyonu.CrmGateway.Infrastructure
{
    // Bounded, process-local limiter. A gateway/WAF must enforce a shared limit when scaling out.
    internal static class WebLeadRateLimiter
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        private static DateTime WindowEnd = DateTime.MinValue;
        private static int Total;

        public static bool TryAcquire(string remoteAddress, out int retryAfter)
        {
            lock (Gate)
            {
                DateTime now = DateTime.UtcNow;
                if (now >= WindowEnd)
                {
                    Counts.Clear();
                    Total = 0;
                    WindowEnd = now.AddMinutes(1);
                }
                retryAfter = Math.Max(1, (int)Math.Ceiling((WindowEnd - now).TotalSeconds));
                string key = remoteAddress ?? "unknown";
                int count;
                Counts.TryGetValue(key, out count);
                if (Total >= 30 || count >= 5) return false;
                Total++;
                Counts[key] = count + 1;
                return true;
            }
        }
    }
}
