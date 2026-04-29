using System.Windows;
using BlueBirdsERP.Desktop.ViewModels;

namespace BlueBirdsERP.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
