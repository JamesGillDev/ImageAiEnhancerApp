using System.Windows;
using System.Windows.Controls;
using ImageAiEnhancerApp.App.ViewModels;

namespace ImageAiEnhancerApp.App.Views;

public partial class SingleImageView : System.Windows.Controls.UserControl
{
    public SingleImageView()
    {
        InitializeComponent();
    }

    private void PreviewBorder_OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void PreviewBorder_OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        if (DataContext is not SingleImageViewModel viewModel)
        {
            return;
        }

        viewModel.LoadDroppedFileCommand.Execute(files[0]);
    }
}
