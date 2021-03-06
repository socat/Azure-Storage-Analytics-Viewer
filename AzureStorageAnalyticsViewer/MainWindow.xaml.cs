﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using AzureStorageAnalyticsViewer.Entities;
using System.Windows.Controls.DataVisualization.Charting;
using Microsoft.WindowsAzure.StorageClient;
using AzureStorageAnalyticsViewer.Download;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Configuration;
using System.Reflection;
using Microsoft.WindowsAzure;

namespace AzureStorageAnalyticsViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += new RoutedEventHandler(MainWindow_Loaded);
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

            // init datetime
            start_dp.SelectedDate = DateTime.UtcNow.Date.Subtract(TimeSpan.FromDays(1));
            end_dp.SelectedDate = DateTime.UtcNow.Date;
            List<TimeSpan> timelist = new List<TimeSpan>();
            for (int i = 0; i < 24; i++)
                timelist.Add(TimeSpan.FromHours(i));
            var time = TimeSpan.FromHours(DateTime.UtcNow.Hour);
            start_cb.ItemsSource = timelist;
            start_cb.SelectedItem = time;
            end_cb.ItemsSource = timelist;
            end_cb.SelectedItem = time;

            // init storage type
            access_cb.ItemsSource = new string[] { "user", "system" };
            access_cb.SelectedIndex = 0;
            trans_cb.ItemsSource = new string[] { "All" };
            trans_cb.SelectedIndex = 0;
            storage_cb.ItemsSource = new StorageType[] { StorageType.Table, StorageType.Blob, StorageType.Queue };
            storage_cb.SelectedIndex = 0;

            // init chart series
            tlc1.AddSeries("TotalRequests");
            tlc1.AddSeries("TotalBillableRequests");
            tlc1.AddSeries("Success");
            tlc2.AddSeries("ServerTimeoutError");
            tlc3.AddSeries("ThrottlingError");
        }

        List<MetricsTransactionsEntity> CompressDataPoints(List<MetricsTransactionsEntity> orilist)
        {
            int datapointnum = int.Parse(ConfigurationManager.AppSettings["datapointsnumber"]);
            List<MetricsTransactionsEntity> retlist = new List<MetricsTransactionsEntity>();

            if(orilist.Count==0)
                return retlist;

            double stepf = orilist.Count / datapointnum;
            if (stepf >= 2)
            {
                int step = (int)stepf;
                MetricsTransactionsEntity tmpmte = null;
                for (int i = 0; i < orilist.Count; i++)
                {
                    if (i % step == 0)
                    {
                        if(tmpmte!=null)
                            retlist.Add(tmpmte);
                        tmpmte = orilist[i];
                    }
                    else
                        tmpmte = tmpmte.Merge(orilist[i]);
                }
                retlist.Add(tmpmte);
                return retlist;
            }
            else
                return orilist;
        }

        CloudStorageAccount _account;
        List<MetricsTransactionsEntity> _mtes;
        public void InitializeMetricsView(StorageType type)
        {
            try
            {
                if (_account == null)
                {
                    MessageBox.Show("Storage account haven't been initialized");
                    return;
                }

                var startdate = start_dp.SelectedDate.Value.Add((TimeSpan)start_cb.SelectedItem);
                var enddate = end_dp.SelectedDate.Value.Add((TimeSpan)end_cb.SelectedItem);
                

                // hours or minutes, if < 4 hours, show minutes!
                _mtes = DownloadTransactionMetrics(type, startdate, enddate);

                tcp1.DataContext = CompressDataPoints(_mtes);
                //tcp1.DataContext = _mtes;

                tl_tb.Text = string.Format("{0} - {1} {2} Metrics ({3};{4})", startdate, enddate, type,
                    access_cb.SelectedValue, trans_cb.SelectedValue);

                if (((enddate > DateTime.Now ? DateTime.Now : enddate) - startdate).TotalHours < 48)
                {
                    _mtes = DownloadTransactionMetrics(type, startdate, enddate, true);
                    if (_mtes.Any())
                    {
                        tcp1.DataContext = CompressDataPoints(_mtes);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        List<MetricsTransactionsEntity> DownloadTransactionMetrics(StorageType type,DateTime startdate, DateTime enddate, bool useMinuteMetrics = false)
        {
            MetricsTransactionsDataContext trancontext = new MetricsTransactionsDataContext(
                _account.TableEndpoint.AbsoluteUri,
                _account.Credentials, useMinuteMetrics);

            IQueryable<MetricsTransactionsEntity> query = null;
            switch (type)
            {
                case StorageType.Blob:
                    query = trancontext.MetricsTransactionsBlob;
                    break;
                case StorageType.Queue:
                    query = trancontext.MetricsTransactionsQueue;
                    break;
                case StorageType.Table:
                    query = trancontext.MetricsTransactionsTable;
                    break;
                default:
                    break;
            }

            query = (from item in query
                            where item.PartitionKey.CompareTo(startdate.ToString("yyyyMMddTHHmm")) >= 0
                            && item.PartitionKey.CompareTo(enddate.ToString("yyyyMMddTHHmm")) <= 0
                            && item.RowKey == string.Format("{0};{1}", access_cb.SelectedValue, trans_cb.SelectedValue)
                        select item).AsTableServiceQuery<MetricsTransactionsEntity>();

            //var ccc= list2.Select(m => m.RowKey).Distinct();

            var list2 = query.ToList();

            return list2;
        }

        Dictionary<string, object> ConvertView(MetricsTransactionsEntity mte)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            Type mtet = typeof(MetricsTransactionsEntity);
            foreach (var prop in mtet.GetProperties())
            {
                //if(prop.Name.StartsWith("Percent"))
                    dict.Add(prop.Name, prop.GetValue(mte, null));
            }
            return dict;
        }

        StorageType _currentstoragetype;

        //load metrics
        private void metric_Click(object sender, RoutedEventArgs e)
        {
            _currentstoragetype = (StorageType)storage_cb.SelectedValue;
            InitializeMetricsView(_currentstoragetype);
        }

        // select time
        MetricsTransactionsEntity _selectedmte;
        void ls_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedmte = e.AddedItems[0] as MetricsTransactionsEntity;
            var dict = ConvertView(_selectedmte);
            ps1.ItemsSource = dict.Where(kp => kp.Key.StartsWith("Percent"));
            chart2.Title = _selectedmte.Time.ToString();

            string propsstr = "";
            foreach (var kp in dict)
            {
                propsstr += (kp.Key + ": " + kp.Value + "\n");
            }
            m1tb.Text = propsstr;
        }

        string FindPrefix(string stra, string strb)
        {
            int i = 0;
            for (i = 0; i < stra.Length; i++)
            {
                if (stra[i] != strb[i])
                    break;
            }
            string retstr = stra.Substring(0, i);
            return retstr;
        }

        /// <summary>
        /// Splits a <see cref="List{T}"/> into multiple chunks.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list">The list to be chunked.</param>
        /// <param name="chunkSize">The size of each chunk.</param>
        /// <returns>A list of chunks.</returns>
        public static List<List<T>> SplitIntoChunks<T>(List<T> list, int chunkSize)
        {
            if (chunkSize <= 0)
            {
                throw new ArgumentException("chunkSize must be greater than 0.");
            }

            List<List<T>> retVal = new List<List<T>>();
            int index = 0;
            while (index < list.Count)
            {
                int count = list.Count - index > chunkSize ? chunkSize : list.Count - index;
                retVal.Add(list.GetRange(index, count));

                index += chunkSize;
            }

            return retVal;
        }

        private string GetSizeLabel(long sizeBytes)
        {
            
            long gbyte = 1073741824;
            long kbyte = 1024;
            long mbyte = 1048576;
            long pbyte = 1125899906842624;
            long tbyte = 1099511627776;
            string suffix = "Kb";
            if (sizeBytes >gbyte)
            {
                suffix = "Gb";
                sizeBytes = sizeBytes / 1024 / 1024 / 1024;

            }else if (sizeBytes > mbyte)
            {
                suffix = "Mb";
                sizeBytes = sizeBytes / 1024 / 1024;
            }
            else if (sizeBytes > kbyte)
            {
                suffix = "Kb";
                sizeBytes = sizeBytes / 1024;
            }
            return string.Format("{0}{1}", sizeBytes, suffix);

        }

        #region Download Log
        // download log
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            log_g.IsEnabled = false;

            if (_account == null)
            {
                MessageBox.Show("Storage account haven't been initialized");
                log_g.IsEnabled = true;
                return;
            }

            CloudBlobClient client = _account.CreateCloudBlobClient();

            string startdatestr = null;
            string enddatestr = null;
            string filename = null;
            string curtypestr = _currentstoragetype.ToString().ToLower();
            DateTime startdate = DateTime.MinValue;
            DateTime enddate = DateTime.MinValue;

            // time range
            if (currb.IsChecked.HasValue && currb.IsChecked.Value)
            {
                if (_selectedmte == null)
                {
                    MessageBox.Show("no timepoint is selected.");
                    log_g.IsEnabled = true;
                    return;
                }
                startdate = _selectedmte.Time;
                enddate = startdate.Add(_selectedmte.TimeSpan);
            }
            else if (allrb.IsChecked.HasValue && allrb.IsChecked.Value)
            {
                startdate = start_dp.SelectedDate.Value.Add((TimeSpan)start_cb.SelectedItem);
                enddate = end_dp.SelectedDate.Value.Add((TimeSpan)end_cb.SelectedItem);
            }

            startdatestr = "$logs/" + curtypestr + startdate.ToString("/yyyy/MM/dd/HHmm");
            enddatestr = "$logs/" + curtypestr + enddate.ToString("/yyyy/MM/dd/HHmm");
            filename = string.Format("{0}_{1}_logging_",
                _account.Credentials.AccountName,
                curtypestr);

            // format
            if (csvrb.IsChecked.HasValue && csvrb.IsChecked.Value)
                filename += ".csv";
            else
                filename += ".log";

            // list with prefix
            var prefixstr = FindPrefix(startdatestr, enddatestr);
            var items = client.ListBlobsWithPrefix(prefixstr, new BlobRequestOptions
            {
                UseFlatBlobListing = true,
            }).ToList();

            // filter with time range
            var filteritems = items.Where(
                (item) =>
                {
                    // $logs/blob/2011/11/21/2000/000000.log
                    var uripath = item.Uri.AbsolutePath.TrimStart('/');

                    if (uripath.CompareTo(startdatestr) >= 0 &&
                        uripath.CompareTo(enddatestr) < 0)
                        return true;
                    else
                        return false;
                }).Select(i=>(CloudBlob)i)
                .ToList();

            if (filteritems.Count == 0)
            {
                MessageBox.Show("No logs in selected time");
                log_g.IsEnabled = true;
                return;
            }

            var dialog = new System.Windows.Forms.FolderBrowserDialog();

            var payLoadMb = filteritems.Sum(p => p.Properties.Length);
            StringBuilder description = new StringBuilder();
            description.AppendFormat(
                "WARNING: You are about to download {0} of logs for the {1}/{2} storage account.", GetSizeLabel(payLoadMb),
                _account.Credentials.AccountName, curtypestr);
            description.AppendLine();
            description.AppendFormat("Log data FROM {0}-{1}", startdate.ToString("dd.MMM.yyyy HH:mm"), enddate.ToString("dd.MMM.yyyy HH:mm"));
            description.AppendLine();
            description.AppendFormat("Choose a folder with at least {0}.", GetSizeLabel(payLoadMb));
            dialog.Description = description.ToString();

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BackgroundWorker bw = new BackgroundWorker();
                bw.WorkerReportsProgress = true;
                bw.DoWork += new DoWorkEventHandler(bw_DoWork);
                bw.RunWorkerAsync(new object[] { filteritems, dialog.SelectedPath + "\\" + filename });
                bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
                bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
            }
            else
            {
                log_g.IsEnabled = true;
            }
        }

        void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                p1.Value = 100;
                MessageBox.Show("Download succeed.");
            }
            else
            {
                MessageBox.Show("Download failed." + e.Error);
            }
            log_g.IsEnabled = true;
        }

        void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            p1.Value = e.ProgressPercentage;
        }

        void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            var bw = sender as BackgroundWorker;
            var objs = e.Argument as object[];
            var blobs = objs[0] as List<CloudBlob>;
            var filename = objs[1] as string;
            bool outputToCsv = filename.EndsWith(".csv");

            // max log size
            int filemaxsize = 50;
            int.TryParse(ConfigurationManager.AppSettings["logsizeinmb"], out filemaxsize);
            int maxDegreeOfParallelism = 1;
            int.TryParse(ConfigurationManager.AppSettings["maxDegreeOfParallelism"], out maxDegreeOfParallelism);
            filemaxsize *= (1024 * 1024);
            bool TrimSuccessFromLog = false;
            bool.TryParse(ConfigurationManager.AppSettings["TrimSuccessFromLog"], out TrimSuccessFromLog);

            int filecount = 1;
            long sizecount = 0;

            //header
            string header = "<version-number>,<request-start-time>,<operation-type>," +
            "<request-status>,<http-status-code>,<end-to-end-latency-in-ms>," +
            "<server-latency-in-ms>,<authentication-type>,<requestor-account-name>," +
            "<owner-account-name>,<service-type>,<request-url>,<requested-object-key>," +
            "<request-id-header>,<operation-count>,<requestor-ip-address>,<request-version-header>," +
            "<request-header-size>,<request-packet-size>,<response-header-size>,<response-packet-size>," +
            "<request-content-length>,<request-md5>,<server-md5>,<etag-identifier>,<last-modified-time>," +
            "<conditions-used>,<user-agent-header>,<referrer-header>,<client-request-id>";
            
            List<List<CloudBlob>> cluster = new List<List<CloudBlob>>();
            List<CloudBlob> ret = new List<CloudBlob>();

            for (int i = 0; i < blobs.Count; i++)
            {
                var blob = blobs[i];

                // split file
                if (sizecount + blob.Properties.Length >= filemaxsize)
                {
                    cluster.Add(ret);
                    ret = new List<CloudBlob>();
                    filecount++;
                    sizecount = 0;
                }
                ret.Add(blob);
                sizecount += blob.Properties.Length;
            }
            cluster.Add(ret);

            ParallelOptions pOptions = new ParallelOptions();
            pOptions.MaxDegreeOfParallelism = maxDegreeOfParallelism;
            int increment = 0;
            Parallel.For(0, cluster.Count(), pOptions, i =>
            {
                CloudBlob[] pBlobs = cluster[i].ToArray();
                if (pBlobs.Count() > 0)
                {
                    var first = pBlobs.First().Properties.LastModifiedUtc.ToString("yyyyMMdd-HHmmss");
                    var last = pBlobs.Last().Properties.LastModifiedUtc.ToString("yyyyMMdd-HHmmss");
                    var fname = filename.Substring(0, filename.Length - 4);
                    string outputCsvName = string.Format("{0}{1}_{2}_{3}.{4}", fname, i.ToString("0000"), first, last,
                        outputToCsv ? "csv" : "log"); 
                    
                    Stream fs = File.Open(outputCsvName, FileMode.Create);
                    StreamWriter writer = new StreamWriter(fs);
                    if (increment == 0)
                    {
                        writer.WriteLine(header);
                    }
                    for (int x = 0; x < pBlobs.Count() ; x++)
                    {
                        var blob = pBlobs[x];
                        var logstring = outputToCsv ? new StringBuilder(blob.DownloadText()).Replace(';', ',').ToString()
                                                    : blob.DownloadText();

                        if (TrimSuccessFromLog)
                        {
                            logstring = string.Join("\r\n", logstring.Split('\n').Where(l => !l.Contains(",Success,")).ToArray());
                        }
                        writer.Write(logstring);
                    }
                    writer.Close();
                    fs.Close();
                
                    Interlocked.Add(ref increment, pBlobs.Count());
                    Interlocked.Increment(ref increment);
                    bw.ReportProgress(increment * 100 / blobs.Count());
                }


            });

            bw.ReportProgress(100);

        }
        #endregion

        #region Download Metrics
        void SaveMetricsToFile(string filepath, IEnumerable<MetricsTransactionsEntity> mtes)
        {
            var stream = File.Open(filepath, FileMode.Create);
            var writer = new StreamWriter(stream);

            writer.WriteLine(
                "PartitionKey,RowKey,AnonymousAuthorizationError,AnonymousClientOtherError," +
                "AnonymousClientTimeoutError,AnonymousNetworkError,AnonymousServerOtherError,AnonymousServerTimeoutError," +
                "AnonymousSuccess,AnonymousThrottlingError,AuthorizationError,Availability," +
                "AverageE2ELatency,AverageServerLatency,ClientOtherError,ClientTimeoutError," +//16
                "NetworkError,PercentAuthorizationError,PercentClientOtherError,PercentNetworkError," +
                "PercentServerOtherError,PercentSuccess,PercentThrottlingError,PercentTimeoutError," +
                "SASAuthorizationError,SASClientOtherError,SASClientTimeoutError,SASNetworkError," +//28
                "SASServerOtherError,SASServerTimeoutError,SASSuccess,SASThrottlingError," +
                "ServerOtherError,ServerTimeoutError,Success,ThrottlingError,Time,Timestamp," +//38
                "TotalBillableRequests,TotalEgress,TotalIngress,TotalRequests");
            foreach (var mte in mtes)
            {
                writer.WriteLine("" +
                    mte.PartitionKey + "," +
                    mte.RowKey + "," +
                    mte.AnonymousAuthorizationError + "," +
                    mte.AnonymousClientOtherError + "," +
                    mte.AnonymousClientTimeoutError + "," +
                    mte.AnonymousNetworkError + "," +
                    mte.AnonymousServerOtherError + "," +
                    mte.AnonymousServerTimeoutError + "," +
                    mte.AnonymousSuccess + "," +
                    mte.AnonymousThrottlingError + "," +
                    mte.AuthorizationError + "," +
                    mte.Availability + "," +
                    mte.AverageE2ELatency + "," +
                    mte.AverageServerLatency + "," +
                    mte.ClientOtherError + "," +
                    mte.ClientTimeoutError + "," + //16
                    mte.NetworkError + "," +
                    mte.PercentAuthorizationError + "," +
                    mte.PercentClientOtherError + "," +
                    mte.PercentNetworkError + "," +
                    mte.PercentServerOtherError + "," +
                    mte.PercentSuccess + "," +
                    mte.PercentThrottlingError + "," +
                    mte.PercentTimeoutError + "," +
                    mte.SASAuthorizationError + "," +
                    mte.SASClientOtherError + "," +
                    mte.SASClientTimeoutError + "," +
                    mte.SASNetworkError + "," + //28
                    mte.SASServerOtherError + "," +
                    mte.SASServerTimeoutError + "," +
                    mte.SASSuccess + "," +
                    mte.SASThrottlingError + "," +
                    mte.ServerOtherError + "," +
                    mte.ServerTimeoutError + "," +
                    mte.Success + "," +
                    mte.ThrottlingError + "," +
                    mte.Time + "," +
                    mte.Timestamp + "," + //38
                    mte.TotalBillableRequests + "," +
                    mte.TotalEgress + "," +
                    mte.TotalIngress + "," +
                    mte.TotalRequests);
            }
            writer.Close();
            stream.Close();
        }

        // download metrics
        private void HourlyClicker(object sender, RoutedEventArgs e)
        {
            if (_account == null)
            {
                MessageBox.Show("Storage account haven't been initialized");
                return;
            }

            var startdate = start_dp.SelectedDate.Value.Add((TimeSpan)start_cb.SelectedItem);
            var enddate = end_dp.SelectedDate.Value.Add((TimeSpan)end_cb.SelectedItem);


            var hourlyMetricsFileName = string.Format("{0}_{1}_metrics_{2}_{3}_hour.csv",
                    _account.Credentials.AccountName,
                    _currentstoragetype,
                    startdate.ToString("yyyy_MM_dd_HHmm"),
                    enddate.ToString("yyyy_MM_dd_HHmm"));

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = hourlyMetricsFileName;
            var result = sfd.ShowDialog();

            if (result.HasValue && result.Value)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                var folder = new FileInfo(sfd.FileName).Directory.FullName;
                // hourly metric download
                _mtes = DownloadTransactionMetrics(_currentstoragetype, startdate, enddate);
                SaveMetricsToFile(System.IO.Path.Combine(folder, hourlyMetricsFileName), _mtes);
                sw.Stop();
                DateTime downloadEnded = DateTime.Now;
                MessageBox.Show(string.Format("Downloaded at {0} succeeded in {1}.", downloadEnded.ToString("HH:mm:ss tt zz"), sw.Elapsed));
            }
        }

        // download metrics
        private void MinuteClicker(object sender, RoutedEventArgs e)
        {
            if (_account == null)
            {
                MessageBox.Show("Storage account haven't been initialized");
                return;
            }

            var startdate = start_dp.SelectedDate.Value.Add((TimeSpan)start_cb.SelectedItem);
            var enddate = end_dp.SelectedDate.Value.Add((TimeSpan)end_cb.SelectedItem);
            
            var minuteMetricsFileName = string.Format("{0}_{1}_metrics_{2}_{3}_minutes.csv",
                    _account.Credentials.AccountName,
                    _currentstoragetype,
                    startdate.ToString("yyyy_MM_dd_HHmm"),
                    enddate.ToString("yyyy_MM_dd_HHmm"));

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = minuteMetricsFileName;
            var result = sfd.ShowDialog();
            if (result.HasValue && result.Value)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                var folder = new FileInfo(sfd.FileName).Directory.FullName;
                // hourly metric download
                _mtes = DownloadTransactionMetrics(_currentstoragetype, startdate, enddate, true);
                SaveMetricsToFile(System.IO.Path.Combine(folder, minuteMetricsFileName), _mtes);
                sw.Stop();
                DateTime downloadEnded = DateTime.Now;
                MessageBox.Show(string.Format("Downloaded at {0} succeeded in {1}.", downloadEnded.ToString("HH:mm:ss tt zz"), sw.Elapsed));
            }
        }
        #endregion

        List<MetricsTransactionsEntity> LoadMetricsFromFile(string filepath, out string error)
        {
            var list = new List<MetricsTransactionsEntity>();
            try
            {
                int errorcount = 0;
                error = null;

                var MTEstr = File.ReadAllText(filepath);
                var MTElines = MTEstr.Split('\n');

                var titleline = MTElines[0].Trim('\r');
                var titles = titleline.Split(',');

                int rowKeyIndex = -1;
                // remove " and find RowKey index
                for (int i = 0; i < titles.Length; i++)
                {
                    titles[i] = titles[i].Trim('\"');
                    if (titles[i] == "RowKey")
                        rowKeyIndex = i;
                }

                for (int i = 1; i < MTElines.Length; i++)
                {
                    try
                    {
                        var line = MTElines[i].Trim('\r');
                        if (string.IsNullOrEmpty(line))
                            continue;

                        // remove "
                        var fields = line.Split(',');
                        for (int j = 0; j < fields.Length; j++)
                        {
                            fields[j] = fields[j].Trim('\"');
                        }

                        if (titles[rowKeyIndex].Equals("RowKey") &&
                            !fields[rowKeyIndex].Equals("user;All"))
                            continue;

                        var newitem = new MetricsTransactionsEntity();
                        var type = newitem.GetType();
                        for (int j = 0; j < titles.Length; j++)
                        {
                            PropertyInfo prop = null;

                            prop = type.GetProperty(titles[j], BindingFlags.Public | BindingFlags.Instance);
                            if (prop == null || prop.Name == "Time")
                                continue;

                            var proptype = prop.PropertyType;
                            object value = fields[j];
                            if (proptype != typeof(string))
                            {
                                var methods = proptype.GetMethods(BindingFlags.Static | BindingFlags.Public);
                                var method = proptype.GetMethod("Parse",
                                    new Type[] { typeof(string) });
                                value = method.Invoke(null, new object[] { value });
                            }
                            prop.SetValue(newitem, value, null);

                        }
                        list.Add(newitem);
                    }
                    catch
                    {
                        errorcount++;
                        error = "Error occured when parsing some metric row. Affected rows:" + errorcount;
                    }
                }

                return list;
            }
            catch(Exception ex)
            {
                error = "Error occured: " + ex.Message;
                return list;
            }
        }

        List<MetricsTransactionsEntity> LoadMetricsFromFile2(string filepath, out string error)
        {
            try
            {
                error = null;
                var MTEstr = File.ReadAllText(filepath);
                var MTElines = MTEstr.Split('\n');

                var list = new List<MetricsTransactionsEntity>();
                int errorcount = 0;
                for (int i = 1; i < MTElines.Length; i++)
                {
                    try
                    {
                        var line2 = MTElines[i].Trim('\r');
                        var cols = line2.Split(',');
                        var item = new MetricsTransactionsEntity
                        {
                            PartitionKey = cols[0],
                            RowKey = cols[1],
                            AnonymousAuthorizationError = long.Parse(cols[2]),
                            AnonymousClientOtherError = long.Parse(cols[3]),
                            AnonymousClientTimeoutError = long.Parse(cols[4]),
                            AnonymousNetworkError = long.Parse(cols[5]),
                            AnonymousServerOtherError = long.Parse(cols[6]),
                            AnonymousServerTimeoutError = long.Parse(cols[7]),
                            AnonymousSuccess = long.Parse(cols[8]),
                            AnonymousThrottlingError = long.Parse(cols[9]),
                            AuthorizationError = long.Parse(cols[10]),
                            Availability = double.Parse(cols[11]),
                            AverageE2ELatency = double.Parse(cols[12]),
                            AverageServerLatency = double.Parse(cols[13]),
                            ClientOtherError = long.Parse(cols[14]),
                            ClientTimeoutError = long.Parse(cols[15]),
                            NetworkError = long.Parse(cols[16]),
                            PercentAuthorizationError = double.Parse(cols[17]),
                            PercentClientOtherError = double.Parse(cols[18]),
                            PercentNetworkError = double.Parse(cols[19]),
                            PercentServerOtherError = double.Parse(cols[20]),
                            PercentSuccess = double.Parse(cols[21]),
                            PercentThrottlingError = double.Parse(cols[22]),
                            PercentTimeoutError = double.Parse(cols[23]),
                            SASAuthorizationError = long.Parse(cols[24]),
                            SASClientOtherError = long.Parse(cols[25]),
                            SASClientTimeoutError = long.Parse(cols[26]),
                            SASNetworkError = long.Parse(cols[27]),
                            SASServerOtherError = long.Parse(cols[28]),
                            SASServerTimeoutError = long.Parse(cols[29]),
                            SASSuccess = long.Parse(cols[30]),
                            SASThrottlingError = long.Parse(cols[31]),
                            ServerOtherError = long.Parse(cols[32]),
                            ServerTimeoutError = long.Parse(cols[33]),
                            Success = long.Parse(cols[34]),
                            ThrottlingError = long.Parse(cols[35]),
                            //Time =DateTime.Parse(cols[36]),
                            Timestamp = DateTime.Parse(cols[37]),
                            TotalBillableRequests = long.Parse(cols[38]),
                            TotalEgress = long.Parse(cols[39]),
                            TotalIngress = long.Parse(cols[40]),
                            TotalRequests = long.Parse(cols[41])
                        };
                        list.Add(item);
                    }
                    catch (Exception ex)
                    {
                        errorcount++;
                        error = "Error occured when parsing some metric row. Affected rows:" + errorcount;
                    }
                }

                return list;
            }
            catch (Exception ex)
            {
                error = "Error occured when loading metrics file.";
                return null;
            }
        }

        // create account
        private void account_tb_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                switch (accountTypeCb.SelectedIndex)
                {
                    case 0://windows azure
                        _account = new CloudStorageAccount(
                        new StorageCredentialsAccountAndKey(
                            account_tb.Text,
                            key_tb.Password), true);
                        break;
                    case 1://mc
                        _account = new CloudStorageAccount(
                            new StorageCredentialsAccountAndKey(
                                account_tb.Text,
                                key_tb.Password),
                                new Uri(string.Format("https://{0}.blob.core.chinacloudapi.cn/", account_tb.Text)),
                                new Uri(string.Format("https://{0}.queue.core.chinacloudapi.cn/", account_tb.Text)),
                                new Uri(string.Format("https://{0}.table.core.chinacloudapi.cn/", account_tb.Text)));
                        break;
                }

                storagemsg.Text = "";
                // storagemsg.Foreground = new SolidColorBrush(Colors.Green);
            }
            catch (Exception ex)
            {
                storagemsg.Text = "Storage account not correct";
                storagemsg.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        // timepoint selection changed
        private void tlc1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                _selectedmte = e.AddedItems[0] as MetricsTransactionsEntity;
                var dict = ConvertView(_selectedmte);

                // disable pie chart
                //ps1.ItemsSource = dict.Where(kp => kp.Key.StartsWith("Percent")); ;
                //chart2.Title = _selectedmte.Time.ToString();

                metrics_tb.Text = string.Format("{0}({4}hour) {1} Metrics ({2};{3})",
                    _selectedmte.Time, _currentstoragetype, access_cb.SelectedValue, trans_cb.SelectedValue,_selectedmte.TimeSpan.TotalHours);

                string propsstr = "";
                foreach (var kp in dict)
                {
                    propsstr += (kp.Key + ": " + kp.Value + "\n");
                }
                m1tb.Text = propsstr;
            }
            else
            {
                metrics_tb.Text = "";
                m1tb.Text = "";
            }
        }

        // link to configuration tool
        private void configmetric_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process
                .Start("http://www.cerebrata.com/Blog/post/Cerebrata-Windows-Azure-Storage-Analytics-Configuration-Utility-A-Free-Utility-to-Configure-Windows-Azure-Storage-Analytics.aspx");
        }

        // link to doc
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process
                .Start("http://msdn.microsoft.com/en-us/library/windowsazure/hh343270.aspx");

        }

        // load metric file
        private void loadmetricfile_click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "CSV file (.csv)|*.csv";
            var result = ofd.ShowDialog();
            if (result != null && result.Value)
            {
                string error;
                tcp1.DataContext = CompressDataPoints(LoadMetricsFromFile(ofd.FileName, out error));
                tl_tb.Text = ofd.SafeFileName;
                tl_tb2.Text = error;
            }
        }

        private void enable_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process
                .Start("https://www.windowsazure.com/en-us/manage/services/storage/how-to-monitor-a-storage-account/#configurestoragemonitoring");

        }
    }


}
