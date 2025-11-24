using EZHolodotNet.Core;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Wpf.Ui;
using Wpf.Ui.Appearance;
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
    public partial class MainWindow : INotifyPropertyChanged
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
                SystemThemeWatcher.Watch(this);
                
                InitializeComponent();
                ImageProcesser = new(this);
                DataContext = ImageProcesser;
                if (Properties.Settings.Default.IsUsingLastConfigEveryTime)
                {
                    //检查软件目录下是否有 last_config.json 文件
                    if (File.Exists("last_config.json"))
                    {
                        ImageProcesser.ImportConfig("last_config.json");
                    }

                }
                ApplicationThemeManager.Apply((ApplicationTheme)ImageProcesser.Theme);
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
            ImageProcesser.MousePixelPosition = new((float)(p.X * ImageProcesser.OriginalImage.Cols/i.ActualWidth), (float)(p.Y * ImageProcesser.OriginalImage.Rows / i.ActualHeight));
            
            if (e.LeftButton == MouseButtonState.Pressed)
                ImageProcesser.ProcessManual(null);

        }

        private void Slider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if(ImageProcesser.IsAutoGeneratePreview)
                ImageProcesser.ProcessScratch();

        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ImageProcesser.OriginalImage == null) return;
            if (e.LeftButton == MouseButtonState.Pressed)
                ImageProcesser.ProcessManual(true);
            else if (e.MiddleButton == MouseButtonState.Pressed)
                ImageProcesser.ProcessMovingView();
            else if (e.RightButton == MouseButtonState.Pressed)
            {
                if (Keyboard.IsKeyDown(Key.LeftShift))
                    ImageProcesser.EraserLower = ImageProcesser.MouseDepth;
                else
                    ImageProcesser.EraserUpper = ImageProcesser.MouseDepth;

            }


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
            ImageProcesser.MousePixelPosition = new (0,0);


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
            if (Keyboard.IsKeyDown(Key.LeftCtrl))
                ImageProcesser.PreviewScaleFactor += e.Delta/1200f;
            else
                ImageProcesser.ChangeRadius(e.Delta/12f);

        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            //Trace.WriteLine($"window moving{DateTime.Now},{e.GetPosition(App.Current.MainWindow)}");
            ImageProcesser.MouseWindowPosition = new(e.GetPosition(App.Current.MainWindow));
            if (e.MiddleButton == MouseButtonState.Pressed)
                ImageProcesser.ProcessMovingView(null);

        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Released)
                ImageProcesser.ProcessMovingView(false);
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            ImageProcesser.ProcessMovingView(false);

        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if(ImageProcesser!=null)
            {
                ImageProcesser.IndicatorX = (float)e.NewSize.Width / 2;
                ImageProcesser.IndicatorY = (float)e.NewSize.Height / 2;
            }
        }

        private void Viewbox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
                ImageProcesser.ProcessMovingView();

        }

        private void BorderOriginalImage_DragEnter(object sender, DragEventArgs e)
        {
            // 检查是否是文件拖拽
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (IsImageFile(files[0]))
                {
                    e.Effects = DragDropEffects.Copy;
                    return;
                }
            }
            e.Effects = DragDropEffects.None;
        }

        private void BorderOriginalImage_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && IsImageFile(files[0]))
                {
                    ImageProcesser.LoadImage(files[0]);
                }
            }
            ImageProcesser.IsDragEntered = false;

        }
        private bool IsImageFile(string filePath)
        {
            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".webp"};
            string ext = System.IO.Path.GetExtension(filePath).ToLower();
            return imageExtensions.Contains(ext);
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            ImageProcesser.IsDragEntered = true;
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            ImageProcesser.IsDragEntered = false;

        }

        private void BorderDepthImage_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && IsImageFile(files[0]))
                {
                    ImageProcesser.LoadDepth(files[0]);
                }
            }
            ImageProcesser.IsDragEntered = false;
        }

        private void BorderPointMap_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    ImageProcesser.ImportPoints(files[0]);
                }
            }
            ImageProcesser.IsDragEntered = false;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            ImageProcesser.HandleExit();

        }

        private void Viewbox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl))
                ImageProcesser.PreviewScaleFactor += e.Delta / 1200f;
        }
    }
}