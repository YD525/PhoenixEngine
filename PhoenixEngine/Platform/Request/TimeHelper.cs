using System;

namespace PhoenixEngine.Platform.Request
{
    public class TimeHelper
    {
        public static long DateTimeToTimestamp(DateTime DateTime)
        {
            DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan TS = DateTime.ToUniversalTime() - Epoch;
            return (long)TS.TotalMilliseconds;
        }

        public static DateTime TimestampToDateTime(long Timestamp)
        {
            DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return Epoch.AddMilliseconds(Timestamp).ToLocalTime();
        }
    }
}
