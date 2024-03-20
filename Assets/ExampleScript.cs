using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class ExampleScript : MonoBehaviour
{
    string statsText;
    ProfilerRecorder systemMemoryRecorder;
    ProfilerRecorder gcMemoryRecorder;
    ProfilerRecorder mainThreadTimeRecorder;
    private GUIStyle _style;
    private ProfilerRecorder renderTexturesMemory;
    private ProfilerRecorder bufferMemory;
    private ProfilerRecorder texturesMemory;
    private AndroidJavaObject _activity;
    private AndroidJavaObject _context;
    private AndroidJavaObject _memoryService;
    private AndroidJavaObject _memoryInfo;
    [SerializeField] private ProfilerRecorder gpuTime;

    static double GetRecorderFrameAverage(ProfilerRecorder recorder)
    {
        var samplesCount = recorder.Capacity;
        if (samplesCount == 0)
            return 0;

        double r = 0;
        unsafe
        {
            var samples = stackalloc ProfilerRecorderSample[samplesCount];
            recorder.CopyTo(samples, samplesCount);
            for (var i = 0; i < samplesCount; ++i)
                r += samples[i].Value;
            r /= samplesCount;
        }

        return r;
    }

    void OnEnable()
    {
        _style = new GUIStyle
        {
            fontSize = 24,
        };
        
        systemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory");
        mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        
        renderTexturesMemory = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render Textures Bytes");
        bufferMemory = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Used Buffers Bytes");
        texturesMemory = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Used Textures Bytes");
        
        gpuTime = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 15);
    }

    void OnDisable()
    {
        systemMemoryRecorder.Dispose();
        gcMemoryRecorder.Dispose();
        mainThreadTimeRecorder.Dispose();
        renderTexturesMemory.Dispose();
        bufferMemory.Dispose();
        texturesMemory.Dispose();
    }

    void Update()
    {
        var sb = new StringBuilder(500);
        sb.AppendLine($"Frame Time: {GetRecorderFrameAverage(mainThreadTimeRecorder) * (1e-6f):F1} ms");
        sb.AppendLine($"GPU Time: {GetRecorderFrameAverage(gpuTime) * (1e-6f):F1} ms");
        sb.AppendLine($"GC Memory: {gcMemoryRecorder.LastValue} Bytes / {gcMemoryRecorder.LastValue / (1024 * 1024)} MB");
        sb.AppendLine($"System Memory: {systemMemoryRecorder.LastValue} Bytes / {systemMemoryRecorder.LastValue / (1024 * 1024)} MB");
        sb.AppendLine($"Total system memory: {SystemInfo.systemMemorySize} Bytes / {SystemInfo.systemMemorySize / (1024 * 1024)} MB");
        sb.AppendLine($"Total GPU memory: {SystemInfo.graphicsMemorySize} Bytes / {SystemInfo.graphicsMemorySize / (1024 * 1024)} MB");
        
        sb.AppendLine($"Render Textures memory: {renderTexturesMemory.LastValue} Bytes / {renderTexturesMemory.LastValue / (1024 * 1024)} MB");
        sb.AppendLine($"Buffers memory: {bufferMemory.LastValue} Bytes / {bufferMemory.LastValue / (1024 * 1024)} MB");
        sb.AppendLine($"Textures memory: {texturesMemory.LastValue} Bytes / {texturesMemory.LastValue / (1024 * 1024)} MB");
        
        var proc = Process.GetCurrentProcess();
        var memoryInBytes = proc.PrivateMemorySize64;
        sb.AppendLine($"Process memory: {memoryInBytes} Bytes / {memoryInBytes / (1024 * 1024)} MB");
        
        var totalMemory = GC.GetTotalMemory(false);
        sb.AppendLine($"GC total memory: {totalMemory} Bytes / {totalMemory / (1024 * 1024)} MB");
        
        #if !UNITY_EDITOR && UNITY_ANDROID 
        using (var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity"))
        {
            if(activity != null)
            {
                using var activityManager = activity.Call<AndroidJavaObject>("getSystemService", "activity");
                if(activityManager != null)
                {
                    using var memoryInfo = new AndroidJavaObject("android.app.ActivityManager$MemoryInfo");
                    activityManager.Call("getMemoryInfo", memoryInfo);
                    var availMem = memoryInfo.Get<long>("availMem");
                    var totalMem = memoryInfo.Get<long>("totalMem");

                    sb.AppendLine("Available Memory: " + (availMem / 1048576L) + " MB");
                    sb.AppendLine("Total Memory: " + (totalMem / 1048576L) + " MB");
                }
                else
                {
                    sb.AppendLine("activityManager null");
                }
            }
            else
            {
                sb.AppendLine("activity null");
            }
        }
        
        if(_memoryService != null)
        {
            _memoryService.Call("getMemoryInfo", _memoryInfo);
            totalMemory = _memoryInfo.Get<long>("totalMem");
            var availMemory = _memoryInfo.Get<long>("availMem");
            var lowMemory = _memoryInfo.Get<bool>("lowMemory");

            sb.AppendLine("Total Memory: " + totalMemory);
            sb.AppendLine("Available Memory: " + availMemory);
            sb.AppendLine("Low Memory Warning: " + lowMemory);
        }
        
        sb.AppendLine("Command line: " + CallMethod());
        
        #else
        sb.AppendLine("Not Android");
        #endif
        
        statsText = sb.ToString();
    }

    private void Start()
    {
#if !UNITY_EDITOR && UNITY_ANDROID
        _activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
        if (_activity == null)
        {
            Debug.Log("Activity null");
            return;
        }
        _context = _activity.Call<AndroidJavaObject>("getApplicationContext");
        if (_context == null)
        {
            Debug.Log("Context null");
            return;
        }
        _memoryService = _context.Call<AndroidJavaObject>("getSystemService", "memory");
        if (_memoryService == null)
        {
            Debug.Log("_memoryService null");
            return;
        }
        _memoryInfo = new AndroidJavaObject("android.app.ActivityManager$MemoryInfo");
        if (_memoryInfo == null)
        {
            Debug.Log("_memoryInfo null");
        }
#endif
    }

    private string CallMethod()
    {
        const string filePath = "/proc/meminfo";
        var localFileReader = new StreamReader(filePath);
        return localFileReader.ReadToEnd();
    }
    

    void OnGUI()
    {
        
        GUI.TextArea(new Rect(10, 30, 800, 400), statsText, _style);
    }
}
