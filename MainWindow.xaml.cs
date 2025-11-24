using EZHolodotNet.Core;
using System;
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
        private CoreProcesserViewModel _coreProcesser;
        public CoreProcesserViewModel CoreProcesser
        {
            get => _coreProcesser;
            set
            {
                if (!Equals(_coreProcesser, value))
                {
                    _coreProcesser = value;
                    DataContext = value;
                    OnPropertyChanged(nameof(_coreProcesser));
                }
            }
        }

        public MainWindow()
        {
            try
            {
                SystemThemeWatcher.Watch(this);
                
                InitializeComponent();
                CoreProcesser = new(this);
                DataContext = CoreProcesser;
                if (Properties.Settings.Default.IsUsingLastConfigEveryTime)
                {
                    //检查软件目录下是否有 last_config.json 文件
                    if (File.Exists("last_config.json"))
                    {
                        CoreProcesser.ImportConfig("last_config.json");
                    }

                }
                ApplicationThemeManager.Apply((ApplicationTheme)CoreProcesser.Theme);
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
            if (CoreProcesser != null)
            {
                if (!CoreProcesser.IsOriginalImageLoaded) return;
                var i = sender as Image;
                var p = e.GetPosition(i);
                CoreProcesser.MousePixelPosition = new((float)(p.X * CoreProcesser.OriginalImage.Cols / i.ActualWidth), (float)(p.Y * CoreProcesser.OriginalImage.Rows / i.ActualHeight));

                if (e.LeftButton == MouseButtonState.Pressed)
                    CoreProcesser.ProcessManual(null);
            }
        }

        private void Slider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (CoreProcesser != null)
            {
                if (CoreProcesser.IsAutoGeneratePreview)
                    CoreProcesser.ProcessScratch();
            }
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (CoreProcesser != null)
            {
                if (!CoreProcesser.IsOriginalImageLoaded) return;
                if (e.LeftButton == MouseButtonState.Pressed)
                    CoreProcesser.ProcessManual(true);
                else if (e.MiddleButton == MouseButtonState.Pressed)
                    CoreProcesser.ProcessMovingView();
                else if (e.RightButton == MouseButtonState.Pressed)
                {
                    if (Keyboard.IsKeyDown(Key.LeftShift))
                        CoreProcesser.EraserLower = CoreProcesser.MouseDepth;
                    else
                        CoreProcesser.EraserUpper = CoreProcesser.MouseDepth;

                }
            }

        }

        private void Image_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (CoreProcesser != null)
            {
                if (!CoreProcesser.IsOriginalImageLoaded) return;
                CoreProcesser.ProcessManual(false);
            }
        }

        private void Image_MouseLeave(object sender, MouseEventArgs e)
        {
            if (CoreProcesser != null)
            {
                if (!CoreProcesser.IsOriginalImageLoaded) return;
                CoreProcesser.ProcessManual(false);
                CoreProcesser.MousePixelPosition = new(0, 0);
            }
        }
     
        private void TimeConsumingSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            CoreProcesser?.RefreshDisplay();

        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CoreProcesser?.ReloadModel();

        }

        private void Image_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (CoreProcesser != null)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl))
                    CoreProcesser.PreviewScaleFactor += e.Delta / 1200f;
                else
                    CoreProcesser.ChangeRadius(e.Delta / 12f);
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            //Trace.WriteLine($"window moving{DateTime.Now},{e.GetPosition(App.Current.MainWindow)}");
            if (CoreProcesser != null)
            {
                CoreProcesser.MouseWindowPosition = new(e.GetPosition(App.Current.MainWindow));
                if (e.MiddleButton == MouseButtonState.Pressed)
                    CoreProcesser.ProcessMovingView(null);
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Released)
                CoreProcesser?.ProcessMovingView(false);
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            CoreProcesser?.ProcessMovingView(false);

        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if(CoreProcesser!=null)
            {
                CoreProcesser.IndicatorX = (float)e.NewSize.Width / 2;
                CoreProcesser.IndicatorY = (float)e.NewSize.Height / 2;
            }
        }

        private void Viewbox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
                CoreProcesser?.ProcessMovingView();

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
                    //ImageProcesser.LoadImage(files[0]);
                    CoreProcesser.LoadImageAsync(files[0]);
                }
            }
            CoreProcesser.IsDragEntered = false;

        }
        private bool IsImageFile(string filePath)
        {
            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".webp"};
            string ext = System.IO.Path.GetExtension(filePath).ToLower();
            return imageExtensions.Contains(ext);
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            CoreProcesser.IsDragEntered = true;
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            CoreProcesser.IsDragEntered = false;

        }

        private void BorderDepthImage_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && IsImageFile(files[0]))
                {
                    CoreProcesser.LoadDepth(files[0]);
                }
            }
            CoreProcesser.IsDragEntered = false;
        }

        private void BorderPointMap_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    CoreProcesser.ImportPoints(files[0]);
                }
            }
            CoreProcesser.IsDragEntered = false;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            CoreProcesser.HandleExit();

        }

        private void Viewbox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl))
                CoreProcesser.PreviewScaleFactor += e.Delta / 1200f;
        }
    }
}