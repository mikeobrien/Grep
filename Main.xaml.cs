using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace Grep
{
    public partial class Main : Window
    {
        private class SearchOptions { 
            public string SearchPath; 
            public string SearchText; 
            public Action<GrepResult,bool> ResultDelegate; 
            public Action<bool> CompleteDelegate;
            public bool Join;
            public bool Regex;
            public bool IgnoreCase;
        }

        private List<GrepResult> _joinedResults;
        
        private const string RegistryKey = @"HKEY_CURRENT_USER\SOFTWARE\Grep";

        public Main()
        {
            InitializeComponent();

        }

        private bool startup = true;

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Filter.Text = (string)Registry.GetValue(RegistryKey, "Filter", "");
            SearchString.Text = (string)Registry.GetValue(RegistryKey, "SearchString", "");
            SearchPath.Text = (string)Registry.GetValue(RegistryKey, "SearchPath", "");
            RegexSearch.IsChecked = bool.Parse((string)Registry.GetValue(RegistryKey, "RegexSearch", "false") ?? "false");
            IgnoreCase.IsChecked = bool.Parse((string)Registry.GetValue(RegistryKey, "IgnoreCase", "false") ?? "true");
            startup = false;
        }

        public void SaveSettings()
        {
            if (startup) return;
            Registry.SetValue(RegistryKey, "Filter", Filter.Text);
            Registry.SetValue(RegistryKey, "SearchString", SearchString.Text);
            Registry.SetValue(RegistryKey, "SearchPath", SearchPath.Text);
            Registry.SetValue(RegistryKey, "RegexSearch", RegexSearch.IsChecked ?? false);
            Registry.SetValue(RegistryKey, "IgnoreCase", IgnoreCase.IsChecked ?? false);
        }

        private void SearchString_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveSettings();
        }

        private void SearchPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveSettings();
        }

        private void Filter_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveSettings();
            try
            {
                Filter.Text.Split(' ').Select(x => new Regex(x)).ToList();
                Filter.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            }
            catch
            {
                Filter.Background = new SolidColorBrush(Color.FromRgb(255, 200, 200));
            }
        }

        private void RegexSearch_Checked(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void RegexSearch_OnUnchecked(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void IgnoreCase_OnChecked(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void IgnoreCase_OnUnchecked(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            StartSearch(false);
        }

        private void StartSearch(bool join)
        {
            Microsoft.Win32.Registry.CurrentUser.SetValue("lastGrepSearch", SearchString.Text);
            Search.IsEnabled = false;
            Status.Content = "Searching |";
            Intersect.IsEnabled = false;

            if (!join)
                Results.Items.Clear();
            else
                _joinedResults = new List<GrepResult>();

            ThreadPool.QueueUserWorkItem(
                Grep,
                new SearchOptions()
                {
                    SearchPath = SearchPath.Text,
                    SearchText = SearchString.Text,
                    ResultDelegate = (r, j) => Dispatcher.Invoke(new Action<GrepResult,bool>(AddItem), r, j),
                    CompleteDelegate = (j) => Dispatcher.Invoke(new Action<bool>(UpdateResults), j),
                    Join = join,
                    Regex = RegexSearch.IsChecked ?? false,
                    IgnoreCase = IgnoreCase.IsChecked ?? false
                });
        }

        private void AddItem(GrepResult result, bool join)
        {
            if (string.IsNullOrEmpty(result.Path) || !IncludeResult(result)) return;
            
            if (!join)
                Results.Items.Add(result);
            else
                _joinedResults.Add(result);

            string lastChar = Status.Content.ToString().Substring(Status.Content.ToString().Length - 1);
            switch (lastChar)
            {
                case "|": Status.Content = "Searching /"; break;
                case "/": Status.Content = "Searching -"; break;
                case "-": Status.Content = "Searching \\"; break;
                case "\\": Status.Content = "Searching |"; break;
            }
        }

        private bool IncludeResult(GrepResult result)
        {
            if (Filter.Text.Trim() == "") return true;
            var regex = Filter.Text.Split(' ').Select(x => new Regex(x)).ToList();
            return regex.All(x => !x.IsMatch(result.Path));
        }

        private void UpdateResults(bool join)
        {
            if (join)
            {
                List<GrepResult> itemsToRemove = new List<GrepResult>();
                foreach (GrepResult item in Results.Items)
                    if (!_joinedResults.Any(i => i.FullPath == item.FullPath))
                        itemsToRemove.Add(item);
                foreach (GrepResult item in itemsToRemove)
                    Results.Items.Remove(item);
                _joinedResults.Clear();
            }

            Status.Content = $"Found {Results.Items.Count} result(s)";
            ResizeColumns(Results);
            Search.IsEnabled = true;
            Intersect.IsEnabled = true;
       }

        private void Grep(object searchOptions)
        {
            string x86Path = @"C:\Program Files\GnuWin32\bin\grep.exe";
            string x64Path = @"C:\Program Files (x86)\GnuWin32\bin\grep.exe";
            string path;

            if (File.Exists(x86Path))
                path = x86Path;
            else if (File.Exists(x64Path))
                path = x64Path;
            else
            {
                MessageBox.Show("Could not find Grep. Please install Grep ans then try again.");
                return;
            }

            SearchOptions options = (SearchOptions)searchOptions;
            ProcessStartInfo processInfo =
                new System.Diagnostics.ProcessStartInfo(path);

            processInfo.Arguments =
                $"-E -n -r {(options.IgnoreCase ? "-i" : "")} --exclude-dir='.git' --exclude-dir=bin --exclude-dir=obj " +
                "--exclude-dir=node_modules --exclude-dir=packages --exclude-dir='.vs' " +
                $"\"{(options.Regex ? options.SearchText : Regex.Escape(options.SearchText))}\" \"{options.SearchPath}\"";
            //
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;

            System.Diagnostics.Process process = System.Diagnostics.Process.Start(processInfo);

            System.IO.TextReader consoleOutput = process.StandardOutput;
            
            string result = string.Empty;
            
            while (result != null)
            {
                result = consoleOutput.ReadLine();
                if (!string.IsNullOrEmpty(result) &&
                    !result.Contains(@"\.svn\") && 
                    !result.Contains(@"/.svn/"))
                    options.ResultDelegate(new GrepResult(options.SearchPath, result), options.Join);
            }

            options.CompleteDelegate(options.Join);
        }

        private void SearchPathLabel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog folderBrowser = new System.Windows.Forms.FolderBrowserDialog();
            folderBrowser.ShowDialog();
            SearchPath.Text = folderBrowser.SelectedPath;
            Microsoft.Win32.Registry.CurrentUser.SetValue("lastGrepSearchPath", SearchPath.Text);
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }

        private void SaveResults_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = string.Format("{0}_Results", SearchString.Text); // Default file name
            dlg.DefaultExt = ".csv"; // Default file extension
            dlg.Filter = "Comma Seperated File (.csv)|*.csv"; // Filter files by extension

            // Show save file dialog box
            bool? result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                // Save document
                ExportResults(dlg.FileName);
            }
        }

        private void ExportResults(string path)
        {
            using (TextWriter writer = new StreamWriter(path))
            {
                foreach (GrepResult result in Results.Items)
                {
                    writer.WriteLine(result.ToCsv());
                }
            }
        }

        private void SearchString_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter) StartSearch(false);
        }

        private void RemoveItems_Click(object sender, RoutedEventArgs e)
        {
            List<GrepResult> matches = new List<GrepResult>();
            foreach (GrepResult result in Results.Items)
            {
                if ((((ComboBoxItem)RemoveType.SelectedValue).Name == "Contains" && RemovePattern.Text != "" && result.Path.ToLower().Contains(RemovePattern.Text.ToLower())) ||
                    ((ComboBoxItem)RemoveType.SelectedValue).Name == "StartsWith" && RemovePattern.Text != "" && result.Path.Trim().ToLower().StartsWith(RemovePattern.Text.ToLower()) ||
                    ((ComboBoxItem)RemoveType.SelectedValue).Name == "EndsWith" && RemovePattern.Text != "" && result.Path.Trim().ToLower().EndsWith(RemovePattern.Text.ToLower()) ||
                    !IncludeResult(result))
                    matches.Add(result);
            }
            foreach (GrepResult result in matches)
                Results.Items.Remove(result);

            UpdateResults(false);
        }

        private void Results_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //if (Results.SelectedItem != null)
            //    RemovePattern.Text = ((GrepResult)Results.SelectedItem).Text;
        }

        private void Results_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete && Results.SelectedItems.Count > 0)
            {
                List<object> items = new List<object>(Results.SelectedItems.Cast<object>());
                foreach (object item in items) Results.Items.Remove(item);
            }    
        }

        private void Results_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (Results.SelectedItem != null)
                    System.Diagnostics.Process.Start(((GrepResult)Results.SelectedItem).FullPath);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message);
            }
        }

        private void ResizeColumns(ListView listView)
        {
            foreach (GridViewColumn col in ((GridView)listView.View).Columns)
            {
                double maxSizeForColumn = 0;
                foreach (object bindingObject in listView.Items)
                {
                    FrameworkElement displayer = null;
                    if (col.DisplayMemberBinding != null)
                    {
                        displayer = new TextBlock();
                        displayer.DataContext = bindingObject;
                        displayer.SetBinding(TextBlock.TextProperty, col.DisplayMemberBinding);
                    }
                    else
                    {
                        ContentPresenter presenter = new ContentPresenter();
                        presenter.Content = bindingObject;
                        presenter.ContentTemplate = col.CellTemplate;
                        displayer = presenter;
                    }
                    displayer.Measure(new Size(1000, 1000));
                    if (displayer.DesiredSize.Width > maxSizeForColumn)
                    {
                        maxSizeForColumn = displayer.DesiredSize.Width;
                    }
                }
                col.Width = maxSizeForColumn;
            }
        }

        private void Intersect_Click(object sender, RoutedEventArgs e)
        {
            StartSearch(true);
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            StringWriter writer = new StringWriter();
            foreach (GrepResult result in Results.Items)
            {
                writer.WriteLine(result.ToTsv());
            }
            Clipboard.Clear();
            Clipboard.SetText(writer.ToString());
        }
    }
}
