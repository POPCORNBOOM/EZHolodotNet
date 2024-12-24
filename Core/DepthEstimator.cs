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

    public class DepthEstimation
    {
        private InferenceSession _onnxSession;

        public DepthEstimation(string modelPath = @"depth_anything_v2_vitb.onnx")
        {
            // 加载模型
            LoadModel(modelPath);
        }

        // 加载ONNX模型
        public void LoadModel(string modelPath)
        {
            SessionOptions options = new SessionOptions
            {
                LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_INFO
            };
            options.AppendExecutionProvider_CPU(0); // 使用CPU执行（如果有GPU可以更改为CUDA）

            // 加载ONNX模型
            _onnxSession = new InferenceSession(modelPath, options);
        }

        // 关闭ONNX会话
        public void CloseSession()
        {
            _onnxSession?.Dispose();
        }

        // 处理图像并进行深度估计
        public Mat ProcessImage(Mat image)
        {
            // 图像预处理
            var inputTensor = PreprocessImage(image.Clone());

            // 推理
            var depthData = RunInference(inputTensor);

            // 后处理
            Mat depthMat = PostprocessDepth(depthData, image.Height, image.Width);
            Cv2.ConvertScaleAbs(depthMat, depthMat, 1.0);

            return depthMat;
        }

        // 图像预处理：将图像调整为 (518, 518) 并转换为 RGB
        private Tensor<float> PreprocessImage(Mat image, int inputSize = 518)
        {
            // 获取原始图像大小
            int originalHeight = image.Height;
            int originalWidth = image.Width;

            // 调整大小
            //int newWidth = (int)(inputSize * originalWidth / Math.Max(originalHeight, originalWidth));
            //int newHeight = (int)(inputSize * originalHeight / Math.Max(originalHeight, originalWidth));
            //Cv2.Resize(image, image, new OpenCvSharp.Size(newWidth, newHeight), interpolation: InterpolationFlags.Linear);
            Cv2.Resize(image, image, new Size(inputSize, inputSize), interpolation: InterpolationFlags.Linear);

            // 转换为 RGB
            Cv2.CvtColor(image, image, ColorConversionCodes.BGR2RGB);

            // 归一化
            image.ConvertTo(image, MatType.CV_32FC3, 1.0 / 255.0); // [0, 255] -> [0, 1]
            float[] mean = { 0.485f, 0.456f, 0.406f };
            float[] std = { 0.229f, 0.224f, 0.225f };
            image -= new Scalar(mean[0], mean[1], mean[2]);
            Mat stdMat = new Mat(image.Size(), image.Type(), new Scalar(std[0], std[1], std[2]));
            Cv2.Divide(image, stdMat, image);
            // 转换为 Tensor
            var tensor = new DenseTensor<float>(new[] { 1, 3, inputSize, inputSize }); // Batch size = 1
            for (int y = 0; y < image.Rows; y++)
            {
                for (int x = 0; x < image.Cols; x++)
                {
                    Vec3f pixel = image.At<Vec3f>(y, x);
                    tensor[0, 0, y, x] = pixel.Item0; // Red
                    tensor[0, 1, y, x] = pixel.Item1; // Green
                    tensor[0, 2, y, x] = pixel.Item2; // Blue
                }
            }

            return tensor;
        }
        private float[] RunInference(Tensor<float> inputTensor)
        {
            var inputMeta = _onnxSession.InputMetadata;
            var container = new List<NamedOnnxValue>();

            // 将输入张量包装为 ONNX 的输入格式
            foreach (var name in inputMeta.Keys)
            {
                container.Add(NamedOnnxValue.CreateFromTensor(name, inputTensor));
            }

            // 推理
            using var results = _onnxSession.Run(container);
            var outputTensor = results.First().AsEnumerable<float>().ToArray();
            return outputTensor;
        }

        private Mat PostprocessDepth(float[] depth, int height, int width, int inputSize = 518)
        {
            // 归一化深度值到 [0, 255]
            float minDepth = depth.Min();
            float maxDepth = depth.Max();
            float[] normalizedDepth = depth.Select(d => (d - minDepth) / (maxDepth - minDepth) * 255.0f).ToArray();

            // 创建一个 Mat
            Mat depthMat = new Mat(inputSize, inputSize, MatType.CV_32FC1);

            // 将 normalizedDepth 填充到 Mat 中
            int index = 0;
            for (int y = 0; y < inputSize; y++)
            {
                for (int x = 0; x < inputSize; x++)
                {
                    depthMat.Set(y, x, normalizedDepth[index++]);
                }
            }
            Cv2.Resize(depthMat, depthMat, new Size(width, height), interpolation: InterpolationFlags.Linear);
            //Mat normalizedMat = new Mat();
            //Cv2.Normalize(depthMat, normalizedMat, 0, 255, NormTypes.MinMax);
            //return normalizedMat;
            return depthMat;
        }


    }
}

