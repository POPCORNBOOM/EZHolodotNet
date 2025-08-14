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
        private ImageProcesser? _imageProcesser;
        public ImageProcesser? ImageProcesser
        {
            get => _imageProcesser;
            set
            {
                if (!Equals(_imageProcesser, value))
                {
                    _imageProcesser = value;
                    DataContext = value;
                    OnPropertyChanged(nameof(_imageProcesser));
                }
            }
        }

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                ImageProcesser = new(this);
                DataContext = ImageProcesser;
            }
            catch (Exception e)
            {
                while(e.InnerException != null)
                    e = e.InnerException;
                var uiMessageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "OhSh*t！启动时发生错误，麻烦你汇报一下TT",
                    Content = e.Message,
                    CloseButtonText = "了解",
                };

                uiMessageBox.ShowDialogAsync();
            }
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            if (ImageProcesser.OriginalImage == null) return;
            var i = sender as Image;
            var p = e.GetPosition(i);
            ImageProcesser.MousePoint = new((float)(p.X * ImageProcesser.OriginalImage.Cols/i.ActualWidth), (float)(p.Y * ImageProcesser.OriginalImage.Rows / i.ActualHeight));
            ImageProcesser.ProcessManual(null, e.MiddleButton == MouseButtonState.Pressed);
        }

        private void Slider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if(ImageProcesser.IsAutoGeneratePreview)
                ImageProcesser.ProcessScratch();

        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ImageProcesser.OriginalImage == null) return;
            ImageProcesser.ProcessManual(true, e.MiddleButton == MouseButtonState.Pressed);
        }

        private void Image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (ImageProcesser.OriginalImage == null) return;
            ImageProcesser.ProcessManual(false);

        }

        private void Image_MouseLeave(object sender, MouseEventArgs e)
        {
            if (ImageProcesser.OriginalImage == null) return;
            ImageProcesser.ProcessManual(false);
            ImageProcesser.MousePoint = new (0,0);


        }
     
        private void TimeConsumingSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ImageProcesser.RefreshDisplay();

        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ImageProcesser.ReloadModel();

        }

        private void Image_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ImageProcesser.PreviewScale += e.Delta/1200f;
        }
    }
}