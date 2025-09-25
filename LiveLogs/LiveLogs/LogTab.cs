using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LiveLogs
{
    public class LogTab : INotifyPropertyChanged
    {
        public string FilePath { get; set; }
        public FileSystemWatcher Watcher { get; set; }
        public long LastPosition { get; set; }
        public ListBox LogTextBox { get; set; }
        public DispatcherTimer UpdateTimer { get; set; }
        public ObservableCollection<string> LogLines { get; } = new ObservableCollection<string>(); 

        public void AppendLog(string newContent)
        {
            var newLines = newContent.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Limit total lines to prevent UI lag
            int excessLines = (LogLines.Count + newLines.Length) - 5000;
            while (LogLines.Count > 0 && excessLines > 0)
            {
                LogLines.RemoveAt(0);
                excessLines--;
            }
            foreach (var line in newLines)
            {
                LogLines.Add(line);
            }
            if (LogTextBox.Items.Count > 0)
            {
                LogTextBox.ScrollIntoView(LogTextBox.Items[LogTextBox.Items.Count - 1]);
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
