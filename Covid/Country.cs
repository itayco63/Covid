using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Text;
using static Covid.CountryStatusEnum;

namespace Covid
{
    public class Country
    {
        public Country(string name, CountryStatus status, ChartValues<double> reportedCv, ChartValues<double> predictedCv, List<string> new_dates)
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
        }
        public Country()
        {
            Name = "Country:";
            Status = CountryStatus.Status;           
            From = "From:";
            To ="To:";
        }
        public string Name { get; set; }
        public List<string> dates { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public CountryStatus Status { get; set;}
        public int NumSickPeople { get; set; }

        public LineSeries reportedSeries { get; set; }
        public LineSeries predictionSeries { get; set; }
    }

}
