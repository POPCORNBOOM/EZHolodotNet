import numpy as np
import cv2
import matplotlib.pyplot as plt

# pointmap 9 个点
pointmap = [
    [0.5, 0.5], [1.5, 0.5], [2.5, 0.5], [0.5, 1.5], [1.5, 1.5], [2.5, 1.5], [0.5, 2.5], [1.5, 2.5], [2.5, 2.5]
]
# 点位对应的深度值
# depth = [[10, 20, 10], [20, 30, 20], [10, 20, 10]]
# 从 gradient255-pyrimid.png 读取深度值
depth_img = cv2.imread("gradient255-pyrimid.png", cv2.IMREAD_ANYDEPTH)
#depth = depth_img.astype(np.float32).tolist()

# 用柏林噪声生成深度图
import noise
width, height = depth_img.shape[1], depth_img.shape[0]
scale = 100.0
depth = [[0 for _ in range(width)] for _ in range(height)]
for y in range(height):
    for x in range(width):
        depth[y][x] = int((noise.pnoise2(x / scale, 
                                         y / scale, 
                                         octaves=6, 
                                         persistence=0.5, 
                                         lacunarity=2.0, 
                                         repeatx=1024, 
                                         repeaty=1024, 
                                         base=42) + 0.5) * 255)


print("Depth Map:")
print(depth)
zero_depth = 50
width = len(depth[0])
height = len(depth)
# 用深度找到点位
depth_reverse_map = {}
for y in range(height):
    for x in range(width):
        # 注意初始情况列表是空的
        d = depth[y][x]
        depth_reverse_map[d] = depth_reverse_map.get(d, []) + [(x, y)]
print("Depth Reverse Map:")
for k in depth_reverse_map:
    print(f"Depth {k}: Blocks {len(depth_reverse_map[k])}")
global_b = 80 / 3
global_a = 0.01

# input (depth,t) output offset(x,y)
"""
获取偏移接口函数
input:
    depth: 当前点的深度值
    t: 动画进度0-1
    delta_depth: 深度变化范围
output:
    offset(x,y): 偏移值
"""
offset_cache = {}


def get_offset(depth, t, a, b):
    if (depth, t, a, b) in offset_cache:
        return offset_cache[(depth, t, a, b)]
    offsetFactor = (1 + 3 * a) / 4
    c = depth / b
    offset = offsetFactor * c
    x0 = -c
    x1 = c
    y0 = -c + offset
    h_x0 = -c * a
    h_x1 = c * a
    h_y = -c * a + offset

    x = (
        np.power(1 - t, 3) * x0
        + 3 * np.power(1 - t, 2) * t * h_x0
        + 3 * (1 - t) * np.power(t, 2) * h_x1
        + np.power(t, 3) * x1
    )
    y = (
        np.power(1 - t, 3) * y0
        + 3 * np.power(1 - t, 2) * t * h_y
        + 3 * (1 - t) * np.power(t, 2) * h_y
        + np.power(t, 3) * y0
    )
    offset_cache[(depth, t, a, b)] = [x, y]
    return [x, y]


def get_interpreted_value(point, value_mat):
    relative_block = (int(point[0]), int(point[1]))
    inside_pos = (point[0] - relative_block[0], point[1] - relative_block[1])
    part_y = (inside_pos[1] - 0.5)
    part_x = (inside_pos[0] - 0.5)
    partial_y = abs(part_y)
    partial_x = abs(part_x)
    block_x = max(0, min(relative_block[0] + (1 if part_x > 0 else -1), len(value_mat[0]) - 1))
    block_y = max(0, min(relative_block[1] + (1 if part_y > 0 else -1), len(value_mat) - 1))
    print(f"Point: {point}, Relative Block: {relative_block}, Inside Pos: {inside_pos}, Partial X: {partial_x}, Partial Y: {partial_y}, Block X: {block_x}, Block Y: {block_y}")
    interpreted_value = (
        value_mat[relative_block[1]][relative_block[0]] * (1 - partial_x) * (1 - partial_y)
        + value_mat[relative_block[1]][block_x] * partial_x * (1 - partial_y)
        + value_mat[block_y][relative_block[0]] * (1 - partial_x) * partial_y
        + value_mat[block_y][block_x] * partial_x * partial_y
    )
    return interpreted_value

"""
def point_is_in_triangle(point, x, y, dx, dy):
    # 三角形是由点(x,y)和偏移(dx,dy)形成的，是一个直角边与坐标轴对齐的直角三角形，水平边长为dx，垂直边长为dy，负数表示向左或向上
    u = (point[0] - x) / dx # 除0未处理
    v = (point[1] - y) / dy
    return 1 >= u >= 0 and 0 <= v <= 1 and u + v <= 1
    
def point_is_in_offset_block(point, block,offset):
    # point: (x,y) 
    # block: (x,y) which is the block's top-left position, width, height are fixed to 1
    # offset: (x,y)
    # block + offset will create a new block, connect the original block and the new block, and check if the point is within this area
    # that is actually checking if the point is within the rectangle formed by the original block and the new block
    
    left = min(block[0], block[0]+offset[0])
    right = max(block[0]+1, block[0]+1+offset[0])
    top = min(block[1], block[1]+offset[1])
    bottom = max(block[1]+1, block[1]+1+offset[1])
    
    if offset[0]>0:
        if point[0]<block[0] or point[0]>block[0]+1+offset[0]:
            return False
        if offset[1]>0:
            # 右下
            if point[1]<block[1] or point[1]>block[1]+1+offset[1]:
                return False
            if point_is_in_triangle(point, left, bottom, offset[0], -offset[1]) or point_is_in_triangle(point, right, top,- offset[0], offset[1]):
                return False
            return True
        else:
            # 右上
            if point[1]<block[1]+offset[1] or point[1]>block[1]+1:
                return False
            if point_is_in_triangle(point, left, top, offset[0], -offset[1]) or point_is_in_triangle(point, right, bottom,- offset[0], offset[1]):
                return False
            return True
    else:
        if point[0]<block[0]+offset[0] or point[0]>block[0]+1:
            return False
        if offset[1]>0:
            # 左下
            if point[1]<block[1] or point[1]>block[1]+1+offset[1]:
                return False
            if point_is_in_triangle(point, left, top,- offset[0], offset[1]) or point_is_in_triangle(point, right, bottom, offset[0], -offset[1]):
                return False
            return True
        else:
            # 左上
            if point[1]<block[1]+offset[1] or point[1]>block[1]+1:
                return False
            if point_is_in_triangle(point, left, bottom,- offset[0], offset[1]) or point_is_in_triangle(point, right, top, offset[0],- offset[1]):
                return False
            return True
"""


def is_point_in_offset_block(point, block, offset):
    if point[0] < min(block[0], block[0] + offset[0]):
        return False
    if point[0] > max(block[0] + 1, block[0] + offset[0] + 1):
        return False
    if point[1] < min(block[1], block[1] + offset[1]):
        return False
    if point[1] > max(block[1] + 1, block[1] + offset[1] + 1):
        return False

    if offset[0] == 0 or offset[1] == 0:
        return True

    k = offset[1] / offset[0]

    if k > 0:
        k1 = -np.sqrt(2) / 2
    else:
        k1 = np.sqrt(2) / 2

    x0 = point[0] - block[0] - 0.5
    y0 = point[1] - block[1] - 0.5

    x1 = (y0 - k1 * x0) / (k - k1)

    return abs(x1 - x0) <= 0.5


# 将[x,y][]array拓展成[x,y,depth][]，并按照depth排序
detailed_points = [[0.0, 0.0, 0.0] for _ in range(len(pointmap))]
for index,point in enumerate(pointmap):
    detailed_points[index][:2] = point
    detailed_points[index][2] = get_interpreted_value(point, depth)
detailed_points = sorted(detailed_points, key=lambda x: x[2])
print("Detailed Points Sorted by Depth:")
print(detailed_points)

# traj: {"origin":P3d(x,y,depth),"traj":[[P2d,P2d,...],[],[],...],"extending":[P2d,P2d,...]}
# output_trajectory = traj[]

output_trajectory = []
for point in detailed_points:
    traj_entry = {"origin": point, "traj": [], "extending": []}
    output_trajectory.append(traj_entry)

print("Output Trajectory Initialized:")
print(output_trajectory)

frames = 20

if frames < 2:
    frames = 2
import tqdm
import time
for step in range(frames):
    t = step / (frames - 1)
    # 【BAD】创建一个3*len(detailed_points)的点集，存放每个点的偏移后的位置[x,y,depth]
    '''frame_scatter_points = np.zeros((len(detailed_points), 3), dtype=np.float32)
    for i, point in enumerate(detailed_points):
        # print(f"Processing Point {i} at Depth {point[2]} with t={t}")
        offset = get_offset(point[2] - 50, t, global_a, global_b)
        frame_scatter_points[i]'''
    # 直接创建 detailed_points 的副本，然后修改x,y
    frame_scatter_points = [row[:] for row in detailed_points]
    for i, point in enumerate(detailed_points):
        offset = get_offset(point[2] - zero_depth, t, global_a, global_b)
        frame_scatter_points[i][0] += offset[0]
        frame_scatter_points[i][1] += offset[1]
    
    # 遍历depth_reverse_map，检查点是否在偏移块内,从最大深度值开始，倒序遍历
    # 目标：对于所有n，检查所有深度小于n的点，是否在"块向n深度块的位移后的新块再向n-1深度块的位移"的区域，n从大到小
    # 对于在区域内的点，将对应值改为None，表示被遮挡
    
    # n 范围：1~len(depth_reverse_map)-1,例如depth_reverse_map有2个深度值，则n=1
    # 初始化画图
    #plt.figure(figsize=(8, 8))
    #plt.xlim(-1, width + 1)
    #plt.ylim(-1, height + 1)
    #plt.gca().set_aspect('equal', adjustable='box')
    #plt.title(f"Frame {step + 1}/{frames}")
    points_to_be_checked = frame_scatter_points.copy()
    depth_keys_sorted = sorted(depth_reverse_map.keys())
    #elements = []
    # # 计时开始
    start_time = time.time()
    for n in tqdm.tqdm(range(len(depth_reverse_map) - 1, 0, -1)):
        
        current_depth = depth_keys_sorted[n]
        previous_depth = depth_keys_sorted[n - 1]
        offset_to_current = get_offset(current_depth - zero_depth, t, global_a, global_b)
        offset_to_previous = get_offset(previous_depth - zero_depth, t, global_a, global_b)
        
        current_blocks = depth_reverse_map[current_depth] #[[x,y],...]
        
        # 从points_to_be_checked筛选掉所有深度大于等于current_depth的点
        points_to_be_checked = [p[:] for p in points_to_be_checked if p[2] < current_depth]
        if len(points_to_be_checked) == 0:
            continue
        #print(f"Depth {current_depth} to {previous_depth}, Checking {len(points_to_be_checked)} Points against {len(current_blocks)} Blocks")
        block_offset = [offset_to_previous[0] - offset_to_current[0], offset_to_previous[1] - offset_to_current[1]]
        for point in points_to_be_checked:
            for block in current_blocks:
                # block: (x,y)
                # 画出块和偏移块
                # 先画block+offset_to_previous，再用直线连接两个块的四对顶点，然后再画block+offset_to_current
                #rect1 = plt.Rectangle((block[0] + offset_to_current[0], block[1] + offset_to_current[1]), 1, 1, linewidth=1, edgecolor='r', facecolor='white', linestyle='-')
                # 画直线连接两个块的四对顶点
                #line1 = plt.Line2D([block[0]+ offset_to_current[0], block[0]+ offset_to_previous[0]], [block[1]+ offset_to_current[1], block[1]+ offset_to_previous[1]], color='r', linestyle='-')
                #line2 = plt.Line2D([block[0]+1+ offset_to_current[0], block[0]+1+ offset_to_previous[0]], [block[1]+ offset_to_current[1], block[1]+ offset_to_previous[1]], color='r', linestyle='-')
                #line3 = plt.Line2D([block[0]+ offset_to_current[0], block[0]+ offset_to_previous[0]], [block[1]+1+ offset_to_current[1], block[1]+1+ offset_to_previous[1]], color='r', linestyle='-')
                #line4 = plt.Line2D([block[0]+1+ offset_to_current[0], block[0]+1+ offset_to_previous[0]], [block[1]+1+ offset_to_current[1], block[1]+1+ offset_to_previous[1]], color='r', linestyle='-')
                # 画block+offset_to_previous
                #rect2 = plt.Rectangle((block[0] + offset_to_previous[0], block[1] + offset_to_previous[1]), 1, 1, linewidth=1, edgecolor='r', facecolor='none', linestyle='-')
                #elements = [rect2, line1, line2, line3, line4, rect1] + elements
                if is_point_in_offset_block(point, [block[0] + offset_to_current[0], block[1] + offset_to_current[1]], block_offset):
                    # 在偏移块内，标记为None
                    idx = frame_scatter_points.index(point)
                    frame_scatter_points[idx][0] = None
                    frame_scatter_points[idx][1] = None
                    del points_to_be_checked[idx]
                    break # 已经被遮挡，无需检查其他块
        #for elem in elements:
        #    plt.gca().add_patch(elem) if isinstance(elem, plt.Rectangle) else plt.gca().add_line(elem)
    # 最后画出frame_scatter_points中非None的点
    for i, point in enumerate(frame_scatter_points):
        if point[0] is not None and point[1] is not None:
            #plt.plot(point[0], point[1], 'bo') 
            # 点未被遮挡，更新output_trajectory
            if output_trajectory[i]["extending"] is None:
                output_trajectory[i]["extending"] = []
            output_trajectory[i]["extending"].append(point[:2])
        else: # 点被遮挡，更新output_trajectory
            if output_trajectory[i]["extending"] is not None:
                output_trajectory[i]["traj"].append(output_trajectory[i]["extending"])
                output_trajectory[i]["extending"] = None
    #plt.show()
    end_time = time.time()
    print(f"Step {step + 1}/{frames} processing time: {end_time - start_time:.2f} seconds")

# 处理最终未闭合的extending
for traj in output_trajectory:
    if traj["extending"] is not None:
        traj["traj"].append(traj["extending"])
        traj["extending"] = None
print("Final Output Trajectory:")
for traj in output_trajectory:
    print(f"Origin: {traj['origin']}")
    for segment in traj["traj"]:
        print(f"  Segment({len(segment)}): {segment}")
    