using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BlueBirdsERP.Desktop;

public class ViewLocator : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item == null) return null;

        var viewModelType = item.GetType();
        var viewTypeName = viewModelType.FullName!
            .Replace("ViewModels.", "Views.")
            .Replace("ViewModel", "View");

        var viewType = viewModelType.Assembly.GetType(viewTypeName);
        if (viewType == null) return null;

        var template = new DataTemplate
        {
            DataType = viewModelType,
            VisualTree = new FrameworkElementFactory(viewType)
        };

        return template;
    }
}
