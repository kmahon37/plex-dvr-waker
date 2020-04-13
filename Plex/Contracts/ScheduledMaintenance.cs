using System;

namespace PlexDvrWaker.Plex.Contracts
{
    internal class ScheduledMaintenance
    {
        public int StartHour { get; set; }
        public int EndHour { get; set; }

        public string StartHourString
        {
            get
            {
                return GetHourString(StartHour);
            }
        }
        public string EndHourString
        {
            get
            {
                return GetHourString(EndHour);
            }
        }

        public DateTime StartTime
        {
            get
            {
                var currentDate = DateTime.Now;
                return GetDateWithHour(currentDate.Hour >= StartHour ? currentDate.AddDays(1) : currentDate, StartHour);
            }
        }
        public DateTime EndTime
        {
            get
            {
                return GetDateWithHour(StartTime, EndHour);
            }
        }

        #region Private

        private DateTime GetDateWithHour(DateTime date, int hour)
        {
            return new DateTime(date.Year, date.Month, date.Day, hour, 0, 0);
        }

        private string GetHourString(int hour)
        {
            if (hour == 0)
            {
                return "Midnight";
            }
            else if (hour == 12)
            {
                return "Noon";
            }
            else if (hour > 12)
            {
                return $"{(hour - 12)}pm";
            }
            else
            {
                return $"{hour}am";
            }
        }

        #endregion Private
    }
}