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
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public RelayCommand ChooseImageCommand => new RelayCommand(() =>
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png";
            if (openFileDialog.ShowDialog() == true)
            {
                LoadImage(openFileDialog.FileName);
                RefreshDisplay();
                ProcessScratch();
            }
        });
        public RelayCommand ProcessDepthCommand => new RelayCommand(ProcessDepth);
        public RelayCommand CreateScratchCommand => new RelayCommand(ProcessScratch);
        public RelayCommand ExportScratchCommand => new RelayCommand(ProcessExportScratch);

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
                    Trace.WriteLine("wtf");
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
        private WriteableBitmap _displayImageScratch;
        public WriteableBitmap DisplayImageScratch
        {
            get => _displayImageScratch;
            set
            {
                if (!Equals(_displayImageScratch, value))
                {
                    _displayImageScratch = value;
                    OnPropertyChanged(nameof(DisplayImageScratch));
                }
            }
        }
        public double AreaDensity { get; set; } = 10;
        public List<Point2d> Points { get; set; } = new List<Point2d>();
        public DepthEstimation DepthEstimation = new DepthEstimation();
        private Point _mousePoint = new(0, 0);
        public int MousePointX
        { 
            get => MousePoint.X; 
        }
        public int MousePointY 
        { 
            get => MousePoint.Y; 
        }
        public int MaximumPointCount = 50000;

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
        private double _overlayOpacity = 0.5;  
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

        public List<Point> ExtractContours(int density = 1)
        {
            if (OriginalImage == null) return new();
            Mat blurred = new Mat();
            Cv2.GaussianBlur(OriginalImage, blurred, new OpenCvSharp.Size(_blurFactor * 2 - 1,_blurFactor * 2 - 1), 0);

            // 边缘检测 (使用 Canny 算法)
            Mat edges = new Mat();
            Cv2.Canny(blurred, edges, Threshold1, Threshold2);

            // 查找轮廓 FindContours
            var contours = new List<Point>();
            Point[][] contourPoints;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(edges, out contourPoints, out hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

            foreach (var contour in contourPoints)
            {
                for (int i = 0; i < contour.Length; i += _lineDensity) // 根据 density 间隔采样
                {
                    contours.Add(contour[i]); // 添加采样的轮廓点
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
            for (int y = 0; y < grayImageEnhanced.Rows; y+= step)
            {
                for (int x = 0; x < grayImageEnhanced.Cols; x+=step)
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

            return points;
        }
        public void ProcessDepth()
        {
            Cv2.ImShow("r", DepthImage);
        }        
        public async void ProcessScratch()
        {
            if (OriginalImage == null) return;
            if (OriginalImage.Width == 0) return; 
            if (SampledPoints.Count > MaximumPointCount) return;
            Mat scratchImage = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
            try
            {
                IsNotProcessingSvg = false;
                string mysvg = await SvgPainter.BuildSvgPath(SampledPoints, DepthImage, ZeroDepth, AFactor, BFactor, PreviewDense, IsPositiveDepthPointOnly, scratchImage);
                DisplayImageScratch = scratchImage.ToWriteableBitmap();
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
        public async void ProcessExportScratch()
        {
            if (OriginalImage == null) return;
            if (OriginalImage.Width == 0) return;
            if (SampledPoints.Count > MaximumPointCount) return;

            Mat scratchImage = new Mat(OriginalImage.Size(), MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
            try
            {
                IsNotProcessingSvg = false;
                string mysvg = await SvgPainter.BuildSvgPath(SampledPoints, DepthImage, ZeroDepth, AFactor, BFactor, PreviewDense, IsPositiveDepthPointOnly);
                SaveSvgToFile(mysvg);
                DisplayImageScratch = scratchImage.ToWriteableBitmap();
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
