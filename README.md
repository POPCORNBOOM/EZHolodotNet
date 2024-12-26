### 点转SVG路径的原理解析

该函数的目标是将一系列点通过贝塞尔曲线生成SVG路径，并根据深度图像(`depthImage`)的深度值调整曲线的形态，同时支持在原始图像上预览路径。

---

#### **1. 高度映射到曲线**

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

---

#### **2. 贝塞尔曲线控制点**

使用**三次贝塞尔曲线**定义路径，曲线由起点、终点和两个控制点构成。

- **原点**

$$
  \mathbf{C} = \begin{bmatrix} x \\\ y \end{bmatrix}
$$

- **起点和终点**

$$
  \mathbf{P_0} = C + curvature \times \begin{bmatrix}  - 1 \\\  - 1 \end{bmatrix} + \begin{bmatrix} 1 \\\ offset \end{bmatrix}
$$

$$
  \mathbf{P_3} = C + curvature \times \begin{bmatrix}  1 \\\ - 1 \end{bmatrix} + \begin{bmatrix} 1 \\\ offset \end{bmatrix}
$$

- **控制点**

$$
  \mathbf{P_1} = C + curvature \times \begin{bmatrix} - 1 \\\ - 1 \end{bmatrix}  \times aFactor + \begin{bmatrix} 1 \\\ offset \end{bmatrix}
$$

$$
  \mathbf{P_2} = C + curvature \times \begin{bmatrix} 1 \\\ - 1 \end{bmatrix} \times aFactor + \begin{bmatrix} 1 \\\ offset \end{bmatrix}
$$

---

#### **3. 贝塞尔曲线方程**

三次贝塞尔曲线的数学方程为：

$$
\mathbf{P}(t) = (1-t)^3 \mathbf{P_0} + 3(1-t)^2 t \mathbf{P_1} + 3(1-t) t^2 \mathbf{P_2} + t^3 \mathbf{P_3} 【1】
$$

- \( t \) 是曲线上的参数，取值范围为 $[0, 1]$。  
- 当 \( t = 0 \) 时，点在起点 $P_0$ 。  
- 当 \( t = 1 \) 时，点在终点  $P_3$。  
- 控制点 $P_1$ 和 $P_2$ 决定了曲线的形态和弯曲程度。

---

#### **4. SVG路径生成**

每个点生成一段三次贝塞尔曲线，并追加到SVG路径中：

$$
\text{<path d="M P0.x,P0.y C P1.x,P1.y P2.x,P2.y P3.x,P3.y" />}
$$

- **M (Move To)**：移动到曲线的起始点 \( \mathbf{P_0} \)  
- **C (Cubic Bézier Curve)**：定义控制点和终点 \( \mathbf{P_1} \), \( \mathbf{P_2} \), \( \mathbf{P_3} \)

