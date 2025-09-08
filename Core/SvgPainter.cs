using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
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
using Size = System.Drawing.Size;

namespace EZHolodotNet.Core
{
    using Microsoft.ML.OnnxRuntime;
    using Microsoft.ML.OnnxRuntime.Tensors;
    using OpenCvSharp;  // 引入OpenCVSharp用于图像处理
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Text.Unicode;
    using System.Windows.Media;
    using System.Windows.Shapes;

    public class SvgPainter
    {
        // width="%wpx" height="%hpx"
        //private const string SvgHeader = "<svg width=\"%wpx\" height=\"%hpx\" viewBox=\"%v\" xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\">\n";
        private const string SvgHeader = "<svg width=\"%wpx\" height=\"%hpx\" xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\">\n";
        private const string SvgFooter = "</svg>";

        public static async Task<string> BuildSvgPath(
            List<Point> points,
            Mat? depthImage,
            float zeroHeight = 128,
            float ignoreHeightDistance = 0,
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

        public static async Task<bool> BuildSketch(
            List<Point> points,
            Mat depthImage,
            string outputDirectory,
            float zeroHeight = 128,
            float ignoreHeightDistance = 0,
            float radiusFactor = 1f,
            float angleFactor = 30f,
            int layerCount = 32,
            float radiusLimit = 5,
            float extraWidthFactor = 1.3f,
            string title = "")
        {
            if (depthImage == null) throw new ArgumentNullException(nameof(depthImage));
            if (points == null || points.Count == 0) return false;

            // 确保输出目录存在
            Directory.CreateDirectory(outputDirectory);

            float layerRange = 255f / layerCount;
            var depthGroups = new ConcurrentDictionary<int, ConcurrentBag<Point>>();
            float extraWidth = (extraWidthFactor - 1) * depthImage.Cols;

            await Task.Run(() =>
            {
                // 计算整个点云的边界（用于归一化坐标）
                /*float minX = points.Min(p => p.X);
                float maxX = points.Max(p => p.X);
                float minY = points.Min(p => p.Y);
                float maxY = points.Max(p => p.Y);*/

                // 使用分区器优化并行处理
                var rangePartitioner = Partitioner.Create(0, points.Count);

                Parallel.ForEach(rangePartitioner, (range, state) =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        Point point = points[i];

                        // 边界检查
                        if (point.X < 0 || point.Y < 0 ||
                            point.Y >= depthImage.Rows ||
                            point.X >= depthImage.Cols)
                        {
                            continue;
                        }

                        float depthValue = depthImage.Get<float>(point.Y, point.X);

                        int layerIndex = (int)(depthValue / layerRange);
                        if(!depthGroups.ContainsKey(layerIndex))
                        {
                            depthGroups[layerIndex] = new ConcurrentBag<Point>();
                        }
                        depthGroups[layerIndex].Add(point);

                    }
                });
                float maxLayerRadius = ((depthGroups.Keys.Max() + 0.5f) * layerRange - zeroHeight) * radiusFactor;
                float minLayerRadius = ((depthGroups.Keys.Min() + 0.5f) * layerRange - zeroHeight) * radiusFactor;
                // 为每一层生成SVG
                Parallel.ForEach(depthGroups, layer =>
                {
                    if (layer.Value.Count == 0) return;
                    float maxY = float.MinValue;
                    float minY = float.MaxValue;
                    float depth = (layer.Key + 0.5f) * layerRange - zeroHeight;
                    float radius = depth * radiusFactor;
                    float lineSpaceing = radius * MathF.Cos(angleFactor * MathF.PI / 180);
                    float halfX = radius * MathF.Sin(angleFactor * MathF.PI / 180);

                    bool isConcave = depth > 0;
                    // 创建SVG文件
                    string svgPath = System.IO.Path.Combine(outputDirectory, $"Layer_{layer.Key}.svg");
                    using (StreamWriter writer = new StreamWriter(svgPath))
                    {
                        writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>");
                        writer.WriteLine($"<svg width=\"{depthImage.Cols + extraWidth + 1}\" height=\"{depthImage.Rows + 1}\" viewBox=\"0 0 {depthImage.Cols + extraWidth + 1} {depthImage.Rows + 1}\" xmlns=\"http://www.w3.org/2000/svg\">");

                        // 添加所有点
                        foreach (Point point in layer.Value)
                        {
                            maxY = MathF.Max(maxY, point.Yf);
                            minY = MathF.Min(minY, point.Yf);
                            writer.WriteLine($"<circle cx=\"{point.Xf}\" cy=\"{point.Yf}\" r=\"0.5%\" fill=\"Black\"/>");
                            //writer.WriteLine($"<path d=\"M {point.Xf-halfX} {point.Yf-lineSpaceing} A {radius} {radius} 0 0 1 {point.Xf + halfX} {point.Yf - lineSpaceing}\" stroke=\"black\" stroke-width=\"1px\" fill=\"transparent\"/>");
                        }

                        //辅助裁切线
                        if(MathF.Abs(radius) > radiusLimit)
                        {
                            float lineY = isConcave ? minY : maxY;
                            float lineEnd = isConcave ? 0 : depthImage.Rows;
                            writer.WriteLine($"<line x1=\"0\" y1=\"{lineY}\" x2=\"{depthImage.Cols}\" y2=\"{lineEnd}\" stroke=\"Black\" stroke-width=\"0.5px\"/>");
                            writer.WriteLine($"<line x1=\"0\" y1=\"{lineEnd}\" x2=\"{depthImage.Cols}\" y2=\"{lineY}\" stroke=\"Black\" stroke-width=\"0.5px\"/>");

                            while (isConcave ? lineY < depthImage.Rows : lineY > 0)
                            {
                                writer.WriteLine($"<line x1=\"0\" y1=\"{lineY}\" x2=\"{depthImage.Cols}\" y2=\"{lineY}\" stroke-dasharray=\"5 12\" stroke=\"Black\" stroke-width=\"0.5px\"/>");
                                lineY += lineSpaceing;
                            }
                        }

                        // 圆规半径标识线
                        float markerX = extraWidth /2 + depthImage.Cols;
                        float markerY1 = (depthImage.Rows - radius) / 2;
                        float markerY2 = markerY1 + radius;
                        writer.WriteLine($"<line x1=\"{markerX}\" y1=\"{markerY1}\" x2=\"{markerX}\" y2=\"{markerY2}\" stroke=\"Black\" stroke-width=\"0.5%\"/>");
                        writer.WriteLine($"<path d=\"M {markerX - halfX} {markerY2 - lineSpaceing} A {radius} {radius} 0 0 1 {markerX + halfX} {markerY2 - lineSpaceing}\" stroke=\"black\" stroke-width=\"0.5%\" fill=\"transparent\"/>");


                        // 装饰线
                        writer.WriteLine($"<rect x=\"0\" y=\"0\" width=\"{depthImage.Cols + extraWidth}\" height=\"{depthImage.Rows}\" style=\"stroke: Black; stroke-width: 1; fill: none;\"/>");
                        writer.WriteLine($"<line x1=\"{depthImage.Cols}\" y1=\"0\" x2=\"{depthImage.Cols}\" y2=\"{depthImage.Rows}\" stroke=\"Black\" stroke-width=\"0.5px\"/>");
                        
                        // 添加层信息文本
                        writer.WriteLine($"<text x=\"10\" y=\"20\" font-family=\"Arial\" font-size=\"12\" fill=\"#BBB\">---Layer{layer.Key}/{layerCount};Points{layer.Value.Count}---</text>");
                        writer.WriteLine("</svg>");
                    }


                });
                float desiredHeight = 20;
                string BaseBoardPath = System.IO.Path.Combine(outputDirectory, $"Base.svg");
                float totalHeight = depthImage.Rows + maxLayerRadius - minLayerRadius + 1;
                float totalWidth = depthImage.Cols + 2 * extraWidth +1;
                float stretchFactor = desiredHeight / (layerRange * radiusFactor); // 5: marker text desired height
                using (StreamWriter writer = new StreamWriter(BaseBoardPath))
                {
                    writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>");
                    writer.WriteLine($"<svg width=\"{totalWidth}\" height=\"{totalHeight}\" viewBox=\"{-extraWidth} {minLayerRadius} {totalWidth} {totalHeight}\" xmlns=\"http://www.w3.org/2000/svg\">");
                    
                    writer.WriteLine($"<rect x=\"{-extraWidth}\" y=\"{minLayerRadius}\" width=\"{totalWidth}\" height=\"{totalHeight}\" style=\"stroke: Black; stroke-width: 0.5px; fill: none;\"/>");
                    
                    writer.WriteLine($"<rect x=\"0\" y=\"0\" width=\"{depthImage.Cols}\" height=\"{depthImage.Rows}\" style=\"stroke: Black; stroke-width: 0.5px; fill: none;\"/>");
                    writer.WriteLine($"<line x1=\"{depthImage.Cols}\" y1=\"{minLayerRadius}\" x2=\"{depthImage.Cols}\" y2=\"{maxLayerRadius+depthImage.Rows}\" stroke=\"Black\" stroke-width=\"0.5px\"/>");
                    writer.WriteLine($"<line x1=\"0\" y1=\"{minLayerRadius}\" x2=\"0\" y2=\"{maxLayerRadius+depthImage.Rows}\" stroke=\"Black\" stroke-width=\"0.5px\"/>");
                    
                    writer.WriteLine($"<line x1=\"0\" y1=\"0\" x2=\"{depthImage.Cols}\" y2=\"{depthImage.Rows}\" stroke=\"Black\" stroke-width=\"0.5px\"/>");
                    writer.WriteLine($"<line x1=\"0\" y1=\"{depthImage.Rows}\" x2=\"{depthImage.Cols}\" y2=\"0\" stroke=\"Black\" stroke-width=\"0.5px\"/>");

                    foreach (int l in depthGroups.Keys)
                    {
                        float depth = (l + 0.5f) * layerRange - zeroHeight;
                        float radius = depth * radiusFactor;

                        if(radius>0)
                        {
                            writer.WriteLine(
                                $"<line x1=\"0\" y1=\"{radius}\" x2=\"{-extraWidth * 1 / 3}\" y2=\"{radius}\" stroke=\"Black\" stroke-width=\"0.5px\"/>");
                            writer.WriteLine(
                                $"<line x1=\"{-extraWidth * 2 / 3}\" y1=\"{radius * stretchFactor}\" x2=\"{-extraWidth * 1 / 3}\" y2=\"{radius}\" stroke=\"Black\" stroke-width=\"0.5px\"/>");
                            writer.WriteLine(
                                $"<line x1=\"{-extraWidth * 2 / 3}\" y1=\"{radius * stretchFactor}\" x2=\"{-extraWidth}\" y2=\"{radius * stretchFactor}\" stroke=\"Black\" stroke-width=\"0.5px\"/>");
                            writer.WriteLine($"<text x=\"{-extraWidth}\" y=\"{radius * stretchFactor}\" font-family=\"Arial\" font-size=\"{desiredHeight}\" fill=\"Black\">{l}</text>");
                        }
                        else
                        {
                            writer.WriteLine(
                                $"<line x1=\"{depthImage.Cols}\" y1=\"{radius + depthImage.Rows}\" x2=\"{depthImage.Cols + extraWidth * 1 / 3}\" y2=\"{radius + depthImage.Rows}\" stroke=\"Black\" stroke-width=\"0.5px\"/>");
                            writer.WriteLine(
                                $"<line x1=\"{depthImage.Cols + extraWidth * 1 / 3}\" y1=\"{radius + depthImage.Rows}\" x2=\"{depthImage.Cols + extraWidth * 2 / 3}\" y2=\"{radius * stretchFactor + depthImage.Rows}\" stroke=\"Black\" stroke-width=\"0.5px\"/>");
                            writer.WriteLine(
                                $"<line x1=\"{depthImage.Cols + extraWidth * 2 / 3}\" y1=\"{radius * stretchFactor + depthImage.Rows}\" x2=\"{depthImage.Cols + extraWidth}\" y2=\"{radius * stretchFactor + depthImage.Rows}\" stroke=\"Black\" stroke-width=\"0.5px\"/>");
                            writer.WriteLine($"<text x=\"{depthImage.Cols + extraWidth}\" y=\"{radius * stretchFactor + depthImage.Rows}\" font-family=\"Arial\" font-size=\"{desiredHeight}\" fill=\"Black\" text-anchor=\"end\">{l}</text>");
                        }

                    }
                    writer.WriteLine($"<text x=\"10\" y=\"20\" font-family=\"Arial\" font-size=\"12\" fill=\"#BBB\">---Ext{(extraWidthFactor-1)}---</text>");
                    writer.WriteLine("</svg>");

                }
            });
            GenerateHtmlSummary(
                outputDirectory: outputDirectory,
                title: $"{title}",
                pageTitle: $"{title}",
                layerRange
            );

            return true;
        }

        private static void GenerateHtmlSummary(string outputDirectory, string title, string pageTitle, float layerRange)
        {
            // 读取HTML模板
            string templatePath = System.IO.Path.Combine(AppContext.BaseDirectory, "TEMPLATE.html");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"未找到模板文件：{templatePath}");

            string templateContent = File.ReadAllText(templatePath, Encoding.UTF8);

            // 获取所有SVG文件
            var svgFiles = Directory.GetFiles(outputDirectory, "*.svg")
                .Select(f => new FileInfo(f))
                .OrderBy(f => ExtractLayerNumber(f.Name))
                .ToList();

            if (svgFiles.Count == 0) return;

            // 查找Base.svg文件并提取扩展因子
            float baseExtendFactor = 1.0f; // 默认扩展因子
            var baseFile = svgFiles.FirstOrDefault(f => f.Name.Equals("Base.svg", StringComparison.OrdinalIgnoreCase));
            if (baseFile != null)
            {
                string baseContent = File.ReadAllText(baseFile.FullName);
                // 使用正则表达式匹配扩展因子
                var match = Regex.Match(baseContent, @"---Ext([\d\.]+)---");
                if (match.Success && float.TryParse(match.Groups[1].Value, out float factor))
                {
                    baseExtendFactor = factor;
                }
            }

            // 生成图层内容
            var layerContentBuilder = new StringBuilder();
            foreach (var svgFile in svgFiles)
            {
                int layerNumber = ExtractLayerNumber(svgFile.Name);
                string svgContent = File.ReadAllText(svgFile.FullName);
                bool isBase = svgFile.Name.Equals("Base.svg", StringComparison.OrdinalIgnoreCase);

                // 清理SVG内容（移除XML声明和命名空间）
                string cleanSvg = svgContent
                    .Replace("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>", "")
                    .Replace("xmlns=\"http://www.w3.org/2000/svg\"", "")
                    .Trim();

                // 添加base类名和扩展因子样式
                string layerClass = isBase ? "layer-item base" : "layer-item";
                string styleAttr = isBase ? $" style=\"--ext-mul: {(1+2*baseExtendFactor)/(1+ baseExtendFactor)}\"" : "";

                layerContentBuilder.AppendLine($"        <div class=\"{layerClass}\"{styleAttr}>");
                layerContentBuilder.AppendLine($"            <h3>{(isBase ? "Base" : $"图层 {layerNumber} [{(layerNumber * layerRange):0.0}~{(layerNumber + 1) * layerRange:0.0}]")}</h3>");
                layerContentBuilder.AppendLine($"            {cleanSvg}");
                layerContentBuilder.AppendLine("        </div>");
            }

            // 替换模板中的占位符
            string finalHtml = templateContent
                .Replace("{{PAGE_TITLE}}", pageTitle)
                .Replace("{{TITLE}}", title)
                .Replace("{{LAYER_COUNT}}", svgFiles.Count.ToString())
                .Replace("{{GENERATE_TIME}}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .Replace("{{LAYER_CONTENT}}", layerContentBuilder.ToString().TrimEnd())
                .Replace("{{TOTAL_FILE_SIZE}}", FormatFileSize(svgFiles.Sum(f => f.Length)))
                .Replace("{{OUTPUT_DIRECTORY}}", outputDirectory);

            // 写入最终HTML文件
            string htmlPath = System.IO.Path.Combine(outputDirectory, $"{title}汇总排版打印.html");
            File.WriteAllText(htmlPath, finalHtml, Encoding.UTF8);

            Process.Start("explorer.exe", htmlPath);
        }
        private static int ExtractLayerNumber(string fileName)
        {
            var match = System.Text.RegularExpressions.Regex.Match(fileName, @"Layer_(\d+)\.svg");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }
        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }
        public static async Task PreviewPath(List<Point> points, Mat? depthImage, float zeroHeight = 128, float ignoreHeightDistance = 0, float aFactor = 0.16f, float bFactor = 1000, int previewDense = 10, Mat? originalImageL = null, Mat? originalImageR = null, Mat? originalImageO = null, Mat? originalImageLine = null,bool drawLineDensity = false)
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
        public static async Task PreviewPathArc(List<Point> points, 
            Mat? depthImage, 
            float zeroHeight = 128, 
            float ignoreHeightDistance = 0,
            float radiusFactor = 1f,
            float angleFactor = 30f,
            int layerCount = 32,
            int previewDense = 10, 
            Mat? originalImageL = null, 
            Mat? originalImageR = null, 
            Mat? originalImageO = null, 
            Mat? originalImageLine = null,
            bool drawLineDensity = false)
        {
            await Task.Run(() =>
            {
                if (depthImage == null)
                {
                    throw new ArgumentNullException(nameof(depthImage));
                }
                bool drawPreview = originalImageL != null;
                float layerRange = 255f / layerCount;

                // 创建一个单通道Mat
                Mat singleChannelMat = new Mat(originalImageLine.Size(), MatType.CV_8UC1, new Scalar(0));
                int MaximumOverlap = 0;
                foreach (var point in points)
                {
                    float depth = depthImage.Get<float>(point.Y, point.X);
                    float radius = (((int)(depth / layerRange) + 0.5f) * layerRange - zeroHeight) * radiusFactor;
                    // 公共过滤条件
                    if (MathF.Abs(depth - zeroHeight) < ignoreHeightDistance) continue;


                    if (drawPreview)
                    {
                        // 创建一个集合来存储已经绘制的点，避免重复
                        HashSet<(int, int)> drawnPoints = new HashSet<(int, int)>();

                        for (float i = 0; i < previewDense; i++)
                        {
                            float t = i / previewDense;
                            // 贝塞尔曲线计算
                            float stepAngle = (t * 2 - 1) * angleFactor * MathF.PI / 180;
                            int roundedX = (int)(point.Xf + MathF.Sin(stepAngle) * radius);
                            int roundedY = (int)(point.Yf + (MathF.Cos(stepAngle) - 1) * radius);


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

                        Cv2.Circle(originalImageL, (int)(point.Xf + MathF.Sin(-angleFactor * MathF.PI / 180) * radius), (int)(point.Yf + (MathF.Cos(-angleFactor * MathF.PI / 180) - 1) * radius), 1, new Scalar(255, 255, 0, 255));
                        Cv2.Circle(originalImageR, (int)(point.Xf + MathF.Sin(angleFactor * MathF.PI / 180) * radius), (int)(point.Yf + (MathF.Cos(angleFactor * MathF.PI / 180) - 1) * radius), 1, new Scalar(255, 0, 255, 255));

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
        public static async Task PreviewPath(List<Point> points, Mat? depthImage, Mat? originalImage, Mat? outImage, float step, float zeroHeight = 128, float ignoreHeightDistance = 0, float aFactor = 0.16f, float bFactor = 1000, int previewDense = 10, string color = "c")
        {
            await Task.Run(() =>
            {
                if (depthImage == null) throw new ArgumentNullException(nameof(depthImage));
                int imageWidth = depthImage.Width;

                // 预计算固定颜色值（如果不是c/d模式）
                Scalar? fixedColor = null;
                if (!color.StartsWith("c") && !color.StartsWith("d"))
                {
                    fixedColor = HexToScalar(color);
                }

                foreach (var point in points)
                {
                    float depth = depthImage.Get<float>(point.Y, point.X);

                    // 公共过滤条件
                    if (depth < zeroHeight) continue;
                    if (color.StartsWith("c") && MathF.Abs(depth - zeroHeight) < ignoreHeightDistance) continue;

                    // 公共计算部分
                    float curvature = (depth - zeroHeight) * imageWidth / bFactor;
                    float offset = (1 + 3 * aFactor) * curvature / 4;
                    float x0 = point.X - curvature;
                    float x1 = point.X + curvature;
                    float y0 = point.Y - curvature + offset;
                    float h_x0 = point.X - curvature * aFactor;
                    float h_x1 = point.X + curvature * aFactor;
                    float h_y = point.Y - curvature * aFactor + offset;

                    // 贝塞尔曲线计算
                    float t = step;
                    float x = MathF.Pow(1 - t, 3) * x0 +
                              3 * MathF.Pow(1 - t, 2) * t * h_x0 +
                              3 * (1 - t) * MathF.Pow(t, 2) * h_x1 +
                              MathF.Pow(t, 3) * x1;
                    float y = MathF.Pow(1 - t, 3) * y0 +
                              3 * MathF.Pow(1 - t, 2) * t * h_y +
                              3 * (1 - t) * MathF.Pow(t, 2) * h_y +
                              MathF.Pow(t, 3) * y0;

                    // 动态选择颜色模式
                    Scalar drawColor;
                    if (color.StartsWith("c"))
                    {
                        Vec3b mcolor = originalImage.Get<Vec3b>(point.Y, point.X);
                        drawColor = new Scalar(mcolor[0], mcolor[1], mcolor[2], 255);
                    }
                    else if (color.StartsWith("d"))
                    {
                        drawColor = new Scalar(depth, depth, depth, 255);
                    }
                    else
                    {
                        drawColor = fixedColor.Value;
                    }

                    Cv2.Circle(outImage, (int)x, (int)y, 1, drawColor);
                }
            });
        }
        public static async Task PreviewPathArc(List<Point> points, 
            Mat? depthImage,
            Mat? originalImage,
            Mat? outImage,
            float step,
            float zeroHeight = 128,
            float ignoreHeightDistance = 0,
            float radiusFactor = 1f,
            float angleFactor = 30f,
            int layerCount = 32,
            int previewDense = 10,
            string color = "c")
        {
            await Task.Run(() =>
            {
                if (depthImage == null) throw new ArgumentNullException(nameof(depthImage));
                float layerRange = 255f / layerCount;

                // 预计算固定颜色值（如果不是c/d模式）
                Scalar? fixedColor = null;
                if (!color.StartsWith("c") && !color.StartsWith("d"))
                {
                    fixedColor = HexToScalar(color);
                }

                foreach (var point in points)
                {
                    float depth = depthImage.Get<float>(point.Y, point.X);
                    float radius = (((int)(depth / layerRange) + 0.5f) * layerRange - zeroHeight) * radiusFactor;
                    // 公共过滤条件
                    if (MathF.Abs(depth - zeroHeight) < ignoreHeightDistance) continue;

                    // 贝塞尔曲线计算
                    float stepAngle = (step * 2 - 1) * angleFactor * MathF.PI / 180;
                    float x = point.Xf + MathF.Sin(stepAngle) * radius;
                    float y = point.Yf + (MathF.Cos(stepAngle) - 1) * radius;

                    // 动态选择颜色模式
                    Scalar drawColor;
                    if (color.StartsWith("c"))
                    {
                        Vec3b mcolor = originalImage.Get<Vec3b>(point.Y, point.X);
                        drawColor = new Scalar(mcolor[0], mcolor[1], mcolor[2], 255);
                    }
                    else if (color.StartsWith("d"))
                    {
                        drawColor = new Scalar(depth, depth, depth, 255);
                    }
                    else
                    {
                        drawColor = fixedColor.Value;
                    }

                    Cv2.Circle(outImage, (int)x, (int)y, 1, drawColor);
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

