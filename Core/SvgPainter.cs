using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;
using Size = System.Drawing.Size;

namespace EZHolodotNet.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Diagnostics;
    using Microsoft.ML.OnnxRuntime;
    using Microsoft.ML.OnnxRuntime.Tensors;
    using OpenCvSharp;  // 引入OpenCVSharp用于图像处理

    public class SvgPainter
    {
        private const string SvgHeader = "<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\">\n";
        private const string SvgFooter = "</svg>";

        public static async Task<string> BuildSvgPath(
            List<Point> points,
            Mat? depthImage,
            int zeroHeight = 128,
            double aFactor = 0.16,
            double bFactor = 1000,
            int previewDense = 10,
            bool isPositiveDepthPointOnly = false)
        {
            if (depthImage == null)
            {
                throw new ArgumentNullException(nameof(depthImage));
            }

            return await Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.Append(SvgHeader);

                int imageWidth = depthImage.Width;
                double curvatureFactor = imageWidth / bFactor;
                double offsetFactor = (1 + 3 * aFactor) / 4;

                foreach (var point in points)
                {
                    int depth = depthImage.Get<Vec3b>(point.Y, point.X)[0];
                    if (isPositiveDepthPointOnly && depth < zeroHeight) continue;

                    double curvature = (depth - zeroHeight) * curvatureFactor;

                    double offset = offsetFactor * curvature;
                    double curvatureAFactor = curvature * aFactor;

                    double x0 = point.X - curvature;
                    double x1 = point.X + curvature;
                    double y0 = point.Y - curvature + offset;
                    double h_x0 = point.X - curvatureAFactor;
                    double h_x1 = point.X + curvatureAFactor;
                    double h_y = point.Y - curvatureAFactor + offset;

                    sb.AppendFormat(
                        "<path d=\"M {0},{1} C {2},{3} {4},{5} {6},{7}\" stroke=\"black\" fill=\"none\" stroke-width=\"1\"/>\n",
                        x0, y0, h_x0, h_y, h_x1, h_y, x1, y0);
                }

                sb.Append(SvgFooter);
                return sb.ToString();
            });
        }
        public static async Task PreviewPath(List<Point> points, Mat? depthImage, int zeroHeight = 128, double aFactor = 0.16, double bFactor = 1000, int previewDense = 10, bool isPositiveDepthPointOnly = false, Mat? originalImageL = null, Mat? originalImageR = null, Mat? originalImageO = null, Mat? originalImageLine = null)
        {
            await Task.Run(() =>
            {
                if (depthImage == null)
                {
                    throw new ArgumentNullException(nameof(depthImage));
                }
                bool drawPreview = originalImageL != null;

                int imageWidth = depthImage.Width;

                foreach (var point in points)
                {
                    int depth = depthImage.Get<Vec3b>(point.Y, point.X)[0];
                    if (isPositiveDepthPointOnly && depth < zeroHeight) continue;

                    double curvature = (depth - zeroHeight) * imageWidth / bFactor;

                    double offset = (1 + 3 * aFactor) * curvature / 4;
                    double x0 = point.X - curvature;
                    double x1 = point.X + curvature;
                    double y0 = point.Y - curvature + offset;
                    double h_x0 = point.X - curvature * aFactor;
                    double h_x1 = point.X + curvature * aFactor;
                    double h_y = point.Y - curvature * aFactor + offset;

                    if (drawPreview)
                    {
                        for (double i = 0; i < previewDense; i++)
                        {
                            double t = i / previewDense;
                            double x = Math.Pow((1 - t), 3) * x0 + 3 * Math.Pow((1 - t), 2) * t * h_x0 + 3 * (1 - t) * Math.Pow(t, 2) * h_x1 + Math.Pow(t, 3) * x1;
                            double y = Math.Pow((1 - t), 3) * y0 + 3 * Math.Pow((1 - t), 2) * t * h_y + 3 * (1 - t) * Math.Pow(t, 2) * h_y + Math.Pow(t, 3) * y0;
                            Cv2.Circle(originalImageLine, new(x, y), 1, new Scalar(depth, depth, depth, 255));
                        }
                        Cv2.Circle(originalImageL, new(x0, y0), 1, new Scalar(0, 255, 0, 255));
                        Cv2.Circle(originalImageR, new(x1, y0), 1, new Scalar(0, 0, 255, 255));

                        Cv2.Circle(originalImageO, point, 1, new Scalar(255, 0, 0, 255));
                    }
                }
            });
        }
        public static async Task PreviewPath(List<Point> points, Mat? depthImage, Mat? originalImage, Mat? outImage, double step, int zeroHeight = 128, double aFactor = 0.16, double bFactor = 1000, int previewDense = 10, bool isPositiveDepthPointOnly = false,string color = "c")
        {
            await Task.Run(() =>
            {
                if (depthImage == null)
                {
                    throw new ArgumentNullException(nameof(depthImage));
                }
                int imageWidth = depthImage.Width;

                if (color.StartsWith("c"))
                {
                    foreach (var point in points)
                    {
                        int depth = depthImage.Get<Vec3b>(point.Y, point.X)[0];
                        Vec3b mcolor = originalImage.Get<Vec3b>(point.Y, point.X);
                        if (isPositiveDepthPointOnly && depth < zeroHeight) continue;

                        double curvature = (depth - zeroHeight) * imageWidth / bFactor;

                        double offset = (1 + 3 * aFactor) * curvature / 4;
                        double x0 = point.X - curvature;
                        double x1 = point.X + curvature;
                        double y0 = point.Y - curvature + offset;
                        double h_x0 = point.X - curvature * aFactor;
                        double h_x1 = point.X + curvature * aFactor;
                        double h_y = point.Y - curvature * aFactor + offset;

                        double t = step;
                        double x = Math.Pow((1 - t), 3) * x0 + 3 * Math.Pow((1 - t), 2) * t * h_x0 +
                                   3 * (1 - t) * Math.Pow(t, 2) * h_x1 + Math.Pow(t, 3) * x1;
                        double y = Math.Pow((1 - t), 3) * y0 + 3 * Math.Pow((1 - t), 2) * t * h_y +
                                   3 * (1 - t) * Math.Pow(t, 2) * h_y + Math.Pow(t, 3) * y0;
                        Cv2.Circle(outImage, new(x, y), 1, new Scalar(mcolor[0], mcolor[1], mcolor[2], 255));
                    }
                }
                else if (color.StartsWith("d"))
                {
                    foreach (var point in points)
                    {
                        int depth = depthImage.Get<Vec3b>(point.Y, point.X)[0];
                        if (isPositiveDepthPointOnly && depth < zeroHeight) continue;

                        double curvature = (depth - zeroHeight) * imageWidth / bFactor;

                        double offset = (1 + 3 * aFactor) * curvature / 4;
                        double x0 = point.X - curvature;
                        double x1 = point.X + curvature;
                        double y0 = point.Y - curvature + offset;
                        double h_x0 = point.X - curvature * aFactor;
                        double h_x1 = point.X + curvature * aFactor;
                        double h_y = point.Y - curvature * aFactor + offset;

                        double t = step;
                        double x = Math.Pow((1 - t), 3) * x0 + 3 * Math.Pow((1 - t), 2) * t * h_x0 +
                                   3 * (1 - t) * Math.Pow(t, 2) * h_x1 + Math.Pow(t, 3) * x1;
                        double y = Math.Pow((1 - t), 3) * y0 + 3 * Math.Pow((1 - t), 2) * t * h_y +
                                   3 * (1 - t) * Math.Pow(t, 2) * h_y + Math.Pow(t, 3) * y0;
                        Cv2.Circle(outImage, new(x, y), 1, new Scalar(depth, depth, depth, 255));
                    }
                }
                else
                {
                    var mcolor = HexToScalar(color);
                    foreach (var point in points)
                    {
                        int depth = depthImage.Get<Vec3b>(point.Y, point.X)[0];
                        if (isPositiveDepthPointOnly && depth < zeroHeight) continue;

                        double curvature = (depth - zeroHeight) * imageWidth / bFactor;

                        double offset = (1 + 3 * aFactor) * curvature / 4;
                        double x0 = point.X - curvature;
                        double x1 = point.X + curvature;
                        double y0 = point.Y - curvature + offset;
                        double h_x0 = point.X - curvature * aFactor;
                        double h_x1 = point.X + curvature * aFactor;
                        double h_y = point.Y - curvature * aFactor + offset;

                        double t = step;
                        double x = Math.Pow((1 - t), 3) * x0 + 3 * Math.Pow((1 - t), 2) * t * h_x0 +
                                   3 * (1 - t) * Math.Pow(t, 2) * h_x1 + Math.Pow(t, 3) * x1;
                        double y = Math.Pow((1 - t), 3) * y0 + 3 * Math.Pow((1 - t), 2) * t * h_y +
                                   3 * (1 - t) * Math.Pow(t, 2) * h_y + Math.Pow(t, 3) * y0;
                        Cv2.Circle(outImage, new(x, y), 1, mcolor);
                    }
                }
            });
        }
        public static Scalar HexToScalar(string hexColor)
        {
            // 去掉 '#' 前缀（如果存在）
            if (hexColor.StartsWith("#"))
            {
                hexColor = hexColor.Substring(1);
            }

            // 检查颜色长度（支持 #RRGGBB 或 #RRGGBBAA 格式）
            if (hexColor.Length != 6 && hexColor.Length != 8)
            {
                return new Scalar(255, 0, 0, 255);
            }

            // 提取 R、G、B 值
            byte r = Convert.ToByte(hexColor.Substring(0, 2), 16);
            byte g = Convert.ToByte(hexColor.Substring(2, 2), 16);
            byte b = Convert.ToByte(hexColor.Substring(4, 2), 16);

            // 如果是 #RRGGBBAA 格式，提取 alpha 值，否则默认为 255
            byte alpha = hexColor.Length == 8 ? Convert.ToByte(hexColor.Substring(6, 2), 16) : (byte)255;

            // 返回 Scalar，注意 OpenCV 使用 BGR 排序
            return new Scalar(b, g, r, alpha);
        }
    }
}

