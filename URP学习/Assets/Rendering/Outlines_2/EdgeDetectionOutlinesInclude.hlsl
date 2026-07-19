//让编译器只编译一次代码
#ifndef SOBELOUTLINES_INCLUDED
#define SOBELOUTLINES_INCLUDED


//sobel卷积矩阵：在水平和垂直方向的每个像素及周围采样值乘以权重，将各自相加后的两个结果作为一个二维向量，取模长得到边缘值
//采样点相对中心像素偏移量
static float2 sobelSamplePoints[9]={
    float2(-1,1), float2(0,1), float2(1,1),
    float2(-1,0), float2(0,0), float2(1,0),
    float2(-1,-1), float2(0,-1), float2(1,-1),
};

//x方向权重
static float sobelXMatrix[9]={
    1, 0, -1,
    2, 0, -2,
    1, 0, -1
};

//y方向权重
static float sobelYMatrix[9]={
    1, 2, 1,
    0, 0, 0, 
    -1, -2, -1
};

//深度边缘函数
//_float告诉ShaderGraph要使用的数值类型，函数要求输入卷积矩阵放置的UV坐标、用于定义矩阵采样距离的厚度值，输出一个float用于存储边缘值
void DepthSobel_float(float2 UV, float Thickness, out float Out)
{
    float2 sobel = 0.0;

    //unroll属性告诉编译器可以优化该循环，因为迭代次数固定
    [unroll] for (int i = 0; i < 9; i++)
    {
        //本轮采样点
        //将像素精确到屏幕的单个像素
        float2 offset = sobelSamplePoints[i] * Thickness / _ScreenParams.xy;
        float depth = SHADERGRAPH_SAMPLE_SCENE_DEPTH(UV + offset);

        //计算权重
        sobel += depth * float2(sobelXMatrix[i], sobelYMatrix[i]);

    }

    //向量取模并返回
        Out = length(sobel);
}

//颜色边缘函数（原理同上）
void ColorSobel_float(float2 UV, float Thickness, out float Out)
{
    float2 sobelR = 0.0;
    float2 sobelG = 0.0;
    float2 sobelB = 0.0;

    [unroll] for (int i = 0; i < 9; i++)
    {
        //本轮采样点
        //将像素精确到屏幕的单个像素
        float2 offset = sobelSamplePoints[i] * Thickness / _ScreenParams.xy;
        float3 rgb = SHADERGRAPH_SAMPLE_SCENE_COLOR(UV + offset);

        //计算权重
        float2 kernel = float2(sobelXMatrix[i], sobelYMatrix[i]);
   
        sobelR += rgb.r * kernel;
        sobelG += rgb.g * kernel;
        sobelB += rgb.b * kernel;

    }

    Out = max(
                length(sobelR), 
                max(length(sobelG), length(sobelB))
             );
}


#endif
