using System;
using System.Collections.Generic;

// Standalone tests exercise the actual profile code without starting the Unity editor.
namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { SubsystemRegistration }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType loadType) { }
    }

    public static class Random
    {
        public static int Range(int minimum, int maximum) => System.Random.Shared.Next(minimum, maximum);
    }

    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, object> Values = new();
        public static int SaveCount { get; private set; }
        public static bool HasKey(string key) => Values.ContainsKey(key);
        public static string GetString(string key) => Values.TryGetValue(key, out object value) ? (string)value : "";
        public static int GetInt(string key, int fallback) => Values.TryGetValue(key, out object value) ? (int)value : fallback;
        public static void SetString(string key, string value) => Values[key] = value;
        public static void SetInt(string key, int value) => Values[key] = value;
        public static void Save() => SaveCount++;
    }
}
