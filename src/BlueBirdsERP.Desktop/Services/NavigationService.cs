using CommunityToolkit.Mvvm.ComponentModel;

namespace BlueBirdsERP.Desktop.Services;

public interface INavigationService
{
    event Action? NavigationChanged;
    ObservableObject? CurrentView { get; }
    void NavigateTo(ObservableObject viewModel);
}

public class NavigationService : INavigationService
{
    public event Action? NavigationChanged;
    public ObservableObject? CurrentView { get; private set; }

    public void NavigateTo(ObservableObject viewModel)
    {
        CurrentView = viewModel;
        NavigationChanged?.Invoke();
    }
}
