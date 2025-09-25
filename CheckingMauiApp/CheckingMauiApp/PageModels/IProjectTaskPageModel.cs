using CommunityToolkit.Mvvm.Input;
using CheckingMauiApp.Models;

namespace CheckingMauiApp.PageModels;

public interface IProjectTaskPageModel
{
	IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
	bool IsBusy { get; }
}