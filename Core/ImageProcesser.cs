using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

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
        public Mat OriginalImage { get; set; }
        public Mat DepthImage { get; set; } = new Mat();
        public Mat ContoursOverDepthImage { get; set; } = new Mat();
        private WriteableBitmap _displayImage;
        public WriteableBitmap DisplayImage
        {
            get => _displayImage;
            set
            {
                if (!Equals(_displayImage, value))
                {
                    _displayImage = value;
                    OnPropertyChanged(nameof(DisplayImage));
                }
            }
        }
        public double LineDensity { get; set; } = 10;
        public double AreaDensity { get; set; } = 10;
        public int ZeroDepth { get; set; } = 128;
        public List<Point2d> Points { get; set; } = new List<Point2d>();
        public DepthEstimation DepthEstimation = new DepthEstimation();
        public double FactorA { get; set; } = 0.16;
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
        }

        public List<Point> ExtractContours(int density = 10)
        {
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
            Cv2.DrawContours(ContoursOverDepthImage, contourPoints, -1, Scalar.Red, 2);

            // 显示在Image
            DisplayImage = ContoursOverDepthImage.ToWriteableBitmap();
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
            Cv2.ImShow("r", DepthEstimation.ProcessImage(OriginalImage));
        }

    }
}
