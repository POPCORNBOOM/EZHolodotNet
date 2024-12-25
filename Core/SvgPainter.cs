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

        public static async Task<string> BuildSvgPath(List<Point> points, Mat? depthImage, int zeroHeight = 128, double aFactor = 0.16, double bFactor = 1000, int previewDense = 50, bool isPositiveDepthPointOnly = false, Mat? originalImage = null)
        {
            string finalresult = await Task.Run(() =>
            {
                if (depthImage == null)
                {
                    throw new ArgumentNullException(nameof(depthImage));
                }
                bool drawPreview = originalImage != null;

                string result = SvgHeader;

                int imageWidth = depthImage.Width;

                foreach (var point in points)
                {
                    int depth = depthImage.Get<Vec3b>(point.Y, point.X)[0] - zeroHeight;
                    if (isPositiveDepthPointOnly&&depth < 0) continue;

                    double curvature = depth * imageWidth / bFactor;

                    double offset = (1 + 3 * aFactor) * curvature / 4;
                    double x0 = point.X - curvature;
                    double x1 = point.X + curvature;
                    double y0 = point.Y - curvature + offset;
                    double h_x0 = point.X - curvature * aFactor;
                    double h_x1 = point.X + curvature * aFactor;
                    double h_y = point.Y - curvature * aFactor + offset;
                    result += $"<path d=\"M {x0},{y0} C {h_x0},{h_y} {h_x1},{h_y} {x1},{y0}\" stroke=\"black\" fill=\"none\" stroke-width=\"1\"/>\n";

                    // Python code
                    /*
                       num_points = 50  # 曲线上的点的数量
                       for i in range(num_points + 1):
                           t = i / num_points
                           x = (1 - t)**3 * x0 + 3 * (1 - t)**2 * t * h_x0 + 3 * (1 - t) * t**2 * h_x1 + t**3 * x1
                           y = (1 - t)**3 * y0 + 3 * (1 - t)**2 * t * h_y + 3 * (1 - t) * t**2 * h_y + t**3 * y0
                           cv2.circle(final_img, (int(x), int(y)), 1, (255, 255, 255), -1)
                           #print(int(x), int(y))
                       cv2.circle(final_img, (int(x0), int(y0)), 2, (0, 255, 0), -1)  # 起点
                       cv2.circle(final_img, (int(x1), int(y0)), 2, (0, 0, 255), -1)  # 终点

                       #cv2.circle(final_img, (int(h_x0), int(h_y)), 1, (255, 0, 255), -1)  # 手柄1
                       #cv2.circle(final_img, (int(h_x1), int(h_y)), 1, (255, 255, 0), -1)  # 手柄2

                       cv2.circle(final_img, (point[0], point[1]), 3, (255, 0, 0), -1)  # 中心点

                     */
                    if (drawPreview)
                    {
                        for (double i = 0; i < previewDense; i++)
                        {
                            double t = i / previewDense;
                            double x = Math.Pow((1 - t), 3) * x0 + 3 * Math.Pow((1 - t), 2) * t * h_x0 + 3 * (1 - t) * Math.Pow(t, 2) * h_x1 + Math.Pow(t, 3) * x1;
                            double y = Math.Pow((1 - t), 3) * y0 + 3 * Math.Pow((1 - t), 2) * t * h_y + 3 * (1 - t) * Math.Pow(t, 2) * h_y + Math.Pow(t, 3) * y0;
                            Cv2.Circle(originalImage, new(x, y), 1, new Scalar(depth, depth, depth, 255));
                        }
                        Cv2.Circle(originalImage, new(x0, y0), 1, new Scalar(0, 255, 0, 255));
                        Cv2.Circle(originalImage, new(x1, y0), 1, new Scalar(0, 0, 255, 255));

                        Cv2.Circle(originalImage, point, 1, new Scalar(255, 0, 0, 255));
                    }
                }
                result += SvgFooter;
                return result;
            });
            return finalresult;
        }
    }
}

