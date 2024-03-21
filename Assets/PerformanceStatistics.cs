using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AdaptivePerformance;
using Debug = UnityEngine.Debug;

public class PerformanceStatistics : MonoBehaviour
{
    private ProfilerRecorder _systemMemoryRecorder;
    private ProfilerRecorder _gcMemoryRecorder;
    private ProfilerRecorder _mainThreadTimeRecorder;
    private GUIStyle _style;
    private AndroidJavaObject _activity;
    private AndroidJavaObject _context;
    private AndroidJavaObject _memoryService;
    private AndroidJavaObject _memoryInfo;
    private ProfilerRecorder _gpuTime;
    private IAdaptivePerformance _adaptivePerformance;
    private StringBuilder _output;

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
        
        _systemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        _gcMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
        
        _gfxMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");
        _texturesMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory");
        _meshesMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Mesh Memory");
        _materialsMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Material Memory");

        _renderingMarkers = new Dictionary<string, ProfilerRecorder>()
        {
            {"SetPass", ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count")},
            {"DrawCall", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count")},
            {"Batches", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Total Batches Count")},
            {"RenderTextures", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render Textures Bytes")},
            {"Buffers", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Used Buffers Bytes")},
            {"Textures", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Used Textures Bytes")},
            
        };

        _mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        
        // Doesn't exist in 2021, but exists in 2022
        _gpuTime = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 15);
    }

    void OnDisable()
    {
        _systemMemoryRecorder.Dispose();
        _gcMemoryRecorder.Dispose();
        _mainThreadTimeRecorder.Dispose();
    }
    
    private UnityEngine.FrameTiming[] m_FrameTiming = new UnityEngine.FrameTiming[1];
    private ProfilerRecorder _gfxMemoryRecorder;
    private ProfilerRecorder _texturesMemoryRecorder;
    private ProfilerRecorder _materialsMemoryRecorder;
    private ProfilerRecorder _meshesMemoryRecorder;
    private Dictionary<string, ProfilerRecorder> _renderingMarkers;

    void LateUpdate()
    {
        _output = new StringBuilder(500);

        if (_adaptivePerformance is not { Active: true })
        {
            _output.AppendLine("[AP ClusterInfo] Adaptive Performance not active.");
        }
        else
        {
            _output.AppendLine("=== Adaptive Performance ===");
            var frameTiming = _adaptivePerformance.PerformanceStatus.FrameTiming;
            _output.AppendLine($"Frame Time: {frameTiming.CurrentFrameTime}");
            _output.AppendLine($"CPU Time: {frameTiming.CurrentCpuFrameTime}");
            _output.AppendLine($"GPU Time: {frameTiming.CurrentGpuFrameTime}");
            _output.AppendLine($"Average Frame Time: {frameTiming.AverageFrameTime}");
            _output.AppendLine($"Average CPU Time: {frameTiming.AverageCpuFrameTime}");
            _output.AppendLine($"Average GPU Time: {frameTiming.AverageGpuFrameTime}");

            var thermalMetrics = _adaptivePerformance.ThermalStatus.ThermalMetrics;
            _output.AppendLine($"Temperature Level: {thermalMetrics.TemperatureLevel}");
            _output.AppendLine($"Temperature Trend: {thermalMetrics.TemperatureTrend}");
            _output.AppendLine($"Temperature Warning Level: {thermalMetrics.WarningLevel}");
        }
        _output.AppendLine("=== FrameTimingManager ===");
        
        FrameTimingManager.CaptureFrameTimings();
        var framesRead = FrameTimingManager.GetLatestTimings(1, m_FrameTiming);
        if (framesRead >= 1)
        {
            var gpuFrameTime = m_FrameTiming[0].gpuFrameTime;
            var cpuFrameTime = m_FrameTiming[0].cpuFrameTime;
            _output.AppendLine($"CPU Time: {cpuFrameTime} ms");
            _output.AppendLine($"GPU Time: {gpuFrameTime} ms");
        }
        else
        {
            _output.AppendLine("No frames read");
        }
        
        _output.AppendLine("=== Profiler Recorder ===");
        
        _output.AppendLine($"Main Thread Time: {GetRecorderFrameAverage(_mainThreadTimeRecorder) * (1e-6f):F1} ms");
        _output.AppendLine($"GPU Time: {GetRecorderFrameAverage(_gpuTime) * (1e-6f):F1} ms");
        
        _output.AppendLine("--- Memory ---");
        _output.AppendLine($"GC Memory: {_gcMemoryRecorder.LastValue} Bytes / {_gcMemoryRecorder.LastValue / (1024 * 1024)} MB");
        _output.AppendLine($"System Memory: {_systemMemoryRecorder.LastValue} Bytes / {_systemMemoryRecorder.LastValue / (1024 * 1024)} MB");
        _output.AppendLine($"Render Textures memory: {_renderingMarkers["RenderTextures"].LastValue} Bytes / {_renderingMarkers["RenderTextures"].LastValue / (1024 * 1024)} MB");
        _output.AppendLine($"Buffers memory: {_renderingMarkers["Buffers"].LastValue} Bytes / {_renderingMarkers["Buffers"].LastValue / (1024 * 1024)} MB");
        _output.AppendLine($"Textures memory: {_renderingMarkers["Textures"].LastValue} Bytes / {_renderingMarkers["Textures"].LastValue / (1024 * 1024)} MB");
        
        _output.AppendLine($"Gfx Memory: {_gfxMemoryRecorder.LastValue} Bytes / {_gfxMemoryRecorder.LastValue / (1024 * 1024)} MB");
        _output.AppendLine($"Textures Memory: {_texturesMemoryRecorder.LastValue} Bytes / {_texturesMemoryRecorder.LastValue / (1024 * 1024)} MB");
        _output.AppendLine($"Meshes Memory: {_meshesMemoryRecorder.LastValue} Bytes / {_meshesMemoryRecorder.LastValue / (1024 * 1024)} MB");
        _output.AppendLine($"Materials Memory: {_materialsMemoryRecorder.LastValue} Bytes / {_materialsMemoryRecorder.LastValue / (1024 * 1024)} MB");
        
        _output.AppendLine("--- Drawing ---");
        _output.AppendLine($"Draw Calls: {_renderingMarkers["DrawCall"].LastValue}");
        _output.AppendLine($"Set Pass Calls: {_renderingMarkers["SetPass"].LastValue}");
        _output.AppendLine($"Batches: {_renderingMarkers["Batches"].LastValue}");
        
        _output.AppendLine("=== SystemInfo ===");
        
        _output.AppendLine($"Total system memory: {SystemInfo.systemMemorySize} Bytes / {SystemInfo.systemMemorySize / (1024 * 1024)} MB");
        _output.AppendLine($"Total GPU memory: {SystemInfo.graphicsMemorySize} Bytes / {SystemInfo.graphicsMemorySize / (1024 * 1024)} MB");
        _output.AppendLine($"Battery level: {SystemInfo.batteryLevel} {SystemInfo.batteryStatus}");
        _output.AppendLine($"supportsGpuRecorder: {SystemInfo.supportsGpuRecorder}");
        
        _output.AppendLine("=== Process ===");
        
        var proc = Process.GetCurrentProcess();
        var memoryInBytes = proc.PrivateMemorySize64;
        _output.AppendLine($"Process memory: {memoryInBytes} Bytes / {memoryInBytes / (1024 * 1024)} MB");
        
        var totalMemory = GC.GetTotalMemory(false);
        _output.AppendLine($"GC total memory: {totalMemory} Bytes / {totalMemory / (1024 * 1024)} MB");
        
        #if !UNITY_EDITOR && UNITY_ANDROID
        _output.AppendLine("=== MemoryInfo ===");
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

                    _output.AppendLine("Available Memory: " + (availMem / 1048576L) + " MB");
                    _output.AppendLine("Total Memory: " + (totalMem / 1048576L) + " MB");
                }
                else
                {
                    _output.AppendLine("activityManager null");
                }
            }
            else
            {
                _output.AppendLine("activity null");
            }
        }
        
        if(_memoryService != null)
        {
            _memoryService.Call("getMemoryInfo", _memoryInfo);
            totalMemory = _memoryInfo.Get<long>("totalMem");
            var availMemory = _memoryInfo.Get<long>("availMem");
            var lowMemory = _memoryInfo.Get<bool>("lowMemory");

            _output.AppendLine("Total Memory: " + totalMemory);
            _output.AppendLine("Available Memory: " + availMemory);
            _output.AppendLine("Low Memory Warning: " + lowMemory);
        }
        
        // sb.AppendLine("Command line: " + CallMethod());
        
        #else
        _output.AppendLine("Not Android");
        #endif
    }

    private void Start()
    {
        _adaptivePerformance = Holder.Instance;
        
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
        GUI.TextArea(new Rect(10, 30, 800, 400), _output.ToString(), _style);
    }
}
