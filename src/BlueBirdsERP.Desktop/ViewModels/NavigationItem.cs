using BlueBirdsERP.Domain.Enums;
using CommunityToolkit.Mvvm.Input;

namespace BlueBirdsERP.Desktop.ViewModels;

public class NavigationItem
{
    public string Label { get; }
    public string IconPathData { get; }
    public string PageKey { get; }
    public RbacPermission RequiredPermission { get; }
    public IRelayCommand NavigateCommand { get; }

    public NavigationItem(string label, string iconPathData, string pageKey, RbacPermission requiredPermission, Action navigateAction)
    {
        Label = label;
        IconPathData = iconPathData;
        PageKey = pageKey;
        RequiredPermission = requiredPermission;
        NavigateCommand = new RelayCommand(navigateAction);
    }
}
