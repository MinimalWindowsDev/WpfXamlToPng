using System.Windows;

namespace ComplexWpfApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Code behind logic that should be ignored by renderer
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Clicked!");
        }
    }
}

namespace ComplexWpfApp.ViewModels 
{
    // Dummy namespace to satisfy xmlns:viewmodels parsing in real app, 
    // though the renderer should strip it.
    public class MainViewModel {}
}
