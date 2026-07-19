# URP Learning

这是一个用于学习 Unity 6 URP 自定义渲染的项目。目前主要包含三个效果：全屏褪色、背面外扩描边，以及基于深度和颜色差异的屏幕空间描边。

## 开发环境

- Unity `6000.3.12f1`
- Universal Render Pipeline `17.3.0`
- Input System `1.19.0`
- Unity 工程目录：`URP学习/`

## 效果总览

| 目录 | 效果类型 | 核心方法 |
| --- | --- | --- |
| [Desaturate](./URP学习/Assets/Rendering/Desaturate/) | 全屏后处理 | Shader Graph + RenderGraph Blit 修改画面饱和度 |
| [BackFaceOutlines](./URP学习/Assets/Rendering/BackFaceOutlines/) | 模型空间描边 | 沿顶点法线外扩模型，并只渲染背面 |
| [Outlines_2](./URP学习/Assets/Rendering/Outlines_2/) | 屏幕空间描边 | 对相机深度和场景颜色执行 Sobel 边缘检测 |

## Desaturate：全屏褪色

这个效果把当前相机颜色作为输入，通过 Shader Graph 调整饱和度，再将处理结果写回相机颜色目标。

### 渲染流程

1. `BlitMaterialFeature` 从 `UniversalResourceData.activeColorTexture` 获取当前相机颜色。
2. RenderGraph 创建一张临时颜色纹理。
3. 第一次 Blit 使用褪色材质，把相机颜色处理后写入临时纹理。
4. 第二次 Blit 使用 URP 内置拷贝材质，把临时纹理原样写回相机颜色。

使用临时纹理是为了避免在同一次 Blit 中同时读取和写入同一张颜色纹理。

### 关键文件

- [`Desaturate.shadergraph`](./URP学习/Assets/Rendering/Desaturate/Desaturate.shadergraph)：采样 `_BlitTexture`，通过 `_Saturation` 控制画面饱和度。
- [`DesaturateMat.mat`](./URP学习/Assets/Rendering/Desaturate/DesaturateMat.mat)：褪色效果使用的材质。
- [`BlitMaterialFeature.cs`](./URP学习/Assets/Rendering/Desaturate/BlitMaterialFeature.cs)：可复用的全屏材质 Renderer Feature，使用 URP RenderGraph 添加 Blit Pass。
- [`DesaturateController.cs`](./URP学习/Assets/Scripts/DesaturateController.cs)：在运行时逐渐修改材质的 `_Saturation`。
- [`DesaturateInputNotifier.cs`](./URP学习/Assets/Scripts/DesaturateInputNotifier.cs)：监听 Input System Action，通过 `Action<float>` 事件通知控制器开始褪色。

### 当前学习点

- `ScriptableRendererFeature` 和 `ScriptableRenderPass` 的职责划分。
- `TextureHandle`、`UniversalResourceData` 和 RenderGraph 临时纹理的使用。
- 使用事件把输入检测与视觉效果控制解耦。

## BackFaceOutlines：背面外扩描边

这是常见的 Inverted Hull（反向外壳）描边方案。Shader 在顶点阶段沿法线扩大模型，只渲染扩大模型的背面，因此原模型轮廓外侧会露出一圈纯色。

### 渲染流程

1. 顶点位置沿模型空间法线移动 `_Thickness` 距离。
2. `Cull Front` 剔除外扩模型的正面，只保留背面。
3. Fragment Shader 输出固定的 `_Color`。
4. `_DepthOffset` 调整裁剪空间深度，处理描边与原模型之间的遮挡关系。

### 平滑法线

模型原始法线在硬边处可能造成描边断裂。`OutlineNormalsCalculator` 会：

1. 查找位置相同或足够接近的顶点。
2. 根据三角形顶角对面法线进行加权累积。
3. 将平均后的平滑法线写入指定 UV/TEXCOORD 通道。
4. Shader 启用 `USE_PRECALCULATED_OUTLINE_NORMALS` 后，从 `TEXCOORD1` 读取平滑法线进行外扩。

### 关键文件

- [`BackFaceOutlines.shader`](./URP学习/Assets/Rendering/BackFaceOutlines/BackFaceOutlines.shader)：声明材质属性、正面剔除和 Shader Pass。
- [`BackFaceOutlines.hlsl`](./URP学习/Assets/Rendering/BackFaceOutlines/BackFaceOutlines.hlsl)：完成顶点外扩、深度偏移和纯色输出。
- [`BackFaceOutlines.mat`](./URP学习/Assets/Rendering/BackFaceOutlines/BackFaceOutlines.mat)：描边材质。
- [`OutlineNormalsCalculator.cs`](./URP学习/Assets/Rendering/BackFaceOutlines/OutlineNormalsCalculator.cs)：运行时计算并写入平滑描边法线。

### 当前限制

- `OutlineNormalsCalculator` 依赖 `MeshFilter`，暂不支持 `SkinnedMeshRenderer`。
- 平滑法线目前在运行时计算；更完整的方案应在编辑器导入或烘焙阶段完成。
- 外扩描边更适合封闭模型，在尖锐结构、开口模型和厚度很大时可能出现穿插。

## Outlines_2：深度与颜色边缘描边

这是一个全屏屏幕空间描边效果。它使用 3×3 Sobel 卷积核，分别检测深度不连续和画面颜色变化，再把两种边缘合并成最终描边遮罩。

### 边缘检测流程

1. `DepthSobel_float` 对 `_CameraDepthTexture` 周围九个像素采样，检测物体轮廓和深度断层。
2. `ColorSobel_float` 对 `_CameraOpaqueTexture` 周围九个像素采样，检测同一深度表面上的颜色变化。
3. `FineTuneEdgeDetection` 子图通过 `Smoothstep -> Power -> Multiply` 分别调整阈值、边缘形状和强度。
4. 两种边缘使用 `Maximum` 合并。
5. 最终遮罩作为 Blend 的 Opacity，在原始画面和描边颜色之间混合。

采样偏移使用 `_ScreenParams.xy` 换算，因此 `Thickness = 1` 表示大约一个屏幕像素，而不是一段固定 UV 距离。

### 主要参数

- `Thickness`：Sobel 采样半径，控制总体描边宽度。
- `DepthThreshold`：深度差达到多大时被识别为边缘。
- `DepthThickness`：通过 Power 调整深度边缘遮罩形状。
- `DepthStrength`：深度边缘强度。
- `ColorThreshold`：颜色差达到多大时被识别为边缘。
- `ColorThickness`：调整颜色边缘遮罩形状。
- `ColorStrength`：颜色边缘强度。
- `Color`：最终描边颜色。

### 关键文件

- [`EdgeDetectionOutlines.shadergraph`](./URP学习/Assets/Rendering/Outlines_2/EdgeDetectionOutlines.shadergraph)：组合场景颜色、深度边缘、颜色边缘和最终描边颜色。
- [`EdgeDetectionOutlinesInclude.hlsl`](./URP学习/Assets/Rendering/Outlines_2/EdgeDetectionOutlinesInclude.hlsl)：实现深度与颜色 Sobel 函数。
- [`FineTuneEdgeDetection.shadersubgraph`](./URP学习/Assets/Rendering/Outlines_2/FineTuneEdgeDetection.shadersubgraph)：封装边缘阈值、形状和强度调整。
- [`PostProcessOutlines.mat`](./URP学习/Assets/Rendering/Outlines_2/PostProcessOutlines.mat)：屏幕空间描边材质和参数。

### 依赖与限制

- Renderer Pass 必须请求 `ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color`。
- Shader 必须启用 `REQUIRE_DEPTH_TEXTURE` 和 `REQUIRE_OPAQUE_TEXTURE`。
- Shader Graph 的相机颜色输入使用 `_BlitTexture`，对应 RenderGraph Blit 的默认源纹理属性。
- `SHADERGRAPH_SAMPLE_SCENE_COLOR` 读取 Camera Opaque Texture，因此颜色边缘默认不包含透明物体。
- 这是屏幕空间效果，无法检测被遮挡的几何，也可能受分辨率、抗锯齿和深度精度影响。

## 共用 Renderer Feature

`Desaturate` 和 `Outlines_2` 当前共用 [`BlitMaterialFeature.cs`](./URP学习/Assets/Rendering/Desaturate/BlitMaterialFeature.cs)。它负责执行以下通用流程：

```text
Camera Color -> 使用指定材质 Blit 到临时纹理 -> 原样拷贝回 Camera Color
```

具体效果由 Renderer Feature Inspector 中配置的 Material、Material Pass Index 和 Render Pass Event 决定。

Renderer 配置资源位于：

- [`My Universal Render Pipeline Asset.asset`](./URP学习/Assets/Rendering/My%20Universal%20Render%20Pipeline%20Asset.asset)
- [`My Universal Render Pipeline Asset_Renderer.asset`](./URP学习/Assets/Rendering/My%20Universal%20Render%20Pipeline%20Asset_Renderer.asset)

## 项目定位

这个仓库主要用于记录 URP 学习过程和 API 迁移实践，当前实现以理解渲染流程为主，并非面向生产环境的最终方案。
