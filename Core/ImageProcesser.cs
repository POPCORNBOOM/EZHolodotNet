using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
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
                MyLinePoints = ExtractContours();
            }
        });
        public RelayCommand ProcessDepthCommand => new RelayCommand(ProcessDepth);

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
        public List<Point> MyLinePoints = new();
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
        public double LineDensity { get; set; } = 10;
        public double AreaDensity { get; set; } = 10;
        public int ZeroDepth { get; set; } = 128;
        public List<Point2d> Points { get; set; } = new List<Point2d>();
        public DepthEstimation DepthEstimation = new DepthEstimation();
        private Point _mousePoint = new(0, 0);
        public int MousePointX
        { get => MousePoint.X; }
        public int MousePointY { get => MousePoint.Y; }

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
        public double FactorA { get; set; } = 0.16;
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
                    MyLinePoints = ExtractContours();

                }
            }
        }
        private double _threshold1 = 100;
        private double _threshold2 = 200;
        public double Threshold2
        {
            get => _threshold2;
            set
            {
                if (!Equals(_threshold2, value))
                {
                    _threshold2 = value;
                    OnPropertyChanged(nameof(Threshold2));
                    MyLinePoints = ExtractContours();
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

        public List<Point> ExtractContours(int density = 10)
        {
            if (OriginalImage == null) return new();
            Mat blurred = new Mat();
            Cv2.GaussianBlur(OriginalImage, blurred, new OpenCvSharp.Size(5, 5), 0);

            // 边缘检测 (使用 Canny 算法)
            Mat edges = new Mat();
            Cv2.Canny(blurred, edges, Threshold1, Threshold2);

            // 查找轮廓 FindContours
            var contours = new List<Point>();
            Point[][] contourPoints;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(edges, out contourPoints, out hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);

            // 在ContoursOverDepthImage上绘制轮廓
            ContoursOverDepthImage = new Mat(OriginalImage.Size(), MatType.CV_8UC3, Scalar.White);
            Cv2.DrawContours(ContoursOverDepthImage, contourPoints, -1, Scalar.Red, 1);

            // 显示在Image
            DisplayImageContour = ContoursOverDepthImage.ToWriteableBitmap();
            foreach (var contour in contourPoints)
            {
                for (int i = 0; i < contour.Length; i += density)
                {
                    contours.Add(contour[i]);
                }
            }
            return contours;
        }

        public void ProcessDepth()
        {
            Cv2.ImShow("r", DepthImage);
        }
        private Mat ApplyColorMap(Mat depthMat)
        {
            Mat colorDepthMat = new Mat();
            Cv2.ApplyColorMap(depthMat, colorDepthMat, (ColormapTypes)_depthColor);
            return colorDepthMat;
        }
    }
}
