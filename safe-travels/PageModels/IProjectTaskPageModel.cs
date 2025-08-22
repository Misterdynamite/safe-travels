using CommunityToolkit.Mvvm.Input;
using safe_travels.Models;

namespace safe_travels.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}