using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

namespace TreeView
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(GName.Text))
                return;
            var selected = TreeViewer.SelectedItem as TreeViewItem;
            TreeViewItem child = new TreeViewItem();
            child.Header = GName.Text;
            if (selected != null && selected.IsSelected)
            {
                selected.Items.Add(child);
                selected.IsExpanded = true;
                selected.IsSelected = false;
            }
            else
            {
                TreeViewer.Items.Add(child);
            }
            GName.Text = "";
        }

        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (TreeViewer.SelectedItem == null)
                return;

            if (TreeViewer.SelectedItem is TreeViewItem selectedItem)
            {
                var parentItem = FindParent(selectedItem);

                if (parentItem == null)
                {
                    TreeViewer.Items.Remove(selectedItem);
                }
                else
                {
                    parentItem.Items.Remove(selectedItem);
                }
            }
        }

        private TreeViewItem FindParent(TreeViewItem item)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(item);
            while (parent != null && !(parent is TreeViewItem))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as TreeViewItem;
        }

    }
}
