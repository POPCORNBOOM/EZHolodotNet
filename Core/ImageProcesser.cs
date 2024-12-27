using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
                    Content = "未发现深度识别模型，请将(.onnx)模型与软件放在同一文件夹后重试",
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

        public RelayCommand ChangePreviewCommand => new RelayCommand((p) => ChangePreviewExecute((string)p));
        public RelayCommand CloseWarningCommand => new RelayCommand((p) => mainWindow.WarningFlyout.Hide());
        public RelayCommand ChooseImageCommand => new RelayCommand((p) =>
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png";
            if (openFileDialog.ShowDialog() == true)
            {
                LoadImage(openFileDialog.FileName);
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
                    "Copyright \u00a9 2024 Yigu Wang\r\n\r\nLicensed under the Apache License, Version 2.0 (the \"License\");\r\nyou may not use this file except in compliance with the License.\r\nYou may obtain a copy of the License at\r\n\r\n    http://www.apache.org/licenses/LICENSE-2.0\r\n\r\nUnless required by applicable law or agreed to in writing, software distributed under the License is distributed on an \"AS IS\" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.\r\nSee the License for the specific language governing permissions and limitations under the License.\r\nMore information at https://github.com/POPCORNBOOM/EZHolodotNet",
                CloseButtonText ="了解",
            };

            uiMessageBox.ShowDialogAsync();
        }

        private string _modelFilePath = "";
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
        private List<Point> _contourPoints = new();
        private List<Point> _brightnessPoints = new();
        public List<Point> SampledPoints
        {
            get => _contourPoints.Concat(_brightnessPoints).ToList();
        }
        public Mat? OriginalImage { get; set; }
        public Mat? _depthImage = new Mat();
        public Mat? DepthImage
        {
            get => _depthImage;
            set
            {
                if (!Equals(_depthImage, value))
                {
                    _depthImage = value;
                    OnPropertyChanged(nameof(DepthImage));
                }
            }
        }
        public Mat ContoursOverDepthImage { get; set; } = new Mat();
        private WriteableBitmap _displayImageDepth;
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
        public WriteableBitmap DisplayImageScratchLine
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
        public WriteableBitmap DisplayImageScratchStep
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
        public double AreaDensity { get; set; } = 10;
        public List<Point2d> Points { get; set; } = new List<Point2d>();
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

        public Point MousePoint
        {
            get => _mousePoint;
            set
            {
                _mousePoint = value;
                OnPropertyChanged(nameof(MousePoint));
                OnPropertyChanged(nameof(MouseDepth));
                OnPropertyChanged(nameof(MousePointX));
                OnPropertyChanged(nameof(MousePointY));

            }
        }

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
        public double PointCount
        {
            get
            {
                return SampledPoints.Count;
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
                    DisplayImageDepth = ApplyColorMap(DepthImage).ToWriteableBitmap();

                }
            }
        }
        private int _depthColor = 0;
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
        private double _aFactor = 0.16;  
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
        private double _bFactor = 1000;  
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

        public void LoadImage(string filepath)
        {
            FilePath = filepath;
            OriginalImage = new Mat(filepath);
            DepthImage = DepthEstimation.ProcessImage(OriginalImage);
            DisplayImageDepth = ApplyColorMap(DepthImage).ToWriteableBitmap();
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
            // 绘图
            OnPropertyChanged(nameof(PointCount));

            ContoursOverDepthImage = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 255));

            foreach (var point in SampledPoints)
            {
                Cv2.Circle(ContoursOverDepthImage, point, 1, Scalar.Red);
            }
            DisplayImageContour = ContoursOverDepthImage.ToWriteableBitmap();
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
                default:
                    PreviewT = 0.5;
                    break;
            }
        }
        public void ProcessDepth()
        {
            Cv2.ImShow("r", DepthImage);
        }        
        private bool CheckOverflow()
        {
            if(SampledPoints.Count > MaximumPointCount)
            {
                mainWindow.WarningFlyout.Show();
            }
            return SampledPoints.Count < MaximumPointCount;

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
                await SvgPainter.PreviewPath(SampledPoints, DepthImage, ZeroDepth, AFactor, BFactor, PreviewDense, IsPositiveDepthPointOnly, scratchImageL,scratchImageR,scratchImageO, scratchImageLine);
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
                await SvgPainter.PreviewPath(SampledPoints, DepthImage, OriginalImage, scratchImageStep,_previewT, ZeroDepth, AFactor, BFactor, PreviewDense, IsPositiveDepthPointOnly,PreviewColorful);
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
        public async void ProcessExportScratch(string p)
        {
            if (OriginalImage == null) return;
            if (OriginalImage.Width == 0) return;
            if (!CheckOverflow()) return;

            Mat scratchImage = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
            try
            {
                IsNotProcessingSvg = false;
                string mysvg = await SvgPainter.BuildSvgPath(SampledPoints, DepthImage, ZeroDepth, AFactor, BFactor, PreviewDense, IsPositiveDepthPointOnly);
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
        private Mat ApplyColorMap(Mat depthMat)
        {
            Mat colorDepthMat = new Mat();
            Cv2.ApplyColorMap(depthMat, colorDepthMat, (ColormapTypes)_depthColor);
            return colorDepthMat;
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

    }
}
