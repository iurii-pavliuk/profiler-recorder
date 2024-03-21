using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

public class PrintAvailableProfilers : MonoBehaviour
{
    class MarkersByCategories
    {
        public readonly Dictionary<ProfilerCategory, List<StatInfo>> markers = new();
    }
    
    struct StatInfo
    {
        public ProfilerCategory Cat;
        public string Name;
        public ProfilerMarkerDataUnit Unit;
    }

    static void EnumerateProfilerStats()
    {
        var markers = new MarkersByCategories();
        var availableStatHandles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(availableStatHandles);

        var availableStats = new List<StatInfo>(availableStatHandles.Count);
        foreach (var h in availableStatHandles)
        {
            var statDesc = ProfilerRecorderHandle.GetDescription(h);
            var statInfo = new StatInfo()
            {
                Cat = statDesc.Category,
                Name = statDesc.Name,
                Unit = statDesc.UnitType
            };
            availableStats.Add(statInfo);

            if (markers.markers.TryGetValue(statDesc.Category, out var marker))
            {
                marker.Add(statInfo);
            }
            else
            {
                markers.markers.Add(statInfo.Cat, new List<StatInfo> {statInfo});
            }
        }
        availableStats.Sort((a, b) =>
        {
            var result = string.Compare(a.Cat.ToString(), b.Cat.ToString());
            if (result != 0)
                return result;

            return string.Compare(a.Name, b.Name);
        });

        foreach (var statInfo in availableStats)
        {
            if (markers.markers.TryGetValue(statInfo.Cat, out var marker))
            {
                marker.Add(statInfo);
            }
            else
            {
                markers.markers.Add(statInfo.Cat, new List<StatInfo> { statInfo });
            }
        }

        var sb = new StringBuilder("Available stats:\n");
        foreach (var s in availableStats)
        {
            sb.AppendLine($"{s.Cat}\t\t - {s.Name}\t\t - {s.Unit}");
        }

        var path = Path.Combine(Application.dataPath, "text.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log($"Txt path: {path}");

        sb.Clear();
        sb.AppendLine("{\n");
        foreach (var markersByCategory in markers.markers)
        {
            sb.AppendLine($"\"{markersByCategory.Key}\"");
            sb.AppendLine(":[\n");
            foreach (var statInfo in markersByCategory.Value)
            {
                // sb.AppendLine($"\"marker\":");
                sb.AppendLine("{\n");
                sb.AppendLine($"\"name\":\"{statInfo.Name}\",\n");
                sb.AppendLine($"\"unit\":\"{statInfo.Unit}\"\n");
                sb.AppendLine("\n},");
            }
            sb.AppendLine("\n],");
        }
        sb.AppendLine("}");
        
        path = Path.Combine(Application.dataPath, "markers.json");
        File.WriteAllText(path, sb.ToString());
        Debug.Log($"JSON path: {path}");
    }

    private void Start()
    {
#if !UNITY_EDITOR && UNITY_ANDROID
        EnumerateProfilerStats();
#endif
    }
}
