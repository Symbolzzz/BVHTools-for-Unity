using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
public class FrameData
{
    public Dictionary<string, float[]> boneFrameData = new Dictionary<string, float[]>();
}

/// <summary>
/// BVHParser 解析 BVH 文件并将其应用于 Unity 骨骼动画
/// </summary>
public class BVHParser : MonoBehaviour
{
    private Dictionary<string, Vector3> jointOffsets = new Dictionary<string, Vector3>();
    private Dictionary<string, int> jointChannelCounts = new Dictionary<string, int>();
    private List<Vector3> bvhJointPositions = new List<Vector3>();
    private List<string> boneNames = new List<string>();
    private List<FrameData> motionFrames = new List<FrameData>();
    private Stack<string> hierarchy = new Stack<string>();
    private List<(Vector3, Vector3)> bvhLines = new List<(Vector3, Vector3)>();

    private int numFrames;
    private float frameTime;

    // 记录骨骼的 Transform 对象
    public Transform rootTransform;
    private Dictionary<string, Transform> boneTransforms = new Dictionary<string, Transform>();

    public Dictionary<string, Quaternion> initialRotations = new Dictionary<string, Quaternion>();
    public Dictionary<string, Vector3> initialPositions = new Dictionary<string, Vector3>();

    [System.Serializable]
    public class RotationMappingConfig
    {
        public enum AxisOrder { XYZ, XZY, YXZ, YZX, ZXY, ZYX }
        public AxisOrder order = AxisOrder.XYZ;

        public int signX = 1;
        public int signY = 1;
        public int signZ = 1;
    }


    // 动画播放控制
    private int currentFrame = 0;

    public int CurrentFrame => currentFrame; // 当前帧索引
    public int TotalFrames => numFrames;    // 总帧数
    public float FrameTime => frameTime;    // 每帧时间

    public float scaleFactor = 0.01f; // 缩放因子
    public bool showSkeleton = true; // 是否可视化骨骼

    public string bones_prefix = "";
    public bool mirrorLeftToRight = true;  // 可在 Inspector 中勾选控制

    public float rotationSmoothFactor = 3000f; // 调整平滑程度，可暴露为参数
    public RotationMappingConfig rotationMapping = new RotationMappingConfig();

    public void setCurrentFrame(int frame)
    {
        if (frame >= 0 && frame < numFrames)
        {
            currentFrame = frame;
        }
        else
        {
            Debug.LogError("Frame index out of range. Frame = " + frame);
        }
    }


    public void setTotalFrames(int frames)
    {
        numFrames = frames;
    }
    public void ParseBVH(string[] bvhLines)
    {
        bool inMotion = false;
        foreach (var line in bvhLines)
        {
            string trimmedLine = line.Trim();

            if (trimmedLine.StartsWith("HIERARCHY"))
            {
                // Start parsing hierarchy
                continue;
            }
            else if (trimmedLine.StartsWith("MOTION"))
            {
                inMotion = true;
                continue;
            }

            if (inMotion)
            {
                ParseMotion(trimmedLine);
            }
            else
            {
                ParseHierarchy(trimmedLine);
            }
        }

        Debug.Log("BVH parsing completed.");
    }

    private void ParseHierarchy(string line)
    {
        bool endSite = false;
        if (line.StartsWith("ROOT") || line.StartsWith("JOINT"))
        {
            string jointName = line.Split(' ')[1];
            // Debug.Log($"Parsing joint: {jointName}");
            hierarchy.Push(jointName);
            boneNames.Add(jointName);
        }
        else if (line.StartsWith("End site") || line.StartsWith("End Site"))
        {
            string endSiteName = hierarchy.Peek() + "_EndSite";
            hierarchy.Push(endSiteName);
            endSite = true;
            // boneNames.Add(endSiteName);
            // jointOffsets.Add(endSiteName, Vector3.zero); // Placeholder, updated by OFFSET
        }
        else if (line.StartsWith("OFFSET"))
        {
            if (!endSite)
            {
                string[] offsetValues = line.Split(' ');
                Vector3 offset = new Vector3(
                    float.Parse(offsetValues[1]),
                    float.Parse(offsetValues[2]),
                    float.Parse(offsetValues[3])
                );

                jointOffsets[hierarchy.Peek()] = offset;
            }
            else
            {
                endSite = false;
            }
        }
        else if (line.StartsWith("CHANNELS"))
        {
            string[] channelValues = line.Split(' ');
            int channelCount = int.Parse(channelValues[1]);
            jointChannelCounts[hierarchy.Peek()] = channelCount;
        }
        else if (line.StartsWith("}"))
        {
            // Debug.Log($"Finished parsing joint: {hierarchy.Peek()}");
            hierarchy.Pop();
        }
    }

    private void ParseMotion(string line)
    {
        if (line.StartsWith("Frames:"))
        {
            numFrames = int.Parse(line.Split(' ')[1]);
        }
        else if (line.StartsWith("Frame Time:"))
        {
            frameTime = float.Parse(line.Split(' ')[2]);
        }
        else
        {
            string[] motionValues = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            float[] allFrameValues = new float[motionValues.Length];

            for (int i = 0; i < motionValues.Length; i++)
            {
                allFrameValues[i] = float.Parse(motionValues[i]);
            }

            int index = 0;
            FrameData frame = new FrameData();

            foreach (var boneName in boneNames)
            {
                int channelCount = jointChannelCounts[boneName];
                float[] boneValues = new float[channelCount];

                for (int j = 0; j < channelCount; j++)
                {
                    boneValues[j] = allFrameValues[index++];
                    
                }

                frame.boneFrameData[boneName] = boneValues;
            }

            motionFrames.Add(frame);
        }
    }

    // 清除动作数据，为读取新的BVH文件做准备
    public void ClearMotionData()
    {
        // 清除所有数据
        Debug.Log("Clearing motion data...");
        motionFrames.Clear();
        currentFrame = 0;
        numFrames = 0;
        frameTime = 0;
        bvhJointPositions.Clear();
        bvhLines.Clear();
        boneNames.Clear();
        jointChannelCounts.Clear();
        jointOffsets.Clear();
        hierarchy.Clear();
    }

    private Quaternion MapEulerToQuat(float x, float y, float z)
    {
        x = rotationMapping.signX * x * Mathf.Deg2Rad;
        y = rotationMapping.signY * y * Mathf.Deg2Rad;
        z = rotationMapping.signZ * z * Mathf.Deg2Rad;

        Quaternion qx = Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right);
        Quaternion qy = Quaternion.AngleAxis(y * Mathf.Rad2Deg, Vector3.up);
        Quaternion qz = Quaternion.AngleAxis(z * Mathf.Rad2Deg, Vector3.forward);

        switch (rotationMapping.order)
        {
            case RotationMappingConfig.AxisOrder.XYZ: return qx * qy * qz;
            case RotationMappingConfig.AxisOrder.XZY: return qx * qz * qy;
            case RotationMappingConfig.AxisOrder.YXZ: return qy * qx * qz;
            case RotationMappingConfig.AxisOrder.YZX: return qy * qz * qx;
            case RotationMappingConfig.AxisOrder.ZXY: return qz * qx * qy;
            case RotationMappingConfig.AxisOrder.ZYX: return qz * qy * qx;
            default: return qx * qy * qz;
        }
    }

    // 将解析到的骨骼及运动数据应用到模型中，目前仅支持30fps
    public void ApplyMotionToBones()
    {
        if (motionFrames.Count == 0)
        {
            Debug.LogWarning("No motion data found to apply.");
            return;
        }

        if (currentFrame < motionFrames.Count)
        {
            var frame = motionFrames[currentFrame];

            foreach (var boneName in boneNames)
            {
                string cleanName = boneName.Contains(":") ? boneName.Split(':')[1] : boneName;

                // 每个骨骼自己在 ApplyBoneData 中判断要读取谁的数据
                ApplyBoneData(cleanName, frame);
            }

            currentFrame++;
        }
        else
        {
            Debug.Log("Animation finished.");
        }
    }
    private void ApplyBoneData(string boneName, FrameData frame)
    {
        string cleanName = boneName.Contains(":") ? boneName.Split(':')[1] : boneName;
        string sourceName = cleanName;

        // 处理镜像逻辑：Left 读取 Right 数据，Right 读取 Left 数据
        if (mirrorLeftToRight)
        {
            if (cleanName.Contains("Left"))
            {
                sourceName = cleanName.Replace("Left", "Right");
            }
            else if (cleanName.Contains("Right"))
            {
                sourceName = cleanName.Replace("Right", "Left");
            }
        }

        // 跳过不存在的骨骼或数据
        if (!boneTransforms.ContainsKey(cleanName) || !frame.boneFrameData.ContainsKey(sourceName))
            return;

        Transform targetTransform = boneTransforms[cleanName];
        float[] values = frame.boneFrameData[sourceName];

        bool hasPosition = jointChannelCounts[sourceName] == 6;

        if (hasPosition)
        {
            float xPos = values[0];
            float yPos = values[1];
            float zPos = values[2];
            float xRot = values[3];
            float yRot = values[4];
            float zRot = values[5];

            Vector3 pos = new Vector3(xPos, yPos, zPos);
            if (mirrorLeftToRight && (cleanName.Contains("Left") || cleanName.Contains("Right")))
                pos.x = -pos.x;

            // 保持 y 坐标不变，只更新 x 和 z
            Vector3 newPos = targetTransform.localPosition;
            newPos.x = pos.x * scaleFactor + jointOffsets[sourceName].x;
            // newPos.y 保持不变
            newPos.z = pos.z * scaleFactor + jointOffsets[sourceName].z;
            // 使用插值平滑位置变化
            targetTransform.localPosition = Vector3.Lerp(targetTransform.localPosition, newPos, Time.deltaTime * rotationSmoothFactor);

            // Quaternion bvhRotation = Quaternion.Euler(zRot, yRot, xRot);
            Quaternion bvhRotation = MapEulerToQuat(xRot, yRot, zRot);

            Matrix4x4 initialMatrix = Matrix4x4.Rotate(initialRotations[bones_prefix + cleanName]);
            Matrix4x4 finalMatrix = initialMatrix.inverse * Matrix4x4.Rotate(bvhRotation) * initialMatrix;
            // Matrix4x4 finalMatrix = initialMatrix *  Matrix4x4.Rotate(bvhRotation);
            Quaternion currentRotation = targetTransform.localRotation;
            targetTransform.localRotation = Quaternion.Slerp(currentRotation, finalMatrix.rotation, Time.deltaTime * rotationSmoothFactor);
        }
        else
        {
            float xRot = values[0];
            float yRot = values[1];
            float zRot = values[2];

            // Quaternion bvhRotation = Quaternion.Euler(zRot, yRot, xRot);
            Quaternion bvhRotation = MapEulerToQuat(xRot, yRot, zRot);

            Matrix4x4 initialMatrix = Matrix4x4.Rotate(initialRotations[bones_prefix + cleanName]);
            Matrix4x4 finalMatrix = initialMatrix.inverse * Matrix4x4.Rotate(bvhRotation) * initialMatrix;
            // Matrix4x4 finalMatrix = initialMatrix *  Matrix4x4.Rotate(bvhRotation);

            Quaternion currentRotation = targetTransform.localRotation;
            targetTransform.localRotation = Quaternion.Slerp(currentRotation, finalMatrix.rotation, Time.deltaTime * rotationSmoothFactor);
            // targetTransform.localRotation = finalMatrix.rotation;
        }
    }

    private float wrapAngle(float a)
    {
        if (a > 180f)
        {
            return a - 360f;
        }
        if (a < -180f)
        {
            return 360f + a;
        }
        return a;
    }

    public void SetBoneTransforms()
    {
        // 清空已有的映射
        boneTransforms.Clear();

        // 从根节点开始递归遍历所有子节点
        MapBoneTransformsRecursive(rootTransform, boneTransforms, boneNames);
    }

    void MapBoneTransformsRecursive(Transform currentTransform, Dictionary<string, Transform> boneTransforms, List<string> boneNames)
    {
        string cleanName = currentTransform.name.Contains(":") ? currentTransform.name.Split(':')[1] : currentTransform.name;

        if (boneNames.Contains(cleanName) && !boneTransforms.ContainsKey(cleanName))
        {
            boneTransforms.Add(cleanName, currentTransform);
        }

        foreach (Transform child in currentTransform)
        {
            MapBoneTransformsRecursive(child, boneTransforms, boneNames);
        }
    }

    // 读取模型初始姿态
    public void ReadInitialPose()
    {
        if (rootTransform == null)
        {
            Debug.LogError("Root transform is not assigned.");
            return;
        }

        initialRotations.Clear();
        initialPositions.Clear();

        TraverseAndStorePose(rootTransform);
        Debug.Log("Initial pose read successfully.");
    }

    // 遍历骨骼树，存储每个骨骼的初始位置和旋转
    private void TraverseAndStorePose(Transform current)
    {
        initialRotations[current.name] = current.rotation;
        initialPositions[current.name] = current.localPosition;

        foreach (Transform child in current)
        {
            TraverseAndStorePose(child);
        }
    }

    private void OnDrawGizmos()
    {
        if (!showSkeleton || boneTransforms == null || boneTransforms.Count == 0)
            return;

        Gizmos.color = Color.green; // 骨骼线条颜色

        foreach (var bone in boneTransforms)
        {
            Transform boneTransform = bone.Value;

            // 绘制骨骼节点
            Gizmos.DrawSphere(boneTransform.position, 0.02f);

            // 绘制父子关系的线条
            if (boneTransform.parent != null && boneTransforms.ContainsValue(boneTransform.parent))
            {
                Gizmos.DrawLine(boneTransform.parent.position, boneTransform.position);
            }
        }

        // 绘制 BVH 骨骼
        DrawBVHVisualization();
    }

    // 调用生成骨骼的方法
    public void VisualizeBVHSkeleton()
    {
        if (jointOffsets.Count == 0)
        {
            Debug.LogWarning("BVH joint offsets are not loaded.");
            return;
        }

        bvhJointPositions.Clear();
        if (rootTransform != null)
        {
            // 假设根关节从 (0, 0, 0) 开始
            GenerateBVHVisualization(rootTransform, rootTransform.transform.position);
            Debug.Log("BVH skeleton visualization generated.");
        }
        else
        {
            Debug.LogError("Root transform is not set.");
        }
    }

    // 递归生成 BVH 骨骼
    private void GenerateBVHVisualization(Transform current, Vector3 parentPosition)
    {
        string[] parts = current.name.Split(':');
        string jointName = parts.Length > 1 ? parts[1] : current.name;
        if (!jointOffsets.TryGetValue(jointName, out Vector3 offset))
        {
            Debug.LogWarning($"Joint offset not found for {jointName}");
            // return;
        }

        // 当前关节的位置
        Vector3 currentPosition = parentPosition + offset;

        // 保存关节的位置
        bvhJointPositions.Add(currentPosition);

        // 绘制从父节点到当前节点的线段
        if (parentPosition != currentPosition)
        {
            bvhLines.Add((parentPosition, currentPosition));
        }

        // 遍历子节点，递归生成
        foreach (Transform child in current)
        {
            GenerateBVHVisualization(child, currentPosition);
        }
    }

    // 在 Gizmos 中绘制 BVH 骨骼
    private void DrawBVHVisualization()
    {
        Gizmos.color = Color.cyan; // BVH 数据线条颜色

        // 绘制关节之间的线条
        foreach (var line in bvhLines)
        {
            Gizmos.DrawLine(line.Item1 * scaleFactor, line.Item2 * scaleFactor);
        }

        // 绘制关节位置的球体
        foreach (var position in bvhJointPositions)
        {
            Gizmos.DrawSphere(position * scaleFactor, 0.02f); // 半径为 0.05
        }
    }

    // Debug 输出
    // public void DebugPrint()
    // {
    //     Debug.Log($"Number of Bones: {boneNames.Count}");
    //     foreach (var bone in boneNames)
    //     {
    //         Debug.Log($"Bone: {bone}, Offset: {jointOffsets[bone]}");
    //     }
    //     foreach (var boneTransform in boneTransforms)
    //     {
    //         Debug.Log($"Bone Transform: {boneTransform.Key}, {boneTransform.Value.name}");
    //     }

    //     Debug.Log($"Number of Frames: {numFrames}");
    //     Debug.Log($"Frame Time: {frameTime}");
    //     Debug.Log($"First Frame Motion Data: {string.Join(", ", motionFrames[0])}");
    //     Debug.Log("First Frame Motion Data:");

    //     Debug.Log("First Frame Motion Data:");

    //     if (motionFrames.Count > 0)
    //     {
    //         float[] firstFrame = motionFrames[0]; // 获取第一帧的数据
    //         int dataIndex = 0; // 用于追踪当前解析到的运动数据索引

    //         foreach (var boneName in jointChannelCounts.Keys)
    //         {
    //             int channelCount = jointChannelCounts[boneName]; // 每个骨骼的通道数

    //             if (channelCount > 0 && dataIndex + channelCount <= firstFrame.Length)
    //             {
    //                 // 提取当前骨骼的运动数据
    //                 float[] boneMotionData = new float[channelCount];
    //                 Array.Copy(firstFrame, dataIndex, boneMotionData, 0, channelCount);

    //                 string boneData = $"{boneName}: {string.Join(", ", boneMotionData)}";
    //                 Debug.Log(boneData);

    //                 dataIndex += channelCount; // 更新数据索引
    //             }
    //             else
    //             {
    //                 Debug.LogWarning($"Insufficient motion data for bone '{boneName}' at index {dataIndex}.");
    //             }
    //         }
    //     }
    //     else
    //     {
    //         Debug.LogWarning("Motion frames are empty.");
    //     }
    // }
}