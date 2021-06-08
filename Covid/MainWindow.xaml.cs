using LiveCharts;
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
    public class TableSortEnum
    {
        public enum TableSort
        {
            NoneSort,
            NamesUp,
            NamesDown,
            DateUp,
            DateDown,
            AccuracyUp,
            AccuracyDown
        }
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<Country> Countries { get; set; } //presented countries list
        private List<Country> full_list = new List<Country>(); //full cuntries list
        List<Root> items; //items from Json file
        public event PropertyChangedEventHandler PropertyChanged;
        int num_of_good, num_of_suspect;
        private const string validation_file = "validation.json";
        private const string script_file = "main.py";
        private const int allowed_deviation = 5;
        private const int allowed_diff = 10;
        private string path;
        TableSortEnum.TableSort table_sort = TableSortEnum.TableSort.NoneSort;

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
        }

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

        public TableSortEnum.TableSort Table_sort { get => table_sort; set => table_sort = value; }

        //////start window
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Countries = new ObservableCollection<Country>();

            num_of_suspect = 0;
            num_of_good = 0;
            path = System.IO.Directory.GetCurrentDirectory();
        }

        /////presenting full list
        private void intializeCountries(List<Country> countries)
        {
            this.Countries.Clear();
            foreach (var country in countries)
            {
                this.Countries.Add(country);
            }
        }

        //////choosing item in table
        private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            int size;
            var selected = sender as ListViewItem;
            Country tempCountry = selected.Content as Country;

            SelectedCountry = tempCountry;

            country.Text = "Country: " + SelectedCountry.Name;
            from.Content = "From: " + SelectedCountry.From;
            to.Content = "To: " + SelectedCountry.To;
            deviation.Content = "Deviation: " + SelectedCountry.accurate + "%";
            countryStatus.Content = "Status: " + SelectedCountry.Status;
            size = SelectedCountry.reportedSeries.Values.Count;
            reported.Content = "Reported: " + SelectedCountry.reportedSeries.Values[size - 1];
            predicted.Content = "Predicted: " + SelectedCountry.predictionSeries.Values[size - 1];
            if (chart.Visibility == Visibility.Hidden)
            {
                chart.Visibility = Visibility.Visible;
            }

            SeriesCollection = new SeriesCollection
                {
                 new LineSeries
                {
                    Title = SelectedCountry.predictionSeries.Title,
                    Values = SelectedCountry.predictionSeries.Values,
                },
                 new LineSeries
                {
                    Title = SelectedCountry.reportedSeries.Title,
                    Values = SelectedCountry.reportedSeries.Values,
                },
                };
            Labels = SelectedCountry.dates.ToArray();
            YFormatter = value => Math.Round(value, 2).ToString();
            DataContext = this;

        }
        
        //////load items from Json file
        public void LoadJson()
        {

            using (StreamReader r = new StreamReader(path + @"\" + validation_file))
            {
                string json = r.ReadToEnd();
                items = JsonConvert.DeserializeObject<List<Root>>(json);
            }

        }

        //////run python file
        private void doPython()
        {
            string strCmdText = path + @"\"+script_file;
            if (train.IsChecked == true)
            {
                strCmdText +=" -t";
            }
            Process p = Process.Start("python.exe", strCmdText);
            p.WaitForExit();
        }

        private void reset()
        {
            full_list.Clear();
            num_of_good = 0;
            num_of_suspect = 0;

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
            double deviation, lastDay, pred, exp, goodPrec, susPrec;
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
                for (int j = 0; j < infections.Count; j++)
                {
                    infections[j] = Math.Round(infections[j]);
                    predictions[j] = Math.Round(predictions[j]);
                }
                new_dates = temp[i].dates;

                exp = temp[i].expected_cases;
                pred = temp[i].Prediction;
                lastDay = temp[i].infection_trend[infections.Count - 2];
                deviation = (((pred - exp) / pred) * 100);

                if (!(lastDay > pred))
                {
                    full_list.Add(new Country(name, getStatus(deviation, pred, exp), new ChartValues<double>(infections), new ChartValues<double>(predictions), new_dates, Math.Round(deviation, 2)));
                }
            }

            intializeCountries(full_list);                                       //putting items in present list
            countriesList.Visibility = Visibility.Visible;
            Mark_Ok.Visibility = Visibility.Visible;
            Mark_sus.Visibility = Visibility.Visible;
            sort.Visibility = Visibility.Visible;
            byAcur.Visibility = Visibility.Visible;
            byDate.Visibility = Visibility.Visible;
            byName.Visibility = Visibility.Visible;
            len = full_list.Count;

            goodPrec = (double)num_of_good / len * 100;                     //calculating statistics           
            susPrec = (double)num_of_suspect / len * 100;
            goodPrec = Math.Round(goodPrec, 2);
            susPrec = Math.Round(susPrec, 2);

            Total.Content = "Total trends: " + len;                                         //showing statistics
            total_sus.Content = "Total suspicious trends: " + num_of_suspect + "  ->  " + susPrec + "%";
            total_good.Content = "Total good trends: " + num_of_good + "  ->  " + goodPrec + "%";

        }

        private CountryStatusEnum.CountryStatus getStatus(double deviation, double pred, double exp)      //get status of trend
        {
            if (allowed_diff < pred - exp && deviation > allowed_deviation )               //check if suspect
            {
                num_of_suspect++;
                return CountryStatusEnum.CountryStatus.Suspect;
            }
            else
            {
                num_of_good++;
                return CountryStatusEnum.CountryStatus.Regular;
            }
        }

        ////// showing only OK countries
        private void OK_Checked(object sender, RoutedEventArgs e)
        {
            int len = Countries.Count;
            for (int i = len - 1; i >= 0; i--)
            {
                if (!Countries[i].Status.Equals(CountryStatusEnum.CountryStatus.Regular))
                {
                    Countries.Remove(Countries[i]);

                }
            }
            Mark_sus.IsEnabled = false;
        }

        ////// showing only Suspect countries
        private void suspect_Checked(object sender, RoutedEventArgs e)
        {
            int len = Countries.Count;
            for (int i = len - 1; i >= 0; i--)
            {
                if (!Countries[i].Status.Equals(CountryStatusEnum.CountryStatus.Suspect))
                {
                    Countries.Remove(Countries[i]);

                }
            }
            Mark_Ok.IsEnabled = false;
        }

        ////// return to full list
        private void Unchecked(object sender, RoutedEventArgs e)
        {
            Country[] temp = new Country[full_list.Count];
            full_list.CopyTo(temp, 0);
            switch (Table_sort)
            {
                case TableSortEnum.TableSort.NamesUp:
                    Array.Sort(temp, Country.CompareByNamesUP);
                    break;
                case TableSortEnum.TableSort.NamesDown:
                    Array.Sort(temp, Country.CompareByNamesDown);
                    break;
                case TableSortEnum.TableSort.DateUp:
                    Array.Sort(temp, Country.CompareByDateUP);
                    break;
                case TableSortEnum.TableSort.DateDown:
                    Array.Sort(temp, Country.CompareByDateDown);
                    break;
                case TableSortEnum.TableSort.AccuracyUp:
                    Array.Sort(temp, Country.CompareByAccuracyUP);
                    break;
                case TableSortEnum.TableSort.AccuracyDown:
                    Array.Sort(temp, Country.CompareByAccuracyDown);
                    break;
                default:
                    break;
            }
            full_list.Clear();
            full_list.AddRange(temp);
            intializeCountries(full_list);
            Mark_Ok.IsEnabled = true;
            Mark_sus.IsEnabled = true;
        }

        private void sortName(object sender, RoutedEventArgs e)
        {
            if (Table_sort == TableSortEnum.TableSort.NamesUp)
            {
                Table_sort = TableSortEnum.TableSort.NamesDown;
            }
            else
            {
                Table_sort = TableSortEnum.TableSort.NamesUp;
            }
            sortTable();

        }

        private void sortDate(object sender, RoutedEventArgs e)
        {
            if (Table_sort == TableSortEnum.TableSort.DateUp)
            {
                Table_sort = TableSortEnum.TableSort.DateDown;
            }
            else
            {
                Table_sort = TableSortEnum.TableSort.DateUp;
            }
            sortTable();
        }

        private void sortAcur(object sender, RoutedEventArgs e)
        {
            if (Table_sort == TableSortEnum.TableSort.AccuracyUp)
            {
                Table_sort = TableSortEnum.TableSort.AccuracyDown;
            }
            else
            {
                Table_sort = TableSortEnum.TableSort.AccuracyUp;
            }
            sortTable();
        }

        public void sortTable()
        {
            Country[] temp = new Country[Countries.Count];
            Countries.CopyTo(temp, 0);
            switch (Table_sort)
            {
                case TableSortEnum.TableSort.NamesUp:
                    Array.Sort(temp, Country.CompareByNamesUP);
                    break;
                case TableSortEnum.TableSort.NamesDown:
                    Array.Sort(temp, Country.CompareByNamesDown);
                    break;
                case TableSortEnum.TableSort.DateUp:
                    Array.Sort(temp, Country.CompareByDateUP);
                    break;
                case TableSortEnum.TableSort.DateDown:
                    Array.Sort(temp, Country.CompareByDateDown);
                    break;
                case TableSortEnum.TableSort.AccuracyUp:
                    Array.Sort(temp, Country.CompareByAccuracyUP);
                    break;
                case TableSortEnum.TableSort.AccuracyDown:
                    Array.Sort(temp, Country.CompareByAccuracyDown);
                    break;
                default:
                    break;
            }
            intializeCountries(new List<Country>(temp));
        }

    }
}
