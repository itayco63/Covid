﻿using LiveCharts;
using LiveCharts.Wpf;
using Json.Net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using System.Diagnostics;
using System.Globalization;

namespace Covid
{

    public partial class MainWindow : Window, INotifyPropertyChanged
    {


        public ObservableCollection<Country> Countries { get; set; } //presented countries list
        private List<Country> MahsanList = new List<Country>(); //full cuntries list
        List<Root> items; //items from Json file
        public event PropertyChangedEventHandler PropertyChanged;
        int numOfGood, numOfSuspect, numOfBad;
        private const string validation_file = "validation.json";
        private string path;
        int table_sort = -1;
        //////graph handling

        public Func<double, string> YFormatter { get; set; }
        private string[] _labels;
        public string[] Labels
        {
            get => _labels;
            set
            {
                _labels = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Labels"));
            }
        }

        private SeriesCollection _seriesCollection;
        public SeriesCollection SeriesCollection
        {
            get => _seriesCollection;
            set
            {
                _seriesCollection = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SeriesCollection"));
            }
        }

        ////////



        ///////class represents one item from Json file

        public class Root
        {

            public string country_name { get; set; }
            public List<string> dates { get; set; }
            public List<double> demographic_inputs { get; set; }
            public double expected_cases { get; set; }
            public List<double> infection_trend { get; set; }
            public List<double> quarantine_trend { get; set; }
            public List<double> restriction_trend { get; set; }
            public List<double> school_trend { get; set; }
            public List<List<double>> temporal_inputs { get; set; }
            public double Prediction { get; set; }


            public static int CompareByNames(Root x, Root y)
            {
                return String.Compare(x.country_name, y.country_name);
            }
            public static int CompareByDate(Root x, Root y)
            {
                if (DateTime.Parse(x.dates[0], CultureInfo.InvariantCulture) <= DateTime.Parse(y.dates[0], CultureInfo.InvariantCulture))
                {
                    return - 1;               
                }
                else
                {
                    return 1;
                }


            }
            public static int CompareByAccuracy(Root x, Root y)
            {
                if (DateTime.Parse(x.dates[0], CultureInfo.InvariantCulture) <= DateTime.Parse(y.dates[0], CultureInfo.InvariantCulture))
                {
                    return -1;
                }
                else
                {
                    return 1;
                }
            }

        }
        ///////



        //////clicked country changed

        private Country _country;
        public Country SelectedCountry
        {
            get => _country;
            set
            {
                if (!value.Name.Equals("Country:"))
                {
                    _country = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedCountry"));
                }

            }
        }

        ////////



        //////start window
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Countries = new ObservableCollection<Country>();
            numOfBad = 0;
            numOfSuspect = 0;
            numOfGood = 0;
            path = System.IO.Directory.GetCurrentDirectory();
        }

        //////




        /////presenting full list

        private void intializeCountries()
        {
            Countries.Clear();
            foreach (var country in MahsanList)
            {
                Countries.Add(country);
            }
        }


        ///////



        //////choosing item in table

        private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            var selected = sender as ListViewItem;
            Country tempCountry = selected.Content as Country;
            if (!tempCountry.Name.Equals("Country:") && !tempCountry.Status.Equals(CountryStatusEnum.CountryStatus.badData))
            {
                SelectedCountry = tempCountry;

                
                country.Content = "Country: " + SelectedCountry.Name;
                from.Content = "From: " + SelectedCountry.From;
                to.Content = "To: " + SelectedCountry.To;
                deviation.Content = "Deviation: " + SelectedCountry.accurate+"%";
                countryStatus.Content = "Status: " + SelectedCountry.Status;

                chart.Visibility = Visibility.Visible;
                country.Visibility = Visibility.Visible;
                from.Visibility = Visibility.Visible;
                to.Visibility = Visibility.Visible;
                deviation.Visibility = Visibility.Visible;
                countryStatus.Visibility = Visibility.Visible;
                if (chart.Visibility == Visibility.Hidden)
                {
                    chart.Visibility = Visibility.Visible;
                }


                SeriesCollection = new SeriesCollection
                {
                new LineSeries
                {
                    Title = SelectedCountry.reportedSeries.Title,
                    Values = SelectedCountry.reportedSeries.Values,
                },
                 new LineSeries
                {
                    Title = SelectedCountry.predictionSeries.Title,
                    Values = SelectedCountry.predictionSeries.Values,
                },
                };

                Labels = SelectedCountry.dates.ToArray();
                YFormatter = value => Math.Round(value,2).ToString();
                DataContext = this;

            }
            else
            {
                chart.Visibility = Visibility.Hidden;
                country.Visibility = Visibility.Hidden;
                from.Visibility = Visibility.Hidden;
                to.Visibility = Visibility.Hidden;
                deviation.Visibility = Visibility.Hidden;
                countryStatus.Visibility = Visibility.Hidden;

            }
        }

        //////





        //////load items from Json file
        public void LoadJson()
        {

            using (StreamReader r = new StreamReader(path + @"\" + validation_file))
            {
                string json = r.ReadToEnd();
                items = JsonConvert.DeserializeObject<List<Root>>(json);
            }

        }
        //////




        //////run python file
        private void doPython()
        {
            string strCmdText;
            if (train.IsChecked == true)
            {
                strCmdText = path + @"\main.py -t";
            }
            else
            {
                strCmdText = path + @"\main.py";
            }
            Process p = Process.Start("python.exe", strCmdText);
            p.WaitForExit();

        }
        ///////


        private void reset()
        {
            MahsanList.Clear();
            numOfGood = 0;
            numOfSuspect = 0;
            numOfBad=0;
        }

        
        //////runing prediction and filling table
        private void Run_prediction(object sender, RoutedEventArgs e)
        {
            reset();
            doPython();
            string name;
            List<double> infections;
            List<double> predictions;
            List<string> new_dates;
            double daviation, lastDay, pred, exp, badPrec, goodPrec, susPrec;
            LoadJson();

            Root[] temp = items.ToArray();              //move to array and sort by names
            
            Array.Sort(temp, Root.CompareByNames);
            int len = temp.Length;


            for (int i = 0; i < len; i++)       //inserting item from file to full list
            {
                name = temp[i].country_name;
                predictions = new List<double>(temp[i].infection_trend);
                predictions.Add(temp[i].Prediction);
                infections = temp[i].infection_trend;
                infections.Add(temp[i].expected_cases);
                for(int j = 0; j < infections.Count; j++)
                {
                    infections[j] = Math.Round(infections[j]);
                    predictions[j] = Math.Round(predictions[j]);
                }
                new_dates = temp[i].dates;

                exp = temp[i].expected_cases;
                pred = temp[i].Prediction;
                lastDay = temp[i].infection_trend[infections.Count - 2];
                daviation = (((pred - exp) / pred) * 100);
                MahsanList.Add(new Country(name, getStatus(daviation,lastDay, pred, exp), new ChartValues<double>(infections), new ChartValues<double>(predictions), new_dates, Math.Round(daviation,2)));

            }
            intializeCountries();                                       //putting items in present list
            countriesList.Visibility = Visibility.Visible;
            Mark_Ok.Visibility = Visibility.Visible;
            Mark_sus.Visibility = Visibility.Visible;
            sort.Visibility = Visibility.Visible;
            byAcur.Visibility = Visibility.Visible;
            byDate.Visibility = Visibility.Visible;
            byName.Visibility = Visibility.Visible;


            goodPrec = (double)numOfGood / len * 100;                     //calculating statistics
            badPrec = (double)numOfBad / len * 100;
            susPrec = (double)numOfSuspect / len * 100;
            goodPrec = Math.Round(goodPrec, 2);
            badPrec = Math.Round(badPrec, 2);
            susPrec = Math.Round(susPrec, 2);

            Total.Content = "Total trends: " + temp.Length;                                         //showing statistics
            total_bad.Content = "Total bad data: " + numOfBad + "  ->  " + badPrec + "%";
            total_sus.Content = "Total suspected trends: " + numOfSuspect + "  ->  " + susPrec + "%";
            total_good.Content = "Total good trends: " + numOfGood + "  ->  " + goodPrec + "%";

        }


        ////////



        private CountryStatusEnum.CountryStatus getStatus(double daviation, double lastDay, double pred, double exp)      //get status of trend
        {
            

            if (lastDay > pred && daviation < -10)            //check if bad descending
            {
                numOfBad++;
                return CountryStatusEnum.CountryStatus.badData;
            }

            else if (10 < pred - exp && daviation > 5)            //check if bad ascending
            {
                numOfSuspect++;
                return CountryStatusEnum.CountryStatus.Suspect;

            }
            else
            {
                numOfGood++;
                return CountryStatusEnum.CountryStatus.Regular;
            }
        }

        //////



        ////// showing only OK countries
        private void OK_Checked(object sender, RoutedEventArgs e)
        {
            int len = Countries.Count;
            for (int i = len - 1; i >= 0; i--)
            {
                if (!(Countries[i].Status.Equals(CountryStatusEnum.CountryStatus.Regular) || Countries[i].Status.Equals(CountryStatusEnum.CountryStatus.Status)))
                {
                    Countries.Remove(Countries[i]);

                }
            }
            Mark_sus.IsEnabled = false;
        }

        //////


        ////// showing only Suspect countries

        private void suspect_Checked(object sender, RoutedEventArgs e)
        {

            int len = Countries.Count;
            for (int i = len - 1; i >= 0; i--)
            {
                if (!(Countries[i].Status.Equals(CountryStatusEnum.CountryStatus.Suspect) || Countries[i].Status.Equals(CountryStatusEnum.CountryStatus.Status)))
                {
                    Countries.Remove(Countries[i]);

                }
            }
            Mark_Ok.IsEnabled = false;
        }


        //////


        ////// return to full list


        private void Unchecked(object sender, RoutedEventArgs e)
        {
            intializeCountries();
            Mark_Ok.IsEnabled = true;
            Mark_sus.IsEnabled = true;
        }


        /////


        private void sortName(object sender, RoutedEventArgs e)
        {
            table_sort = 0;
            sortTable();
        }
        private void sortDate(object sender, RoutedEventArgs e)
        {
            table_sort = 1;
            sortTable();
        }
        private void sortAcur(object sender, RoutedEventArgs e)
        {
            if(table_sort == 2)
            {
                table_sort = 3;
            }
            else
            {
                table_sort = 2;
            }
            sortTable();
        }
        
        public void sortTable()
        {
            Country[] temp = MahsanList.ToArray();
            switch (table_sort)
            {
                case 0:
                    Array.Sort(temp, Country.CompareByNames);
                    break;
                case 1:
                    Array.Sort(temp, Country.CompareByDate);
                    break;
                case 2:
                    Array.Sort(temp, Country.CompareByAccuracyDown);
                    break;
                case 3:
                    Array.Sort(temp, Country.CompareByAccuracyUP);
                    break;
                default:
                    break;
            }
            MahsanList.Clear();
            MahsanList.AddRange(temp);
            intializeCountries();
        }

    }
}
