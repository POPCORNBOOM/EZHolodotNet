# EZHolo功能简介

**EZHolodotNet** 是一款基于 .Net 框架，从 2D 图像/深度图快速生成全息刮擦轨迹并进行 3D 预览的工具。支持自动采样、手动精修、多种后处理与导出功能，适合创作者与工程师使用。

---

## 1. 配置与采样

### 配置导入/导出
<p>保存软件设置，避免重复调整。</p>
<p align="center">
  <img src="https://github.com/user-attachments/assets/22165a3b-e024-498e-a94d-ab6ae1afcfd5" height="180"/>
</p>

### 手动采样
<p>使用画笔/橡皮在深度图上自由绘制、删除采样点；支持 <b>导入/导出</b> 点数据。</p>
<p align="center">
  <img src="https://github.com/user-attachments/assets/5b7dd8db-fad0-4bf3-a7e1-61348cbd6e61" height="250"/>
  <img src="https://github.com/user-attachments/assets/1a6d395c-b57a-44a8-a357-624a5918c83f" height="250"/>
</p>

### 转移到手动采样
<p>将轮廓/明度采样结果转移到手动模式，进行细节编辑。</p>
<p align="center">
  <img src="https://github.com/user-attachments/assets/ab96809f-01e1-416a-be7c-26cdc9137996" height="250"/>
</p>

---

## 2. 采样与后处理

### 涂抹工具
<p>可用于将异常外边缘采样点向内抹平。</p>
<p align="center">
  <img src="https://github.com/user-attachments/assets/d3fd5f82-7e87-4ff5-8fcb-07d0f2cddb95" height="250"/>
  <img src="https://github.com/user-attachments/assets/02946d37-b80b-42ce-9443-3a2af7cd4e2c" height="250"/>
</p>

### 梯度偏移工具
<p>让采样点沿梯度方向/反方向偏移，支持局部涂抹或全局应用。</p>
<p align="center">
  <img src="https://github.com/user-attachments/assets/bfd85751-1435-43ab-a750-d5b5fcdbc514" height="250"/>
  <img src="https://github.com/user-attachments/assets/7b4df010-69c4-4594-9251-422a37af2e4e" height="250"/>
</p>

### 高级去重
<p>高效、均匀去重采样点，避免过密。</p>
<p><b>Before</b></p>
<p align="center">
  <img src="https://github.com/user-attachments/assets/02c266bb-d199-4d40-8e3c-cc41df5ef6d2" height="250"/>
  <img src="https://github.com/user-attachments/assets/01f8beb8-34b0-4fcc-aab6-9cdfb2ded42b" height="250"/>
</p>
<p><b>After</b></p>
<p align="center">
  <img src="https://github.com/user-attachments/assets/dbc41843-7398-4c1f-b455-d73526f3a6b1" height="250"/>
  <img src="https://github.com/user-attachments/assets/31c6c120-c5b0-4a01-926a-f3619d645446" height="250"/>
</p>

### 基于深度的轮廓采样
<p>自动在深度图梯度模最大的位置采样，可配合“二值降噪”排除杂点。</p>
<p align="center">
  <img src="https://github.com/user-attachments/assets/3155a5b3-7dc7-4df6-acd2-c848fcc673b9" height="250"/>
  <img src="https://github.com/user-attachments/assets/f4dc8bce-9fa2-4cf0-ab43-aaaf232455f8" height="250"/>
  <img src="https://github.com/user-attachments/assets/04c0529b-f022-4dae-ac31-aace8cb5942b" height="250"/>
</p>

---

## 3. 可视化指示

### 高度/梯度指示器
<p>显示高度大小，梯度大小方向，辅助判断采样点走向。</p>
<p align="center">
  <img src="https://github.com/user-attachments/assets/ad744c9f-8d87-4965-995e-009543f953f0" height="120"/>
</p>

### 轨迹密度显示
<p>揭示可能刮坏板子的高重叠区域，最高重叠数显示在左上角。</p>
<p align="center">
  <img src="https://github.com/user-attachments/assets/bd560f71-5c9e-4c08-9ac5-c8ee3704feae" height="250"/>
  <img src="https://github.com/user-attachments/assets/dd248d69-a283-405b-9cbb-7d91e869a22c" height="250"/>
</p>

---

## 4. 3D 预览
<p>
将刮擦预览转为 <b>平行眼图像</b>，支持 <b>自动播放</b>；并可在 <b>平行眼/交叉眼模式</b>间切换，交换左右眼视图。
</p>
<p align="center">
  <img src="https://github.com/user-attachments/assets/769ffe63-bf30-459d-8dba-ba9251be90ed" height="250"/>
  <img src="https://github.com/user-attachments/assets/04d3beec-1036-4677-883f-3f42ad9c5939" height="250"/>
</p>

---

## 5. 导出
<ul>
  <li><b>SVG 导出</b>：采样点支持无损导出为 SVG 格式（<code>&lt;circle/&gt;</code> 保存坐标）。</li>
</ul>
<p align="center">
  <img src="https://github.com/user-attachments/assets/dea327d9-e480-4574-a0e0-c38e45515e8d" height="180"/>
</p>

---


# 下载

[windows_anycpu最新发行版本](https://github.com/POPCORNBOOM/EZHolodotNet/releases/latest)

# 刮擦痕迹计算解析

[English](https://github.com/POPCORNBOOM/EZHolodotNet/blob/main/README_EN.md)|[中文](https://github.com/POPCORNBOOM/EZHolodotNet/blob/main/README.md)

由于Svg没有直接的双曲线型线段，我们的目标是通过三次贝塞尔曲线生成SVG刮擦路径。

---

## 1. 高度映射到曲线

- **高度值计算**  
  每个点的高度值由 `depthImage` 的像素值决定，并减去一个基准高度 `zeroHeight`。
  
  $$
  depth = depthImage(y, x) - zeroHeight
  $$

- **“曲率”（Curvature）**  
  曲率通过将高度值映射到图像宽度上，高度大于0的点将产生向上弯曲的曲线，反之向下，公式为：
  
$$
  curvature = \frac{depth \times imageWidth}{bFactor}
$$

- **偏移量（Offset）**  
  由于三次函数在顶点处不通过原点，我们将\( t = 0.5 \)代入后文的【1】取得一个偏移量offset：
  
$$
  offset = \frac{(1 + 3 \times aFactor) \times curvature}{4}
$$

- 原函数

![原函数](https://github.com/user-attachments/assets/644e49ac-1081-4be3-a704-3025e4440fca)

- 添加补偿后

![添加补偿后](https://github.com/user-attachments/assets/218fd7d7-90b7-4e14-8cd3-3115389c5088)



---

## 2. 贝塞尔曲线控制点

使用**三次贝塞尔曲线**定义路径，曲线由起点、终点和两个控制点构成。

- **原点(补偿)**

$$
  \mathbf{C} = \begin{bmatrix} x \\\ y \end{bmatrix} + \begin{bmatrix} 0 \\\ offset \end{bmatrix}
$$

- **起点和终点**

$$
  \mathbf{P_0} = C + curvature \times \begin{bmatrix}  - 1 \\\  - 1 \end{bmatrix}
$$

$$
  \mathbf{P_3} = C + curvature \times \begin{bmatrix}  1 \\\ - 1 \end{bmatrix}
$$

- **控制点**

$$
  \mathbf{P_1} = C + curvature \times \begin{bmatrix} - 1 \\\ - 1 \end{bmatrix}  \times aFactor
$$

$$
  \mathbf{P_2} = C + curvature \times \begin{bmatrix} 1 \\\ - 1 \end{bmatrix} \times aFactor
$$

---

## 3. 贝塞尔曲线方程

三次贝塞尔曲线的数学方程为：

$$
\mathbf{P}(t) = (1-t)^3 \mathbf{P_0} + 3(1-t)^2 t \mathbf{P_1} + 3(1-t) t^2 \mathbf{P_2} + t^3 \mathbf{P_3} 【1】
$$

- \( t \) 是曲线上的参数，取值范围为 $[0, 1]$。  
- 当 \( t = 0 \) 时，点在起点 $P_0$ 。  
- 当 \( t = 1 \) 时，点在终点  $P_3$。  
- 控制点 $P_1$ 和 $P_2$ 决定了曲线的形态和弯曲程度。

---

## 4. SVG路径生成

每个点生成一段三次贝塞尔曲线，并追加到SVG路径中：

$$
\text{<path d="M P0.x,P0.y C P1.x,P1.y P2.x,P2.y P3.x,P3.y" />}
$$

- **M (Move To)**：移动到曲线的起始点 $P_0$
- **C (Cubic Bézier Curve)**：定义控制点和终点 $P_1$, $P_2$, $P_3$

---

[![Stargazers over time](https://starchart.cc/POPCORNBOOM/EZHolodotNet.svg?variant=adaptive)](https://starchart.cc/POPCORNBOOM/EZHolodotNet)

## 捐赠
<p align="center">
  <img src='https://github.com/user-attachments/assets/ffb9ac18-5248-4dc0-9e78-7feaba247eb0' width='360' align='center'/>
</p>
<p align="right">© 2025 Yigu Wang</p>
