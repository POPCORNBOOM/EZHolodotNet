using OpenCvSharp;
using OpenCvSharp.Flann;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace EZHolodotNet.Core
{
    public class OccusionPathHelper
    {
        public void debug(float tick)
        {
            try{
                Mat greyscale = new(_frameDepthMats[(int)(tick * (20 * 2 + 1))].Size(), MatType.CV_8UC1);
                Cv2.ConvertScaleAbs(_frameDepthMats[(int)(tick * (20 * 2 + 1))], greyscale);
                Cv2.ImShow($"DebugGS", greyscale);
            }
            catch (Exception e)
            {

            }
        }
        private List<(float t_start, float t_end)>[]? _pointShownIntervals = null;
        private Mat[]? _frameDepthMats = null;
        private static List<(float t_start, float t_end)> MergeConsecutiveFramesToIntervals(List<int> frameIndices,int totalFrames)
        {
            var intervals = new List<(float t_start, float t_end)>();

            if (frameIndices.Count == 0)
                return intervals;

            // 排序确保连续
            frameIndices.Sort();

            int startFrame = frameIndices[0];
            int endFrame = frameIndices[0];

            for (int i = 0; i < frameIndices.Count; i++)
            {
                if (frameIndices[i] == endFrame + 1 || frameIndices[i] == endFrame)
                {
                    endFrame = frameIndices[i];
                }
                else
                {
                    float t_start = startFrame / (float)(totalFrames - 1);
                    float t_end = endFrame / (float)(totalFrames - 1);
                    intervals.Add((t_start, t_end));

                    startFrame = frameIndices[i];
                    endFrame = frameIndices[i];
                }
            }
            if (endFrame != startFrame)
            {
                float t_start = startFrame / (float)(totalFrames - 1);
                float t_end = endFrame / (float)(totalFrames - 1);
                intervals.Add((t_start, t_end));
            }

            return intervals;
        }
        public readonly float Tolerance = 5f;
        private async Task UpdateHiddenIntervals(
            MissionWithProgress mission,
            List<Point> points, 
            Mat depthImage,
            int FramesCount,
            float ignoreHeightDistance,
            float zeroHeight,
            float aFactor,
            float bFactor)
        {

            mission.Detail = "计算遮挡";
            if (_pointShownIntervals == null || _cachedDepthMatInfo != (depthImage, FramesCount, zeroHeight, aFactor, bFactor) || _pointShownIntervals.Count() != points.Count)
            {
                _pointShownIntervals = new List<(float t_start, float t_end)>[points.Count];
                for (int i = 0; i < points.Count; i++)
                {
                    _pointShownIntervals[i] = new List<(float t_start, float t_end)>();
                }
                int imageWidth = depthImage.Width;
                Parallel.For(0, points.Count, i =>
                {

                    Point point = points[i];
                    float depth = depthImage.Get<float>(point.Y, point.X);

                    // 该点的原始贝塞尔曲线
                    BezierCurve motherCurve = SvgPainter.GenerateCurve(point, depth, zeroHeight, aFactor, imageWidth / bFactor);

                    if (MathF.Abs(depth - zeroHeight) < ignoreHeightDistance)
                        return;

                    // 收集所有隐藏的帧索引
                    List<int> shownFrameIndices = new List<int>();

                    for (int index = 0; index < 2 * FramesCount + 1; index++)
                    {
                        float t = index / (float)(2 * FramesCount);
                        Mat currentFrameMat = _frameDepthMats[index];

                        //Point newPos = point + CalculateBezierCurve(depth, t, aFactor, (depth - zeroHeight) * imageWidth / bFactor);
                        Point newPos = motherCurve.GetPoint(t);

                        if (newPos.X >= 0 && newPos.X < currentFrameMat.Cols &&
                            newPos.Y >= 0 && newPos.Y < currentFrameMat.Rows)
                        {
                            float currentDepth = currentFrameMat.Get<float>(newPos.Y, newPos.X);
                            //if(i==0)
                            //Trace.WriteLine($"Point {i} at {t} SELF {depth} MASK {currentDepth}");
                            if (currentDepth - 1 < depth)
                            {
                                shownFrameIndices.Add(index);
                            }
                        }
                    }

                    // 将连续的帧索引合并为时间区间
                    _pointShownIntervals[i] = MergeConsecutiveFramesToIntervals(shownFrameIndices, 2 * FramesCount + 1);
                    // Trace.WriteLine($"({point.Xf},{point.Yf}) Done");

                });
            }
        }
        private void ClearFrameDepthMats()
        {
            if(_frameDepthMats != null){
                foreach (var mat in _frameDepthMats)
                {
                    mat?.Dispose();
                }
                _frameDepthMats = null;
            }
        }
        private (Mat,int,float,float,float)? _cachedDepthMatInfo = null;
        public async Task UpdateFrameDepthMats(
            MissionWithProgress mission,
            Mat depthImage,
            int FramesCount,
            float zeroHeight,
            float aFactor,
            float bFactor)
        {
            if (_cachedDepthMatInfo == null || _cachedDepthMatInfo != (depthImage, FramesCount, zeroHeight, aFactor, bFactor))
            {
                mission.Detail = "构造深度变换";
                mission.Progress.Report(0);
                //Trace.WriteLine("Updating Frame Depth Mats");
                _cachedDepthMatInfo = (depthImage, FramesCount, zeroHeight, aFactor, bFactor);
                ClearFrameDepthMats();
                _frameDepthMats = new Mat[2 * FramesCount + 1];

                Mat newMat = new(depthImage.Size(), depthImage.Type());
                depthImage.CopyTo(newMat);
                _frameDepthMats[FramesCount] = newMat;
                mission.Progress.Report(1.0 / (2 * FramesCount + 1));

                for (int i = 1; i <= FramesCount; i++)
                {
                    float t = 0.5f * i / (float)(FramesCount);
                    _frameDepthMats[FramesCount + i] = DepthBezierMaping(depthImage, 0.5f + t, zeroHeight, aFactor, bFactor, _frameDepthMats[FramesCount + i - 1]);
                    mission.Progress.Report((2.0 * i) / (2 * FramesCount + 1));
                    _frameDepthMats[FramesCount - i] = DepthBezierMaping(depthImage, 0.5f - t, zeroHeight, aFactor, bFactor, _frameDepthMats[FramesCount - i + 1]);
                    mission.Progress.Report((2.0 * i + 1.0) / (2 * FramesCount + 1));
                    //Trace.Write($"Frame {i} rendered");
                }
            }

        }
        public async Task<string> BuildSvgPath(
            MissionWithProgress mission,
            List<Point> points,
            Mat? depthImage,
            float zeroHeight = 128,
            float ignoreHeightDistance = 0,
            float aFactor = 0.16f,
            float bFactor = 1000,
            int FramesCount = 20)
        {
            if (depthImage == null)
            {
                throw new ArgumentNullException(nameof(depthImage));
            }

            return await Task.Run(async() =>
            {
                var sb = new StringBuilder();
                int imageWidth = depthImage.Width;
                var pathBag = new ConcurrentBag<string>();
                int totalPoints = points.Count;

                // 进度报告节流
                DateTime lastProgressReport = DateTime.MinValue;
                double lastReportedProgress = -1;
                object progressLock = new object();
                try
                {
                    await UpdateFrameDepthMats(mission,depthImage, FramesCount, zeroHeight, aFactor, bFactor);
                    await UpdateHiddenIntervals(mission, points, depthImage, FramesCount, ignoreHeightDistance, zeroHeight, aFactor, bFactor);

                    mission.Detail = "准备导出SVG路径";
                    mission.Progress.Report(0);

                    int processedCount = 0;

                    Parallel.For(0, points.Count, i =>
                    {
                        Point point = points[i];
                        float depth = depthImage.Get<float>(point.Y, point.X);
                        BezierCurve motherCurve = SvgPainter.GenerateCurve(point, depth, zeroHeight, aFactor, imageWidth / bFactor);

                        foreach (var (t_start, t_end) in _pointShownIntervals[i])
                        {
                            BezierCurve subCurve = motherCurve.GetSubCurve(t_start, t_end);
                            pathBag.Add($"<path d=\"{subCurve.GetSvgPath()}\" stroke=\"black\" fill=\"none\" stroke-width=\"1\"/>\n");
                        }

                        //Trace.WriteLine($"({point.Xf},{point.Yf}) Processed");


                        int currentProcessed = Interlocked.Increment(ref processedCount);
                        double progressPercentage = (double)currentProcessed / totalPoints;
                        lock (progressLock)
                        {
                             mission.Progress.Report(progressPercentage);
                        }
                    });

                }
                finally
                {
                    mission.Progress.Report(1.0);
                }

                sb.Append(SvgPainter.SvgHeader.Replace("%w", depthImage.Cols.ToString()).Replace("%h", depthImage.Rows.ToString()));

                foreach (var path in pathBag)
                {
                    sb.Append(path);
                }

                sb.Append(SvgPainter.SvgFooter);
                return sb.ToString();
            });
        }
        /*
        private static Vec2f CalculateBezierCurve(
            float depth,
            float t,
            float aFactor,
            float curvature)
        {
            float offsetFactor = (1 + 3 * aFactor) / 4f;
            float offset = offsetFactor * curvature;

            float x0 = -curvature;
            float x1 = curvature;
            float y0 = -curvature + offset;
            float hX0 = -curvature * aFactor;
            float hX1 = curvature * aFactor;
            float hY = -curvature * aFactor + offset;

            float oneMinusT = 1 - t;
            float oneMinusTSquared = oneMinusT * oneMinusT;
            float tSquared = t * t;

            float x = oneMinusTSquared * oneMinusT * x0
                + 3 * oneMinusTSquared * t * hX0
                + 3 * oneMinusT * tSquared * hX1
                + tSquared * t * x1;

            float y = oneMinusTSquared * oneMinusT * y0
                + 3 * oneMinusTSquared * t * hY
                + 3 * oneMinusT * tSquared * hY
                + tSquared * t * y0;

            return new Vec2f(x,y);
        }*/
        public async Task BuildPathPreview(
            MissionWithProgress mission,
            List<Point> points, 
            Mat? depthImage,
            float zeroHeight = 128,
            float ignoreHeightDistance = 0, 
            float aFactor = 0.16f, 
            float bFactor = 1000, 
            int FramesCount = 10, 
            Mat? originalImageL = null, 
            Mat? originalImageR = null,
            Mat? originalImageO = null, 
            Mat? originalImageLine = null, 
            bool drawLineDensity = false)
        {
            await Task.Run(async() =>
            {
                if (depthImage == null)
                {
                    throw new ArgumentNullException(nameof(depthImage));
                }
  
                await UpdateFrameDepthMats(mission,depthImage, FramesCount, zeroHeight, aFactor, bFactor);
                await UpdateHiddenIntervals(mission, points, depthImage, FramesCount, ignoreHeightDistance, zeroHeight, aFactor, bFactor);
                
                bool drawPreview = originalImageL != null;

                int imageWidth = depthImage.Width;
                // 创建一个单通道Mat
                Mat singleChannelMat = new Mat(originalImageLine.Size(), MatType.CV_8UC1, new Scalar(0));
                int MaximumOverlap = 0;


                for(int pindex = 0;pindex<points.Count;pindex++)
                {
                    Point point = points[pindex];
                    float depth = depthImage.Get<float>(point.Y, point.X);
                    if (MathF.Abs(depth - zeroHeight) < ignoreHeightDistance) continue;

                    BezierCurve bezierCurve = SvgPainter.GenerateCurve(point, depth, zeroHeight, aFactor, imageWidth / bFactor);
                    if (drawPreview)
                    {
                        // 创建一个集合来存储已经绘制的点，避免重复
                        HashSet<(int, int)> drawnPoints = new HashSet<(int, int)>();

                        for (float i = 0; i < FramesCount * 2 + 1; i++)
                        {
                            float t = i / (FramesCount * 2);
                            if(! _pointShownIntervals[pindex].Any(interval => t >= interval.t_start && t <= interval.t_end))
                            {
                                // 该点在当前帧被隐藏，跳过绘制
                                continue;
                            }
                            Point r = bezierCurve.GetPoint(t);
                            int roundedX = (int)(r.Xf + 0.5);  // 使用四舍五入的方式来减少重复
                            int roundedY = (int)(r.Yf + 0.5);

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
                                    }

                                    // 记录该点已绘制
                                    drawnPoints.Add((roundedX, roundedY));
                                }
                            }
                        }

                        Cv2.Circle(originalImageL, bezierCurve.P0.X, bezierCurve.P0.Y, 1, new Scalar(255, 255, 0, 255));
                        Cv2.Circle(originalImageR, bezierCurve.P3.X, bezierCurve.P3.Y, 1, new Scalar(0, 0, 255, 0));

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
                    Cv2.PutText(coloredMat, MaximumOverlap.ToString(), new(4, 32), HersheyFonts.HersheySimplex, 1, new(0, 0, 255), 3);
                    // 将颜色映射结果存储回 originalImageLine（如果需要）
                    coloredMat.CopyTo(originalImageLine);
                }
            });
        }
        public async Task PreviewPath(
            List<Point> points,
            Mat? depthImage,
            Mat? originalImage,
            Mat? outImage,
            float t,
            float zeroHeight = 128,
            float ignoreHeightDistance = 0,
            float aFactor = 0.16f,
            float bFactor = 1000,
            int FramesCount = 10,
            string color = "c")
        {

            await Task.Run(async () =>
            {
                if (depthImage == null) throw new ArgumentNullException(nameof(depthImage));
                int imageWidth = depthImage.Width;

                // 预计算固定颜色值（如果不是c/d模式）
                Scalar? fixedColor = null;
                if (!color.StartsWith("c") && !color.StartsWith("d"))
                {
                    fixedColor = SvgPainter.HexToScalar(color);
                }
                var mission = new MissionWithProgress("预览路径");
                await UpdateFrameDepthMats(mission, depthImage, FramesCount, zeroHeight, aFactor, bFactor);
                await UpdateHiddenIntervals(mission, points, depthImage, FramesCount, ignoreHeightDistance, zeroHeight, aFactor, bFactor);


                //greyscale.Dispose();

                for (int pindex = 0; pindex < points.Count; pindex++)
                {
                    if (!_pointShownIntervals[pindex].Any(interval => t >= interval.t_start && t <= interval.t_end))
                    {
                        // 该点在当前帧被隐藏，跳过绘制
                        continue;
                    }
                    Point point = points[pindex];
                    float depth = depthImage.Get<float>(point.Y, point.X);

                    if (MathF.Abs(depth - zeroHeight) < ignoreHeightDistance) continue;

                    BezierCurve bezierCurve = SvgPainter.GenerateCurve(point, depth, zeroHeight, aFactor, imageWidth / bFactor);

                    Point r = bezierCurve.GetPoint(t);

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

                    Cv2.Circle(outImage, r.X, r.Y, 1, drawColor);
                }
            });
        }

        public static Mat DepthBezierMaping(
            Mat src, 
            float t,
            float zeroHeight,
            float aFactor,
            float bFactor,
            Mat evolveFrom)
        {
            Mat mapx = new Mat(src.Size(), MatType.CV_32F);
            Mat mapy = new Mat(src.Size(), MatType.CV_32F);
            //Mat dst = new Mat(src.Size(), src.Type());
            for (int i = 0; i < src.Rows; i++)
            {
                for (int j = 0; j < src.Cols; j++)
                {
                    //float wave = 10 * (float)Math.Sin(2 * Math.PI * i / 60);
                    float depth = src.Get<float>(i, j);
                    BezierCurve bezierCurve = SvgPainter.GenerateCurve(new Point(0, 0), depth, zeroHeight, aFactor, src.Cols / bFactor);
                    Point offset = bezierCurve.GetPoint(t);
                    mapx.Set<float>(i, j, j + offset.Xf);
                    mapy.Set<float>(i, j, i + offset.Yf);
                }
            }

            //Cv2.Remap(src, dst, mapx, mapy, InterpolationFlags.WarpInverseMap,BorderTypes.Replicate);
            //return dst;
            //Mat blurred = new Mat();
            //Cv2.GaussianBlur(RemapWithMaxParallel(src, mapx, mapy), blurred, new Size(11, 11), 0);
            //return blurred;

            return RemapWithMaxParallel(src, mapx, mapy, evolveFrom);
        }
        public static Mat RemapWithMaxParallel(Mat src, Mat mapx, Mat mapy,Mat evolveFrom)
        {
            Mat dst = new Mat(src.Size(), src.Type(), new Scalar(0));
            var maxData = new float[dst.Rows, dst.Cols];
            var triggerData = new bool[dst.Rows, dst.Cols];

            // 初始化最大值数组
            for (int i = 0; i < dst.Rows; i++)
            {
                for (int j = 0; j < dst.Cols; j++)
                {
                    maxData[i, j] = float.MinValue;
                    triggerData[i, j] = false;
                }
            }

            // 并行处理
            Parallel.For(0, src.Rows, i =>
            {
                for (int j = 0; j < src.Cols; j++)
                {
                    float x = mapx.Get<float>(i, j);
                    float y = mapy.Get<float>(i, j);

                    if (x >= 0 && x < dst.Cols && y >= 0 && y < dst.Rows)
                    {
                        int targetX = (int)Math.Round(Math.Clamp(x, 0, src.Cols-1));
                        int targetY = (int)Math.Round(Math.Clamp(y, 0, src.Rows-1));

                        float srcVal = src.Get<float>(i, j);

                        // 使用锁来保证线程安全
                        lock (maxData)
                        {
                            triggerData[targetY, targetX] = true;
                            if (srcVal > maxData[targetY, targetX])
                            {
                                maxData[targetY, targetX] = srcVal;
                            }
                        }
                    }
                }
            });

            // 将结果复制到Mat
            for (int i = 0; i < dst.Rows; i++){
                for (int j = 0; j < dst.Cols; j++)
                {
                    if (!triggerData[i, j]) dst.Set<float>(i, j, evolveFrom.Get<float>(i, j));
                    else if (maxData[i, j] > float.MinValue) 
                    { 
                       // dst.Set<float>(i, j,Math.Max(maxData[i, j],evolveFrom.Get<float>(i, j))); 
                        dst.Set<float>(i, j, maxData[i, j]); 
                    }
                }
            }
            return dst;
        }
    }

}
