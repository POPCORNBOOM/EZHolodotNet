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
    using System.Windows.Media;
    using System.Collections.Concurrent;

    public class SvgPainter
    {
        // width="%wpx" height="%hpx"
        //private const string SvgHeader = "<svg width=\"%wpx\" height=\"%hpx\" viewBox=\"%v\" xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\">\n";
        private const string SvgHeader = "<svg width=\"%wpx\" height=\"%hpx\" xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\">\n";
        private const string SvgFooter = "</svg>";

        public static async Task<string> BuildSvgPath(
            List<Point> points,
            Mat? depthImage,
            int zeroHeight = 128, 
            int ignoreHeightDistance = 0,
            float aFactor = 0.16f,
            float bFactor = 1000,
            int previewDense = 10,
            bool isPositiveDepthPointOnly = false)
        {
            if (depthImage == null)
            {
                throw new ArgumentNullException(nameof(depthImage));
            }
            /*
            return await Task.Run(() =>
            {
                var sb = new StringBuilder();
                sb.Append(SvgHeader.Replace("%w",depthImage.Cols.ToString()).Replace("%h",depthImage.Rows.ToString()));

                int imageWidth = depthImage.Width;
                float curvatureFactor = imageWidth / bFactor;
                float offsetFactor = (1 + 3 * aFactor) / 4;

                foreach (var point in points)
                {
                    float depth = depthImage.Get<float>(point.Y, point.X);
                    if (isPositiveDepthPointOnly && depth < zeroHeight) continue;
                    if (MathF.Abs(depth - zeroHeight) < ignoreHeightDistance) continue;

                    float curvature = (depth - zeroHeight) * imageWidth / bFactor;

                    float offset = offsetFactor * curvature;
                    float curvatureAFactor = curvature * aFactor;

                    float x0 = point.X - curvature;
                    float x1 = point.X + curvature;
                    float y0 = point.Y - curvature + offset;
                    float h_x0 = point.X - curvatureAFactor;
                    float h_x1 = point.X + curvatureAFactor;
                    float h_y = point.Y - curvatureAFactor + offset;

                    sb.AppendFormat(
                        "<path d=\"M {0:0.00},{1:0.00} C {2:0.00},{3:0.00} {4:0.00},{5:0.00} {6:0.00},{7:0.00}\" stroke=\"black\" fill=\"none\" stroke-width=\"1\"/>\n",
                        x0, y0, h_x0, h_y, h_x1, h_y, x1, y0);
                }

                sb.Append(SvgFooter);
                return sb.ToString();
            });*/
            return await Task.Run(() =>
            {
                //float minX = float.MaxValue , minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                var sb = new StringBuilder();
                

                int imageWidth = depthImage.Width;
                float curvatureFactor = imageWidth / bFactor;
                float offsetFactor = (1 + 3 * aFactor) / 4;

                // Thread-safe collection to store SVG path strings
                var pathBag = new ConcurrentBag<string>();

                Parallel.ForEach(points, point =>
                {
                    float depth = depthImage.Get<float>(point.Y, point.X);
                    if (isPositiveDepthPointOnly && depth < zeroHeight) return;
                    if (MathF.Abs(depth - zeroHeight) < ignoreHeightDistance) return;

                    float curvature = (depth - zeroHeight) * imageWidth / bFactor;
                    float offset = offsetFactor * curvature;
                    float curvatureAFactor = curvature * aFactor;

                    float x0 = point.Xf - curvature;
                    float x1 = point.Xf + curvature;
                    float y0 = point.Yf - curvature + offset;
                    float h_x0 = point.Xf - curvatureAFactor;
                    float h_x1 = point.Xf + curvatureAFactor;
                    float h_y = point.Yf - curvatureAFactor + offset;

                    /*if (x0 < minX) minX = x0;
                    if (x1 > maxX) maxX = x1;
                    if (x1 < minX) minX = x1;
                    if (x0 > maxX) maxX = x0;
                    if (point.Yf < minY) minY = point.Yf;
                    if (point.Yf > maxY) maxY = point.Yf;
                    if (y0 < minY) minY = y0;
                    if (y0 > maxY) maxY = y0;
                    */
                    var pathString = string.Format(
                        "<path d=\"M {0:0.00},{1:0.00} C {2:0.00},{3:0.00} {4:0.00},{5:0.00} {6:0.00},{7:0.00}\" stroke=\"black\" fill=\"none\" stroke-width=\"1\"/>\n",
                        x0, y0, h_x0, h_y, h_x1, h_y, x1, y0);

                    pathBag.Add(pathString);
                });
                sb.Append(SvgHeader.Replace("%w", depthImage.Cols.ToString()).Replace("%h", depthImage.Rows.ToString()));//.Replace("%v", $"{minX},{minY},{maxX - minX},{maxY - minY}"));
                // Combine all paths
                foreach (var path in pathBag)
                {
                    sb.Append(path);
                }

                sb.Append(SvgFooter);
                return sb.ToString();
            });
        }
        public static async Task PreviewPath(List<Point> points, Mat? depthImage, int zeroHeight = 128, int ignoreHeightDistance = 0, float aFactor = 0.16f, float bFactor = 1000, int previewDense = 10, Mat? originalImageL = null, Mat? originalImageR = null, Mat? originalImageO = null, Mat? originalImageLine = null,bool drawLineDensity = false)
        {
            await Task.Run(() =>
            {
                if (depthImage == null)
                {
                    throw new ArgumentNullException(nameof(depthImage));
                }
                bool drawPreview = originalImageL != null;

                int imageWidth = depthImage.Width;
                // 创建一个单通道Mat
                Mat singleChannelMat = new Mat(originalImageLine.Size(), MatType.CV_8UC1, new Scalar(0));
                int MaximumOverlap = 0;
                foreach (var point in points)
                {
                    float depth = depthImage.Get<float>(point.Y, point.X);
                    //if (isPositiveDepthPointOnly && depth < zeroHeight) continue;
                    if (MathF.Abs(depth - zeroHeight) < ignoreHeightDistance) continue;

                    float curvature = (depth - zeroHeight) * imageWidth / bFactor;

                    float offset = (1 + 3 * aFactor) * curvature / 4;
                    float x0 = point.X - curvature;
                    float x1 = point.X + curvature;
                    float y0 = point.Y - curvature + offset;
                    float h_x0 = point.X - curvature * aFactor;
                    float h_x1 = point.X + curvature * aFactor;
                    float h_y = point.Y - curvature * aFactor + offset;

                    if (drawPreview)
                    {
                        // 创建一个集合来存储已经绘制的点，避免重复
                        HashSet<(int, int)> drawnPoints = new HashSet<(int, int)>();

                        for (float i = 0; i < previewDense; i++)
                        {
                            float t = i / previewDense;
                            float x = MathF.Pow((1 - t), 3) * x0 + 3 * MathF.Pow((1 - t), 2) * t * h_x0 + 3 * (1 - t) * MathF.Pow(t, 2) * h_x1 + MathF.Pow(t, 3) * x1;
                            float y = MathF.Pow((1 - t), 3) * y0 + 3 * MathF.Pow((1 - t), 2) * t * h_y + 3 * (1 - t) * MathF.Pow(t, 2) * h_y + MathF.Pow(t, 3) * y0;

                            int roundedX = (int)(x + 0.5);  // 使用四舍五入的方式来减少重复
                            int roundedY = (int)(y + 0.5);

                            // 保证坐标在图像范围内
                            if (roundedX >= 0 && roundedX < originalImageLine.Width && roundedY >= 0 && roundedY < originalImageLine.Height)
                            {
                                // 如果点还没被绘制过，才进行绘制
                                if (!drawnPoints.Contains((roundedX, roundedY)))
                                {
                                    if (!drawLineDensity)
                                    {
                                        Cv2.Circle(originalImageLine, roundedX, roundedY, 1, new Scalar(depth, depth, depth, 255));
                                    }
                                    else
                                    {
                                        int count = singleChannelMat.At<byte>(roundedY, roundedX) += 1;
                                        if (count > MaximumOverlap)
                                            MaximumOverlap = count;
                                        /*singleChannelMat.At<byte>(roundedY + 1, roundedX) += 1;
                                        singleChannelMat.At<byte>(roundedY, roundedX + 1) += 1;
                                        singleChannelMat.At<byte>(roundedY - 1, roundedX) += 1;
                                        singleChannelMat.At<byte>(roundedY, roundedX - 1) += 1;*/
                                    }

                                    // 记录该点已绘制
                                    drawnPoints.Add((roundedX, roundedY));
                                }
                            }
                        }

                        Cv2.Circle(originalImageL, (int)x0, (int)y0, 1, new Scalar(255, 255, 0, 255));
                        Cv2.Circle(originalImageR, (int)x1, (int)y0, 1, new Scalar(255, 0, 255, 255));

                        Cv2.Circle(originalImageO, point.X, point.Y, 1, new Scalar(255, 0, 0, 255));
                        //Cv2.Circle(originalImageO, new (h_x0,h_y), 1, new Scalar(255, 255, 128, 255));
                        //Cv2.Circle(originalImageO, new(h_x1, h_y), 1, new Scalar(255, 128, 255, 255));
                        //Cv2.Circle(originalImageO, new(x0, y0), 1, new Scalar(255, 255, 0, 255));
                        //Cv2.Circle(originalImageO, new(x1, y0), 1, new Scalar(255, 0, 255, 255));
                    }
                }
                // 归一化单通道图像

                // 使用颜色映射
                if (drawLineDensity)
                {
                    Mat normalizedMat = new Mat();
                    Cv2.Normalize(singleChannelMat, normalizedMat, 0, 255, NormTypes.MinMax);

                    Cv2.Blur(normalizedMat, normalizedMat, new Size(3, 3));
                    Mat coloredMat = new Mat();
                    //Cv2.ImShow("debug", normalizedMat);
                    Cv2.ApplyColorMap(normalizedMat, coloredMat, ColormapTypes.Jet);
                    Cv2.PutText(coloredMat, MaximumOverlap.ToString(), new(4, 32), HersheyFonts.HersheySimplex, 1, new(0, 0, 255),3);
                    // 将颜色映射结果存储回 originalImageLine（如果需要）
                    coloredMat.CopyTo(originalImageLine);
                }
            });
        }
        public static async Task PreviewPathParallel(List<Point> points, Mat? depthImage, Mat? originalImage, Mat? outImageLeft, Mat? outImageRight, float step, float stepSpan, int zeroHeight = 128, int ignoreHeightDistance = 0, float aFactor = 0.16f, float bFactor = 1000f, int previewDense = 10, bool isPositiveDepthPointOnly = false, string color = "c")
        {
            await Task.Run(() =>
            {
                step *= (1 - stepSpan);
                if (depthImage == null)
                {
                    throw new ArgumentNullException(nameof(depthImage));
                }
                int imageWidth = depthImage.Width;

                if (color.StartsWith("c"))
                {
                    foreach (var point in points)
                    {
                        float depth = depthImage.Get<float>(point.Y, point.X);
                        Vec3b mcolor = originalImage.Get<Vec3b>(point.Y, point.X);
                        if (isPositiveDepthPointOnly && depth < zeroHeight) continue;
                        if (MathF.Abs(depth - zeroHeight) < ignoreHeightDistance) continue;

                        float curvature = (depth - zeroHeight) * imageWidth / bFactor;

                        float offset = (1 + 3 * aFactor) * curvature / 4;
                        float x0 = point.X - curvature;
                        float x1 = point.X + curvature;
                        float y0 = point.Y - curvature + offset;
                        float h_x0 = point.X - curvature * aFactor;
                        float h_x1 = point.X + curvature * aFactor;
                        float h_y = point.Y - curvature * aFactor + offset;

                        float Cal(float t,bool forX = true)
                        {
                            return forX? MathF.Pow((1 - t), 3) * x0 + 3 * MathF.Pow((1 - t), 2) * t * h_x0 +
                                       3 * (1 - t) * MathF.Pow(t, 2) * h_x1 + MathF.Pow(t, 3) * x1:
                                       MathF.Pow((1 - t), 3) * y0 + 3 * MathF.Pow((1 - t), 2) * t * h_y +
                                       3 * (1 - t) * MathF.Pow(t, 2) * h_y + MathF.Pow(t, 3) * y0;
                        }
                        Cv2.Circle(outImageLeft, (int)Cal(step), (int)Cal(step,false), 1, new Scalar(mcolor[0], mcolor[1], mcolor[2], 255));
                        Cv2.Circle(outImageRight, (int)Cal(step + stepSpan), (int)Cal(step + stepSpan,false), 1, new Scalar(mcolor[0], mcolor[1], mcolor[2], 255));
                    }
                }
                else if (color.StartsWith("d"))
                {
                    foreach (var point in points)
                    {
                        float depth = depthImage.Get<float>(point.Y, point.X);
                        if (isPositiveDepthPointOnly && depth < zeroHeight) continue;

                        float curvature = (depth - zeroHeight) * imageWidth / bFactor;

                        float offset = (1 + 3 * aFactor) * curvature / 4;
                        float x0 = point.X - curvature;
                        float x1 = point.X + curvature;
                        float y0 = point.Y - curvature + offset;
                        float h_x0 = point.X - curvature * aFactor;
                        float h_x1 = point.X + curvature * aFactor;
                        float h_y = point.Y - curvature * aFactor + offset;

                        float Cal(float t, bool forX = true)
                        {
                            return forX ? MathF.Pow((1 - t), 3) * x0 + 3 * MathF.Pow((1 - t), 2) * t * h_x0 +
                                          3 * (1 - t) * MathF.Pow(t, 2) * h_x1 + MathF.Pow(t, 3) * x1 :
                                MathF.Pow((1 - t), 3) * y0 + 3 * MathF.Pow((1 - t), 2) * t * h_y +
                                3 * (1 - t) * MathF.Pow(t, 2) * h_y + MathF.Pow(t, 3) * y0;
                        }
                        Cv2.Circle(outImageLeft, (int)Cal(step), (int)Cal(step, false), 1, new Scalar(depth, depth, depth, 255)); 
                        Cv2.Circle(outImageRight, (int)Cal(step + stepSpan), (int)Cal(step + stepSpan, false), 1, new Scalar(depth, depth, depth, 255)); 

                    }
                }
                else
                {
                    var mcolor = HexToScalar(color);
                    foreach (var point in points)
                    {
                        float depth = depthImage.Get<float>(point.Y, point.X);
                        if (isPositiveDepthPointOnly && depth < zeroHeight) continue;

                        float curvature = (depth - zeroHeight) * imageWidth / bFactor;

                        float offset = (1 + 3 * aFactor) * curvature / 4;
                        float x0 = point.X - curvature;
                        float x1 = point.X + curvature;
                        float y0 = point.Y - curvature + offset;
                        float h_x0 = point.X - curvature * aFactor;
                        float h_x1 = point.X + curvature * aFactor;
                        float h_y = point.Y - curvature * aFactor + offset;

                        float Cal(float t, bool forX = true)
                        {
                            return forX ? MathF.Pow((1 - t), 3) * x0 + 3 * MathF.Pow((1 - t), 2) * t * h_x0 +
                                          3 * (1 - t) * MathF.Pow(t, 2) * h_x1 + MathF.Pow(t, 3) * x1 :
                                MathF.Pow((1 - t), 3) * y0 + 3 * MathF.Pow((1 - t), 2) * t * h_y +
                                3 * (1 - t) * MathF.Pow(t, 2) * h_y + MathF.Pow(t, 3) * y0;
                        }
                        Cv2.Circle(outImageLeft, (int)Cal(step), (int)Cal(step, false), 1, mcolor);
                        Cv2.Circle(outImageRight, (int)Cal(step + stepSpan), (int)Cal(step + stepSpan, false), 1, mcolor);
                    }
                }
            });

        }
        public static async Task PreviewPath(List<Point> points, Mat? depthImage, Mat? originalImage, Mat? outImage, float step, int zeroHeight = 128, int ignoreHeightDistance = 0, float aFactor = 0.16f, float bFactor = 1000, int previewDense = 10, bool isPositiveDepthPointOnly = false,string color = "c")
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
                        float depth = depthImage.Get<float>(point.Y, point.X);
                        Vec3b mcolor = originalImage.Get<Vec3b>(point.Y, point.X);
                        if (isPositiveDepthPointOnly && depth < zeroHeight) continue;
                        if (MathF.Abs(depth - zeroHeight) < ignoreHeightDistance) continue;

                        float curvature = (depth - zeroHeight) * imageWidth / bFactor;

                        float offset = (1 + 3 * aFactor) * curvature / 4;
                        float x0 = point.X - curvature;
                        float x1 = point.X + curvature;
                        float y0 = point.Y - curvature + offset;
                        float h_x0 = point.X - curvature * aFactor;
                        float h_x1 = point.X + curvature * aFactor;
                        float h_y = point.Y - curvature * aFactor + offset;

                        float t = step;
                        float x = MathF.Pow((1 - t), 3) * x0 + 3 * MathF.Pow((1 - t), 2) * t * h_x0 +
                                   3 * (1 - t) * MathF.Pow(t, 2) * h_x1 + MathF.Pow(t, 3) * x1;
                        float y = MathF.Pow((1 - t), 3) * y0 + 3 * MathF.Pow((1 - t), 2) * t * h_y +
                                   3 * (1 - t) * MathF.Pow(t, 2) * h_y + MathF.Pow(t, 3) * y0;
                        Cv2.Circle(outImage, new(x, y), 1, new Scalar(mcolor[0], mcolor[1], mcolor[2], 255));
                    }
                }
                else if (color.StartsWith("d"))
                {
                    foreach (var point in points)
                    {
                        float depth = depthImage.Get<float>(point.Y, point.X);
                        if (isPositiveDepthPointOnly && depth < zeroHeight) continue;

                        float curvature = (depth - zeroHeight) * imageWidth / bFactor;

                        float offset = (1 + 3 * aFactor) * curvature / 4;
                        float x0 = point.X - curvature;
                        float x1 = point.X + curvature;
                        float y0 = point.Y - curvature + offset;
                        float h_x0 = point.X - curvature * aFactor;
                        float h_x1 = point.X + curvature * aFactor;
                        float h_y = point.Y - curvature * aFactor + offset;

                        float t = step;
                        float x = MathF.Pow((1 - t), 3) * x0 + 3 * MathF.Pow((1 - t), 2) * t * h_x0 +
                                   3 * (1 - t) * MathF.Pow(t, 2) * h_x1 + MathF.Pow(t, 3) * x1;
                        float y = MathF.Pow((1 - t), 3) * y0 + 3 * MathF.Pow((1 - t), 2) * t * h_y +
                                   3 * (1 - t) * MathF.Pow(t, 2) * h_y + MathF.Pow(t, 3) * y0;
                        Cv2.Circle(outImage, new(x, y), 1, new Scalar(depth, depth, depth, 255));
                    }
                }
                else
                {
                    var mcolor = HexToScalar(color);
                    foreach (var point in points)
                    {
                        float depth = depthImage.Get<float>(point.Y, point.X);
                        if (isPositiveDepthPointOnly && depth < zeroHeight) continue;

                        float curvature = (depth - zeroHeight) * imageWidth / bFactor;

                        float offset = (1 + 3 * aFactor) * curvature / 4;
                        float x0 = point.X - curvature;
                        float x1 = point.X + curvature;
                        float y0 = point.Y - curvature + offset;
                        float h_x0 = point.X - curvature * aFactor;
                        float h_x1 = point.X + curvature * aFactor;
                        float h_y = point.Y - curvature * aFactor + offset;

                        float t = step;
                        float x = MathF.Pow((1 - t), 3) * x0 + 3 * MathF.Pow((1 - t), 2) * t * h_x0 +
                                   3 * (1 - t) * MathF.Pow(t, 2) * h_x1 + MathF.Pow(t, 3) * x1;
                        float y = MathF.Pow((1 - t), 3) * y0 + 3 * MathF.Pow((1 - t), 2) * t * h_y +
                                   3 * (1 - t) * MathF.Pow(t, 2) * h_y + MathF.Pow(t, 3) * y0;
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

