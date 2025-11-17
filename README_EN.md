[![zread](https://img.shields.io/badge/Ask_Zread-_.svg?style=flat&color=00b0aa&labelColor=000000&logo=data%3Aimage%2Fsvg%2Bxml%3Bbase64%2CPHN2ZyB3aWR0aD0iMTYiIGhlaWdodD0iMTYiIHZpZXdCb3g9IjAgMCAxNiAxNiIgZmlsbD0ibm9uZSIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj4KPHBhdGggZD0iTTQuOTYxNTYgMS42MDAxSDIuMjQxNTZDMS44ODgxIDEuNjAwMSAxLjYwMTU2IDEuODg2NjQgMS42MDE1NiAyLjI0MDFWNC45NjAxQzEuNjAxNTYgNS4zMTM1NiAxLjg4ODEgNS42MDAxIDIuMjQxNTYgNS42MDAxSDQuOTYxNTZDNS4zMTUwMiA1LjYwMDEgNS42MDE1NiA1LjMxMzU2IDUuNjAxNTYgNC45NjAxVjIuMjQwMUM1LjYwMTU2IDEuODg2NjQgNS4zMTUwMiAxLjYwMDEgNC45NjE1NiAxLjYwMDFaIiBmaWxsPSIjZmZmIi8%2BCjxwYXRoIGQ9Ik00Ljk2MTU2IDEwLjM5OTlIMi4yNDE1NkMxLjg4ODEgMTAuMzk5OSAxLjYwMTU2IDEwLjY4NjQgMS42MDE1NiAxMS4wMzk5VjEzLjc1OTlDMS42MDE1NiAxNC4xMTM0IDEuODg4MSAxNC4zOTk5IDIuMjQxNTYgMTQuMzk5OUg0Ljk2MTU2QzUuMzE1MDIgMTQuMzk5OSA1LjYwMTU2IDE0LjExMzQgNS42MDE1NiAxMy43NTk5VjExLjAzOTlDNS42MDE1NiAxMC42ODY0IDUuMzE1MDIgMTAuMzk5OSA0Ljk2MTU2IDEwLjM5OTlaIiBmaWxsPSIjZmZmIi8%2BCjxwYXRoIGQ9Ik0xMy43NTg0IDEuNjAwMUgxMS4wMzg0QzEwLjY4NSAxLjYwMDEgMTAuMzk4NCAxLjg4NjY0IDEwLjM5ODQgMi4yNDAxVjQuOTYwMUMxMC4zOTg0IDUuMzEzNTYgMTAuNjg1IDUuNjAwMSAxMS4wMzg0IDUuNjAwMUgxMy43NTg0QzE0LjExMTkgNS42MDAxIDE0LjM5ODQgNS4zMTM1NiAxNC4zOTg0IDQuOTYwMVYyLjI0MDFDMTQuMzk4NCAxLjg4NjY0IDE0LjExMTkgMS42MDAxIDEzLjc1ODQgMS42MDAxWiIgZmlsbD0iI2ZmZiIvPgo8cGF0aCBkPSJNNCAxMkwxMiA0TDQgMTJaIiBmaWxsPSIjZmZmIi8%2BCjxwYXRoIGQ9Ik00IDEyTDEyIDQiIHN0cm9rZT0iI2ZmZiIgc3Ryb2tlLXdpZHRoPSIxLjUiIHN0cm9rZS1saW5lY2FwPSJyb3VuZCIvPgo8L3N2Zz4K&logoColor=ffffff)](https://zread.ai/POPCORNBOOM/EZHolodotNet)
# EZHolo — Quick Overview

**EZHolodotNet** is a .NET-based tool that quickly generates holographic scratch (etch) strokes from 2D images/depth maps and provides 3D previews. It supports automatic sampling, manual refinement, multiple post-processing options, and export functions. It includes tools for sampling adjustment, de-duplication, density visualization, and cubic Bézier-based SVG export.

---

## 1. Configuration & Sampling

### Import/Export Settings
Save and load application settings to avoid repeated adjustments.

<p align="center">
  <img src="https://github.com/user-attachments/assets/22165a3b-e024-498e-a94d-ab6ae1afcfd5" height="180"/>
</p>

### Manual Sampling
Use brush/eraser to draw or remove sampling points on the depth map; supports point import/export.

<p align="center">
  <img src="https://github.com/user-attachments/assets/5b7dd8db-fad0-4bf3-a7e1-61348cbd6e61" height="250"/>
  <img src="https://github.com/user-attachments/assets/1a6d395c-b57a-44a8-a357-624a5918c83f" height="250"/>
</p>

### Transfer to Manual Mode
Transfer contour/brightness sampling results into manual mode for fine edits.

<p align="center">
  <img src="https://github.com/user-attachments/assets/ab96809f-01e1-416a-be7c-26cdc9137996" height="250"/>
</p>

---

## 2. Sampling & Post-processing

### Smear Tool
Used to push abnormal outer-edge sampling points inward to smooth results.

<p align="center">
  <img src="https://github.com/user-attachments/assets/d3fd5f82-7e87-4ff5-8fcb-07d0f2cddb95" height="250"/>
  <img src="https://github.com/user-attachments/assets/02946d37-b80b-42ce-9443-3a2af7cd4e2c" height="250"/>
</p>

### Gradient Offset Tool
Shift sampling points along (or against) the gradient direction. Can be applied locally with brush or globally.

<p align="center">
  <img src="https://github.com/user-attachments/assets/bfd85751-1435-43ab-a750-d5b5fcdbc514" height="250"/>
  <img src="https://github.com/user-attachments/assets/7b4df010-69c4-4594-9251-422a37af2e4e" height="250"/>
</p>

### Advanced Deduplication
Efficiently and evenly reduce sampling point density to avoid overcrowding.

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

### Depth-based Contour Sampling
Automatically sample at positions of maximal gradient magnitude in the depth map; can be combined with "binary denoise" to exclude stray points.

<p align="center">
  <img src="https://github.com/user-attachments/assets/3155a5b3-7dc7-4df6-acd2-c848fcc673b9" height="250"/>
  <img src="https://github.com/user-attachments/assets/f4dc8bce-9fa2-4cf0-ab43-aaaf232455f8" height="250"/>
  <img src="https://github.com/user-attachments/assets/04c0529b-f022-4dae-ac31-aace8cb5942b" height="250"/>
</p>

---

## 3. Visualization Aids

### Height / Gradient Indicators
Show height magnitude and gradient magnitude/direction to help determine sampling direction.

<p align="center">
  <img src="https://github.com/user-attachments/assets/ad744c9f-8d87-4965-995e-009543f953f0" height="120"/>
</p>

### Stroke Density Display
Reveal high-overlap areas that may over-scratch the plate. The maximum overlap count is shown in the top-left corner.

<p align="center">
  <img src="https://github.com/user-attachments/assets/bd560f71-5c9e-4c08-9ac5-c8ee3704feae" height="250"/>
  <img src="https://github.com/user-attachments/assets/dd248d69-a283-405b-9cbb-7d91e869a22c" height="250"/>
</p>

---

## 4. 3D Preview
Convert etched-stroke preview into parallel-view stereo images with autoplay support; toggle between parallel and cross-eye viewing modes and swap left/right eye views.

<p align="center">
  <img src="https://github.com/user-attachments/assets/769ffe63-bf30-459d-8dba-ba9251be90ed" height="250"/>
  <img src="https://github.com/user-attachments/assets/04d3beec-1036-4677-883f-3f42ad9c5939" height="250"/>
</p>

---

## 5. Export
- SVG export: sampling points can be losslessly exported into SVG (using <code>&lt;circle/&gt;</code> to preserve coordinates).

<p align="center">
  <img src="https://github.com/user-attachments/assets/dea327d9-e480-4574-a0e0-c38e45515e8d" height="180"/>
</p>

---


# Download

Get the latest Windows AnyCPU release:
- https://github.com/POPCORNBOOM/EZHolodotNet/releases/latest

# Scratch (Etch) Stroke Calculation & Analysis

[English](https://github.com/POPCORNBOOM/EZHolodotNet/blob/main/README_EN.md) | [中文](https://github.com/POPCORNBOOM/EZHolodotNet/blob/main/README.md)

Because SVG does not natively support hyperbolic-shaped segments, we generate scratch (etch) paths using cubic Bézier curves.

---

## 1. Mapping Height to Curves

- **Height calculation**  
  Each point's height is determined by the pixel value of the depthImage minus a reference base height zeroHeight.

  $$
  depth = depthImage(y, x) - zeroHeight
  $$

- **Curvature**  
  Curvature maps the height onto the image width. Points with positive heights curve upward; negative heights curve downward:

  $$
  curvature = \frac{depth \times imageWidth}{bFactor}
  $$

- **Offset**  
  Cubic Bézier curves don't pass through the origin at their midpoint; substituting \( t = 0.5 \) into equation [1] below yields an offset:

  $$
  offset = \frac{(1 + 3 \times aFactor) \times curvature}{4}
  $$

- **Original function illustration**

  ![Original function](https://github.com/user-attachments/assets/644e49ac-1081-4be3-a704-3025e4440fca)

- **After applying compensation**

  ![After compensation](https://github.com/user-attachments/assets/218fd7d7-90b7-4e14-8cd3-3115389c5088)

---

## 2. Bézier Control Points

Paths are defined by cubic Bézier curves composed of a start point, end point, and two control points.

- **Compensated center point**

  $$
  \mathbf{C} = \begin{bmatrix} x \\ y \end{bmatrix} + \begin{bmatrix} 0 \\ offset \end{bmatrix}
  $$

- **Start and end points**

  $$
  \mathbf{P_0} = C + curvature \times \begin{bmatrix}  - 1 \\  - 1 \end{bmatrix}
  $$

  $$
  \mathbf{P_3} = C + curvature \times \begin{bmatrix}  1 \\ - 1 \end{bmatrix}
  $$

- **Control points**

  $$
  \mathbf{P_1} = C + curvature \times \begin{bmatrix} - 1 \\ - 1 \end{bmatrix}  \times aFactor
  $$

  $$
  \mathbf{P_2} = C + curvature \times \begin{bmatrix} 1 \\ - 1 \end{bmatrix} \times aFactor
  $$

---

## 3. Cubic Bézier Equation

The cubic Bézier curve is:

$$
\mathbf{P}(t) = (1-t)^3 \mathbf{P_0} + 3(1-t)^2 t \mathbf{P_1} + 3(1-t) t^2 \mathbf{P_2} + t^3 \mathbf{P_3} \quad [1]
$$

- \( t \) is the curve parameter in the range \( [0, 1] \).  
- \( t = 0 \) corresponds to the start point \( P_0 \).  
- \( t = 1 \) corresponds to the end point \( P_3 \).  
- Control points \( P_1 \) and \( P_2 \) shape the curve and determine the bend.

---

## 4. SVG Path Generation

Each sample point generates one cubic Bézier segment appended to the SVG path:

$$
\text{<path d="M P0.x,P0.y C P1.x,P1.y P2.x,P2.y P3.x,P3.y" />}
$$

- **M (Move To)**: Move to the curve's start point \( P_0 \)
- **C (Cubic Bézier Curve)**: Define control points and end point \( P_1 \), \( P_2 \), \( P_3 \)

---

[![Stargazers over time](https://starchart.cc/POPCORNBOOM/EZHolodotNet.svg?variant=adaptive)](https://starchart.cc/POPCORNBOOM/EZHolodotNet)

## Donations
<p align="center">
  <img src='https://github.com/user-attachments/assets/ffb9ac18-5248-4dc0-9e78-7feaba247eb0' width='360' align='center'/>
</p>
<p align="right">© 2025 Yigu Wang</p>
