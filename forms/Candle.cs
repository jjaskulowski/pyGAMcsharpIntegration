
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatisticalFrowOnSeries
{
    public sealed class Candle
    {
        public DateTime Date { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }

        TimePeriod _periods;
        public TimePeriod Periods => _periods != 0 ? _periods : _periods = CalcPeriod(Date);

        private static TimePeriod CalcPeriod(DateTime date)
        {
            return TimePeriod.M1
                | (date.Minute % 5 == 0 ? TimePeriod.M5 : 0)
                | (date.Minute % 15 == 0 ? TimePeriod.M15 : 0)
                | (date.Minute % 30 == 0 ? TimePeriod.M30 : 0)
                | (date.Minute == 0 ? TimePeriod.H1 : 0)
                | (date.Minute == 0 && date.Hour % 4 == 0 ? TimePeriod.H4 : 0)
                | (date.Minute == 0 && date.Hour == 0 ? TimePeriod.D1 : 0)
                ;
        }
    }

    public enum TimePeriod
    {
        M1 = 1,
        M5 = 2,
        M15 = 4,
        M30 = 8,
        H1 = 16,
        H4 = 32,
        D1 = 64,

    }
}
