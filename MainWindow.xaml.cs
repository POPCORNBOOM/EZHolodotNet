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
        private readonly Action<object> _execute; // 支持参数的 Action
        private readonly Func<object, bool> _canExecute; // 支持参数的 Func

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
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
        public ImageProcesser ImageProcesser;

        public MainWindow()
        {
            InitializeComponent();
            ImageProcesser = new(this);
            DataContext = ImageProcesser;
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            var i = sender as Image;
            var p = e.GetPosition(i);
            if (ImageProcesser.OriginalImage == null) return;
            ImageProcesser.MousePoint = new(p.X * ImageProcesser.OriginalImage.Cols/i.ActualWidth, p.Y * ImageProcesser.OriginalImage.Rows / i.ActualHeight);
        }

        private void Slider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if(ImageProcesser.IsAutoGeneratePreview)
                ImageProcesser.ProcessScratch();

        }
    }
}