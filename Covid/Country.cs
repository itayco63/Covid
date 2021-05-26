using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using static Covid.CountryStatusEnum;

namespace Covid
{
    public class Country
    {
        public Country(string name, CountryStatus status, ChartValues<double> reportedCv, ChartValues<double> predictedCv, List<string> new_dates,double daviation)
        {
            Name = name;
            Status = status;
            dates = new_dates;
            From = dates[0];
            To = dates[dates.Count - 1];
            reportedSeries = new LineSeries();
            reportedSeries.Values = reportedCv;
            reportedSeries.Title = "reported";
            predictionSeries = new LineSeries();
            predictionSeries.Values = predictedCv;
            predictionSeries.Title = "predicted";
            accurate = daviation;
        }
        
        public string Name { get; set; }
        public List<string> dates { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public CountryStatus Status { get; set;}
        public double accurate { get; set; }

        public LineSeries reportedSeries { get; set; }
        public LineSeries predictionSeries { get; set; }

        public static int CompareByNames(Country x, Country y)
        {
            return String.Compare(x.Name, y.Name);
        }

        public static int CompareByDate(Country x, Country y)
        {         
            return String.Compare(x.From, y.From);
        }

        public static int CompareByAccuracyUP(Country x, Country y)
        {
            if (x.accurate - y.accurate >= 0)
            {
                return 1;
            }
            else
            {
                return -1;
            }
        }

        public static int CompareByAccuracyDown(Country x, Country y)
        {
            if (x.accurate - y.accurate >= 0)
            {
                return -1;
            }
            else
            {
                return 1;
            }
        }
    }
}


