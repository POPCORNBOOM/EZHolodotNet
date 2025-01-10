using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using EZHolodotNet.Views;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using Point = OpenCvSharp.Point;

namespace EZHolodotNet.Core
{
    public class ImageProcesser:INotifyPropertyChanged
    {
        public ImageProcesser()
        {

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
        public ImageProcesser(MainWindow mainWindow)
        {
            //遍历所有.onnx文件
            // 获取所有 .onnx 文件
            string runningDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string[] onnxFiles = Directory.GetFiles(runningDirectory, "*.onnx", SearchOption.AllDirectories);
            if (onnxFiles.Length == 0)
            {
                var uiMessageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "错误",
                    Content = "未发现深度识别模型，请将(.onnx)模型与软件(.exe)放在同一文件夹后重试",
                    CloseButtonText = "了解",
                };

                uiMessageBox.ShowDialogAsync();
                Application.Current.Shutdown(0);
                return;
            }
            ModelFilePath = onnxFiles[0].Split("\\").Last();
            DepthEstimation = new DepthEstimation(onnxFiles[0]);
            this.mainWindow = mainWindow;
        }

        public RelayCommand ConvertToManualCommand => new RelayCommand((p) => ConvertToManualPoint(p.ToString()??"c"));
        public RelayCommand ClearManualPointsCommand => new RelayCommand((p) => AddOperation(new(_manualPointsStored),false));
        public RelayCommand UndoStepCommand => new RelayCommand((p) => Undo());
        public RelayCommand RedoStepCommand => new RelayCommand((p) => Redo());
        public RelayCommand SetManualToolCommand => new RelayCommand((p) => ManualTool=int.Parse(p.ToString()??"0"));
        public RelayCommand ImportConfigCommand => new RelayCommand((p) => ImportConfig());
        public RelayCommand ExportConfigCommand => new RelayCommand((p) => ExportConfig());
        public RelayCommand ExportDepthCommand => new RelayCommand((p) => ExportDepth());
        public RelayCommand ImportDepthCommand => new RelayCommand((p) => ImportDepth());
        public RelayCommand ExportPointsCommand => new RelayCommand((p) => ExportPoints((string)p));
        public RelayCommand ImportPointsCommand => new RelayCommand((p) => ImportPoints());
        public RelayCommand Open3DPreviewCommand => new RelayCommand((p) => new ThreeDPreviewWindow(this).Show());
        public RelayCommand ChangePreviewCommand => new RelayCommand((p) => ChangePreviewExecute((string)p));
        public RelayCommand CloseWarningCommand => new RelayCommand((p) => mainWindow.WarningFlyout.Hide());
        public RelayCommand ChooseImageCommand => new RelayCommand((p) =>
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png";
            if (openFileDialog.ShowDialog() == true)
            {
                LoadImage(openFileDialog.FileName);
                _manualPointsStored = new();
                RefreshDisplay();
                if (IsAutoGeneratePreview)
                    ProcessScratch();
            }
        });
        public RelayCommand ProcessDepthCommand => new RelayCommand((p) => ProcessDepth());
        public RelayCommand CreateScratchCommand => new RelayCommand((p) => ProcessScratch());
        public RelayCommand ExportScratchCommand => new RelayCommand((p) => ProcessExportScratch((string)p));
        public RelayCommand OpenCustomMessageBoxCommand => new RelayCommand((p) => OpenCustomMessageBox());
        private void OpenCustomMessageBox()
        {
            var uiMessageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "开源信息",
                Content =
                    "Copyright \u00a9 2025 Yigu Wang\r\n\r\nLicensed under the Apache License, Version 2.0 (the \"License\");\r\nyou may not use this file except in compliance with the License.\r\nYou may obtain a copy of the License at\r\n\r\n    http://www.apache.org/licenses/LICENSE-2.0\r\n\r\nUnless required by applicable law or agreed to in writing, software distributed under the License is distributed on an \"AS IS\" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.\r\nSee the License for the specific language governing permissions and limitations under the License.\r\nMore information at https://github.com/POPCORNBOOM/EZHolodotNet",
                CloseButtonText ="了解",
            };

            uiMessageBox.ShowDialogAsync();
        }

        private string _modelFilePath = "";
        [XmlIgnore]
        public string ModelFilePath
        {
            get => _modelFilePath;
            set
            {
                if (!Equals(_modelFilePath, value))
                {
                    _modelFilePath = value;
                    OnPropertyChanged(nameof(ModelFilePath));
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
        private List<Point> _manualPointsStored = new();
        private List<Point> _manualPoints = new();
        private List<Point> _contourPoints = new();
        private List<Point> _brightnessPoints = new();
        [XmlIgnore]public List<Point> SampledPoints
        {
            get
            {
                if (IsPostProcessEnabled)
                {
                    return PostProcessPoints(_contourPoints.Concat(_brightnessPoints).Concat(_manualPoints).ToList());
                }
                else
                {
                    ExcludedPointCount = 0;
                    return _contourPoints.Concat(_brightnessPoints).Concat(_manualPoints).ToList();

                }

            }
        }
        [XmlIgnore]public Mat? OriginalImage { get; set; }
        [XmlIgnore]public Mat? _depthImage = new Mat();
        [XmlIgnore]public Mat? DepthImage
        {
            get => _depthImage;
            set
            {
                if (!Equals(_depthImage, value))
                {
                    _depthImage = value;
                    OnPropertyChanged(nameof(DepthImage));
                    DisplayImageDepth = ApplyColorMap(DepthImage).ToWriteableBitmap();
                }
            }
        }
        private readonly Mat _gradient = CreateGradientMat(100, 100);
        [XmlIgnore]public WriteableBitmap GradientColor => ApplyColorMap(_gradient).ToWriteableBitmap();
        [XmlIgnore]public Mat ContoursOverDepthImage { get; set; } = new Mat();
        private WriteableBitmap _displayImageDepth;
        [XmlIgnore]public WriteableBitmap DisplayImageDepth
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
        [XmlIgnore]public WriteableBitmap DisplayImageContour
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
        [XmlIgnore]public WriteableBitmap DisplayImageScratchL
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
        [XmlIgnore]public WriteableBitmap DisplayImageScratchR
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
        [XmlIgnore]public WriteableBitmap DisplayImageScratchO
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
        [XmlIgnore]public WriteableBitmap DisplayImageScratchLine
        {
            get => _displayImageScratchLine;
            set
            {
                if (!Equals(_displayImageScratchLine, value))
                {
                    _displayImageScratchLine = value;
                    OnPropertyChanged(nameof(DisplayImageScratchLine));
                }
            }
        }        
        private WriteableBitmap _displayImageScratchStep;
        [XmlIgnore]public WriteableBitmap DisplayImageScratchStep
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
        private WriteableBitmap _displayImageScratch3D;
        [XmlIgnore]public WriteableBitmap DisplayImageScratch3D
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
        public double AreaDensity { get; set; } = 10;
        public List<Point2d> Points { get; set; } = new List<Point2d>();
        [XmlIgnore]
        public DepthEstimation DepthEstimation;
        private Point _mousePoint = new(0, 0);
        public int MousePointX
        { 
            get => MousePoint.X; 
        }
        public int MousePointY 
        { 
            get => MousePoint.Y; 
        }        
        public int OriginImageWidth
        { 
            get => OriginalImage?.Cols ?? 0; 
        }
        public int OriginImageHeight
        { 
            get => OriginalImage?.Rows ?? 0; 
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
        [XmlIgnore]
        public Point MousePoint
        {
            get => _mousePoint;
            set
            {
                _mousePoint = value;
                OnPropertyChanged(nameof(MousePoint));
                OnPropertyChanged(nameof(MouseDepth));
                OnPropertyChanged(nameof(EraserCenterY));
                OnPropertyChanged(nameof(EraserCenterX));
                OnPropertyChanged(nameof(MousePointX));
                OnPropertyChanged(nameof(MousePointY));

            }
        }
        [XmlIgnore]
        public double MouseDepth
        {
            get
            {
                if (DepthImage == null) return 0;
                if (DepthImage.Cols == 0) return 0;
                Vec3b normalizedValue = DepthImage.Get<Vec3b>(MousePoint.Y, MousePoint.X);
                //Trace.WriteLine(normalizedValue[0]);
                return normalizedValue[0];
            }
        }
        [XmlIgnore]
        public int PointCount
        {
            get
            {
                return SampledPoints.Count;
            }
        }
        public int ContourPointCount => _contourPoints.Count;
        public int BrightnessPointCount => _brightnessPoints.Count;
        public int ManualPointCount => _manualPointsStored.Count;
        public int DepthColor
        {
            get => _depthColor;
            set
            {
                if (!Equals(_depthColor, value))
                {
                    _depthColor = value;
                    OnPropertyChanged(nameof(DepthColor));
                    OnPropertyChanged(nameof(GradientColor));
                    if(DepthImage.Cols!=0)
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

                }
            }
        }
        private int _manualTool = 0;
        private double _overlayOpacity = 0.5;

        public double OverlayOpacity
        {
            get => _overlayOpacity;
            set
            {
                if (!Equals(_overlayOpacity, value))
                {
                    _overlayOpacity = value;
                    OnPropertyChanged(nameof(OverlayOpacity));
                }
            }
        }        
        private double _previewT = 0.5;

        public double PreviewT
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
                    ProcessScratchStep();
                }
            }
        }        
        private double _previewStepDistance = 0.2;

        public double PreviewStepDistance
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
        private double _previewStep = 0.5;

        public double PreviewStep
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
        private int _zeroDepth = 128;  
        public int ZeroDepth
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
        private int _ignoreZeroDepthDistance = 2;  
        public int IgnoreZeroDepthDistance
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
        private double _aFactor = 0.69;  
        public double AFactor
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
        private double _bFactor = 800;  
        public double BFactor
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
        private int _previewDense = 50;  
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
        public double Threshold1
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
        private double _threshold1 = 20;
        private double _threshold2 = 100;
        public double Threshold2
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
        private double _blurFactor = 3;
        public double BlurFactor
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
        private double _approximationFactor = 1;
        public double ApproximationFactor
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
        private double _manualDensity = 0.1;
        public double ManualDensity
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
        private double _eraserRadius = 25;
        public double EraserRadius
        {
            get => _eraserRadius;
            set
            {
                if (!Equals(_eraserRadius, value))
                {
                    _eraserRadius = value;
                    OnPropertyChanged(nameof(EraserRadius));
                    OnPropertyChanged(nameof(EraserDiameter));
                    RefreshDisplay();
                }
            }
        }          
        public double EraserCenterX
        {
            get => _mousePoint.X - EraserRadius;
        }       
        public double EraserCenterY
        {
            get => _mousePoint.Y - EraserRadius;
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
                    if(value)
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
        private bool _isPreviewingLeft = true;
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
        private bool _isPreviewingRight = true;
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
        private bool _isPreviewingOrigin = true;
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
        private string _previewColorful = "c";
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
        private bool _isPositiveDepthPointOnly = false;
        public bool IsPositiveDepthPointOnly
        {
            get => _isPositiveDepthPointOnly;
            set
            {
                if (!Equals(_isPositiveDepthPointOnly, value))
                {
                    _isPositiveDepthPointOnly = value;
                    OnPropertyChanged(nameof(IsPositiveDepthPointOnly));
                    if (IsAutoGeneratePreview)
                        ProcessScratch();
                }
            }
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
                    if(value)ProcessScratch();
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
        private double _brightnessBaseDensity = 0.16;
        public double BrightnessBaseDensity
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
        private double _brightnessDensityFactor = 0;
        public double BrightnessDensityFactor
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
        private double _brightnessEnhanceGamma = 1.5;
        public double BrightnessEnhanceGamma
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
        private double _autoPlayMaxFps = 30;
        public double AutoPlayMaxFps
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
        private double _realTimeFps = 0;
        private readonly int _historySize = 10;  // 滑动窗口大小，即存储多少帧的FPS数据
        private readonly Queue<double> _fpsHistory = new Queue<double>();

        [XmlIgnore]
        public double RealTimeFps
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
            double currentFps = 1 / spf.TotalSeconds;

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

        public void LoadImage(string filepath)
        {
            FilePath = filepath;
            OriginalImage = new Mat(filepath);
            OnPropertyChanged(nameof(OriginImageHeight));
            OnPropertyChanged(nameof(OriginImageWidth));
            DepthImage = DepthEstimation.ProcessImage(OriginalImage);
            //DisplayImageDepth = ApplyColorMap(DepthImage).ToWriteableBitmap();
        }

        public List<Point> ExtractContours()
        {
            if (OriginalImage == null) return new();

            Mat blurred = new Mat();
            Cv2.GaussianBlur(OriginalImage, blurred, new OpenCvSharp.Size(_blurFactor * 2 - 1, _blurFactor * 2 - 1), 0);

            Mat edges = new Mat();
            Cv2.Canny(blurred, edges, Threshold1, Threshold2,3,true);

            var contours = new List<Point>();
            Point[][] contourPoints;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(edges, out contourPoints, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
            for (int i = 0; i < contourPoints.Length; i++)
            {
                for (int j = 0; j < contourPoints[i].Length; j += _lineDensity) // 根据 density 间隔采样
                {
                    contours.Add(contourPoints[i][j]); // 添加采样的轮廓点
                }
            }

            return contours;
        }

        public void RefreshDisplay()
        {
            if (OriginalImage == null || OriginalImage.Cols == 0) return;

            // 应用采样策略逻辑
            _contourPoints = IsContourMethodEnabled ? ExtractContours() : new();
            _brightnessPoints = IsBrightnessMethodEnabled ? GetPointsByLuminance() : new();
            _manualPoints = IsManualMethodEnabled ? _manualPointsStored : new();
            // 绘图
            OnPropertyChanged(nameof(ContourPointCount));
            OnPropertyChanged(nameof(BrightnessPointCount));
            OnPropertyChanged(nameof(ManualPointCount));

            ContoursOverDepthImage = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 255));



            foreach (var point in SampledPoints)
            {
                Cv2.Circle(ContoursOverDepthImage, point, 1, Scalar.Red);
            }
            DisplayImageContour = ContoursOverDepthImage.ToWriteableBitmap();
            OnPropertyChanged(nameof(PointCount));

        }
        public Mat EnhanceContrastGamma(Mat inputImage, double gamma = 1.5)
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
        }
        public List<Point> GetPointsByLuminance()
        {

            if (OriginalImage == null || OriginalImage.Empty()) return new();

            // 转换为灰度图像
            Mat grayImage = new Mat();
            Cv2.CvtColor(OriginalImage, grayImage, ColorConversionCodes.BGR2GRAY);
            Mat grayImageEnhanced = EnhanceContrastGamma(grayImage, _brightnessEnhanceGamma);
            // 创建存储结果的点列表
            List<Point> points = new List<Point>();
            int step =(int)(1 / _brightnessBaseDensity);
            // 遍历灰度图的每个像素
            if(IsDarknessMode)
            {
                for (int y = 0; y < grayImageEnhanced.Rows; y += step)
                {
                    for (int x = 0; x < grayImageEnhanced.Cols; x += step)
                    {
                        // 获取当前像素的亮度值 (0-255)
                        byte brightness = grayImageEnhanced.At<byte>(y, x);

                        // 随机采样生成点
                        Random random = new Random();
                        if (random.Next(0, 255) > brightness * Math.Exp(_brightnessDensityFactor))
                        {
                            points.Add(new Point(x, y));
                        }
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

                        // 随机采样生成点
                        Random random = new Random();
                        if (random.Next(0, 255) < brightness * Math.Exp(_brightnessDensityFactor))
                        {
                            points.Add(new Point(x, y));
                        }
                    }
                }
            }

            return points;
        }
        private void ExportDepth()
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
                try
                {
                    if (DepthImage == null)
                    {
                        MessageBox.Show("深度图像为空，无法导出。");
                        return;
                    }

                    // 归一化深度图像到 0-65535 范围
                    Mat normalizedDepth = new Mat();
                    double minVal, maxVal;
                    Cv2.MinMaxLoc(DepthImage, out minVal, out maxVal);
                    DepthImage.ConvertTo(normalizedDepth, MatType.CV_16UC1, 65535.0 / (maxVal - minVal), -minVal * 65535.0 / (maxVal - minVal));

                    // 保存为 PNG
                    Cv2.ImWrite(saveFileDialog.FileName, normalizedDepth);

                    MessageBox.Show("深度图像成功导出。");
                }
                catch (Exception e)
                {
                    MessageBox.Show($"导出深度图像时出错: {e.Message}");
                }
            }
        }
        private void ImportDepth()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "PNG 文件 (*.png)|*.png|JPEG 文件 (*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP 文件 (*.bmp)|*.bmp|TIFF 文件 (*.tiff;*.tif)|*.tiff;*.tif|所有文件 (*.*)|*.*",
                DefaultExt = "png"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    Mat loadedDepth = Cv2.ImRead(openFileDialog.FileName, ImreadModes.Grayscale);
                    if (loadedDepth.Empty())
                    {
                        MessageBox.Show("无法读取深度图像");
                        return;
                    }
                    loadedDepth.ConvertTo(loadedDepth, MatType.CV_8UC1);  // 确保是 CV_8UC1 类型

                    // 如果图像尺寸与原始图像不一致，则调整为原始图像的尺寸
                    if (loadedDepth.Rows != OriginalImage.Rows || loadedDepth.Cols != OriginalImage.Cols)
                    {
                        Mat resizedDepth = new Mat();
                        Cv2.Resize(loadedDepth, resizedDepth, OriginalImage.Size());
                        loadedDepth = resizedDepth;
                    }

                    DepthImage = loadedDepth;

                    MessageBox.Show("深度图像成功导入");
                }
                catch (Exception e)
                {
                    MessageBox.Show($"导入深度图像时出错: {e.Message}");
                }
            }
        }

        private void ExportPoints(string type)
        {
            if (OriginalImage == null)
            {
                MessageBox.Show("请先选择一张处理图片");
                return;
            }            
            if (OriginalImage.Cols == 0)
            {
                MessageBox.Show("请先选择一张处理图片");
                return;
            }
            // 使用保存对话框选择保存路径
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PNG 文件 (*.png)|*.png",
                DefaultExt = "png",
                AddExtension = true,
                FileName = $"{Path.GetFileNameWithoutExtension(FilePath)}{(type =="a" ? "_A":"_M")}_PointMap.png"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {

                    //Todo: Create a new white Mat,with the same size of OriginalImage, foreach point in _manualPointsStored,Set to Black
                    // 创建与原始图片相同大小的单通道白色 Mat
                    Mat pointMap = new Mat(OriginalImage.Rows, OriginalImage.Cols, MatType.CV_8UC1, new Scalar(255));

                    foreach (var point in type=="a"? SampledPoints: _manualPointsStored)
                    {
                        if (point.X >= 0 && point.X < pointMap.Cols && point.Y >= 0 && point.Y < pointMap.Rows)
                        {
                            pointMap.Set(point.Y, point.X, (byte)0); // 设置为黑色
                        }
                    }
                    // 保存为 PNG
                    Cv2.ImWrite(saveFileDialog.FileName, pointMap);

                    MessageBox.Show("点图成功导出。");
                }
                catch (Exception e)
                {
                    MessageBox.Show($"导出点图时出错: {e.Message}");
                }
            }
        }
        private void ImportPoints()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "PNG 文件 (*.png)|*.png|JPEG 文件 (*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP 文件 (*.bmp)|*.bmp|TIFF 文件 (*.tiff;*.tif)|*.tiff;*.tif|所有文件 (*.*)|*.*",
                DefaultExt = "png"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    Mat pointMap = Cv2.ImRead(openFileDialog.FileName, ImreadModes.Grayscale);
                    if (pointMap.Empty())
                    {
                        MessageBox.Show("无法读取点图");
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
                    List<Point> result = new();
                    //Todo:不为白色就添加点

                    for (int y = 0; y < pointMap.Rows; y++)
                    {
                        for (int x = 0; x < pointMap.Cols; x++)
                        {
                            byte pixelValue = pointMap.At<byte>(y, x);
                            if (pixelValue < 255)
                            {
                                result.Add(new Point(x, y));
                            }
                        }
                    }

                    AddOperation(result);

                    MessageBox.Show($"成功从点图导入 {result.Count} 个点");
                }
                catch (Exception e)
                {
                    MessageBox.Show($"导入点图时出错: {e.Message}");
                }
            }
        }
        private void ExportConfig()
        {
            // 使用保存对话框选择保存路径
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Xml 文件 (*.xml)|*.xml",
                DefaultExt = "xml",
                AddExtension = true,
                FileName = $"{(Path.GetFileNameWithoutExtension(FilePath)==""? "EZHolo":Path.GetFileNameWithoutExtension(FilePath))}_Config.xml"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(ImageProcesser));
                    using (StreamWriter fileWriter = new StreamWriter(saveFileDialog.FileName))
                    {
                        serializer.Serialize(fileWriter, this);
                    }

                    MessageBox.Show("配置成功导出。");
                }
                catch (Exception e)
                {
                    MessageBox.Show($"导出配置时出错: {e.Message}");
                }
            }
        }
        private void ImportConfig()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Xml 文件 (*.xml)|*.xml",
                DefaultExt = "xml",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(ImageProcesser));
                    using (FileStream fs = new FileStream(openFileDialog.FileName, FileMode.Open))
                    {
                        ImageProcesser ipp = (ImageProcesser)serializer.Deserialize(fs);
                        mainWindow.ImageProcesser = ipp;
                        ipp.DepthEstimation = DepthEstimation;
                        //ipp.RefreshBinding();
                    }

                    MessageBox.Show($"成功打开配置文件");
                }
                catch (Exception e)
                {
                    MessageBox.Show($"打开配置文件时出错: {e.Message}");
                }
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
                    PreviewT -= 0.05;
                    break;
                case "3":
                    PreviewT = 1;
                    break;
                case "2":
                    PreviewT += 0.05;
                    break;
                case "5":
                    PreviewStep = 0;
                    break;
                case "6":
                    PreviewStep -= 0.05;
                    break;
                case "7":
                    PreviewStep = 0.5;
                    break;
                case "8":
                    PreviewStep += 0.05;
                    break;
                case "9":
                    PreviewStep = 1;
                    break;
                default:
                    PreviewT = 0.5;
                    break;
            }
        }
        public void ProcessDepth()
        {
            if (OriginalImage == null) return;
            if (OriginalImage.Width == 0) return;
            DepthImage = DepthEstimation.ProcessImage(OriginalImage);
            //Cv2.ImShow("r", DepthImage);
        }        
        private bool CheckOverflow()
        {
            if(SampledPoints.Count > MaximumPointCount)
            {
                mainWindow.WarningFlyout.Show();
            }
            return SampledPoints.Count < MaximumPointCount;

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
        public double EraserDiameter
        {
            get => 2 * EraserRadius;
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
        double _penPathLength = 0;
        Point? LastPoint;
        public void ProcessManual(bool? isMouseDown = null)
        {
            if (!_isManualMethodEnabled) return;
            if (isMouseDown == null) // moving
            {
                if (IsManualEditing)
                {
                    switch (ManualTool)
                    {
                        case 1:
                            _penPathLength += Math.Sqrt(Math.Pow(MousePoint.X - LastPoint.Value.X, 2) + Math.Pow(MousePoint.Y - LastPoint.Value.Y, 2));
                            if (_penPathLength > 1 / _manualDensity)
                            {
                                _manualPointsStored.Add(MousePoint);
                                AddOperation([MousePoint]);
                                //LastStepAmount ++;
                                _penPathLength -= 1 / _manualDensity;
                            }
                            break;
                        case 2:
                            var p = RemovePointsInRadius(MousePoint, EraserRadius);
                            if (p.Count!=0)
                                AddOperation(p, false);
                            break;


                    }
                    LastPoint = MousePoint;
                }
            }
            else if (isMouseDown == true) // start
            {
                //LastStepAmount = 0;
                StartMousePoint = MousePoint;
                LastPoint = MousePoint;
                _penPathLength = 0;
            }
            else //end
            {
                if (IsManualEditing)
                {
                    if(ManualTool==3)
                    {
                        var interpolated =
                            GetUniformInterpolatedPoints(StartMousePoint.Value, MousePoint, _manualDensity);
                        if (interpolated.Count == 2)
                        {
                            AddOperation([StartMousePoint.Value]);
                            //_manualPointsStored.Add(StartMousePoint.Value);
                            //LastStepAmount = 1;
                        }
                        else
                        {
                            AddOperation(interpolated);
                            //_manualPointsStored.AddRange(interpolated);
                            //LastStepAmount = interpolated.Count;
                        }
                    }
                }
                StartMousePoint = null;
                LastPoint = null;
                _penPathLength = 0;
            }
        }
        public List<Point> RemovePointsInRadius(Point center, double radius)
        {
            if (_manualPointsStored == null)
                return new();

            return _manualPointsStored.Where(point =>
            {
                // 计算当前点与圆心之间的距离
                double distance = Math.Sqrt(Math.Pow(point.X - center.X, 2) + Math.Pow(point.Y - center.Y, 2));
                return distance < radius;
            }).ToList();
        }
        private static List<Point> GetUniformInterpolatedPoints(Point p1, Point p2, double density)
        {
            List<Point> points = new List<Point>();
            points.Add(p1);
            // 计算两点之间的欧几里得距离
            double distance = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

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
        private class Operation
        {
            public List<Point> Points { get; }
            public bool IsAddOperation { get; }

            public Operation(List<Point> points, bool isAddOperation)
            {
                Points = new List<Point>(points);
                IsAddOperation = isAddOperation;
            }
        }

        private readonly Stack<Operation> _undoStack = new Stack<Operation>();
        private readonly Stack<Operation> _redoStack = new Stack<Operation>();

        public void AddOperation(List<Point> points, bool isAddOperation = true)
        {
            // 执行操作
            if (isAddOperation)
                _manualPointsStored.AddRange(points);
            else
                _manualPointsStored.RemoveAll(p => points.Contains(p));

            // 记录操作
            _undoStack.Push(new Operation(points, isAddOperation));
            _redoStack.Clear(); // 每次新操作后清空 redo 栈

            RefreshDisplay();
        }
        public void ConvertToManualPoint(string type)
        {
            switch (type)
            {
                case "c": // contour
                    AddOperation(_contourPoints);
                    IsContourMethodEnabled = false;
                    IsManualMethodEnabled = true;
                    break;
                case "b": // brightness
                    AddOperation(_brightnessPoints);
                    IsBrightnessMethodEnabled = false;
                    IsManualMethodEnabled = true;
                    break;
            }
        }
        public void Undo()
        {
            if (_undoStack.Count == 0) return;

            var operation = _undoStack.Pop();
            if (operation.IsAddOperation)
                _manualPointsStored.RemoveAll(p => operation.Points.Contains(p));
            else
                _manualPointsStored.AddRange(operation.Points);

            _redoStack.Push(operation);
            RefreshDisplay();

        }

        public void Redo()
        {
            if (_redoStack.Count == 0) return;

            var operation = _redoStack.Pop();
            if (operation.IsAddOperation)
                _manualPointsStored.AddRange(operation.Points);
            else
                _manualPointsStored.RemoveAll(p => operation.Points.Contains(p));

            _undoStack.Push(operation);
            RefreshDisplay();

        }
        public async void ProcessScratch()
        {
            if (OriginalImage == null) return;
            if (OriginalImage.Width == 0) return;
            if (!CheckOverflow()) return;
            Mat scratchImageL = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
            Mat scratchImageR = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
            Mat scratchImageO = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
            Mat scratchImageLine = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
            try
            {
                IsNotProcessingSvg = false;
                await SvgPainter.PreviewPath(SampledPoints, DepthImage, ZeroDepth, IgnoreZeroDepthDistance, AFactor, BFactor, PreviewDense, IsPositiveDepthPointOnly, scratchImageL,scratchImageR,scratchImageO, scratchImageLine);
                DisplayImageScratchL = scratchImageL.ToWriteableBitmap();
                DisplayImageScratchR = scratchImageR.ToWriteableBitmap();
                DisplayImageScratchO = scratchImageO.ToWriteableBitmap();
                DisplayImageScratchLine = scratchImageLine.ToWriteableBitmap();
                ProcessScratchStep();

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
        public async void ProcessScratchStep()
        {
            if (OriginalImage == null) return;
            if (OriginalImage.Width == 0) return;
            if (!CheckOverflow()) return;

            Mat scratchImageStep = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
            try
            {
                IsNotProcessingSvg = false;
                await SvgPainter.PreviewPath(SampledPoints, DepthImage, OriginalImage, scratchImageStep,_previewT, ZeroDepth, IgnoreZeroDepthDistance, AFactor, BFactor, PreviewDense, IsPositiveDepthPointOnly,PreviewColorful);
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
            if (OriginalImage == null) return;
            if (OriginalImage.Width == 0) return;
            if (!CheckOverflow()) return;


            Mat scratchImageL = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
            Mat scratchImageR = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
            try
            {
                IsNotProcessingSvg = false;
                var spf = await ExecuteWithMinimumDuration(async () =>
                    {
                        await SvgPainter.PreviewPathParallel(SampledPoints, DepthImage, OriginalImage, scratchImageL,
                                scratchImageR, PreviewStep, PreviewStepDistance, ZeroDepth, IgnoreZeroDepthDistance, AFactor,
                                BFactor, PreviewDense, IsPositiveDepthPointOnly, PreviewColorful);

                        // 水平拼接两张图片
                        Mat concatenated = new Mat();
                        Cv2.HConcat(new Mat[] { scratchImageL, scratchImageR }, concatenated);
                        DisplayImageScratch3D = concatenated.ToWriteableBitmap();
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
                    PreviewStep += 0.01 * inverse;
            }
        }
        public async void ProcessExportScratch(string p)
        {
            if (OriginalImage == null) return;
            if (OriginalImage.Width == 0) return;
            if (!CheckOverflow()) return;

            Mat scratchImage = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
            try
            {
                IsNotProcessingSvg = false;
                string mysvg = await SvgPainter.BuildSvgPath(SampledPoints, DepthImage, ZeroDepth, IgnoreZeroDepthDistance, AFactor, BFactor, PreviewDense, IsPositiveDepthPointOnly);
                SaveSvgToFile(mysvg);
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
        private Mat ApplyColorMap(Mat originMat)
        {
            Mat colorMat = new Mat();
            Cv2.ApplyColorMap(originMat, colorMat, (ColormapTypes)_depthColor);
            return colorMat;
        }
        private void SaveSvgToFile(string svgContent)
        {
            // 使用保存对话框选择保存路径
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
                    File.WriteAllText(saveFileDialog.FileName, svgContent);
                    MessageBox.Show($"成功保存到: {saveFileDialog.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                }
                catch (Exception e)
                {
                    Trace.WriteLine($"保存时发生错误: {e.Message}");
                    MessageBox.Show("保存SVG文件失败，请尝试其他位置，或以管理员身份运行", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private static Mat CreateGradientMat(int w,int h)
        {
            Mat gradientMat = new Mat(w, h, MatType.CV_8UC1, new Scalar(0));

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
            if(IsDublicateRemoveEnabled)
            {
                HashSet<Point> distinctPointsSet = new HashSet<Point>(points);
                result = new(distinctPointsSet);
            }
            ExcludedPointCount = originCount - result.Count;
            return result;
        }


    }
}
