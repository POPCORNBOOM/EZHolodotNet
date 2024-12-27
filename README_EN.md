### Scratch Mark Calculation and Analysis

[English](https://github.com/POPCORNBOOM/EZHolodotNet/blob/main/README_EN.md)|[中文](https://github.com/POPCORNBOOM/EZHolodotNet/blob/main/README.md)

Since SVG does not directly support hyperbolic segments, our goal is to generate an SVG scratch path using cubic Bézier curves.

---

#### **1. Mapping Height to Curves**

- **Height Value Calculation**  
  The height of each point is determined by the pixel value from the `depthImage` and subtracting a baseline height `zeroHeight`.

$$
  depth = depthImage(y, x) - zeroHeight
$$

- **Curvature**  
  Curvature is mapped by translating the height value to the image width. Points with a height greater than zero will produce an upward curve, while points with a negative height will curve downward. The formula for curvature is:

$$
  curvature = \frac{depth \times imageWidth}{bFactor}
$$

- **Offset**  
  Since cubic functions do not pass through the origin at the vertex, we substitute \( t = 0.5 \) into the following equation to obtain an offset:

$$
  offset = \frac{(1 + 3 \times aFactor) \times curvature}{4}
$$

- **Original Function**

![Original Function](https://github.com/user-attachments/assets/644e49ac-1081-4be3-a704-3025e4440fca)

- **After Compensation**

![After Compensation](https://github.com/user-attachments/assets/218fd7d7-90b7-4e14-8cd3-3115389c5088)

---

#### **2. Bézier Curve Control Points**

We use a **cubic Bézier curve** to define the path, which consists of a start point, an endpoint, and two control points.

- **Origin (Compensation)**

$$
  \mathbf{C} = \begin{bmatrix} x \\\ y \end{bmatrix} + \begin{bmatrix} 1 \\\ offset \end{bmatrix}
$$

- **Start and End Points**

$$
  \mathbf{P_0} = C + curvature \times \begin{bmatrix}  - 1 \\\  - 1 \end{bmatrix}
$$

$$
  \mathbf{P_3} = C + curvature \times \begin{bmatrix}  1 \\\ - 1 \end{bmatrix}
$$

- **Control Points**

$$
  \mathbf{P_1} = C + curvature \times \begin{bmatrix} - 1 \\\ - 1 \end{bmatrix}  \times aFactor
$$

$$
  \mathbf{P_2} = C + curvature \times \begin{bmatrix} 1 \\\ - 1 \end{bmatrix} \times aFactor
$$

---

#### **3. Bézier Curve Equation**

The mathematical equation for a cubic Bézier curve is:

$$
\mathbf{P}(t) = (1-t)^3 \mathbf{P_0} + 3(1-t)^2 t \mathbf{P_1} + 3(1-t) t^2 \mathbf{P_2} + t^3 \mathbf{P_3} \quad【1】
$$

- \( t \) is the parameter on the curve, where \( t \) ranges from 0 to 1.  
- When \( t = 0 \), the point is at the start point \( P_0 \).  
- When \( t = 1 \), the point is at the endpoint \( P_3 \).  
- The control points \( P_1 \) and \( P_2 \) determine the shape and curvature of the curve.

---

#### **4. SVG Path Generation**

For each point, a cubic Bézier curve is generated and added to the SVG path:

$$
\text{<path d="M P0.x,P0.y C P1.x,P1.y P2.x,P2.y P3.x,P3.y" />}
$$

- **M (Move To)**: Move to the start point $( P_0 )$
- **C (Cubic Bézier Curve)**: Define control points and endpoint $( P_1, P_2, P_3 )$
