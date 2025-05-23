using System.Collections;
using UnityEngine;

public class BVHPlayer : MonoBehaviour
{
    public BVHParser bvhParser;
    public string bvhFilePath;

    private float playbackTimer = 0f; // 用于控制帧时间
    private bool isPlaying = false;

    public delegate void EventDelegate(bool state);
    //声明事件
    public static event EventDelegate eventHandler;

    // 这个脚本挂在角色上，这样就可以直接获取角色的Animator组件
    private Animator animator;

    // 在这里写两个可以在unity里设置的变量，使得他们可以调用Play和Stop函数
    [SerializeField]
    private bool playOnStart = false;

    [SerializeField]
    private bool stopAnimation = false;

    void OnValidate()
    {
        if (playOnStart)
        {
            // Play(bvhFilePath);
            Play();
            playOnStart = false;
        }
        if (stopAnimation)
        {
            Stop();
            stopAnimation = false;
        }
    }
    // void OnValidate()
    // {
    //     if (playOnStart)
    //     {
    //         Play();
    //         playOnStart = false;
    //     }
    //     if (stopAnimation)
    //     {
    //         Stop();
    //         stopAnimation = false;
    //     }
    // }

    void Start()
    {
        // bvhFilePath = "/sdcard/Android/data/2_scott_0_2_2.bvh";
        // 解析 BVH 文件
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("Animator component not found on this GameObject.");
            return;
        }
        // LoadAndParseBVH();
        bvhParser.ReadInitialPose();
        // LoadAndParseBVH();
    }

    void LoadAndParseBVH()
    {
        string[] bvhLines = System.IO.File.ReadAllLines(bvhFilePath);
        bvhParser.ParseBVH(bvhLines);
        // 设置骨骼映射
        bvhParser.SetBoneTransforms();
    }

    bool getIsPlaying()
    {
        return isPlaying;
    }
    void setIsPlaying(bool value)
    {
        isPlaying = value;
    }
    public void SetBVHFilePath(string path)
    {
        bvhFilePath = path;
    }
    private Coroutine playbackCoroutine;

    void Update()
    {
        // 不再在Update里处理播放逻辑
    }

    private IEnumerator PlayBVH()
    {
        while (isPlaying && bvhParser.CurrentFrame < bvhParser.TotalFrames)
        {
            bvhParser.ApplyMotionToBones();
            yield return new WaitForSeconds(bvhParser.FrameTime);
        }
        isPlaying = false;
        Debug.Log("Animation finished.");
    }

    public void Play(string newBvhPath = null, float len = -1f)
    {
        if (!string.IsNullOrEmpty(newBvhPath))
        {
            bvhFilePath = newBvhPath;
            Debug.Log("New BVH file path: " + bvhFilePath);
            // 重新加载 BVH 文件
            bvhParser.ClearMotionData();
            // bvhParser.ReadInitialPose(); // Reset the initial pose
            LoadAndParseBVH(); // Reload BVH with new path
        }

        if (animator != null)
        {
            animator.enabled = false;
        }
        isPlaying = true;
        playbackTimer = 0f;
        bvhParser.setCurrentFrame(0);
        if (playbackCoroutine != null)
        {
            StopCoroutine(playbackCoroutine);
        }

        if (len > 0)
        {
            // Calculate frames to play based on length in seconds
            int framesToPlay = Mathf.Min(
                bvhParser.TotalFrames,
                Mathf.RoundToInt(len / bvhParser.FrameTime)
            );
            bvhParser.setTotalFrames(framesToPlay);
        }

        playbackCoroutine = StartCoroutine(PlayBVH());
    }

    public void Stop()
    {
        if (animator != null)
        {
            animator.enabled = true;
        }
        isPlaying = false;
        playbackTimer = 0f;
        if (playbackCoroutine != null)
        {
            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;
        }
    }
    
}