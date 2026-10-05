using System.Windows;
namespace GameTranslator.App;

public partial class MainWindow : Window
{
    public MainWindow() { InitializeComponent(); DataContext = new MainViewModel(); }
}
