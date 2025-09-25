using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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

namespace TryingAPIs
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
        private async void GetFactButton_Click(object sender, RoutedEventArgs e)
        {
            string Name = NameBox.Text;
            string fact = await FetchCatFactAsync(Name);

            if (fact != null)
            {
                FactTextBlock.Text = fact == "male" ? "Whoo! Men 😎" : "Oops! Women☕︎";
                return;
            }
            FactTextBlock.Text = "Failed to fetch! Try again";
        }

        private async Task<string> FetchCatFactAsync(string name="")
        {
            try
            {
               //string apiUrl = "https://catfact.ninja/fact";
                string apiUrl = $"https://api.genderize.io/?name={name}";
                HttpClient client = new HttpClient();
                HttpResponseMessage response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var catFact = JsonConvert.DeserializeObject<CatFact>(json);
                    return catFact?.gender;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "API Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return null;
        }
    }

    public class CatFact
    {
        public string gender { get; set; }
    }
}

