using EZHolodotNet.Core;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Wpf.Ui.Controls;
using Image = System.Windows.Controls.Image;

namespace EZHolodotNet
{
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window,INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public ImageProcesser ImageProcesser = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = ImageProcesser;
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            var i = sender as Image;
            var p = e.GetPosition(i);
            ImageProcesser.MousePoint = new(p.X * ImageProcesser.OriginalImage.Cols/i.ActualWidth, p.Y * ImageProcesser.OriginalImage.Rows / i.ActualHeight);
        }

        private void Slider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ImageProcesser.ProcessScratch();
        }
    }
}