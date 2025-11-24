using EZHolodotNet.Views;
using Microsoft.ML.Trainers;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using Wpf.Ui.Interop.WinDef;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
//using Point = OpenCvSharp.Point;

namespace EZHolodotNet.Core
{
    public struct Point(float x, float y)
    {
        public int X
        {
            get => (int)Xf;
            set => Xf = value;
        }

        public int Y
        {
            get => (int)Yf;
            set => Yf = value;
        }

        public float Xf = x;

        public float Yf = y;

        public Point(OpenCvSharp.Point p)
            : this(p.X, p.Y)
        {

        }
        public Point(System.Windows.Point p)
            : this((float)p.X, (float)p.Y)
        {

        }
        public static Point operator *(Point pt, double scale) => new((float)(pt.Xf * scale), (float)(pt.Yf * scale));
        public static Vec2f operator -(Point pt1, Point pt2) => new Vec2f(pt1.Xf - pt2.Xf, pt1.Yf - pt2.Yf);
        public static Point operator +(Point pt, Vec2f vec) => new Point(pt.Xf - vec.Item0, pt.Yf - vec.Item1);
        public static Point Lerp(Point pt1, Point pt2, float t = 0.5f) => new Point(pt1.Xf + (pt2.Xf - pt1.Xf) * t, pt1.Yf + (pt2.Yf - pt1.Yf) * t);


    }
    public class MissionWithProgress : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            // Always raise on UI thread to be safe for bindings
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
            }
            else
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public MissionWithProgress(string name)
        {
            _title = name;
            _detail = name;
            // Ensure Progress.Report callbacks marshal updates to UI thread
            Progress = new Progress<double>(value =>
            {
                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(() => ProgressValue = value);
                }
                else
                {
                    ProgressValue = value;
                }
            });
        }

        public IProgress<double> Progress { get; }

        private double _progressValue = 0;
        public double ProgressValue
        {
            get => _progressValue;
            set
            {
                if (!Equals(_progressValue, value))
                {
                    if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _progressValue = value;
                            OnPropertyChanged(nameof(ProgressValue));
                        });
                    }
                    else
                    {
                        _progressValue = value;
                        OnPropertyChanged(nameof(ProgressValue));
                    }
                }
            }
        }

        private string _detail;
        public string Detail
        {
            get => _detail;
            set
            {
                if (!Equals(_detail, value))
                {
                    if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _detail = value;
                            OnPropertyChanged(nameof(Detail));
                        });
                    }
                    else
                    {
                        _detail = value;
                        OnPropertyChanged(nameof(Detail));
                    }
                }
            }
        }

        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                if (!Equals(_title, value))
                {
                    if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _title = value;
                            OnPropertyChanged(nameof(Title));
                        });
                    }
                    else
                    {
                        _title = value;
                        OnPropertyChanged(nameof(Title));
                    }
                }
            }
        }
    }
    public class PointDistance
    {
        public Point Point { get; set; }
        public float Distance { get; set; }

        public PointDistance(Point point, float distance)
        {
            Point = point;
            Distance = distance;
        }
    }
    public class CoreProcesserViewModel : INotifyPropertyChanged
    {
        public CoreProcesserViewModel()
        {

        }
        public async void ShowTipsTemporarilyAsync(string info, int time = 3000)
        {

            Application.Current.Dispatcher.Invoke(() => TipsString = info);
            Application.Current.Dispatcher.Invoke(() => IsTipsPanelVisible = true);
            await Task.Delay(time);
            Application.Current.Dispatcher.Invoke(() => IsTipsPanelVisible = false);
        }
        public async Task<TimeSpan> ExecuteWithMinimumDuration(Func<Task> taskFunc, TimeSpan minimumDuration)
        {
            var stopwatch = Stopwatch.StartNew();

            // 执行主要任务
            var mainTask = taskFunc();

            await mainTask;

            var elapsed = stopwatch.Elapsed;

            // 如果主要任务提前完成，等待剩余的时间
            if (elapsed < minimumDuration)
            {
                await Task.Delay(minimumDuration - elapsed);
            }
            stopwatch.Stop();

            //Trace.WriteLine($"Task finished in: {stopwatch.ElapsedMilliseconds} ms");
            return stopwatch.Elapsed;
        }

        private MainWindow mainWindow;
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public CoreProcesserViewModel(MainWindow mainWindow)
        {
            if (RefreshModel())
                DepthEstimation = new DepthEstimation(ModelFilePath);
            this.mainWindow = mainWindow;
        }
        public bool RefreshModel()
        {
            //遍历所有.onnx文件
            // 获取所有 .onnx 文件
            string runningDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string[] onnxFiles = Directory.GetFiles(runningDirectory, "*.onnx", SearchOption.AllDirectories);
            if (onnxFiles.Length == 0)
            {
                var uiMessageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "警告",
                    Content = "未发现深度识别模型，请将(.onnx)模型与软件(.exe)放在同一文件夹",
                    CloseButtonText = "了解",
                };
                ModelsPath = null;
                OnPropertyChanged(nameof(ModelsPath));
                OnPropertyChanged(nameof(IsModelLoaded));

                uiMessageBox.ShowDialogAsync();
                //Application.Current.Shutdown(0);
                return false;
            }
            ModelsPath = onnxFiles.Select(f => f.Split("\\").Last()).ToList();
            OnPropertyChanged(nameof(ModelsPath));
            OnPropertyChanged(nameof(IsModelLoaded));

            return true;
        }

        public RelayCommand ProcessStartCommand => new RelayCommand((p) => Process.Start("explorer.exe", ((string)p)));
        public RelayCommand DebugCommand => new RelayCommand((p) => debug((string)p));
        private void debug(string d)
        {
            switch (d)
            {
                case "DepthContourMapContrust":
                    if (_enhanced != null)
                        Cv2.ImShow(d, _enhanced);
                    break;


            }
        }
        public RelayCommand RestoreViewCommand => new RelayCommand((p) =>
        {
            PreviewX = 0;
            PreviewY = 0;
            PreviewScaleFactor = 0;
        });
        private string _currentCultureInfo = "zh-CN";
        public string CurrentCultureInfo
        {
            get => _currentCultureInfo;
            set
            {
                if (!Equals(value, _currentCultureInfo))
                {
                    _currentCultureInfo = value;
                    LanguageManager.Instance.ChangeLanguage(new CultureInfo(value));

                }
            }
        }
        public RelayCommand SwitchLanguageCommand => new RelayCommand((p) =>
            CurrentCultureInfo = (string)p);        
        public RelayCommand SwitchThemeCommand => new RelayCommand((p) =>
            Theme = int.Parse((string)p));
        public RelayCommand OpenFolderCommand => new RelayCommand((p) => Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory));
        public RelayCommand RefreshModelCommand => new RelayCommand((p) => ReloadModel());
        public RelayCommand ConvertToManualCommand => new RelayCommand((p) => ConvertToManualPoint(p.ToString() ?? "c"));
        public RelayCommand ClearManualPointsCommand => new RelayCommand((p) => NewOperation(new(_manualPointsStored), false));
        public RelayCommand GradientDraftCommand => new RelayCommand((p) => GradientDraftExecute());
        public RelayCommand DeduplicationCommand => new RelayCommand((p) => DeduplicationExecute());
        public RelayCommand UndoStepCommand => new RelayCommand((p) => Undo());
        public RelayCommand RedoStepCommand => new RelayCommand((p) => Redo());
        public RelayCommand SetManualToolCommand => new RelayCommand((p) => ManualTool = int.Parse(p.ToString() ?? "0"));
        public RelayCommand ShowMissionFlyoutCommand => new RelayCommand((p) => IsShowMissionFlyout = true);
        public RelayCommand ImportConfigCommand => new RelayCommand((p) =>
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Xml 文件 (*.xml)|*.xml",
                DefaultExt = "xml",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                ImportConfig(openFileDialog.FileName);

            }
        });
        public RelayCommand ExportConfigCommand => new RelayCommand((p) =>
        { // 使用保存对话框选择保存路径
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Xml 文件 (*.xml)|*.xml",
                DefaultExt = "xml",
                AddExtension = true,
                FileName = $"{(Path.GetFileNameWithoutExtension(FilePath) == "" ? "EZHolo" : Path.GetFileNameWithoutExtension(FilePath))}_Config.xml"
            };

            if (saveFileDialog.ShowDialog() == true)
            {

            }
            ExportConfig(saveFileDialog.FileName);
        });
        public RelayCommand ExportDepthCommand => new RelayCommand(async (p) => await ExportDepthAsync());
        public RelayCommand ImportDepthCommand => new RelayCommand(async (p) =>
        {
            string localized_FILE = LanguageManager.Instance["Globe_File"];
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = $"PNG {localized_FILE} (*.png)|*.png|JPEG {localized_FILE} (*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP {localized_FILE} (*.bmp)|*.bmp|TIFF {localized_FILE} (*.tiff;*.tif)|*.tiff;*.tif|{LanguageManager.Instance["Globe_All"]}{localized_FILE} (*.*)|*.*",
                DefaultExt = "png"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await LoadDepthAsync(openFileDialog.FileName);
            }
        });
        public RelayCommand ExportPointsCommand => new RelayCommand(async (p) => await ExportPointsAsync((string)p));
        public RelayCommand ExportSvgPointsCommand => new RelayCommand(async (p) => await ExportPointsAsync((string)p, "svg"));
        public RelayCommand ImportPointsCommand => new RelayCommand(async (p) =>
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "所有支持的点图文件|*.png;*.jpg;*.jpeg;*PointMap.svg|图像文件|*.png;*.jpg;*.jpeg|SVG文件|*PointMap.svg|全部文件|*.*",
                DefaultExt = "png"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                await ImportPointsAsync(openFileDialog.FileName);

            }
        });
        public RelayCommand Open3DPreviewCommand => new RelayCommand((p) => {
            var window = new ThreeDPreviewWindow(this);
            SystemThemeWatcher.Watch(window);
            window.Show();
            });
        public RelayCommand ChangePreviewCommand => new RelayCommand((p) => ChangePreviewExecute((string)p));
        public RelayCommand CloseWarningCommand => new RelayCommand((p) => mainWindow.WarningFlyout.Hide());
        public RelayCommand ChooseImageCommand => new RelayCommand(async (p) =>
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            if (p == null)
            {
                openFileDialog.Filter =
                    "图像文件 (*.bmp, *.jpg, *.jpeg, *.png, *.tiff, *.webp) | *.bmp; *.jpg; *.jpeg; *.png; *.tiff; *.webp";
                if (openFileDialog.ShowDialog() == true)
                {
                    await LoadImageAsync(openFileDialog.FileName);

                }
            }
            else
            {
                string path = SaveClipboardImage();
                if (path != "")
                    await LoadImageAsync(path);
                else
                    ShowTipsTemporarilyAsync("剪贴板中没有图像");


            }
        });

        public RelayCommand UnloadImageCommand => new RelayCommand((p) =>
        {
            UnloadImage();
        });
        public List<string> _modelsPath = new();
        public List<string> ModelsPath
        {
            get => _modelsPath;
            set => _modelsPath = value;
        }
        public bool IsModelLoaded
        {
            get => DepthEstimation != null;
        }
        public RelayCommand ProcessDepthCommand => new RelayCommand(async (p) => await ProcessDepthAsync());
        public RelayCommand CreateScratchCommand => new RelayCommand((p) => ProcessScratch());
        public RelayCommand ExportPathCommand => new RelayCommand((p) => ExportPath());
        public RelayCommand OpenCustomMessageBoxCommand => new RelayCommand((p) => OpenCustomMessageBox());
        private void OpenCustomMessageBox()
        {
            var uiMessageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = LanguageManager.Instance["Title_Open_Source_Information"],
                Content =
                    "Copyright \u00a9 2025 Yigu Wang\r\n\r\nLicensed under the Apache License, Version 2.0 (the \"License\");\r\nyou may not use this file except in compliance with the License.\r\nYou may obtain a copy of the License at\r\n\r\n    http://www.apache.org/licenses/LICENSE-2.0\r\n\r\nUnless required by applicable law or agreed to in writing, software distributed under the License is distributed on an \"AS IS\" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.\r\nSee the License for the specific language governing permissions and limitations under the License.\r\nMore information at https://github.com/POPCORNBOOM/EZHolodotNet",
                CloseButtonText = LanguageManager.Instance["Globe_Got_It"],
            };

            uiMessageBox.ShowDialogAsync();
        }

        public string ModelFilePath
        {
            get
            {
                string filepath = "无";
                try
                {
                    if (_modelsPath != null)
                        filepath = _modelsPath[_selectedModelPathIndex == -1 ? 0 : _selectedModelPathIndex];
                }
                catch (Exception e)
                {

                }
                return filepath;
            }
        }
        private int _selectedModelPathIndex = 0;
        [XmlIgnore]
        public int SelectedModelPathIndex
        {
            get => _selectedModelPathIndex;
            set
            {
                if (!Equals(_selectedModelPathIndex, value))
                {
                    _selectedModelPathIndex = value;
                    OnPropertyChanged(nameof(SelectedModelPathIndex));
                    OnPropertyChanged(nameof(ModelFilePath));
                    ReloadModel();
                }
            }
        }

        public void ReloadModel()
        {
            DepthEstimation?.CloseSession();
            DepthEstimation = null;
            if (RefreshModel())
            {
                DepthEstimation = new DepthEstimation(ModelFilePath);
                OnPropertyChanged(nameof(ModelFilePath));
            }
        }
        public bool IsUsingLastConfigEveryTime
        {
            get => Properties.Settings.Default.IsUsingLastConfigEveryTime;
            set
            {
                if (!Equals(Properties.Settings.Default.IsUsingLastConfigEveryTime, value))
                {
                    Properties.Settings.Default.IsUsingLastConfigEveryTime = value;
                    Properties.Settings.Default.Save();
                    OnPropertyChanged(nameof(IsUsingLastConfigEveryTime));
                }
            }
        }
        public int IsMachineUser
        {
            get => Properties.Settings.Default.IsMachineUser;
            set
            {
                if (!Equals(Properties.Settings.Default.IsMachineUser, value))
                {
                    Properties.Settings.Default.IsMachineUser = value;
                    Properties.Settings.Default.Save();
                    OnPropertyChanged(nameof(IsMachineUser));
                }
            }
        }

        private string _filePath = "";
        [XmlIgnore]
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (!Equals(_filePath, value))
                {
                    _filePath = value;
                    OnPropertyChanged(nameof(FilePath));
                }
            }
        }
        private HashSet<Point> _manualPointsStored = new();
        private HashSet<Point> _manualPoints
        {
            get => IsManualMethodEnabled ? _manualPointsStored : new();
        }
        private List<Point> _contourPoints = new();
        private List<Point> _brightnessPoints = new();

        private List<Point> _postProcessedPointsCache = new();
        private int _sampledPointsCount = 0;
        [XmlIgnore]
        public List<Point> SampledPoints
        {
            get
            {
                if (IsPostProcessEnabled)
                {
                    if (_postProcessedPointsCache.Count == 0)
                    {
                        var r = PostProcessPoints(_contourPoints.Concat(_brightnessPoints).Concat(_manualPoints)
                            .ToList());
                        _postProcessedPointsCache = r;
                        _sampledPointsCount = r.Count;
                        return r;
                    }
                    else
                    {
                        _sampledPointsCount = _postProcessedPointsCache.Count;
                        return _postProcessedPointsCache;
                    }
                }
                else
                {
                    ExcludedPointCount = 0;
                    var r = _contourPoints.Concat(_brightnessPoints).Concat(_manualPoints).ToList();
                    _sampledPointsCount = r.Count;
                    return r;
                }
            }
        }
        [XmlIgnore] private Mat? _originalImage = new();
        [XmlIgnore]
        public Mat? OriginalImage
        {
            get => _originalImage;
            set
            {
                if (!Equals(_originalImage, value))
                {
                    _originalImage?.Dispose();
                    _originalImage = value;
                    if (IsOriginalImageLoaded)
                    {
                        float ratio = _originalImage!.Width / _originalImage!.Height;
                        if ((ratio < 0.75f || ratio > 1.33f) && !DonotShowOriginalImageWarning)
                            IsShowBadOriginalImageWarning = true;
                    }
                    OnPropertyChanged(nameof(OriginalImage));
                    OnPropertyChanged(nameof(IsOriginalImageLoaded)); 

                    OnPropertyChanged(nameof(OriginImageHeight));
                    OnPropertyChanged(nameof(OriginImageWidth));
                    OnPropertyChanged(nameof(RadiusMax));
                }
            }
        }

        public bool IsOriginalImageLoaded
        {
            get => _originalImage != null && !_originalImage.Empty();
        }
        [XmlIgnore] private Mat? _depthImage = new Mat(); //32FC1
        [XmlIgnore]
        public Mat? DepthImage
        {
            get => _depthImage;
            set
            {
                if (!Equals(_depthImage, value))
                {

                    //_depthImage?.Dispose(); TODO: 检查异步操作DepthImage的所有位置
                    _depthImage = value;
                    UpdateGradientAsync();
                    OnPropertyChanged(nameof(DepthImage));
                    OnPropertyChanged(nameof(IsDepthImageLoaded));
                    if (_depthImage != null)
                        DisplayImageDepth = ApplyColorMap(_depthImage).ToWriteableBitmap();
                }
            }
        }
        public bool IsDepthImageLoaded
        {
            get => _depthImage != null && _depthImage.Cols != 0;
        }
        [XmlIgnore] private Mat? _gradientImage = new Mat();
        [XmlIgnore]
        public Mat? GradientImage
        {
            get => _gradientImage;
            set
            {
                if (!Equals(_gradientImage, value))
                {
                    _gradientImage?.Dispose();
                    _gradientImage = value;
                    OnPropertyChanged(nameof(GradientImage));
                }
            }
        }
        [XmlIgnore] private Mat? _gradientModuleImage = new Mat(); // 深度图梯度模长,用作基于深度图的边缘算法
        [XmlIgnore]
        public Mat? GradientModuleImage
        {
            get => _gradientModuleImage;
            set
            {
                if (!Equals(_gradientModuleImage, value))
                {
                    //_gradientModuleImage?.Dispose();
                    _gradientModuleImage = value;
                    OnPropertyChanged(nameof(GradientModuleImage));
                }
            }
        }
        private readonly Mat _gradient = CreateGradientMat(2, 256);
        private Mat? _gradientColored = null;
        [XmlIgnore] public WriteableBitmap? GradientColor => ApplyColorMap(_gradient).ToWriteableBitmap();

        [XmlIgnore] public Mat ContoursOverDepthImage { get; set; } = new Mat();
        private WriteableBitmap _displayImageDepth;
        [XmlIgnore]
        public WriteableBitmap DisplayImageDepth
        {
            get => _displayImageDepth;
            set
            {
                if (!Equals(_displayImageDepth, value))
                {
                    _displayImageDepth = value;
                    OnPropertyChanged(nameof(DisplayImageDepth));
                }
            }
        }
        private WriteableBitmap _displayImageContour;
        [XmlIgnore]
        public WriteableBitmap DisplayImageContour
        {
            get => _displayImageContour;
            set
            {
                if (!Equals(_displayImageContour, value))
                {
                    _displayImageContour = value;
                    OnPropertyChanged(nameof(DisplayImageContour));
                }
            }
        }
        private WriteableBitmap _displayImageScratchL;
        [XmlIgnore]
        public WriteableBitmap DisplayImageScratchL
        {
            get => _displayImageScratchL;
            set
            {
                if (!Equals(_displayImageScratchL, value))
                {
                    _displayImageScratchL = value;
                    OnPropertyChanged(nameof(DisplayImageScratchL));
                }
            }
        }
        private WriteableBitmap _displayImageScratchR;
        [XmlIgnore]
        public WriteableBitmap DisplayImageScratchR
        {
            get => _displayImageScratchR;
            set
            {
                if (!Equals(_displayImageScratchR, value))
                {
                    _displayImageScratchR = value;
                    OnPropertyChanged(nameof(DisplayImageScratchR));
                }
            }
        }
        private WriteableBitmap _displayImageScratchO;
        [XmlIgnore]
        public WriteableBitmap DisplayImageScratchO
        {
            get => _displayImageScratchO;
            set
            {
                if (!Equals(_displayImageScratchO, value))
                {
                    _displayImageScratchO = value;
                    OnPropertyChanged(nameof(DisplayImageScratchO));
                }
            }
        }
        private WriteableBitmap _displayImageScratchLine;
        [XmlIgnore]
        public WriteableBitmap DisplayImageScratchLine
        {
            get => _displayImageScratchLine;
            set
            {
                if (!Equals(_displayImageScratchLine, value))
                {
                    //Cv2.ImWrite("D:/Test.png", value.ToMat());

                    _displayImageScratchLine = value;
                    OnPropertyChanged(nameof(DisplayImageScratchLine));
                }
            }
        }
        private BitmapSource _displayImageScratchStep;
        [XmlIgnore]
        public BitmapSource DisplayImageScratchStep
        {
            get => _displayImageScratchStep;
            set
            {
                if (!Equals(_displayImageScratchStep, value))
                {
                    _displayImageScratchStep = value;
                    OnPropertyChanged(nameof(DisplayImageScratchStep));
                }
            }
        }
        private async void UpdateDisplayImageScratchStep()
        {
            DisplayImageScratchStep = await ProcessAndCacheScratchTick(_previewT);

        }
        private BitmapSource _displayImageScratch3DLeft;
        [XmlIgnore]
        public BitmapSource DisplayImageScratch3DLeft
        {
            get => _displayImageScratch3DLeft;
            set
            {
                if (!Equals(_displayImageScratch3DLeft, value))
                {
                    _displayImageScratch3DLeft = value;
                    OnPropertyChanged(nameof(DisplayImageScratch3DLeft));
                }
            }
        }
        private BitmapSource _displayImageScratch3DRight;
        [XmlIgnore]
        public BitmapSource DisplayImageScratch3DRight
        {
            get => _displayImageScratch3DRight;
            set
            {
                if (!Equals(_displayImageScratch3DRight, value))
                {
                    _displayImageScratch3DRight = value;
                    OnPropertyChanged(nameof(DisplayImageScratch3DRight));
                }
            }
        }
        private WriteableBitmap _displayImageScratch3D;
        [XmlIgnore]
        public WriteableBitmap DisplayImageScratch3D
        {
            get => _displayImageScratch3D;
            set
            {
                if (!Equals(_displayImageScratch3D, value))
                {
                    _displayImageScratch3D = value;
                    OnPropertyChanged(nameof(DisplayImageScratch3D));
                }
            }
        }
        public float AreaDensity { get; set; } = 10;
        public List<Point2d> Points { get; set; } = new List<Point2d>();
        [XmlIgnore]
        public DepthEstimation? DepthEstimation;
        public OccusionPathHelper OccusionPathHelper = new OccusionPathHelper();


        public int OriginImageWidth
        {
            get => OriginalImage?.Width ?? 1;
        }
        public int OriginImageHeight
        {
            get => OriginalImage?.Height ?? 1;
        }
        private int _maximumPointCount = 50000;
        public int MaximumPointCount
        {
            get => _maximumPointCount;
            set
            {
                if (!Equals(_maximumPointCount, value))
                {
                    _maximumPointCount = value;
                    OnPropertyChanged(nameof(MaximumPointCount));
                }
            }
        }

        private Point _mousePixelPosition = new(0, 0);
        private Point _mouseWindowPosition = new(0, 0);

        private int _mousePointX;
        private int _mousePointY;
        private bool _isMouseMoving;
        private float _mouseDepth;
        private string _mouseDepthColor = "#000000";
        private string _mouseDepthColorBW = "#AAFFFFFF";
        private float _mouseGradient;
        private float _mouseGradientX;
        private float _mouseGradientY;
        private float _eraserCenterX;
        private float _eraserCenterY;
        private float _smearCenterX;
        private float _smearCenterY;
        private float _draftCenterX;
        private float _draftCenterY;
        private int _mouseOnLMR;
        [XmlIgnore]
        public int MouseOnLMR
        {
            get => _mouseOnLMR;
            set
            {
                if (!Equals(_mouseOnLMR, value))
                {
                    _mouseOnLMR = value;
                    OnPropertyChanged(nameof(MouseOnLMR));
                }
            }
        }
        [XmlIgnore]
        public Point MousePixelPosition
        {
            get => _mousePixelPosition;
            set
            {
                var clamped = new Point(
                    Math.Clamp(value.X, 0, Math.Max(0, OriginImageWidth - 1)),
                    Math.Clamp(value.Y, 0, Math.Max(0, OriginImageHeight - 1))
                );

                if (!Equals(_mousePixelPosition, clamped))
                {
                    _mousePixelPosition = clamped;
                    OnPropertyChanged(nameof(MousePixelPosition));

                    // 同步更新依赖属性（尽量保持 UI 绑定最小延迟）
                    MousePointX = clamped.X;
                    MousePointY = clamped.Y;
                    IsMouseMoving = !Equals(clamped, new Point(0, 0));

                    // 更新深度与梯度相关（容错）
                    float depthVal = 0f;
                    try
                    {
                        if (DepthImage != null && DepthImage.Cols > 0 && DepthImage.Rows > 0)
                            depthVal = DepthImage.Get<float>(_mousePixelPosition.Y, _mousePixelPosition.X);
                    }
                    catch { depthVal = 0f; }
                    MouseDepth = depthVal;

                    if (GradientImage != null && !GradientImage.Empty())
                    {
                        int gy = Math.Clamp(_mousePixelPosition.Y, 0, GradientImage.Rows - 1);
                        int gx = Math.Clamp(_mousePixelPosition.X, 0, GradientImage.Cols - 1);
                        try
                        {
                            Vec2f grad = GradientImage.Get<Vec2f>(gy, gx);
                            float norm = MathF.Sqrt(grad.Item0 * grad.Item0 + grad.Item1 * grad.Item1);
                            MouseGradient = norm;
                            MouseGradientX = (norm == 0 ? 0f : grad.Item0 * 255f / Math.Max(norm, float.Epsilon)) + 256f;
                            MouseGradientY = (norm == 0 ? 0f : grad.Item1 * 255f / Math.Max(norm, float.Epsilon)) + 256f;
                        }
                        catch
                        {
                            MouseGradient = 0f;
                            MouseGradientX = 256f;
                            MouseGradientY = 256f;
                        }
                    }
                    else
                    {
                        MouseGradient = 0f;
                        MouseGradientX = 256f;
                        MouseGradientY = 256f;
                    }

                    // 更新显示颜色（容错）
                    try
                    {
                        if (_gradientColored == null) _gradientColored = ApplyColorMap(_gradient);
                        var color = _gradientColored.Get<Vec3b>(255 - (int)MouseDepth, 0);
                        MouseDepthColor = ColorTranslator.ToHtml(System.Drawing.Color.FromArgb(color[2], color[1], color[0]));
                        float L = (0.299f * color[0] + 0.587f * color[1] + 0.114f * color[2]) / 255f;
                        MouseDepthColorBW = L > 0.5f ? "#AA000000" : "#AAFFFFFF";
                    }
                    catch
                    {
                        MouseDepthColor = "#000000";
                        MouseDepthColorBW = "#AAFFFFFF";
                    }
                }
            }
        }

        [XmlIgnore]
        public Point MouseWindowPosition
        {
            get => _mouseWindowPosition;
            set
            {
                if (!Equals(_mouseWindowPosition, value))
                {
                    _mouseWindowPosition = value;
                    OnPropertyChanged(nameof(MouseWindowPosition));
                }
            }
        }
        [XmlIgnore]
        public int MousePointX
        {
            get => _mousePointX;
            private set
            {
                if (_mousePointX != value)
                {
                    _mousePointX = value;
                    OnPropertyChanged(nameof(MousePointX));
                }
            }
        }
        [XmlIgnore]
        public int MousePointY
        {
            get => _mousePointY;
            private set
            {
                if (_mousePointY != value)
                {
                    _mousePointY = value;
                    OnPropertyChanged(nameof(MousePointY));
                }
            }
        }

        [XmlIgnore]
        public bool IsMouseMoving
        {
            get => _isMouseMoving;
            private set
            {
                if (_isMouseMoving != value)
                {
                    _isMouseMoving = value;
                    OnPropertyChanged(nameof(IsMouseMoving));
                }
            }
        }

        [XmlIgnore]
        public float MouseDepth
        {
            get => _mouseDepth;
            private set
            {
                if (Math.Abs(_mouseDepth - value) > float.Epsilon)
                {
                    _mouseDepth = value;
                    OnPropertyChanged(nameof(MouseDepth));
                }
            }
        }

        [XmlIgnore]
        public string MouseDepthColor
        {
            get => _mouseDepthColor;
            private set
            {
                if (_mouseDepthColor != value)
                {
                    _mouseDepthColor = value;
                    OnPropertyChanged(nameof(MouseDepthColor));
                }
            }
        }

        [XmlIgnore]
        public string MouseDepthColorBW
        {
            get => _mouseDepthColorBW;
            private set
            {
                if (_mouseDepthColorBW != value)
                {
                    _mouseDepthColorBW = value;
                    OnPropertyChanged(nameof(MouseDepthColorBW));
                }
            }
        }

        [XmlIgnore]
        public float MouseGradient
        {
            get => _mouseGradient;
            private set
            {
                if (Math.Abs(_mouseGradient - value) > float.Epsilon)
                {
                    _mouseGradient = value;
                    OnPropertyChanged(nameof(MouseGradient));
                }
            }
        }

        [XmlIgnore]
        public float MouseGradientX
        {
            get => _mouseGradientX;
            private set
            {
                if (Math.Abs(_mouseGradientX - value) > float.Epsilon)
                {
                    _mouseGradientX = value;
                    OnPropertyChanged(nameof(MouseGradientX));
                }
            }
        }

        [XmlIgnore]
        public float MouseGradientY
        {
            get => _mouseGradientY;
            private set
            {
                if (Math.Abs(_mouseGradientY - value) > float.Epsilon)
                {
                    _mouseGradientY = value;
                    OnPropertyChanged(nameof(MouseGradientY));
                }
            }
        }

        [XmlIgnore]
        public Vec2f MouseGradientVectorNormal = new();


        [XmlIgnore]
        public int PointCount
        {
            get
            {
                return _sampledPointsCount;
            }
        }
        public int ContourPointCount => _contourPoints.Count;
        public int BrightnessPointCount => _brightnessPoints.Count;
        public int ManualPointCount => _manualPointsStored.Count;
        private bool _isShowMissionFlyout = false;
        public bool IsShowMissionFlyout
        {
            get => _isShowMissionFlyout;
            set
            {
                if (!Equals(_isShowMissionFlyout, value))
                {
                    _isShowMissionFlyout = value;
                    OnPropertyChanged(nameof(IsShowMissionFlyout));
                }
            }
        }
        public int DepthColor
        {
            get => _depthColor;
            set
            {
                if (!Equals(_depthColor, value))
                {
                    _depthColor = value;
                    OnPropertyChanged(nameof(DepthColor));

                    _gradientColored = ApplyColorMap(_gradient);
                    OnPropertyChanged(nameof(GradientColor));
                    OnPropertyChanged(nameof(MouseDepthColor));
                    OnPropertyChanged(nameof(MouseDepthColorBW));

                    if (DepthImage.Cols != 0)
                        DisplayImageDepth = ApplyColorMap(DepthImage).ToWriteableBitmap();

                }
            }
        }
        private int _depthColor = 0;
        public int ManualTool
        {
            get => _manualTool;
            set
            {
                if (!Equals(_manualTool, value))
                {
                    _manualTool = value;
                    OnPropertyChanged(nameof(ManualTool));
                    OnToolDisplayUpdated();
                }
            }
        }
        private int _manualTool = 0;

        private float _overlayOpacity = 0.5f;

        public float OverlayOpacity
        {
            get => _overlayOpacity;
            set
            {
                if (!Equals(_overlayOpacity, value))
                {
                    _overlayOpacity =Math.Clamp(value,0,1);
                    OnPropertyChanged(nameof(OverlayOpacity));
                }
            }
        }
        private float _indicatorX = 0;

        public float IndicatorX
        {
            get => _indicatorX;
            set
            {
                if (!Equals(_indicatorX, value))
                {
                    _indicatorX = value;
                    OnPropertyChanged(nameof(IndicatorX));
                }
            }
        }
        private float _indicatorY = 0;

        public float IndicatorY
        {
            get => _indicatorY;
            set
            {
                if (!Equals(_indicatorY, value))
                {
                    _indicatorY = value;
                    OnPropertyChanged(nameof(IndicatorY));
                }
            }
        }
        private float _previewX = 0;

        public float PreviewX
        {
            get => _previewX;
            set
            {
                if (!Equals(_previewX, value))
                {
                    _previewX = value;
                    OnPropertyChanged(nameof(PreviewX));
                }
            }
        }
        private float _previewY = 0;
        public float PreviewY
        {
            get => _previewY;
            set
            {
                if (!Equals(_previewY, value))
                {
                    _previewY = value;
                    OnPropertyChanged(nameof(PreviewY));
                }
            }
        }


        private float _previewScaleFactor = 0;

        public float RePreviewScale
        {
            get => 1 / MathF.Pow(10, _previewScaleFactor);
        }
        public float PreviewScale
        {
            get => MathF.Pow(10, _previewScaleFactor);
        }
        public float PreviewScaleFactor
        {
            get => _previewScaleFactor;
            set
            {
                if (!Equals(_previewScaleFactor, value))
                {
                    _previewScaleFactor = Math.Clamp(value, -0.5f, 0.5f);

                    OnPropertyChanged(nameof(PreviewScaleFactor));
                    OnPropertyChanged(nameof(PreviewScale));
                    OnPropertyChanged(nameof(RePreviewScale));
                }
            }
        }
        private float _previewT = 0.5f;

        public float PreviewT
        {
            get => _previewT;
            set
            {
                if (!Equals(_previewT, value))
                {
                    _previewT = value;
                    if (_previewT > 1) _previewT = 1;
                    if (_previewT < 0) _previewT = 0;
                    OnPropertyChanged(nameof(PreviewT));
                    UpdateDisplayImageScratchStep();
                }
            }
        }

        private float _previewStepDistance = 0.2f;

        public float PreviewStepDistance
        {
            get => _previewStepDistance;
            set
            {
                if (!Equals(_previewStepDistance, value))
                {
                    _previewStepDistance = value;
                    if (_previewStepDistance > 1) _previewStepDistance = 1;
                    if (_previewStepDistance < 0) _previewStepDistance = 0;
                    OnPropertyChanged(nameof(PreviewStepDistance));
                    ProcessScratchStep3D();
                }
            }
        }
        private float _previewStep = 0.5f;

        public float PreviewStep
        {
            get => _previewStep;
            set
            {
                if (!Equals(_previewStep, value))
                {
                    _previewStep = value;
                    if (_previewStep > 1)
                    {
                        _previewStep = 1;
                        inverse = -1;
                    }
                    if (_previewStep < 0)
                    {
                        _previewStep = 0;
                        inverse = 1;
                    }
                    OnPropertyChanged(nameof(PreviewStep));
                    ProcessScratchStep3D();
                }
            }
        }
        private float _zeroDepth = 128;
        public float ZeroDepth
        {
            get => _zeroDepth;
            set
            {
                if (!Equals(_zeroDepth, value))
                {
                    _zeroDepth = value;
                    OnPropertyChanged(nameof(ZeroDepth));
                }
            }
        }
        private float _ignoreZeroDepthDistance = 0;
        public float IgnoreZeroDepthDistance
        {
            get => _ignoreZeroDepthDistance;
            set
            {
                if (!Equals(_ignoreZeroDepthDistance, value))
                {
                    _ignoreZeroDepthDistance = value;
                    OnPropertyChanged(nameof(IgnoreZeroDepthDistance));
                }
            }
        }
        private float _radiusFactor = 1f;
        public float RadiusFactor
        {
            get => _radiusFactor;
            set
            {
                if (!Equals(_radiusFactor, value))
                {
                    _radiusFactor = value;
                    OnPropertyChanged(nameof(RadiusFactor));
                }
            }
        }
        private float _angleFactor = 45f;
        public float AngleFactor
        {
            get => _angleFactor;
            set
            {
                if (!Equals(_angleFactor, value))
                {
                    _angleFactor = Math.Clamp(value, 10, 60);
                    OnPropertyChanged(nameof(AngleFactor));
                }
            }
        }
        private int _layerCount = 32;
        public int LayerCount
        {
            get => _layerCount;
            set
            {
                if (!Equals(_layerCount, value))
                {
                    _layerCount = Math.Clamp(value, 5, 64);
                    OnPropertyChanged(nameof(LayerCount));
                }
            }
        }
        private float _aFactor = 0.66f;
        public float AFactor
        {
            get => _aFactor;
            set
            {
                if (!Equals(_aFactor, value))
                {
                    _aFactor = value;
                    OnPropertyChanged(nameof(AFactor));
                }
            }
        }
        private float _bFactor = 1400;
        public float BFactor
        {
            get => _bFactor;
            set
            {
                if (!Equals(_bFactor, value))
                {
                    _bFactor = value;
                    OnPropertyChanged(nameof(BFactor));
                }
            }
        }
        private int _previewDense = 150;
        public int PreviewDense
        {
            get => _previewDense;
            set
            {
                if (!Equals(_previewDense, value))
                {
                    _previewDense = value;
                    OnPropertyChanged(nameof(PreviewDense));
                }
            }
        }
        public float Threshold1
        {
            get => _threshold1;
            set
            {
                if (!Equals(_threshold1, value))
                {
                    _threshold1 = value;
                    OnPropertyChanged(nameof(Threshold1));
                    RefreshDisplay();

                }
            }
        }
        private int _lineDensity = 5;
        public int LineDensity
        {
            get => _lineDensity;
            set
            {
                if (!Equals(_lineDensity, value))
                {
                    _lineDensity = value;
                    OnPropertyChanged(nameof(LineDensity));
                    RefreshDisplay();
                }
            }
        }
        private int _excludedPointCount = 0;
        public int ExcludedPointCount
        {
            get => _excludedPointCount;
            set
            {
                if (!Equals(_excludedPointCount, value))
                {
                    _excludedPointCount = value;
                    OnPropertyChanged(nameof(ExcludedPointCount));
                }
            }
        }
        private int _lineOffsetFactor = 5;
        public int LineOffsetFactor
        {
            get => _lineOffsetFactor;
            set
            {
                if (!Equals(_lineOffsetFactor, value))
                {
                    _lineOffsetFactor = value;
                    OnPropertyChanged(nameof(LineOffsetFactor));
                    RefreshDisplay();
                }
            }
        }
        private float _threshold1 = 20;
        private float _threshold2 = 100;
        public float Threshold2
        {
            get => _threshold2;
            set
            {
                if (!Equals(_threshold2, value))
                {
                    _threshold2 = value;
                    OnPropertyChanged(nameof(Threshold2));
                    RefreshDisplay();
                }
            }
        }
        private float _blurFactor = 3;
        public float BlurFactor
        {
            get => _blurFactor;
            set
            {
                if (!Equals(_blurFactor, value))
                {
                    _blurFactor = value;
                    OnPropertyChanged(nameof(BlurFactor));
                    RefreshDisplay();
                }
            }
        }
        private float _approximationFactor = 1;
        public float ApproximationFactor
        {
            get => _approximationFactor;
            set
            {
                if (!Equals(_approximationFactor, value))
                {
                    _approximationFactor = value;
                    OnPropertyChanged(nameof(ApproximationFactor));
                    RefreshDisplay();
                }
            }
        }
        private float _manualDensity = 0.1f;
        public float ManualDensity
        {
            get => _manualDensity;
            set
            {
                if (!Equals(_manualDensity, value))
                {
                    _manualDensity = value;
                    OnPropertyChanged(nameof(ManualDensity));
                    RefreshDisplay();
                }
            }
        }
        public void ChangeRadius(float delta)
        {
            switch (ManualTool)
            {
                case 2:
                    EraserRadius += delta;
                    break;
                case 4:
                    SmearRadius += delta;
                    break;
                case 5:
                    DraftRadius += delta;
                    break;
                default:
                    PreviewScaleFactor += delta / 120f;
                    break;
            }
        }
        public float RadiusMax
        {
            get => Math.Clamp(OriginImageWidth / 2, 30, 500);

        }
        private float _eraserRadius = 25;
        public float EraserRadius
        {
            get => _eraserRadius;
            set
            {
                if (!Equals(_eraserRadius, value))
                {
                    _eraserRadius = Math.Clamp(value, 0, RadiusMax);
                    OnPropertyChanged(nameof(EraserRadius));
                    OnPropertyChanged(nameof(EraserDiameter));
                    OnToolDisplayUpdated();
                }
            }
        }
        private float _eraserLower = 0;
        public float EraserLower
        {
            get => _eraserLower;
            set
            {
                if (!Equals(_eraserLower, value))
                {
                    _eraserLower = Math.Clamp(value, 0, _eraserUpper);
                    OnPropertyChanged(nameof(EraserLower));
                }
            }
        }
        private float _eraserUpper = 100;
        public float EraserUpper
        {
            get => _eraserUpper;
            set
            {
                if (!Equals(_eraserUpper, value))
                {
                    _eraserUpper = Math.Clamp(value, _eraserLower, 255);
                    OnPropertyChanged(nameof(EraserUpper));
                }
            }
        }
        private float _smearRadius = 25;
        public float SmearRadius
        {
            get => _smearRadius;
            set
            {
                if (!Equals(_smearRadius, value))
                {
                    _smearRadius = Math.Clamp(value, 0, RadiusMax);
                    OnPropertyChanged(nameof(SmearRadius));
                    OnPropertyChanged(nameof(SmearDiameter)); 
                    OnToolDisplayUpdated();
                }
            }
        }
        private float _draftRadius = 25;
        public float DraftRadius
        {
            get => _draftRadius;
            set
            {
                if (!Equals(_draftRadius, value))
                {
                    _draftRadius = Math.Clamp(value, 0, RadiusMax);
                    OnPropertyChanged(nameof(DraftRadius));
                    OnPropertyChanged(nameof(DraftDiameter));
                    OnToolDisplayUpdated();
                }
            }
        }
        private void OnToolDisplayUpdated()
        {
            OnPropertyChanged(nameof(ToolRadius));
            OnPropertyChanged(nameof(ToolDiameter));
            OnPropertyChanged(nameof(MToolRadius));
        }
        public float ToolRadius
        {
            get
            {
                switch (ManualTool)
                {
                    case 2:
                        return EraserRadius;
                    case 4:
                        return SmearRadius;
                    case 5:
                        return DraftRadius;
                    default:
                        return 0;
                }
            }
        }
        public float ToolDiameter
        {
            get => ToolRadius * 2;
        }
        public float MToolRadius
        {
            get => - ToolRadius;
        }
        private float _smearStrength = 0.5f;
        public float SmearStrenth
        {
            get => _smearStrength;
            set
            {
                if (!Equals(_smearStrength, value))
                {
                    _smearStrength = value;
                    OnPropertyChanged(nameof(SmearStrenth));
                }
            }
        }
        private float _draftStrength = 0.01f;
        public float DraftStrength
        {
            get => _draftStrength;
            set
            {
                if (!Equals(_draftStrength, value))
                {
                    _draftStrength = value;
                    OnPropertyChanged(nameof(DraftStrength));
                }
            }
        }
        private bool _isDragEntered = false;
        public bool IsDragEntered
        {
            get => _isDragEntered;
            set
            {
                if (!Equals(_isDragEntered, value))
                {
                    _isDragEntered = value;
                    OnPropertyChanged(nameof(IsDragEntered));
                }
            }
        }
        private bool _isSmartEraserEnabled = false;
        public bool IsSmartEraserEnabled
        {
            get => _isSmartEraserEnabled;
            set
            {
                if (!Equals(_isSmartEraserEnabled, value))
                {
                    _isSmartEraserEnabled = value;
                    OnPropertyChanged(nameof(IsSmartEraserEnabled));
                }
            }
        }
        private bool _isTipsPanelVisible = false;
        [XmlIgnore]
        public bool IsTipsPanelVisible
        {
            get => _isTipsPanelVisible;
            set
            {
                if (!Equals(_isTipsPanelVisible, value))
                {
                    _isTipsPanelVisible = value;
                    OnPropertyChanged(nameof(IsTipsPanelVisible));
                }
            }
        }
        private bool _isBinaryDeNoise = true;
        public bool IsBinaryDeNoise
        {
            get => _isBinaryDeNoise;
            set
            {
                if (!Equals(_isBinaryDeNoise, value))
                {
                    _isBinaryDeNoise = value;
                    OnPropertyChanged(nameof(IsBinaryDeNoise));
                    RefreshDisplay();

                }
            }
        }
        private bool _isShowEnhancedMat = false;
        public bool IsShowEnhancedMat
        {
            get => _isShowEnhancedMat;
            set
            {
                if (!Equals(_isShowEnhancedMat, value))
                {
                    _isShowEnhancedMat = value;
                    OnPropertyChanged(nameof(IsShowEnhancedMat));
                }
            }
        }
        private bool _isParallelEye3DMode = true;
        public bool IsParallelEye3DMode
        {
            get => _isParallelEye3DMode;
            set
            {
                if (!Equals(_isParallelEye3DMode, value))
                {
                    _isParallelEye3DMode = value;
                    OnPropertyChanged(nameof(IsParallelEye3DMode));
                }
            }
        }
        private bool _isNotProcessingSvg = true;
        public bool IsNotProcessingSvg
        {
            get => _isNotProcessingSvg;
            set
            {
                if (!Equals(_isNotProcessingSvg, value))
                {
                    _isNotProcessingSvg = value;
                    OnPropertyChanged(nameof(IsNotProcessingSvg));
                }
            }
        }
        /*private double _svgExportProgress;
        public double SvgExportProgress
        {
            get => _svgExportProgress;
            set
            {
                if (!Equals(_svgExportProgress, value))
                {
                    _svgExportProgress = value;
                    OnPropertyChanged(nameof(SvgExportProgress));
                }
            }
        }*/
        private bool _isWarningPointsOverflow = false;
        public bool IsWarningPointsOverflow
        {
            get => _isWarningPointsOverflow;
            set
            {
                if (!Equals(_isWarningPointsOverflow, value))
                {
                    _isWarningPointsOverflow = value;
                    OnPropertyChanged(nameof(IsWarningPointsOverflow));
                }
            }
        }
        private bool _isAutoPlay3D = false;
        public bool IsAutoPlay3D
        {
            get => _isAutoPlay3D;
            set
            {
                if (!Equals(_isAutoPlay3D, value))
                {
                    _isAutoPlay3D = value;
                    OnPropertyChanged(nameof(IsAutoPlay3D));
                    if (value)
                        ProcessScratchStep3D();
                }
            }
        }

        private bool _isPreviewingOriginImage = true;
        public bool IsPreviewingOriginImage
        {
            get => _isPreviewingOriginImage;
            set
            {
                if (!Equals(_isPreviewingOriginImage, value))
                {
                    _isPreviewingOriginImage = value;
                    OnPropertyChanged(nameof(IsPreviewingOriginImage));
                }
            }
        }
        private bool _isPreviewingLeft = false;
        public bool IsPreviewingLeft
        {
            get => _isPreviewingLeft;
            set
            {
                if (!Equals(_isPreviewingLeft, value))
                {
                    _isPreviewingLeft = value;
                    OnPropertyChanged(nameof(IsPreviewingLeft));
                }
            }
        }
        private bool _isPreviewingRight = false;
        public bool IsPreviewingRight
        {
            get => _isPreviewingRight;
            set
            {
                if (!Equals(_isPreviewingRight, value))
                {
                    _isPreviewingRight = value;
                    OnPropertyChanged(nameof(IsPreviewingRight));
                }
            }
        }
        private bool _isPreviewingOrigin = false;
        public bool IsPreviewingOrigin
        {
            get => _isPreviewingOrigin;
            set
            {
                if (!Equals(_isPreviewingOrigin, value))
                {
                    _isPreviewingOrigin = value;
                    OnPropertyChanged(nameof(IsPreviewingOrigin));
                }
            }
        }
        private bool _isPreviewingLine = true;
        public bool IsPreviewingLine
        {
            get => _isPreviewingLine;
            set
            {
                if (!Equals(_isPreviewingLine, value))
                {
                    _isPreviewingLine = value;
                    OnPropertyChanged(nameof(IsPreviewingLine));
                }
            }
        }
        private bool _isPreviewingLineDensity = false;
        public bool IsPreviewingLineDensity
        {
            get => _isPreviewingLineDensity;
            set
            {
                if (!Equals(_isPreviewingLineDensity, value))
                {
                    _isPreviewingLineDensity = value;
                    OnPropertyChanged(nameof(IsPreviewingLineDensity));
                    ProcessScratch();

                }
            }
        }
        private bool _isPreviewingStep = true;
        public bool IsPreviewingStep
        {
            get => _isPreviewingStep;
            set
            {
                if (!Equals(_isPreviewingStep, value))
                {
                    _isPreviewingStep = value;
                    OnPropertyChanged(nameof(IsPreviewingStep));
                }
            }
        }
        private string _tipsString = "";
        [XmlIgnore]
        public string TipsString
        {
            get => _tipsString;
            set
            {
                if (!Equals(_tipsString, value))
                {
                    _tipsString = value;
                    OnPropertyChanged(nameof(TipsString));
                }
            }
        }
        private string _previewColorful = "#FFFFFF";
        public string PreviewColorful
        {
            get => _previewColorful;
            set
            {
                if (!Equals(_previewColorful, value))
                {
                    _previewColorful = value;
                    OnPropertyChanged(nameof(PreviewColorful));
                    if (IsAutoGeneratePreview)
                        ProcessScratch();
                }
            }
        }
        public enum GeneratingMode
        {
            Default,
            Handcraft,
            Occulsion
        }
        private int _pathGeneratingMode = 0;
        public int PathGeneratingMode
        {
            get => _pathGeneratingMode;
            set
            {
                if (!Equals(_pathGeneratingMode, value))
                {
                    _pathGeneratingMode = value;
                    OnPropertyChanged(nameof(PathGeneratingMode));
                    OnPropertyChanged(nameof(IsUsingOccusion));
                    OnPropertyChanged(nameof(IsGeneratingHandCraftSketchMode));
                    if (IsAutoGeneratePreview)
                        ProcessScratch();
                }
            }
        }
        private int _theme = 1;
        public int Theme
        {
            get => _theme;
            set
            {
                if (!Equals(_theme, value))
                {
                    _theme = value;
                    OnPropertyChanged(nameof(Theme));
                    ApplicationThemeManager.Apply((ApplicationTheme)value);
                }
            }
        }
        public bool IsUsingOccusion
        {
            get => _pathGeneratingMode == (int)GeneratingMode.Occulsion;
        }
        public bool IsGeneratingHandCraftSketchMode
        {
            get => _pathGeneratingMode == (int)GeneratingMode.Handcraft;
        }
        private bool _isAutoGeneratePreview = true;
        public bool IsAutoGeneratePreview
        {
            get => _isAutoGeneratePreview;
            set
            {
                if (!Equals(_isAutoGeneratePreview, value))
                {
                    _isAutoGeneratePreview = value;
                    OnPropertyChanged(nameof(IsAutoGeneratePreview));
                    if (value) ProcessScratch();
                }
            }
        }
        private bool _isContourMethodEnabled = true;
        public bool IsContourMethodEnabled
        {
            get => _isContourMethodEnabled;
            set
            {
                if (!Equals(_isContourMethodEnabled, value))
                {
                    _isContourMethodEnabled = value;
                    OnPropertyChanged(nameof(IsContourMethodEnabled));
                    RefreshDisplay();
                }
            }
        }
        private bool _isManualMethodEnabled = true;
        public bool IsManualMethodEnabled
        {
            get => _isManualMethodEnabled;
            set
            {
                if (!Equals(_isManualMethodEnabled, value))
                {
                    _isManualMethodEnabled = value;
                    OnPropertyChanged(nameof(IsManualMethodEnabled));
                    RefreshDisplay();
                }
            }
        }
        private bool _isPostProcessEnabled = true;
        public bool IsPostProcessEnabled
        {
            get => _isPostProcessEnabled;
            set
            {
                if (!Equals(_isPostProcessEnabled, value))
                {
                    _isPostProcessEnabled = value;
                    OnPropertyChanged(nameof(IsPostProcessEnabled));
                    RefreshDisplay();
                }
            }
        }
        private bool _isBrightnessMethodEnabled = false;
        public bool IsBrightnessMethodEnabled
        {
            get => _isBrightnessMethodEnabled;
            set
            {
                if (!Equals(_isBrightnessMethodEnabled, value))
                {
                    _isBrightnessMethodEnabled = value;
                    OnPropertyChanged(nameof(IsBrightnessMethodEnabled));
                    RefreshDisplay();
                }
            }
        }
        private bool _isDarknessMode = false;
        public bool IsDarknessMode
        {
            get => _isDarknessMode;
            set
            {
                if (!Equals(_isDarknessMode, value))
                {
                    _isDarknessMode = value;
                    OnPropertyChanged(nameof(IsDarknessMode));
                    RefreshDisplay();
                }
            }
        }
        private bool _isDublicateRemoveEnabled = true;
        public bool IsDublicateRemoveEnabled
        {
            get => _isDublicateRemoveEnabled;
            set
            {
                if (!Equals(_isDublicateRemoveEnabled, value))
                {
                    _isDublicateRemoveEnabled = value;
                    OnPropertyChanged(nameof(IsDublicateRemoveEnabled));
                    RefreshDisplay();
                }
            }
        }
        private bool _isDublicateRemovePlusEnabled = false;
        public bool IsDublicateRemovePlusEnabled
        {
            get => _isDublicateRemovePlusEnabled;
            set
            {
                if (!Equals(_isDublicateRemovePlusEnabled, value))
                {
                    _isDublicateRemovePlusEnabled = value;
                    OnPropertyChanged(nameof(IsDublicateRemovePlusEnabled));
                    RefreshDisplay();
                }
            }
        }

        private bool _isUniformDeduplicationEnabled = true;
        public bool IsUniformDeduplicationEnabled
        {
            get => _isUniformDeduplicationEnabled;
            set
            {
                if (!Equals(_isUniformDeduplicationEnabled, value))
                {
                    _isUniformDeduplicationEnabled = value;
                    OnPropertyChanged(nameof(IsUniformDeduplicationEnabled));

                }
            }
        }

        private float _deduplicationToolAccuracy = 0.8f;

        public float DeduplicationToolAccuracy
        {
            get => _deduplicationToolAccuracy;
            set
            {
                if (!Equals(_deduplicationToolAccuracy, value))
                {
                    _deduplicationToolAccuracy = value;
                    OnPropertyChanged(nameof(DeduplicationToolAccuracy));
                    OnPropertyChanged(nameof(DeduplicationToolGridHeight));
                    OnPropertyChanged(nameof(DeduplicationToolGridWidth));
                    OnPropertyChanged(nameof(DeduplicationToolMaxCount));
                    //RefreshDisplay();//实时更新较卡，已移交MouseUp Event，下同
                }
            }
        }
        private float _deduplicationToolDensity = 0.85f;

        public float DeduplicationToolDensity
        {
            get => _deduplicationToolDensity;
            set
            {
                if (!Equals(_deduplicationToolDensity, value))
                {
                    _deduplicationToolDensity = value;
                    OnPropertyChanged(nameof(DeduplicationToolDensity));
                    OnPropertyChanged(nameof(DeduplicationToolMaxCount));
                    //RefreshDisplay();
                }
            }
        }
        public float DeduplicationToolGridWidth
        {
            get
            {
                /*
                float gridWidth = 1;
                try
                {
                    gridWidth = OriginImageWidth * Math.Pow(0.5, Math.Log2(OriginImageWidth) * _deduplicationToolAccuracy);
                }
                catch (Exception e)
                {

                }
                gridWidth = gridWidth <= 1 ? 2 : gridWidth;
                return gridWidth;*/
                return 1 + (OriginImageWidth / 2f - 1) * (1 - MathF.Pow(_deduplicationToolAccuracy, 0.2f));
            }
        }
        public float DeduplicationToolGridHeight
        {
            get
            {
                /*
                int gridHeight = 2;
                try
                {
                    gridHeight = (int)(OriginImageHeight * Math.Pow(0.5, Math.Log2(OriginImageHeight) * _deduplicationToolAccuracy));
                }
                catch (Exception e)
                {

                }
                gridHeight = gridHeight <= 1 ? 2 : gridHeight;
                return gridHeight;*/
                return 1 + (OriginImageHeight / 2f - 1) * (1 - MathF.Pow(_deduplicationToolAccuracy, 0.2f));
            }
        }
        public int DeduplicationToolMaxCount
        {
            get
            {
                int maxCount = 1;
                try
                {
                    int gridSize = (int)(DeduplicationToolGridWidth * DeduplicationToolGridHeight);
                    maxCount = (int)(gridSize * Math.Pow(0.5, Math.Log2(gridSize) * _deduplicationToolDensity));
                }
                catch (Exception e)
                {

                }
                maxCount = maxCount == 0 ? 1 : maxCount;
                return maxCount;
            }
        }
        private bool _isExcludeMaskEnabled = false;
        public bool IsExcludeMaskEnabled
        {
            get => _isExcludeMaskEnabled;
            set
            {
                if (!Equals(_isExcludeMaskEnabled, value))
                {
                    _isExcludeMaskEnabled = value;
                    OnPropertyChanged(nameof(IsExcludeMaskEnabled));
                    RefreshDisplay();
                }
            }
        }
        private float _excludingRangeMin = 0;
        public float ExcludingRangeMin
        {
            get => _excludingRangeMin;
            set
            {
                if (!Equals(_excludingRangeMin, value))
                {
                    _excludingRangeMin = value;
                    OnPropertyChanged(nameof(ExcludingRangeMin));
                    //RefreshDisplay();
                }
            }
        }
        private float _excludingRangeMax = 255;
        public float ExcludingRangeMax
        {
            get => _excludingRangeMax;
            set
            {
                if (!Equals(_excludingRangeMax, value))
                {
                    _excludingRangeMax = value;
                    OnPropertyChanged(nameof(ExcludingRangeMax));
                    //RefreshDisplay();
                }
            }
        }
        private float _brightnessBaseDensity = 0.16f;
        public float BrightnessBaseDensity
        {
            get => _brightnessBaseDensity;
            set
            {
                if (!Equals(_brightnessBaseDensity, value))
                {
                    _brightnessBaseDensity = value;
                    OnPropertyChanged(nameof(BrightnessBaseDensity));
                    RefreshDisplay();
                }
            }
        }
        private float _brightnessDensityFactor = 0;
        public float BrightnessDensityFactor
        {
            get => _brightnessDensityFactor;
            set
            {
                if (!Equals(_brightnessDensityFactor, value))
                {
                    _brightnessDensityFactor = value;
                    OnPropertyChanged(nameof(BrightnessDensityFactor));
                    RefreshDisplay();
                }
            }
        }
        private float _depthContourDetailFactor = 0;
        public float DepthContourDetailFactor
        {
            get => _depthContourDetailFactor;
            set
            {
                if (!Equals(_depthContourDetailFactor, value))
                {
                    _depthContourDetailFactor = value;
                    OnPropertyChanged(nameof(DepthContourDetailFactor));
                    //RefreshDisplay();已使用DragComplete
                }
            }
        }
        private float _depthContourDensityFactor = 2;
        public float DepthContourDensityFactor
        {
            get => _depthContourDensityFactor;
            set
            {
                if (!Equals(_depthContourDensityFactor, value))
                {
                    _depthContourDensityFactor = value;
                    OnPropertyChanged(nameof(DepthContourDensityFactor));
                    //RefreshDisplay();已使用DragComplete
                }
            }
        }

        private float _brightnessEnhanceGamma = 0f;
        public float BrightnessEnhanceGamma
        {
            get => _brightnessEnhanceGamma;
            set
            {
                if (!Equals(_brightnessEnhanceGamma, value))
                {
                    _brightnessEnhanceGamma = value;
                    OnPropertyChanged(nameof(BrightnessEnhanceGamma));
                    RefreshDisplay();
                }
            }
        }
        public ObservableCollection<MissionWithProgress> ProgressedMissions { get; set; } = new();
        public int ProgressedMissionsCount
        {
            get => ProgressedMissions.Count;
        }
        public MissionWithProgress StartMission(string name)
        {
            MissionWithProgress mission = new MissionWithProgress(name);
            Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                ProgressedMissions.Add(mission);
            }));
            OnPropertyChanged(nameof(ProgressedMissionsCount));
            return mission;
        }

        public void EndMission(MissionWithProgress mission)
        {
            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                ProgressedMissions.Remove(mission);
            }));
            OnPropertyChanged(nameof(ProgressedMissionsCount));
        }
        private float _autoPlayMaxFps = 30;
        public float AutoPlayMaxFps
        {
            get => _autoPlayMaxFps;
            set
            {
                if (!Equals(_autoPlayMaxFps, value))
                {
                    _autoPlayMaxFps = value;
                    OnPropertyChanged(nameof(AutoPlayMaxFps));
                }
            }
        }
        private float _realTimeFps = 0;
        private readonly int _historySize = 10;  // 滑动窗口大小，即存储多少帧的FPS数据
        private readonly Queue<float> _fpsHistory = new Queue<float>();

        [XmlIgnore]
        public float RealTimeFps
        {
            get => _realTimeFps;
            set
            {
                if (!Equals(_realTimeFps, value))
                {
                    _realTimeFps = value;
                    OnPropertyChanged(nameof(RealTimeFps));
                }
            }
        }

        public void UpdateFps(TimeSpan spf)
        {
            // 计算当前帧的FPS
            float currentFps = 1 / (float)spf.TotalSeconds;

            // 将当前帧FPS值添加到队列
            _fpsHistory.Enqueue(currentFps);

            // 保证队列的大小不超过滑动窗口大小
            if (_fpsHistory.Count > _historySize)
            {
                _fpsHistory.Dequeue();
            }

            // 计算滑动平均FPS
            RealTimeFps = _fpsHistory.Average();
        }
        public static string SaveClipboardImage()
        {
            // 检查剪贴板中是否有图像
            if (!Clipboard.ContainsImage())
            {
                return "";
            }

            // 从剪贴板获取 BitmapSource
            BitmapSource bitmapSource = Clipboard.GetImage();

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            string path = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)}\\{Guid.NewGuid().ToString()}.png";
            using (var stream = new FileStream(path, FileMode.Create))
            {
                encoder.Save(stream);
            }

            return path;
        }
        [Obsolete]
        public void LoadImage(string filepath)
        {
            IsNotImporting = false;


            FilePath = filepath;
            OriginalImage = new Mat(filepath);
            float ratio = OriginalImage.Width / OriginalImage.Height;
            if ((ratio < 3/5 || ratio > 5/3) && !DonotShowOriginalImageWarning)
                IsShowBadOriginalImageWarning = true;
            OnPropertyChanged(nameof(OriginImageHeight));
            OnPropertyChanged(nameof(OriginImageWidth));
            OnPropertyChanged(nameof(RadiusMax));
            if (IsModelLoaded)
                DepthImage = DepthEstimation.ProcessImage(OriginalImage);
            else
                DepthImage = new(OriginalImage.Size(), MatType.CV_32FC1);
            UpdateGradient();
            _manualPointsStored = new();
            RefreshDisplay();
            if (IsAutoGeneratePreview)
                ProcessScratch();

            IsNotImporting = true;
        }
        public void UnloadImage()
        {
            FilePath = "";
            OriginalImage?.Dispose();
            OriginalImage = null;
            OnPropertyChanged(nameof(OriginImageHeight));
            OnPropertyChanged(nameof(OriginImageWidth));
            OnPropertyChanged(nameof(RadiusMax));
            //DepthImage = null;
            DisplayImageDepth = null;
            DisplayImageContour = null;
            DisplayImageScratchL = null;
            DisplayImageScratchR = null;
            DisplayImageScratchO = null;
            DisplayImageScratchLine = null;
            DisplayImageScratchStep = null;
            _manualPointsStored = new();
            RefreshDisplay();
        }

        // 异步载入原图
        public async Task LoadImageAsync(string filepath)
        {
            
            if (string.IsNullOrWhiteSpace(filepath)) return;

            IsNotImporting = false;
            var mission = StartMission("载入原图");
            try
            {
                
                FilePath = filepath;

                Mat? loaded = null;
                await Task.Run(() => { loaded = new Mat(filepath); });
                Application.Current.Dispatcher.Invoke(() => OriginalImage = loaded);
                
                // 如果深度估算模型已加载
                if (IsModelLoaded)
                {
                    mission.Progress.Report(0.5);
                    mission.Detail = "进行深度估算";
                    Mat? depth = null;
                    await Task.Run(() => { depth = DepthEstimation!.ProcessImage(loaded!); });
                    Application.Current.Dispatcher.Invoke(() => DepthImage = depth);
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() => DepthImage = new Mat(loaded!.Size(), MatType.CV_32FC1));
                }
                mission.Progress.Report(0.5);


                _manualPointsStored = new();
                RefreshDisplay();
                if (IsAutoGeneratePreview)
                    await ProcessScratch();
            }
            catch (Exception ex)
            {
                ShowTipsTemporarilyAsync($"载入图片失败: {ex.Message}");
                UnloadImage();
            }
            finally
            {
                IsNotImporting = true;
            }
            EndMission(mission);
        }

        private bool _isDepthContourMode = false;
        public bool IsDepthContourMode
        {
            get => _isDepthContourMode;
            set
            {
                if (!Equals(_isDepthContourMode, value))
                {
                    _isDepthContourMode = value;
                    OnPropertyChanged(nameof(IsDepthContourMode));
                    RefreshDisplay();
                }
            }
        }
        Mat _enhanced = new();
        public List<Point> ExtractContours()
        {
            var contours = new List<Point>();

            if (IsDepthContourMode)
            {
                _enhanced?.Dispose();
                //_enhanced = EnhanceContrastGamma(GradientModuleImage, _depthContourDensityFactor);
                _enhanced = AdjustContrastBrightness(GradientModuleImage, -_depthContourDetailFactor * 64 + 128, MathF.Pow(10, _depthContourDensityFactor));
                if (_isBinaryDeNoise)
                    Cv2.Threshold(_enhanced, _enhanced, 128, 255, ThresholdTypes.Binary);
                if (_isShowEnhancedMat)
                    Cv2.ImShow("Preview", _enhanced);
                //Trace.WriteLine(_enhanced.Type().ToString());
                byte[] pixelData = new byte[_enhanced.Total()];
                _enhanced.GetArray(out pixelData);
                int index = 0;
                if (GradientModuleImage == null) return new();
                for (int i = 0; i < GradientModuleImage.Rows; i++)
                {
                    for (int j = 0; j < GradientModuleImage.Cols; j++) // 根据 density 间隔采样
                    {
                        byte probability = pixelData[index++];                        //if(Random.Shared.Next(0,255)<probability)
                        //Trace.WriteLine(Math.Round(probability));
                        //if (r != 0) 
                        //    Trace.WriteLine(r);
                        if (Random.Shared.Next(0, 255) < probability)
                            contours.Add(new Point(j, i)); // 添加采样的轮廓点
                    }
                }
            }
            else
            {
                if (!IsOriginalImageLoaded) return new();

                Mat blurred = new Mat();
                Cv2.GaussianBlur(OriginalImage!, blurred, new OpenCvSharp.Size(_blurFactor * 2 - 1, _blurFactor * 2 - 1),
                    0);

                Mat edges = new Mat();
                Cv2.Canny(blurred, edges, Threshold1, Threshold2, 3, true);

                OpenCvSharp.Point[][] contourPoints;
                HierarchyIndex[] hierarchy;
                Cv2.FindContours(edges, out contourPoints, out hierarchy, RetrievalModes.List,
                    ContourApproximationModes.ApproxSimple);
                for (int i = 0; i < contourPoints.Length; i++)
                {
                    for (int j = 0; j < contourPoints[i].Length; j += _lineDensity) // 根据 density 间隔采样
                    {
                        contours.Add(new Point(contourPoints[i][j])); // 添加采样的轮廓点
                    }
                }
            }
            return contours;
        }
        public Mat AdjustContrastBrightness(Mat input, float a, float b)
        {
            if (input.Empty())
                throw new ArgumentException("输入图像为空");
            a = Math.Clamp(a, 0, 255f);
            //Trace.WriteLine(a+"b:"+b);
            Mat src8u = input;
            if (input.Depth() != MatType.CV_8U)
            {
                src8u = new Mat();
                Mat tmp = new Mat();
                input.ConvertTo(tmp, MatType.CV_32F);
                Cv2.Normalize(tmp, tmp, 0, 255, NormTypes.MinMax);
                tmp.ConvertTo(src8u, MatType.CV_8U);
                tmp.Dispose();
            }

            float p = Math.Max(float.Epsilon, b);
            float epsA = Math.Max(a, float.Epsilon);
            float epsU = Math.Max(255f - a, float.Epsilon);

            Mat debug_curve = Mat.Zeros(MatType.CV_8UC1, 512, 256);
            byte[] lutArr = new byte[256];
            for (int x = 0; x <= 255; x++)
            {
                int y;
                if (x < a)
                {
                    y = (int)(a * Math.Pow(x / epsA, p));
                }
                else if (x > a)
                {
                    y = (int)(255f - (255f - a) * Math.Pow((255f - x) / epsU, p));
                }
                else
                {
                    y = (int)a;
                }
                //Trace.WriteLine(x+":"+y);
                // 数值安全与量化
                lutArr[x] = (byte)Math.Clamp(y, 0, 255);
                debug_curve.Set<byte>(255 - y + x, x, 255);
            }
            lutArr[0] = lutArr[1];
            lutArr[255] = lutArr[254];


            Mat lut = new Mat(1, 256, MatType.CV_8UC1);
            lut.SetArray(lutArr);
            Mat output = new Mat();
            Cv2.LUT(src8u, lut, output);
            //Cv2.ImShow("lut_delta", debug_curve);

            if (!ReferenceEquals(src8u, input))
                src8u.Dispose();
            lut.Dispose();
            Cv2.ConvertScaleAbs(output, output);
            return output;
        }
        public void RefreshDisplay()
        {
            if (!IsOriginalImageLoaded) return;
            _postProcessedPointsCache = new();
            // 应用采样策略逻辑
            _contourPoints = IsContourMethodEnabled ? ExtractContours() : new();
            _brightnessPoints = IsBrightnessMethodEnabled ? GetPointsByLuminance() : new();
            // 绘图
            OnPropertyChanged(nameof(ContourPointCount));
            OnPropertyChanged(nameof(BrightnessPointCount));
            OnPropertyChanged(nameof(ManualPointCount));

            ContoursOverDepthImage = new Mat(OriginalImage!.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 255));



            foreach (var point in SampledPoints)
            {
                Cv2.Circle(ContoursOverDepthImage, point.X, point.Y, 1, Scalar.Red);
            }
            DisplayImageContour = ContoursOverDepthImage.ToWriteableBitmap();
            OnPropertyChanged(nameof(PointCount));

        }
        /*
        public Mat EnhanceContrastGamma(Mat inputImage, float gamma = 1.5f)
        {
            // 创建查找表
            Mat lookUpTable = new Mat(1, 256, MatType.CV_8UC1);
            for (int i = 0; i < 256; i++)
            {
                lookUpTable.Set(0, i, (byte)(Math.Pow(i / 255.0, gamma) * 255.0));
            }

            // 应用查找表
            Mat outputImage = new Mat();
            Cv2.LUT(inputImage, lookUpTable, outputImage);

            return outputImage;
        }*/
        public List<Point> GetPointsByLuminance()
        {

            if (!IsOriginalImageLoaded) return new();

            // 转换为灰度图像
            Mat grayImage = new Mat();
            Cv2.CvtColor(OriginalImage, grayImage, ColorConversionCodes.BGR2GRAY);
            //Mat grayImageEnhanced = EnhanceContrastGamma(grayImage, _brightnessEnhanceGamma);
            Mat grayImageEnhanced = AdjustContrastBrightness(grayImage, -_brightnessDensityFactor * 64 + 128, MathF.Pow(10, _brightnessEnhanceGamma));
            //Cv2.ImShow("tes", grayImageEnhanced);
            // 创建存储结果的点列表
            List<Point> points = new List<Point>();
            int step = (int)(1 / _brightnessBaseDensity);
            // 遍历灰度图的每个像素
            if (IsDarknessMode)
            {
                for (int y = 0; y < grayImageEnhanced.Rows; y += step)
                {
                    for (int x = 0; x < grayImageEnhanced.Cols; x += step)
                    {
                        // 获取当前像素的亮度值 (0-255)
                        byte brightness = grayImageEnhanced.At<byte>(y, x);

                        // 随机采样生成点
                        /*
                        Random random = new Random();
                        if (random.Next(0, 255) > brightness * Math.Exp(_brightnessDensityFactor))
                        {
                            points.Add(new Point(x, y));
                        }*/
                        if (Random.Shared.Next(0, 255) > brightness)
                            points.Add(new Point(x, y));

                    }
                }
            }
            else
            {
                for (int y = 0; y < grayImageEnhanced.Rows; y += step)
                {
                    for (int x = 0; x < grayImageEnhanced.Cols; x += step)
                    {
                        // 获取当前像素的亮度值 (0-255)
                        byte brightness = grayImageEnhanced.At<byte>(y, x);
                        //Trace.WriteLine(brightness);
                        // 随机采样生成点
                        /*Random random = new Random();
                        if (random.Next(0, 255) < brightness * Math.Exp(_brightnessDensityFactor))
                        {
                            points.Add(new Point(x, y));
                        }*/
                        if (Random.Shared.Next(0, 255) < brightness)
                            points.Add(new Point(x, y));

                    }
                }
            }

            return points;
        }
        private async Task ExportDepthAsync()
        {
            // 使用保存对话框选择保存路径
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PNG 文件 (*.png)|*.png",
                DefaultExt = "png",
                AddExtension = true,
                FileName = $"{Path.GetFileNameWithoutExtension(FilePath)}_DepthMap.png"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                var mission = StartMission("Export Depth");
                IsNotImporting = false;
                try
                {
                    if (DepthImage == null)
                    {
                        ShowTipsTemporarilyAsync(LanguageManager.Instance["Tips_Null_Depth"]);
                        return;
                    }

                    string path = saveFileDialog.FileName;
                    await Task.Run(() =>
                    {
                        Mat normalizedDepth = new Mat();
                        Cv2.ConvertScaleAbs(DepthImage, normalizedDepth);
                        Cv2.ImWrite(path, normalizedDepth);
                        normalizedDepth.Dispose();
                    });

                    ShowTipsTemporarilyAsync(LanguageManager.Instance["Tips_Depth_Successfully_Exported"]);
                }
                catch (Exception e)
                {
                    ShowTipsTemporarilyAsync($"{LanguageManager.Instance["Tips_Fail_to_Export_Depth"]}{e.Message}");
                }
                finally
                {
                    IsNotImporting = true;
                    EndMission(mission);
                }
            }
        }
        private bool _isShowBadOriginalImageWarning = false;
        public bool IsShowBadOriginalImageWarning
        {
            get => _isShowBadOriginalImageWarning;
            set
            {
                if (!Equals(_isShowBadOriginalImageWarning, value))
                {
                    _isShowBadOriginalImageWarning = value;
                    OnPropertyChanged(nameof(IsShowBadOriginalImageWarning));
                }
            }
        }
        private bool _donotShowOriginalImageWarning = false;
        public bool DonotShowOriginalImageWarning
        {
            get => _donotShowOriginalImageWarning;
            set
            {
                if (!Equals(_donotShowOriginalImageWarning, value))
                {
                    _donotShowOriginalImageWarning = value;
                    OnPropertyChanged(nameof(DonotShowOriginalImageWarning));
                }
            }
        }
        private bool _isNotImporting = true;
        public bool IsNotImporting
        {
            get => _isNotImporting;
            set
            {
                if (!Equals(_isNotImporting, value))
                {
                    _isNotImporting = value;
                    OnPropertyChanged(nameof(IsNotImporting));
                }
            }
        }

        private void ImportDepth()
        {

            string localized_FILE = LanguageManager.Instance["Globe_File"];
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = $"PNG {localized_FILE} (*.png)|*.png|JPEG {localized_FILE} (*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP {localized_FILE} (*.bmp)|*.bmp|TIFF {localized_FILE} (*.tiff;*.tif)|*.tiff;*.tif|{LanguageManager.Instance["Globe_All"]}{localized_FILE} (*.*)|*.*",
                DefaultExt = "png"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadDepth(openFileDialog.FileName);
            }

        }
        public void LoadDepth(string path)
        {
            IsNotImporting = false;
            try
            {

                Mat loadedDepth = Cv2.ImRead(path, ImreadModes.Grayscale);
                if (loadedDepth.Empty())
                {
                    //MessageBox.Show();
                    ShowTipsTemporarilyAsync(LanguageManager.Instance["Tips_Fail_to_Load_depth"]);

                    return;
                }
                loadedDepth.ConvertTo(loadedDepth, MatType.CV_32FC1);

                // 如果图像尺寸与原始图像不一致，则调整为原始图像的尺寸
                if (loadedDepth.Rows != OriginalImage.Rows || loadedDepth.Cols != OriginalImage.Cols)
                {
                    Mat resizedDepth = new Mat();
                    Cv2.Resize(loadedDepth, resizedDepth, OriginalImage.Size());
                    loadedDepth = resizedDepth;
                }

                DepthImage = loadedDepth;
                ShowTipsTemporarilyAsync(LanguageManager.Instance["Tips_Depth_Successfully_Imported"]);

            }
            catch (Exception e)
            {
                ShowTipsTemporarilyAsync($"{LanguageManager.Instance["Tips_Fail_to_Import_Depth"]}{e.Message}");
            }
            IsNotImporting = true;
        }

        // 异步加载深度图
        public async Task LoadDepthAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            IsNotImporting = false;
            var mission = StartMission("载入深度图");
            try
            {

                Mat loadedDepth = null;
                await Task.Run(() => { loadedDepth = Cv2.ImRead(path, ImreadModes.Grayscale); });

                if (loadedDepth == null || loadedDepth.Empty())
                {
                    ShowTipsTemporarilyAsync(LanguageManager.Instance["Tips_Fail_to_Load_depth"]);
                    return;
                }

                await Task.Run(() => { loadedDepth.ConvertTo(loadedDepth, MatType.CV_32FC1); });

                if (IsOriginalImageLoaded && (loadedDepth.Rows != OriginalImage!.Rows || loadedDepth.Cols != OriginalImage!.Cols))
                {
                    Mat? resized = null;
                    await Task.Run(() =>
                    {
                        resized = new Mat();
                        Cv2.Resize(loadedDepth, resized, OriginalImage.Size());
                        loadedDepth = resized;
                    });
                }

                Application.Current.Dispatcher.Invoke(() => DepthImage = loadedDepth);
                ShowTipsTemporarilyAsync(LanguageManager.Instance["Tips_Depth_Successfully_Imported"]);
            }
            catch (Exception e)
            {
                ShowTipsTemporarilyAsync($"{LanguageManager.Instance["Tips_Fail_to_Import_Depth"]}{e.Message}");
            }
            finally
            {
                IsNotImporting = true;
                EndMission(mission);
            }
        }

        private async Task ExportPointsAsync(string type, string ext = "png")
        {
            if (!IsOriginalImageLoaded)
            {
                ShowTipsTemporarilyAsync("请先选择一张处理图片");
                return;
            }

            // 使用保存对话框选择保存路径（UI 线程）
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = ext == "png" ? "PNG 文件 (*.png)|*.png" : "可缩放矢量图形 SVG 文件 (*.svg)|*.svg",
                DefaultExt = ext,
                AddExtension = true,
                FileName = $"{Path.GetFileNameWithoutExtension(FilePath)}{(type == "a" ? "_A" : "_M")}_PointMap.{ext}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var mission = StartMission("导出点图");
                IsNotImporting = false;
                try
                {
                    string path = saveFileDialog.FileName;

                    if (ext == "png")
                    {
                        // 在后台线程生成点图并保存
                        await Task.Run(() =>
                        {
                            Mat pointMap = new Mat(OriginalImage.Rows, OriginalImage.Cols, MatType.CV_8UC1, new Scalar(255));
                            var points = type == "a" ? SampledPoints : _manualPointsStored.ToList();
                            foreach (var point in points)
                            {
                                if (point.X >= 0 && point.X < pointMap.Cols && point.Y >= 0 && point.Y < pointMap.Rows)
                                {
                                    pointMap.Set(point.Y, point.X, (byte)0);
                                }
                            }
                            Cv2.ImWrite(path, pointMap);
                            pointMap.Dispose();
                        });

                        ShowTipsTemporarilyAsync("点图成功导出。");
                    }
                    else
                    {
                        // 在后台线程生成 SVG 文本并保存
                        await Task.Run(() =>
                        {
                            StringBuilder svgBuilder = new StringBuilder();
                            svgBuilder.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{OriginalImage.Cols}\" height=\"{OriginalImage.Rows}\"> ");
                            svgBuilder.AppendLine($"<rect width=\"100%\" height=\"100%\" fill=\"white\"/>");

                            var points = type == "a" ? SampledPoints : _manualPointsStored.ToList();
                            foreach (var point in points)
                            {
                                svgBuilder.AppendLine($"<circle cx=\"{point.X}\" cy=\"{point.Y}\" r=\"0.5\" fill=\"black\"/>");
                            }

                            svgBuilder.AppendLine("</svg>");
                            File.WriteAllText(path, svgBuilder.ToString());
                        });

                        ShowTipsTemporarilyAsync("SVG点图成功导出。");
                    }
                }
                catch (Exception e)
                {
                    ShowTipsTemporarilyAsync($"导出点图时出错: {e.Message}");
                }
                finally
                {
                    IsNotImporting = true;
                    EndMission(mission);
                }
            }
        }
        public void ImportPoints(string filename)
        {

            try
            {
                List<Point> result = new List<Point>();
                string extension = System.IO.Path.GetExtension(filename).ToLower();

                // SVG 文件处理
                if (extension == ".svg")
                {
                    // 读取SVG文件内容
                    string svgContent = File.ReadAllText(filename);

                    // 使用正则表达式提取所有圆点
                    Regex circleRegex = new Regex(@"<circle[^>]*cx=""([\d\.]+)""[^>]*cy=""([\d\.]+)""[^>]*>",
                                                  RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    var matches = circleRegex.Matches(svgContent);
                    if (matches.Count == 0)
                    {
                        ShowTipsTemporarilyAsync("未找到SVG中的点数据");
                        return;
                    }

                    // 解析坐标点
                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count >= 3)
                        {
                            float cx = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                            float cy = float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                            if (cx >= 0 && cx < OriginalImage.Cols && cy >= 0 && cy < OriginalImage.Rows)
                                result.Add(new Point(cx, cy));
                        }
                    }

                    ShowTipsTemporarilyAsync($"成功从SVG导入 {result.Count} 个点");
                }
                // 图像文件处理（PNG/JPG）
                else
                {
                    Mat pointMap = Cv2.ImRead(filename, ImreadModes.Grayscale);
                    if (pointMap.Empty())
                    {
                        ShowTipsTemporarilyAsync("无法读取点图");
                        return;
                    }

                    pointMap.ConvertTo(pointMap, MatType.CV_8UC1);  // 确保是 CV_8UC1 类型

                    // 如果图像尺寸与原始图像不一致，则调整为原始图像的尺寸
                    if (pointMap.Rows != OriginalImage.Rows || pointMap.Cols != OriginalImage.Cols)
                    {
                        Mat resizedPointMap = new Mat();
                        Cv2.Resize(pointMap, resizedPointMap, OriginalImage.Size());
                        pointMap = resizedPointMap;
                    }

                    // 查找黑色点
                    for (int y = 0; y < pointMap.Rows; y++)
                    {
                        for (int x = 0; x < pointMap.Cols; x++)
                        {
                            byte pixelValue = pointMap.At<byte>(y, x);
                            // 考虑抗锯齿：低于200灰度的都视为点
                            if (pixelValue < 200)
                            {
                                result.Add(new Point(x, y));
                            }
                        }
                    }

                    ShowTipsTemporarilyAsync($"成功从点图导入 {result.Count} 个点");
                }

                NewOperation(result);
            }
            catch (Exception e)
            {
                ShowTipsTemporarilyAsync($"导入点图时出错: {e.Message}");
            }

        }
        // 异步导入点图或SVG
        public async Task ImportPointsAsync(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename)) return;

            IsNotImporting = false;
            try
            {
                List<Point> result = new List<Point>();
                string extension = System.IO.Path.GetExtension(filename).ToLower();

                if (extension == ".svg")
                {
                    string svgContent = await Task.Run(() => File.ReadAllText(filename));
                    Regex circleRegex = new Regex(@"<circle[^>]*cx=""([\d\.]+)""[^>]*cy=""([\d\.]+)""[^>]*>",
                                                  RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    var matches = circleRegex.Matches(svgContent);
                    if (matches.Count == 0)
                    {
                        ShowTipsTemporarilyAsync("未找到SVG中的点数据");
                        return;
                    }

                    foreach (Match match in matches)
                    {
                        if (match.Groups.Count >= 3)
                        {
                            float cx = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                            float cy = float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                            if (IsOriginalImageLoaded && cx >= 0 && cx < OriginalImage!.Cols && cy >= 0 && cy < OriginalImage.Rows)
                                result.Add(new Point(cx, cy));
                        }
                    }

                    ShowTipsTemporarilyAsync($"成功从SVG导入 {result.Count} 个点");
                }
                else
                {
                    Mat pointMap = null;
                    await Task.Run(() => { pointMap = Cv2.ImRead(filename, ImreadModes.Grayscale); });
                    if (pointMap == null || pointMap.Empty())
                    {
                        ShowTipsTemporarilyAsync("无法读取点图");
                        return;
                    }

                    await Task.Run(() => { pointMap.ConvertTo(pointMap, MatType.CV_8UC1); });

                    if (IsOriginalImageLoaded && (pointMap.Rows != OriginalImage!.Rows || pointMap.Cols != OriginalImage!.Cols))
                    {
                        Mat resized = null;
                        await Task.Run(() => { resized = new Mat(); Cv2.Resize(pointMap, resized, OriginalImage.Size()); pointMap = resized; });
                    }

                    await Task.Run(() =>
                    {
                        for (int y = 0; y < pointMap.Rows; y++)
                        {
                            for (int x = 0; x < pointMap.Cols; x++)
                            {
                                byte pixelValue = pointMap.At<byte>(y, x);
                                if (pixelValue < 200)
                                {
                                    result.Add(new Point(x, y));
                                }
                            }
                        }
                    });

                    ShowTipsTemporarilyAsync($"成功从点图导入 {result.Count} 个点");
                }

                NewOperation(result);
            }
            catch (Exception e)
            {
                ShowTipsTemporarilyAsync($"导入点图时出错: {e.Message}");
            }
            finally
            {
                IsNotImporting = true;
            }
        }
        public void HandleExit()
        {
            if (IsUsingLastConfigEveryTime)
                ExportConfig("last_config.json");

        }
        public void ExportConfig(string path)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(CoreProcesserViewModel));
                using (StreamWriter fileWriter = new StreamWriter(path))
                {
                    serializer.Serialize(fileWriter, this);
                }

                ShowTipsTemporarilyAsync("配置成功导出。");
            }
            catch (Exception e)
            {
                ShowTipsTemporarilyAsync($"导出配置时出错: {e.Message}");
            }
        }
        public void ImportConfig(string path)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(CoreProcesserViewModel));
                using (FileStream fs = new FileStream(path, FileMode.Open))
                {
                    CoreProcesserViewModel ipp = (CoreProcesserViewModel)serializer.Deserialize(fs);
                    ipp.mainWindow = mainWindow;
                    mainWindow.CoreProcesser = ipp;
                    ipp.DepthEstimation = DepthEstimation;
                    DepthEstimation = null;
                    ipp.OccusionPathHelper = OccusionPathHelper;
                    OccusionPathHelper = null;
                    //ipp.RefreshBinding();
                }

                ShowTipsTemporarilyAsync($"成功打开配置文件");
            }
            catch (Exception e)
            {
                ShowTipsTemporarilyAsync($"打开配置文件时出错: {e.Message}");
            }
        }
        private void RefreshBinding()
        {
            // 使用反射遍历所有属性并手动触发 PropertyChanged
            var properties = this.GetType().GetProperties()
                .Where(p => p.CanRead && p.CanWrite); // 确保是公共属性

            foreach (var property in properties)
            {
                // 手动触发 PropertyChanged 事件
                OnPropertyChanged(property.Name);
            }
        }

        private void ChangePreviewExecute(string t)
        {
            switch (t)
            {
                case "0":
                    PreviewT = 0;
                    break;
                case "1":
                    PreviewT -= 0.05f;
                    break;
                case "3":
                    PreviewT = 1;
                    break;
                case "2":
                    PreviewT += 0.05f;
                    break;
                case "5":
                    PreviewStep = 0;
                    break;
                case "6":
                    PreviewStep -= 0.05f;
                    break;
                case "7":
                    PreviewStep = 0.5f;
                    break;
                case "8":
                    PreviewStep += 0.05f;
                    break;
                case "9":
                    PreviewStep = 1;
                    break;
                default:
                    PreviewT = 0.5f;
                    break;
            }
        }
        public void ProcessDepth()
        {
            if (!IsOriginalImageLoaded) return;
            if (!IsModelLoaded) return;
            DepthImage = DepthEstimation.ProcessImage(OriginalImage);
            UpdateGradient();

            //Cv2.ImShow("r", DepthImage);
        }

        // 异步版本：使用模型处理并更新深度图与梯度
        public async Task ProcessDepthAsync()
        {
            if (!IsOriginalImageLoaded) return;
            if (!IsModelLoaded) return;

            IsNotImporting = false;
            try
            {
                Mat? depth = null;
                await Task.Run(() => { depth = DepthEstimation!.ProcessImage(OriginalImage); });
                Application.Current.Dispatcher.Invoke(() => DepthImage = depth);
                
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
            }
            finally
            {
                IsNotImporting = true;
            }
        }
        private bool CheckOverflow()
        {
            if (PointCount > MaximumPointCount)
            {
                mainWindow.WarningFlyout.Show();
            }
            return PointCount < MaximumPointCount;

        }
        private Point? _startMousePoint;
        [XmlIgnore]
        public Point? StartMousePoint
        {
            get => _startMousePoint;
            set
            {
                if (!Equals(_startMousePoint, value))
                {
                    _startMousePoint = value;
                    OnPropertyChanged(nameof(StartMousePoint));
                    OnPropertyChanged(nameof(StartMousePointX));
                    OnPropertyChanged(nameof(StartMousePointY));
                    OnPropertyChanged(nameof(IsManualEditing));
                }
            }
        }
        public float EraserDiameter
        {
            get => 2 * EraserRadius;
        }
        public float SmearDiameter
        {
            get => 2 * SmearRadius;
        }
        public float DraftDiameter
        {
            get => 2 * DraftRadius;
        }
        public int StartMousePointX
        {
            get => StartMousePoint.GetValueOrDefault(new Point(0, 0)).X;
        }
        public int StartMousePointY
        {
            get => StartMousePoint.GetValueOrDefault(new Point(0, 0)).Y;
        }

        public bool IsManualEditing => StartMousePoint != null;
        float _penPathLength = 0;
        Point? _lastPoint;
        List<Point> _draftingPoints = new();
        List<Point> _erasingPoints = new();
        List<Point> _drawingPoints = new();
        Vec2f _startMovingPosition = new(0, 0);
        Point? _movingLastPoint;




        public void ProcessMovingView(bool? isStartMoving = true)
        {
            if (isStartMoving == true) // start moving
            {
                _startMovingPosition = new(_previewX, _previewY);
                _movingLastPoint = MouseWindowPosition;
                //Trace.WriteLine($"start moving {_previewX},{_previewY}");
            }
            else if (isStartMoving == false) // stop moving
            {
                _movingLastPoint = null;
            }
            else // else is voving
            {
                if (_movingLastPoint == null) return;
                Vec2f delta = MouseWindowPosition - _movingLastPoint.Value;
                Vec2f newLocation = _startMovingPosition + delta / PreviewScale;
                PreviewX = newLocation.Item0;
                PreviewY = newLocation.Item1;
                //Trace.WriteLine($"move to {_previewX},{_previewY}");

            }
        }
        public void ProcessManual(bool? isMouseDown = null)
        {
            if (isMouseDown == null) // moving
            {
                if (!_isManualMethodEnabled) return;

                if (IsManualEditing)
                {
                    switch (ManualTool)
                    {
                        case 1:
                            _penPathLength += MathF.Sqrt(MathF.Pow(MousePixelPosition.X - _lastPoint.Value.X, 2) + MathF.Pow(MousePixelPosition.Y - _lastPoint.Value.Y, 2));
                            if (_penPathLength > 1 / _manualDensity)
                            {
                                /*
                                _manualPointsStored.Add(MousePixelPosition);
                                NewOperation([MousePixelPosition]);
                                //LastStepAmount ++;
                                _penPathLength -= 1 / _manualDensity;*/
                                _drawingPoints.Add(MousePixelPosition);
                                _penPathLength -= 1 / _manualDensity;
                            }
                            break;
                        case 2:
                            var p0 = GetPointsInRadius(MousePixelPosition, EraserRadius);
                            if (p0.Count != 0)
                            {
                                _erasingPoints = _erasingPoints?.Union(p0.Select(pd => pd.Point).ToList()).ToList();
                            }
                            break;
                        case 4://smear
                            var p1 = GetPointsInRadius(MousePixelPosition, SmearRadius);
                            if (p1.Count != 0)
                            {
                                Point movedistance = new(MousePixelPosition.X - _lastPoint.Value.X, MousePixelPosition.Y - _lastPoint.Value.Y);
                                List<Point> offsets = new();
                                foreach (var pd in p1)
                                {
                                    offsets.Add(movedistance * (1 - Math.Pow(pd.Distance / _smearRadius, 2)) * _smearStrength);
                                }
                                NewOperation(p1.Select(pd => pd.Point).ToList(), offsets);
                            }
                            break;
                        case 5://draft
                            var p2 = GetPointsInRadius(MousePixelPosition, DraftRadius);
                            if (p2.Count != 0)
                            {
                                _draftingPoints = _draftingPoints?.Union(p2.Select(pd => pd.Point).ToList()).ToList();
                            }
                            break;


                    }
                    _lastPoint = MousePixelPosition;
                }
            }
            else if (isMouseDown == true) // start
            {
                if (!_isManualMethodEnabled) return;

                //LastStepAmount = 0;
                StartMousePoint = MousePixelPosition;
                _lastPoint = MousePixelPosition;
                _penPathLength = 0;
                _drawingPoints.Clear();
                _erasingPoints.Clear();
                _draftingPoints.Clear();
            }
            else //end
            {
                if (!_isManualMethodEnabled) return;
                if (IsManualEditing)
                {
                    switch (ManualTool)
                    {
                        case 1:
                            {
                                if (_drawingPoints.Count != 0)
                                {
                                    NewOperation(_drawingPoints);
                                    _drawingPoints.Clear();
                                }
                                break;
                            }
                        case 3:
                            {
                                var interpolated =
                                    GetUniformInterpolatedPoints(StartMousePoint.Value, MousePixelPosition, _manualDensity);
                                if (interpolated.Count == 2)
                                {
                                    NewOperation([StartMousePoint.Value]);
                                    //_manualPointsStored.Add(StartMousePoint.Value);
                                    //LastStepAmount = 1;
                                }
                                else
                                {
                                    NewOperation(interpolated);
                                    //_manualPointsStored.AddRange(interpolated);
                                    //LastStepAmount = interpolated.Count;
                                }
                            }
                            break;
                        case 2:
                            {
                                /*if (_erasingPoints.Count != 0)
                                {
                                    if (_isSmartEraserEnabled)
                                    {
                                        float averageDepth = 0;
                                        Dictionary<Point, float> pointDepth = new();
                                        foreach(var point in _erasingPoints)
                                        {
                                            float d = DepthImage.Get<float>(point.Y, point.X);
                                            pointDepth[point] = d;
                                            averageDepth += d;
                                        }
                                        averageDepth /= _erasingPoints.Count;
                                        //取出depth大于averageDepth的points（List）赋值给_erasingPoints
                                    }
                                    NewOperation(_erasingPoints, false);
                                    _erasingPoints.Clear();
                                }*/
                                if (_erasingPoints.Count != 0)
                                {
                                    if (_isSmartEraserEnabled)
                                    {
                                        if (_erasingPoints.Count > 1)
                                        {
                                            // 并行计算深度值
                                            var pointsWithDepth = new (Point Point, float Depth)[_erasingPoints.Count];
                                            Parallel.For(0, _erasingPoints.Count, i =>
                                            {
                                                var point = _erasingPoints[i];
                                                pointsWithDepth[i] = (point, DepthImage.Get<float>(point.Y, point.X));
                                            });

                                            // 并行计算平均值（使用 SIMD 优化）
                                            float sum = 0;
                                            foreach (var (_, depth) in pointsWithDepth)
                                            {
                                                sum += depth;
                                            }

                                            float averageDepth = sum / pointsWithDepth.Length;
                                            Trace.WriteLine($"avg:{averageDepth}");

                                            // 并行过滤
                                            var filteredPoints =
                                                new System.Collections.Concurrent.ConcurrentBag<Point>();
                                            Parallel.ForEach(pointsWithDepth, item =>
                                            {
                                                if (item.Depth < averageDepth)
                                                    filteredPoints.Add(item.Point);
                                            });

                                            _erasingPoints = [.. filteredPoints];
                                        }
                                    }
                                    else if (_eraserUpper - _eraserLower != 255)
                                    {
                                        var pointsWithDepth = new (Point Point, float Depth)[_erasingPoints.Count];
                                        Parallel.For(0, _erasingPoints.Count, i =>
                                        {
                                            var point = _erasingPoints[i];
                                            pointsWithDepth[i] = (point, DepthImage.Get<float>(point.Y, point.X));
                                        });
                                        var filteredPoints =
                                            new System.Collections.Concurrent.ConcurrentBag<Point>();
                                        Parallel.ForEach(pointsWithDepth, item =>
                                        {
                                            if (_eraserLower <= item.Depth && item.Depth <= _eraserUpper)
                                                filteredPoints.Add(item.Point);
                                        });
                                        _erasingPoints = [.. filteredPoints];
                                    }

                                    NewOperation(_erasingPoints, false);
                                    _erasingPoints.Clear();
                                }
                            }
                            break;
                        case 5:
                            {
                                if (_draftingPoints.Count != 0)
                                {
                                    List<Point> offsets = new();
                                    List<Point> points = new();
                                    foreach (var p in _draftingPoints)
                                    {
                                        points.Add(p);
                                        Vec2f v = GetGradientDirection(p) * DraftStrength;
                                        offsets.Add(new Point(
                                            v.Item0,
                                            v.Item1
                                        ));
                                    }

                                    NewOperation(points, offsets);
                                    _draftingPoints.Clear();
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
                StartMousePoint = null;
                _lastPoint = null;
                _penPathLength = 0;
            }
        }


        public List<PointDistance> GetPointsInRadius(Point center, float radius)
        {
            if (_manualPointsStored == null)
                return new();

            return _manualPointsStored.Where(point =>
                {
                    // 计算当前点与圆心之间的距离
                    float distance = MathF.Sqrt(MathF.Pow(point.X - center.X, 2) + MathF.Pow(point.Y - center.Y, 2));
                    return distance < radius;
                })
                .Select(point =>
                {
                    // 返回一个包含点和距离的对象
                    float distance = MathF.Sqrt(MathF.Pow(point.X - center.X, 2) + MathF.Pow(point.Y - center.Y, 2));
                    return new PointDistance(point, distance);
                })
                .ToList();
        }
        private static List<Point> GetUniformInterpolatedPoints(Point p1, Point p2, float density)
        {
            List<Point> points = new List<Point>();
            points.Add(p1);
            // 计算两点之间的欧几里得距离
            float distance = MathF.Sqrt(MathF.Pow(p2.X - p1.X, 2) + MathF.Pow(p2.Y - p1.Y, 2));

            // 根据密度计算插值点数量（不包括起点和终点）
            int numberOfPoints = (int)(distance * density);

            for (int i = 1; i <= numberOfPoints; i++)
            {
                float t = (float)i / (numberOfPoints + 1); // 计算比例 t
                float x = (1 - t) * p1.X + t * p2.X;
                float y = (1 - t) * p1.Y + t * p2.Y;

                points.Add(new Point(x, y));
            }
            points.Add(p2);
            return points;
        }

        //根据深度图梯度偏移所有点
        private void GradientDraftExecute()
        {

            if (_manualPointsStored.Count == 0)
                return;
            List<Point> offsets = new();
            List<Point> points = new(_manualPointsStored);
            foreach (var p in _manualPointsStored)
            {
                Vec2f v = GetGradientDirection(p) * DraftStrength;
                offsets.Add(new Point(
                    v.Item0,
                    v.Item1
                ));
            }
            NewOperation(points, offsets);

        }
        private void DeduplicationExecute()
        {

            if (_manualPointsStored.Count == 0)
                return;
            Dictionary<(int, int), List<Point>> gridMap = new Dictionary<(int, int), List<Point>>();

            // 4. 将每个点映射到对应的网格
            foreach (var point in _manualPointsStored)
            {
                int gridX = (int)(point.X / DeduplicationToolGridWidth);
                int gridY = (int)(point.Y / DeduplicationToolGridHeight);
                var gridKey = (gridX, gridY);

                if (!gridMap.ContainsKey(gridKey))
                {
                    gridMap[gridKey] = new List<Point>();
                }

                gridMap[gridKey].Add(point);
            }

            // 5. 遍历每个网格，检查点数是否超过理想个数
            List<Point> pointsToRemove = new List<Point>();
            if (IsUniformDeduplicationEnabled)
            {
                foreach (var grid in gridMap)
                {
                    List<Point> gridPoints = grid.Value;

                    // 如果网格中的点数超过理想个数
                    if (gridPoints.Count > DeduplicationToolMaxCount)
                    {
                        // 随机选择多余的点进行删除
                        int pointsToDelete = gridPoints.Count - DeduplicationToolMaxCount;
                        for (int i = 0; i < pointsToDelete; i++)
                        {

                            int indexToRemove = Random.Shared.Next(gridPoints.Count);
                            pointsToRemove.Add(gridPoints[indexToRemove]);
                            gridPoints.RemoveAt(indexToRemove);
                        }
                    }
                }
            }
            else
            {
                foreach (var grid in gridMap)
                {
                    List<Point> gridPoints = grid.Value;
                    int pointsToDelete = (int)(gridPoints.Count * DeduplicationToolDensity);
                    for (int i = 0; i < pointsToDelete; i++)
                    {

                        int indexToRemove = Random.Shared.Next(gridPoints.Count);
                        pointsToRemove.Add(gridPoints[indexToRemove]);
                        gridPoints.RemoveAt(indexToRemove);
                    }

                }
            }

            NewOperation(pointsToRemove, false);

        }

        private void UpdateGradient()
        {
            if (DepthImage == null) return;

            //Mat depthFloat = new Mat();
            //DepthImage.ConvertTo(depthFloat, MatType.CV_32F);

            // 计算 X、Y 梯度
            Mat gradX = new Mat();
            Mat gradY = new Mat();
            Cv2.Sobel(DepthImage, gradX, MatType.CV_32F, 1, 0, ksize: 3);
            Cv2.Sobel(DepthImage, gradY, MatType.CV_32F, 0, 1, ksize: 3);

            // 计算梯度方向存储到GradientImage里
            var merged = new Mat();
            Cv2.Merge(new[] { gradX, gradY }, merged);
            //GradientImage?.Dispose(); //属性set方法已自带
            GradientImage = merged;

            var gradientMagnitude = new Mat();
            Cv2.Magnitude(gradX, gradY, gradientMagnitude);
            Cv2.ConvertScaleAbs(gradientMagnitude, gradientMagnitude);
            GradientModuleImage = gradientMagnitude;
            Trace.WriteLine(gradientMagnitude.Type().ToString());
            //Cv2.MinMaxLoc(gradientMagnitude, out double min, out double max);
            //Trace.WriteLine(min + " / " + max);
            //Cv2.ImShow("t", gradientMagnitude);

            /*Cv2.Sobel(gradientMagnitude, gradX, MatType.CV_32F, 1, 0, ksize: 3);
            Cv2.Sobel(gradientMagnitude, gradY, MatType.CV_32F, 0, 1, ksize: 3);

            var merged = new Mat();
            Cv2.Merge(new[] { gradX, gradY }, merged);
            Cv2.Magnitude(gradX, gradY, gradientMagnitude);
            Cv2.ConvertScaleAbs(gradientMagnitude, gradientMagnitude); 
            Cv2.GaussianBlur(gradientMagnitude, gradientMagnitude, new OpenCvSharp.Size(31, 31), 30, 30);
            Cv2.MinMaxLoc(gradientMagnitude, out double min, out double max);
            Trace.WriteLine(min + " / " + max);
            Cv2.Normalize(gradientMagnitude, gradientMagnitude);
            Cv2.MinMaxLoc(gradientMagnitude, out double min1, out double max1);
            Trace.WriteLine(min1 + " / " + max1);

            Cv2.ImShow("2", gradientMagnitude);
            GradientImage?.Dispose();
            GradientImage = merged;*/

        }

        // 异步版梯度计算：在后台线程计算然后在 UI 线程设置属性
        private async Task UpdateGradientAsync()
        {
            if (DepthImage == null) return;

            var mission = StartMission("更新深度梯度");
            Mat? gradMerged = null;
            Mat? gradMagnitude = null;

            await Task.Run(() =>
            {
                Mat depthCopy = DepthImage.Clone();
                Mat gradX = new Mat();
                Mat gradY = new Mat();
                Cv2.Sobel(depthCopy, gradX, MatType.CV_32F, 1, 0, ksize: 3);
                Cv2.Sobel(depthCopy, gradY, MatType.CV_32F, 0, 1, ksize: 3);
                gradMerged = new Mat();
                Cv2.Merge(new[] { gradX, gradY }, gradMerged);

                gradMagnitude = new Mat();
                Cv2.Magnitude(gradX, gradY, gradMagnitude);
                Cv2.ConvertScaleAbs(gradMagnitude, gradMagnitude);

                gradX.Dispose();
                gradY.Dispose();
                depthCopy.Dispose();
            });

            Application.Current.Dispatcher.Invoke(() =>
            {
                GradientImage = gradMerged;
                GradientModuleImage = gradMagnitude;
                //Trace.WriteLine(gradMagnitude.Type().ToString());
            });
            EndMission(mission);
        }

        private Vec2f GetGradientDirection(Point pos)
        {
            if (GradientImage == null || GradientImage.Empty())
                throw new InvalidOperationException("请先调用 UpdateGradient() 计算梯度方向。");
            Vec2f r = new(0, 0);
            if (pos.X >= GradientImage.Cols || pos.X < 0 || pos.Y < 0 || pos.Y >= GradientImage.Rows)
                return r;
            r = GradientImage.Get<Vec2f>(pos.Y, pos.X);

            return r;

        }
        public static T MatSafeGet<T>(Mat m, int x, int y, T defaultValue) where T : struct
        {
            T r = defaultValue;
            if (x >= m.Cols || x < 0 || y < 0 || y >= m.Rows)
                return r;
            r = m.Get<T>(y, x);

            return r;
        }
        private class Operation
        {
            private int maxWidth = 0;
            private int maxHeight = 0;
            private HashSet<Point> manualPointsCheckPoint;
            private HashSet<Point> manualPoints;
            private List<Point> _points = null;
            //public List<int> PointIndice { get; }
            public bool? IsAddOperation { get; }  // 修改为 bool?，null 表示 Move 操作
            public List<Point>? Offset { get; }  // 移动操作的偏移量，null 时表示没有偏移

            // 构造函数：支持 Add 操作或 Remove 操作
            /*public Operation(List<Point> manualPointsStored,List<Point> points, bool isAddOperation,int maxW,int maxH)
            {
                maxWidth = maxW;
                maxHeight = maxH;
                manualPoints = manualPointsStored;
                manualPointsSet = new HashSet<Point>(manualPointsStored);
                _points = new List<Point>(points);
                IsAddOperation = isAddOperation;  // Add 操作时传入 true 或 false
                Offset = null;  // 没有偏移
            }*/

            // 移动 操作
            public Operation(HashSet<Point> manualPointsStored, List<Point> points, List<Point> offset, int maxW, int maxH)
            {
                manualPointsCheckPoint = new HashSet<Point>(manualPointsStored);
                maxWidth = maxW;
                maxHeight = maxH;
                manualPoints = manualPointsStored;
                _points = new(points);
                IsAddOperation = null;  // null 表示 Move 操作
                Offset = offset;  // 设置偏移量
            }

            // 增/删 操作
            public Operation(HashSet<Point> manualPointsStored, List<Point> points, bool isAddOperation, int maxW, int maxH)
            {
                manualPointsCheckPoint = new HashSet<Point>(manualPointsStored);
                maxWidth = maxW;
                maxHeight = maxH;
                manualPoints = manualPointsStored;
                _points = new(points);
                IsAddOperation = isAddOperation;
                Offset = null;  // 没有偏移
            }

            // 执行 增/删/移 操作
            public void Execute()
            {
                if (IsAddOperation.HasValue)
                {
                    /*if (IsAddOperation.Value)
                    {
                        manualPoints.AddRange(_points);
                    }
                    else
                    {
                        //manualPoints.RemoveAll(p => _points.Contains(p));
                        foreach(var point in _points)
                        {
                            manualPoints.Remove(point);
                        }
                    }*/
                    if (IsAddOperation.Value)// add
                    {
                        manualPoints.UnionWith(_points);
                    }
                    else// remove
                    {
                        manualPoints.ExceptWith(_points);
                    }
                }
                else // Move
                {
                    if (Offset.Count == _points.Count)
                    {
                        manualPoints.ExceptWith(_points);
                        HashSet<Point> movedPoints = new HashSet<Point>();
                        for (int i = 0; i < _points.Count; i++)
                        {
                            // 移动每个点
                            float x = Math.Clamp(_points[i].Xf + Offset[i].Xf, 0, maxWidth);
                            float y = Math.Clamp(_points[i].Yf + Offset[i].Yf, 0, maxHeight);
                            Offset[i] = new(x - _points[i].Xf, y - _points[i].Yf);
                            movedPoints.Add(new Point(x, y));
                        }
                        manualPoints.UnionWith(movedPoints);
                    }
                }
            }

            // 执行 Move 操作
            public void Undo()
            {
                manualPoints.Clear();
                manualPoints.UnionWith(manualPointsCheckPoint);

            }
        }


        private readonly Stack<Operation> _undoStack = new Stack<Operation>();
        private readonly Stack<Operation> _redoStack = new Stack<Operation>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="points"></param>
        /// <param name="isAddOperation">true:Add;false:Remove;</param>
        public void NewOperation(List<Point> points, bool isAddOperation = true)
        {
            if (points.Count == 0) return;
            // 记录操作
            var o = new Operation(_manualPointsStored, points, isAddOperation, OriginImageWidth, OriginImageHeight);
            o.Execute();
            _undoStack.Push(o);
            _redoStack.Clear(); // 每次新操作后清空 redo 栈

            RefreshDisplay();
        }
        public void NewOperation(List<Point> points, List<Point> offsets)
        {

            // 记录操作
            var o = new Operation(_manualPointsStored, points, offsets, OriginImageWidth, OriginImageHeight);
            o.Execute();
            _undoStack.Push(o);
            _redoStack.Clear(); // 每次新操作后清空 redo 栈

            RefreshDisplay();
        }
        public void ConvertToManualPoint(string type)
        {
            switch (type)
            {
                case "c": // contour
                    NewOperation(_contourPoints);
                    IsContourMethodEnabled = false;
                    IsManualMethodEnabled = true;
                    break;
                case "b": // brightness
                    NewOperation(_brightnessPoints);
                    IsBrightnessMethodEnabled = false;
                    IsManualMethodEnabled = true;
                    break;
            }
        }
        public void Undo()
        {
            if (_undoStack.Count == 0) return;

            var operation = _undoStack.Pop();
            operation.Undo();

            _redoStack.Push(operation);
            RefreshDisplay();

        }

        public void Redo()
        {
            if (_redoStack.Count == 0) return;

            var operation = _redoStack.Pop();
            operation.Execute();

            _undoStack.Push(operation);
            RefreshDisplay();

        }
        public async Task ProcessScratch()
        {
            if (!IsOriginalImageLoaded) return;
            if (!CheckOverflow()) return;
            ClearAllCache();
            Mat scratchImageL = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));//起点
            Mat scratchImageR = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));//终点
            Mat scratchImageO = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));//原点
            Mat scratchImageLine = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));//轨迹
            var mission = StartMission("构建预览");
            try
            {
                IsNotProcessingSvg = false;
                /*
                if (IsGeneratingHandCraftSketchMode) 
                    await SvgPainter.PreviewPathArc(SampledPoints, DepthImage, ZeroDepth, IgnoreZeroDepthDistance, RadiusFactor, AngleFactor, LayerCount, PreviewDense, scratchImageL, scratchImageR, scratchImageO, scratchImageLine, IsPreviewingLineDensity);
                else
                    await SvgPainter.PreviewPath(SampledPoints, DepthImage, ZeroDepth, IgnoreZeroDepthDistance, AFactor, BFactor, PreviewDense, scratchImageL, scratchImageR, scratchImageO, scratchImageLine, IsPreviewingLineDensity);
                */

                switch (PathGeneratingMode)
                {
                    case (int)GeneratingMode.Handcraft:
                        await SvgPainter.PreviewPathArc(SampledPoints, DepthImage, ZeroDepth, IgnoreZeroDepthDistance, RadiusFactor, AngleFactor, LayerCount, PreviewDense, scratchImageL, scratchImageR, scratchImageO, scratchImageLine, IsPreviewingLineDensity);
                        break;
                    case (int)GeneratingMode.Occulsion:
                        await OccusionPathHelper.BuildPathPreview(mission, SampledPoints, DepthImage, ZeroDepth, IgnoreZeroDepthDistance, AFactor, BFactor, 20, scratchImageL, scratchImageR, scratchImageO, scratchImageLine, IsPreviewingLineDensity);
                        break;
                    default:
                        await SvgPainter.PreviewPath(SampledPoints, DepthImage, ZeroDepth, IgnoreZeroDepthDistance, AFactor, BFactor, PreviewDense, scratchImageL, scratchImageR, scratchImageO, scratchImageLine, IsPreviewingLineDensity);
                        break;
                }
                DisplayImageScratchL = scratchImageL.ToWriteableBitmap();
                DisplayImageScratchR = scratchImageR.ToWriteableBitmap();
                DisplayImageScratchO = scratchImageO.ToWriteableBitmap();
                DisplayImageScratchLine = ApplyColorMap(scratchImageLine).ToWriteableBitmap();
                UpdateDisplayImageScratchStep();
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
            }
            finally
            {
                IsNotProcessingSvg = true;
                EndMission(mission);
            }
        }
        public void ClearAllCache()
        {
            foreach (var kvp in ScratchImageStepCache)
            {
                //kvp.Value?.Freeze();
            }
            ScratchImageStepCache.Clear();
        }
        [XmlIgnore]
        public Dictionary<string, BitmapSource> ScratchImageStepCache = new Dictionary<string, BitmapSource>();

        public async Task<BitmapSource?> ProcessAndCacheScratchTick(float tick)
        {
            if (!IsOriginalImageLoaded || !CheckOverflow())
                return null;

            //if(PathGeneratingMode==(int)GeneratingMode.Occulsion)
            //    OccusionPathHelper.debug(tick);
            // 直接从缓存获取WriteableBitmap（如果存在）
            if (ScratchImageStepCache.TryGetValue(tick.ToString("0.000"), out BitmapSource cachedBitmap))
            {
                //Trace.WriteLine($"CacheGot{tick:0.000}");
                return cachedBitmap;
            }

            using (Mat scratchImageStep = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0)))
            {
                try
                {

                    //Trace.WriteLine($"new{tick:0.000}");
                    IsNotProcessingSvg = false;

                    switch (PathGeneratingMode)
                    {
                        case (int)GeneratingMode.Handcraft:
                            await SvgPainter.PreviewPathArc(
                            SampledPoints,
                            DepthImage,
                            OriginalImage,
                            scratchImageStep,
                            tick,
                            ZeroDepth,
                            IgnoreZeroDepthDistance,
                            RadiusFactor,
                            AngleFactor,
                            LayerCount,
                            PreviewDense,
                            PreviewColorful);
                            break;
                        case (int)GeneratingMode.Occulsion:
                            await OccusionPathHelper.PreviewPath(
                                SampledPoints,
                                DepthImage,
                                OriginalImage,
                                scratchImageStep,
                                tick,
                                ZeroDepth,
                                IgnoreZeroDepthDistance,
                                AFactor,
                                BFactor,
                                20,
                                PreviewColorful);
                            break;
                        default:
                            await SvgPainter.PreviewPath(
                             SampledPoints,
                             DepthImage,
                             OriginalImage,
                             scratchImageStep,
                             tick,
                             ZeroDepth,
                             IgnoreZeroDepthDistance,
                             AFactor,
                             BFactor,
                             PreviewDense,
                             PreviewColorful);
                            break;
                    }
                    // 创建新的WriteableBitmap并缓存
                    var newBitmap = scratchImageStep.ToBitmapSource();
                    newBitmap.Freeze();
                    ScratchImageStepCache[tick.ToString("0.000")] = newBitmap;
                    return newBitmap;
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e.Message);
                }
                finally
                {
                    IsNotProcessingSvg = true;
                }
                return null;

            } // using语句确保Mat被释放
        }
        [Obsolete]
        public async void ProcessScratchStepOLD()
        {
            if (!IsOriginalImageLoaded) return;
            if (!CheckOverflow()) return;

            Mat scratchImageStep = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
            try
            {
                IsNotProcessingSvg = false;
                if (IsGeneratingHandCraftSketchMode)
                    await SvgPainter.PreviewPathArc(SampledPoints, DepthImage, OriginalImage, scratchImageStep, _previewT, ZeroDepth, IgnoreZeroDepthDistance, RadiusFactor, AngleFactor, LayerCount, PreviewDense, PreviewColorful);
                else
                    await SvgPainter.PreviewPath(SampledPoints, DepthImage, OriginalImage, scratchImageStep, _previewT, ZeroDepth, IgnoreZeroDepthDistance, AFactor, BFactor, PreviewDense, PreviewColorful);
                DisplayImageScratchStep = scratchImageStep.ToWriteableBitmap();
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
            }
            finally
            {
                IsNotProcessingSvg = true;
            }
        }
        int inverse = 1;
        public async void ProcessScratchStep3D()
        {
            if (!IsOriginalImageLoaded) return;
            if (!CheckOverflow()) return;

            float step = PreviewStep * (1 - PreviewStepDistance);
            //Mat scratchImageL = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
            //Mat scratchImageR = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
            try
            {
                IsNotProcessingSvg = false;
                var spf = await ExecuteWithMinimumDuration(async () =>
                    {
                        DisplayImageScratch3DLeft = await ProcessAndCacheScratchTick(step);
                        DisplayImageScratch3DRight = await ProcessAndCacheScratchTick(step + PreviewStepDistance);

                        /*await SvgPainter.PreviewPathParallel(SampledPoints, DepthImage, OriginalImage, scratchImageL,
                                scratchImageR, PreviewStep, PreviewStepDistance, ZeroDepth, IgnoreZeroDepthDistance, AFactor,
                                BFactor, PreviewDense, IsGeneratingHandCraftSketchMode, PreviewColorful);

                        // 水平拼接两张图片
                        Mat concatenated = new Mat();
                        Cv2.HConcat(new Mat[] { scratchImageL, scratchImageR }, concatenated);
                        DisplayImageScratch3D = concatenated.ToWriteableBitmap();*/
                        //Mat concatenated = new Mat();
                        //Cv2.HConcat(new Mat[] { , scratchImageR }, concatenated);//TBD:combine 2 image
                        //DisplayImageScratch3D = concatenated.ToWriteableBitmap();

                    }, TimeSpan.FromSeconds(IsAutoPlay3D ? 1 / _autoPlayMaxFps : 0));
                //RealTimeFps = 1/spf.TotalSeconds;
                UpdateFps(spf);

            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
            }
            finally
            {
                IsNotProcessingSvg = true;
                if (IsAutoPlay3D)
                    PreviewStep += 0.01f * inverse;
            }
        }
        public async void ExportPath()
        {
            if (!IsOriginalImageLoaded) return;
            if (!CheckOverflow()) return;

            IsNotProcessingSvg = false;
            try
            {
                switch (_pathGeneratingMode)
                {
                    case 2:
                        var missionA = StartMission("Occulsion SVG Export");

                        var svgA = await OccusionPathHelper.BuildSvgPath(missionA, SampledPoints, DepthImage, ZeroDepth, IgnoreZeroDepthDistance, AFactor, BFactor);
                        await SaveSvgToFileAsync(svgA);

                        EndMission(missionA);
                        break;
                    case 1:
                        var missionB = StartMission("Sketch SVG Export");
                        OpenFolderDialog openFolderDialog = new OpenFolderDialog();

                        if (openFolderDialog.ShowDialog() == true)
                        {
                            string outputPath = $"{openFolderDialog.FolderName}\\{Path.GetFileNameWithoutExtension(FilePath)}草图集";
                            if (!Directory.Exists(outputPath))
                                Directory.CreateDirectory(outputPath);

                            await SvgPainter.BuildSketch(SampledPoints, DepthImage, outputPath, ZeroDepth, IgnoreZeroDepthDistance, RadiusFactor, AngleFactor, LayerCount, 10, 1.3f, System.IO.Path.GetFileNameWithoutExtension(FilePath) + " 草图集");
                        }

                        EndMission(missionB);
                        break;
                    default:
                        var missionC = StartMission("SVG Export");

                        var svgC = await SvgPainter.BuildSvgPath(SampledPoints, DepthImage, ZeroDepth, IgnoreZeroDepthDistance, AFactor, BFactor);
                        await SaveSvgToFileAsync(svgC);

                        EndMission(missionC);
                        break;
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
            }
            finally
            {
                IsNotProcessingSvg = true;
            }
        }
        /*public void ProcessExportSketch()
        {
            if (OriginalImage == null) return;
            if (OriginalImage.Width == 0) return;
            if (!CheckOverflow()) return;
            // 使用保存对话框选择保存路径
            OpenFolderDialog openFolderDialog = new OpenFolderDialog();


            if (openFolderDialog.ShowDialog() == true)
            {
                try
                {
                    IsNotProcessingSvg = false;
                    //string mysvg = await SvgPainter.BuildSvgPath(SampledPoints, DepthImage, ZeroDepth, IgnoreZeroDepthDistance, AFactor, BFactor, PreviewDense, IsGeneratingHandCraftSketchMode);
                    string outputPath = $"{openFolderDialog.FolderName}\\{Path.GetFileNameWithoutExtension(FilePath)}草图集";
                    if (!Directory.Exists(outputPath))
                        Directory.CreateDirectory(outputPath);

                    SvgPainter.BuildSketch(SampledPoints, DepthImage, outputPath, ZeroDepth, IgnoreZeroDepthDistance, RadiusFactor, AngleFactor, LayerCount,10,1.3f,System.IO.Path.GetFileNameWithoutExtension(FilePath)+" 草图集");
                    //SaveSvgToFile(mysvg);
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e.Message);
                }
                finally
                {
                    IsNotProcessingSvg = true;
                }
            }
        }*/
        private Mat ApplyColorMap(Mat originMat)
        {
            Mat colorMat = new Mat();
            Cv2.ConvertScaleAbs(originMat, colorMat, 1);

            // 检查通道数
            int channels = colorMat.Channels();

            // 处理四通道图像（RGBA）
            if (channels == 4)
            {
                Mat[] bgraChannels = colorMat.Split();

                Mat rgbMat = new Mat();
                Cv2.Merge(new Mat[] { bgraChannels[0], bgraChannels[1], bgraChannels[2] }, rgbMat);

                Mat alphaMat = bgraChannels[3].Clone();
                Mat colored = new Mat();
                Cv2.ApplyColorMap(rgbMat, colored, (ColormapTypes)_depthColor);

                colorMat.Dispose();
                rgbMat.Dispose();
                foreach (var ch in bgraChannels) ch.Dispose();

                Mat result = new Mat();
                Cv2.Merge([colored, alphaMat], result);

                alphaMat.Dispose();
                colored.Dispose();

                return result;
            }
            // 处理单通道图像（灰度）
            else
            {
                Mat colored = new Mat();
                Cv2.ApplyColorMap(colorMat, colored, (ColormapTypes)_depthColor);
                colorMat.Dispose();
                //colored.SaveImage($"D:/{DateTime.Now.ToString("fffff")}.png");
                return colored;
            }

        }
        private async Task SaveSvgToFileAsync(string svgContent)
        {
            // 使用保存对话框选择保存路径（UI 线程）
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "SVG Files (*.svg)|*.svg",
                DefaultExt = "svg",
                AddExtension = true,
                FileName = $"{FilePath.Split("\\").Last()}.svg"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    string path = saveFileDialog.FileName;
                    await Task.Run(() => File.WriteAllText(path, svgContent));
                    ShowTipsTemporarilyAsync($"成功保存到: {path}");
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"保存时发生错误: {e.Message}");
                    ShowTipsTemporarilyAsync("保存SVG文件失败，请尝试其他位置，或以管理员身份运行");
                }
            }
        }
        private static Mat CreateGradientMat(int w, int h)
        {
            Mat gradientMat = new Mat(h, w, MatType.CV_8UC1, new Scalar(0));

            for (int i = 0; i < gradientMat.Rows; i++)
            {
                byte value = (byte)(255 - (i * 255 / (gradientMat.Rows - 1)));

                gradientMat.Row(i).SetTo(new Scalar(value));
            }

            return gradientMat;
        }


        private List<Point> PostProcessPoints(List<Point> points)
        {
            int originCount = points.Count;
            List<Point> result = points;
            if (originCount > MaximumPointCount)
            {
                ShowTipsTemporarilyAsync("需要去重的点量太大，已跳过去重");
            }
            else
            {
                if (IsDublicateRemoveEnabled)
                {
                    HashSet<Point> distinctPointsSet = new HashSet<Point>(points);
                    result = new List<Point>(distinctPointsSet);

                    if (IsDublicateRemovePlusEnabled)
                    {
                        /*
                        List<Point> pointsToRemove = new List<Point>();

                        // 遍历每个点，检查是否满足移除条件
                        int index = 0;
                        while (index < result.Count)  // 使用 ToList() 防止在迭代时修改列表
                        {
                            if (result.Where(p =>
                            {
                                // 计算当前点与圆心之间的距离
                                float distance = Math.Sqrt(Math.Pow(p.X - result[index].X, 2) + Math.Pow(p.Y - result[index].Y, 2));
                                return distance < KillRange;
                            }).ToList().Count > KillCount)
                                result.RemoveAt(index);
                            else index++;

                        }
                        */

                        //int gridWidth = (int)(OriginImageWidth * Math.Pow(0.5, Math.Log2(OriginImageWidth) * _deduplicationAccuracy));
                        //int gridHeight = (int)(OriginImageHeight * Math.Pow(0.5, Math.Log2(OriginImageHeight) * _deduplicationAccuracy));

                        //Trace.WriteLine($"w{gridWidth},h{gridHeight},maxcount{maxCount}");
                        // 3. 创建一个字典来存储每个网格内的点

                        /*Dictionary<(int, int), List<Point>> gridMap = new Dictionary<(int, int), List<Point>>();

                        // 4. 将每个点映射到对应的网格
                        foreach (var point in result)
                        {
                            int gridX = point.X / DeduplicationGridWidth;
                            int gridY = point.Y / DeduplicationGridHeight;
                            var gridKey = (gridX, gridY);

                            if (!gridMap.ContainsKey(gridKey))
                            {
                                gridMap[gridKey] = new List<Point>();
                            }

                            gridMap[gridKey].Add(point);
                        }

                        // 5. 遍历每个网格，检查点数是否超过理想个数
                        List<Point> pointsToRemove = new List<Point>();

                        foreach (var grid in gridMap)
                        {
                            List<Point> gridPoints = grid.Value;

                            // 如果网格中的点数超过理想个数
                            if (gridPoints.Count > DeduplicationMaxCount)
                            {
                                // 随机选择多余的点进行删除
                                int pointsToDelete = gridPoints.Count - DeduplicationMaxCount;
                                for (int i = 0; i < pointsToDelete; i++)
                                {
                                    
                                    int indexToRemove = Random.Shared.Next(gridPoints.Count);
                                    pointsToRemove.Add(gridPoints[indexToRemove]);
                                    gridPoints.RemoveAt(indexToRemove);
                                }
                            }
                        }
                        foreach (var point in pointsToRemove)
                        {
                            result.Remove(point);// TODO: 优化算法
                        }*/
                    }
                }

            }
            if (IsExcludeMaskEnabled)
            {
                if (DepthImage != null)
                {
                    if (!(_excludingRangeMin == 0 && _excludingRangeMax == 255))
                    {
                        for (int i = result.Count - 1; i >= 0; i--)
                        {
                            float depth = DepthImage.Get<float>(result[i].Y, result[i].X);
                            if (depth < _excludingRangeMin || depth > _excludingRangeMax)
                                result.Remove(result[i]);
                        }
                    }
                }
            }
            ExcludedPointCount = originCount - result.Count;
            return result;
        }

    }
}
