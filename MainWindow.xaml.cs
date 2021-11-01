using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using CsvHelper;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading;

namespace PriceUndercutter
{

    public enum OrderType
    {
        Buy, Sell
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string DefaultFileFilter = "*.txt";
        
        FileSystemWatcher FolderWatcher;

        static readonly object locker = new object();
        DateTime lastRead = DateTime.MinValue;

        
        private OrderType CurrentOrderType = OrderType.Sell;
        string PathToMarketLogs = "";
        string NewestMarketLogPath = "";
        double TopPrice = 0;


        public void UpdateOrderType()
        {
            if ((bool)sell.IsChecked)
                CurrentOrderType = OrderType.Sell;
            if ((bool)buy.IsChecked)
                CurrentOrderType = OrderType.Buy;
        }

        static string DefaultPathToMarketLogs()
        {
            var user_profile_path = Environment.GetEnvironmentVariable("USERPROFILE");
            var pathToMarketLogs = "";
            if (user_profile_path != null)
                pathToMarketLogs = System.IO.Path.Combine(user_profile_path.ToString(), "Documents", "EVE", "logs", "MarketLogs");
            return pathToMarketLogs;
        }

        private void PrintException(Exception ex)
        {
            if (ex != null)
            {
                SafeUpdateLabelText($"Message: {ex.Message}; Stacktrace: {ex.StackTrace}",  ErrorLabel);
            }
        }

        private FileSystemWatcher StartFolderWatcher(string directoryPathToWatch, string filter_ = DefaultFileFilter)
        {
            var watcher = new FileSystemWatcher(directoryPathToWatch);
            watcher.NotifyFilter = NotifyFilters.FileName;

            watcher.Created += new FileSystemEventHandler(OnCreated);
            watcher.Error += OnError;
            watcher.Deleted += new FileSystemEventHandler(OnCreated);

            watcher.Filter = filter_;
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;

            return watcher;
        }

        string NewestFilePathInDirectory(string directoryPath, string pattern = DefaultFileFilter)
        {
            var directoryInfo = new System.IO.DirectoryInfo(directoryPath);
            var file = directoryInfo.GetFiles(pattern)
                         .OrderByDescending(f => f.LastWriteTime);
                         
            
            return (file.Count() > 0) ? file.First().FullName : "";
        }

        double TopPriceFromCsv(string filePath)
        {
            using (var reader = new System.IO.StreamReader(filePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csv.GetRecords<MarketData>();
                return SearchForTopPrice(records);
            }
        }

        double SearchForTopPrice(IEnumerable<MarketData> marketData)
        {
            bool buy = CurrentOrderType == OrderType.Buy;
            var filteredOrders = marketData.Where(order => order.Bid == buy);
            double newPrice = 0;
            if (buy)
                newPrice = filteredOrders.Max(order => order.Price);
            else
                newPrice = filteredOrders.Min(order => order.Price);

            return newPrice;
        }

        public void Reprocess()
        {
            SafeUpdateLabelText("", ErrorLabel);
            SafeUpdateLabelText("Reprocessing starting...", StatusLabel);

            NewestMarketLogPath = NewestFilePathInDirectory(PathToMarketLogs);

            // validate log path
            if (string.IsNullOrEmpty(NewestMarketLogPath))
            {
                PrintException(new Exception("There is no market log to be processed, check if you selected correct directory."));
                SafeUpdateLabelText("Error", StatusLabel);

            }
            else
            {
                SafeUpdateLabelText("Processing market data...", StatusLabel);
                TopPrice = TopPriceFromCsv(NewestMarketLogPath);
                ModifyClipBoardFromSTAThread(TopPrice.ToString());
                SafeUpdateLabelText("Top price pasted to clippboard!", StatusLabel);
            }

            UpdateUI();
        }

        void ModifyClipBoardFromSTAThread(string text)
        {
            Thread t = new Thread((ThreadStart)(() => {
                System.Windows.Clipboard.SetData(System.Windows.DataFormats.Text, text);
            }));

            // Run your code from a thread that joins the STA Thread
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
        }

        void UpdateUI()
        {
            SafeUpdateLabelText(PathToMarketLogs.ToString(), CurrentFolderLabel);
            
            string currentFileLabelContent = (!string.IsNullOrEmpty(NewestMarketLogPath)) ?
                new FileInfo(NewestMarketLogPath).Name.ToString() : " Invalid directory selected ";
            SafeUpdateLabelText(currentFileLabelContent, CurrentFileLabel);
            
            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate ()
            {
                TopPriceTextArea.Text = $"{TopPrice:N}";
            }));
            
        }

        void SafeUpdateLabelText(string text, System.Windows.Controls.Label labelToUpdate)
        {
            // update status text on event thread if necessary
            Dispatcher.BeginInvoke(DispatcherPriority.Background, (SendOrPostCallback)delegate
              {
                  labelToUpdate.Content = text;
              }, null);
        }

        public MainWindow()
        {
            InitializeComponent();
            VersionLabel.Content = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            UpdateOrderType();

            // Default folder = C:\Users\%USERNAME%\Documents\EVE\logs\Marketlogs
            PathToMarketLogs = DefaultPathToMarketLogs();

            FolderWatcher = StartFolderWatcher(PathToMarketLogs);

            StatusLabel.Content = "-";
            Reprocess();

            // TODO: Bonus objectives:
            // allow modification of MarketLogs folder location
            // add some graphical shit
            // Show item image you are getting top price for (from name of the log file using EVE api)
        }

        protected void OnCreated(object sender, System.IO.FileSystemEventArgs e)
        {
            try
            {
                while (IsFileLocked(e.FullPath))
                {
                    Thread.Sleep(100);
                }
                lock (locker)
                {
                    Reprocess();

                    DateTime lastWriteTime = File.GetLastWriteTime(e.FullPath);
                    if (lastWriteTime != lastRead)
                    {
                        lastRead = lastWriteTime;
                    }
                }
            }
            catch (FileNotFoundException)
            {
                SafeUpdateLabelText("File not found ", ErrorLabel);
            }
            catch (Exception ex)
            {
                SafeUpdateLabelText("File: \"" + e.FullPath + "\"\n\r ERROR processing file (" + ex.Message + ")", ErrorLabel);
            }
        }

        private static bool IsFileLocked(string file)
        {
            FileStream stream = null;

            try
            {
                stream = new FileInfo(file).Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (IOException)
            {
                //the file is unavailable because it is:  
                //still being written to  
                //or being processed by another thread  
                //or does not exist (has already been processed)  
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked  
            return false;
        }

        private void OnError(object sender, System.IO.ErrorEventArgs e) => PrintException(e.GetException());

        private void sell_Click(object sender, RoutedEventArgs e)
        {
            UpdateOrderType();
            Reprocess();
        }

        private void buy_Click(object sender, RoutedEventArgs e)
        {
            UpdateOrderType();
            Reprocess();
        }

        private void SelectFolderPath_Click(object sender, RoutedEventArgs e)
        {
            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog dialog = new();
            if (dialog.ShowDialog(this).GetValueOrDefault())
            {
                PathToMarketLogs = dialog.SelectedPath;
                FolderWatcher.Path = PathToMarketLogs;
                Reprocess();
            }
        }

        private void ForceReprocessButton_Click(object sender, RoutedEventArgs e)
        {
            Reprocess();
        }

        private void DefaultFolderPathButton_Click(object sender, RoutedEventArgs e)
        {
            PathToMarketLogs = DefaultPathToMarketLogs();
            Reprocess();
        }
    }
}
