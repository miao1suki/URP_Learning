# URP Learning

这是一个用于学习 Unity 6 URP 自定义渲染的项目。目前包含全屏褪色、背面外扩描边、自定义深度法线纹理，以及结合深度、颜色和法线差异的屏幕空间描边。

## 开发环境

- Unity `6000.3.12f1`
- Universal Render Pipeline `17.3.0`
- Input System `1.19.0`
- Unity 工程目录：`URP学习/`

## 当前项目进度

- [x] 使用 RenderGraph 完成可复用的全屏材质 Blit，并通过临时纹理避免读写同一颜色目标。
- [x] 完成运行时褪色控制，并通过 Input System 和 `Action<float>` 解耦输入与效果逻辑。
- [x] 完成模型背面外扩描边和平滑法线计算。
- [x] 完成基于相机深度和场景颜色的 Sobel 屏幕空间描边。
- [x] 使用 RendererList 和 Override Material 生成自定义 `_DepthNormalsTexture`。
- [x] 完成深度法线纹理的运行时可视化，并将 Shader Graph 属性声明修正为 `Global`。
- [x] 解码打包的深度与观察空间法线，将法线 Sobel 接入屏幕空间描边，当前场景显示正常。
- [ ] 继续调整不同距离和观察角度下的法线边缘阈值，并验证更多模型与场景。
- [ ] 补充不同平台、动态分辨率、MSAA、透明物体和移动端性能验证。

## 效果总览

| 目录 | 效果类型 | 核心方法 |
| --- | --- | --- |
| [Desaturate](./URP学习/Assets/Rendering/Desaturate/) | 全屏后处理 | Shader Graph + RenderGraph Blit 修改画面饱和度 |
| [BackFaceOutlines](./URP学习/Assets/Rendering/BackFaceOutlines/) | 模型空间描边 | 沿顶点法线外扩模型，并只渲染背面 |
| [DepthNormalsOutlines](./URP学习/Assets/Rendering/DepthNormalsOutlines/) | 深度法线纹理 | 使用 RenderGraph RendererList 生成并发布自定义深度法线纹理 |
| [Outlines_2](./URP学习/Assets/Rendering/Outlines_2/) | 屏幕空间描边 | 对相机深度、场景颜色和观察空间法线执行 Sobel 边缘检测 |

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

## DepthNormalsOutlines：自定义深度法线纹理

这一阶段把旧版 URP 教程中的 `RenderTargetHandle + Configure + Execute` 流程迁移到了 Unity 6 的 RenderGraph。它会额外渲染场景中的不透明物体，并生成供后续全屏 Shader 采样的 `_DepthNormalsTexture`。

### 渲染流程

1. `DepthNormalsFeature` 创建 `Hidden/Internal-DepthNormalsTexture` Override Material。
2. `RecordRenderGraph` 创建临时颜色纹理，并使用相机深度附件进行深度测试。
3. `RenderingUtils.CreateDrawingSettings` 和 `RendererListParams` 描述需要绘制的不透明物体。
4. `ExecutePass` 通过 `DrawRendererList` 实际提交绘制命令。
5. `SetGlobalTextureAfterPass` 将结果发布为全局 `_DepthNormalsTexture`。
6. 调试用 Fullscreen Shader Graph 采样这张纹理，确认打包的深度法线结果正确。

Shader Graph 中的 `_DepthNormalsTexture` 必须使用 `Global` HLSL Declaration。若声明为 `Per Material`，材质中未赋值的纹理槽会覆盖脚本设置的全局纹理，结果表现为全屏白色。

### 关键文件

- [`DepthNormalsFeature.cs`](./URP学习/Assets/Rendering/DepthNormalsOutlines/DepthNormalsFeature.cs)：创建深度法线纹理、RendererList 和 Raster Render Pass。
- [`DepthNormals.shadergraph`](./URP学习/Assets/Rendering/DepthNormalsOutlines/DepthNormals.shadergraph)：运行时显示 `_DepthNormalsTexture` 的 Fullscreen Shader Graph。
- [`DepthNormalsDebug.mat`](./URP学习/Assets/Rendering/DepthNormalsOutlines/DepthNormalsDebug.mat)：深度法线纹理调试材质。

### 当前学习点

- `TextureHandle` 只在当前 RenderGraph 记录周期内有效，不再使用旧版 `RenderTargetHandle.Init`。
- `SetRenderAttachment` 对应旧版 `ConfigureTarget`，RenderGraph 负责临时资源生命周期，因此不再需要 `FrameCleanup` 释放临时 RT。
- `SetRenderFunc` 注册执行回调；`RecordRenderGraph` 描述资源、RendererList 和依赖，`ExecutePass` 提交绘制命令。
- 自定义全局纹理的生产者使用 `SetGlobalTextureAfterPass`；严格的 RenderGraph 消费者可使用 `UseGlobalTexture` 显式声明读取依赖。

## Outlines_2：深度、颜色与法线边缘描边

这是一个全屏屏幕空间描边效果。它使用 3×3 Sobel 卷积核，分别检测深度不连续、画面颜色变化和法线方向变化，再把三种边缘合并成最终描边遮罩。

### 边缘检测流程

1. `DepthSobel_float` 对 `_CameraDepthTexture` 周围九个像素采样，检测物体轮廓和深度断层。
2. `ColorSobel_float` 对 `_CameraOpaqueTexture` 周围九个像素采样，检测同一深度表面上的颜色变化。
3. `NormalSobel_float` 对 `_DepthNormalsTexture` 周围九个像素采样，解码观察空间法线并检测法线方向突变。
4. `FineTuneEdgeDetection` 子图通过 `Smoothstep -> Power -> Multiply` 分别调整阈值、边缘形状和强度。
5. 深度、颜色和法线边缘使用 `Maximum` 逐级合并。
6. 最终遮罩作为 Blend 的 Opacity，在原始画面和描边颜色之间混合。

采样偏移使用 `_ScreenParams.xy` 换算，因此 `Thickness = 1` 表示大约一个屏幕像素，而不是一段固定 UV 距离。

### 主要参数

- `Thickness`：Sobel 采样半径，控制总体描边宽度。
- `DepthThreshold`：深度差达到多大时被识别为边缘。
- `DepthThickness`：通过 Power 调整深度边缘遮罩形状。
- `DepthStrength`：深度边缘强度。
- `ColorThreshold`：颜色差达到多大时被识别为边缘。
- `ColorThickness`：调整颜色边缘遮罩形状。
- `ColorStrength`：颜色边缘强度。
- `NormalThreshold`：法线差达到多大时被识别为边缘。
- `NormalThickness`：调整法线边缘遮罩形状。
- `NormalStrength`：法线边缘强度。
- `NormalAdjustNearDepth` / `NormalAdjustFarDepth`：根据深度调整法线边缘检测范围。
- `NormalFarThreshold`：控制远处法线边缘的阈值变化。
- `AcuteStartDot` / `AcuteDepthThreshold`：根据视线与法线夹角调整锐角区域的深度判断。
- `Color`：最终描边颜色。

### 关键文件

- [`EdgeDetectionOutlines.shadergraph`](./URP学习/Assets/Rendering/Outlines_2/EdgeDetectionOutlines.shadergraph)：组合场景颜色、深度边缘、颜色边缘和最终描边颜色。
- [`EdgeDetectionOutlinesInclude.hlsl`](./URP学习/Assets/Rendering/Outlines_2/EdgeDetectionOutlinesInclude.hlsl)：实现深度、颜色与法线 Sobel，以及屏幕 UV 到观察空间方向的计算。
- [`DecodeDepthNormals.hlsl`](./URP学习/Assets/Rendering/Outlines_2/DecodeDepthNormals.hlsl)：从自定义纹理的 RGBA 通道解码深度和观察空间法线。
- [`FineTuneEdgeDetection.shadersubgraph`](./URP学习/Assets/Rendering/Outlines_2/FineTuneEdgeDetection.shadersubgraph)：封装边缘阈值、形状和强度调整。
- [`PostProcessOutlines.mat`](./URP学习/Assets/Rendering/Outlines_2/PostProcessOutlines.mat)：屏幕空间描边材质和参数。

### 依赖与限制

- Renderer Pass 必须请求 `ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color`。
- `DepthNormalsFeature` 必须早于屏幕空间描边 Pass 执行，并发布 `_DepthNormalsTexture`。
- Shader 必须启用 `REQUIRE_DEPTH_TEXTURE` 和 `REQUIRE_OPAQUE_TEXTURE`。
- Shader Graph 的相机颜色输入使用 `_BlitTexture`，对应 RenderGraph Blit 的默认源纹理属性。
- `_DepthNormalsTexture` 是全局 Shader 属性，不能在显示或描边材质中声明为 `Per Material`。
- `SHADERGRAPH_SAMPLE_SCENE_COLOR` 读取 Camera Opaque Texture，因此颜色边缘默认不包含透明物体。
- 这是屏幕空间效果，无法检测被遮挡的几何，也可能受分辨率、抗锯齿和深度精度影响。

## 共用 Renderer Feature

`Desaturate` 和 `Outlines_2` 当前共用 [`BlitMaterialFeature.cs`](./URP学习/Assets/Rendering/Desaturate/BlitMaterialFeature.cs)。它负责执行以下通用流程：

```text
Camera Color -> 使用指定材质 Blit 到临时纹理 -> 原样拷贝回 Camera Color
```

具体效果由 Renderer Feature Inspector 中配置的 Material、Material Pass Index 和 Render Pass Event 决定。`Global Texture Dependencies` 可选列表用于告诉 RenderGraph 材质还会读取哪些自定义全局纹理；当前深度法线调试 Pass 使用 `_DepthNormalsTexture`，其他不读取自定义全局纹理的 Blit 可以保持为空。

Renderer 配置资源位于：

- [`My Universal Render Pipeline Asset.asset`](./URP学习/Assets/Rendering/My%20Universal%20Render%20Pipeline%20Asset.asset)
- [`My Universal Render Pipeline Asset_Renderer.asset`](./URP学习/Assets/Rendering/My%20Universal%20Render%20Pipeline%20Asset_Renderer.asset)

## 项目定位

这个仓库主要用于记录 URP 学习过程和 API 迁移实践，当前实现以理解渲染流程为主，并非面向生产环境的最终方案。
