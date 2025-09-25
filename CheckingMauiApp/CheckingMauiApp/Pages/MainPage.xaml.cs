using CheckingMauiApp.Models;
using CheckingMauiApp.PageModels;

namespace CheckingMauiApp.Pages;

public partial class MainPage : ContentPage
{
	public MainPage(MainPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}