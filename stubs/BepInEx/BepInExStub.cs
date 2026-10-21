// Reference-only stub of the BepInEx 5 public API surface used by the plugin.
// Signatures match BepInEx 5.4.23 (branch v5-lts). Bodies are never executed.
using System;
using UnityEngine;

namespace BepInEx.Logging
{
    public class ManualLogSource
    {
        public void LogFatal(object data) { }
        public void LogError(object data) { }
        public void LogWarning(object data) { }
        public void LogMessage(object data) { }
        public void LogInfo(object data) { }
        public void LogDebug(object data) { }
    }
}

namespace BepInEx.Configuration
{
    public abstract class ConfigEntryBase { }

    public sealed class ConfigEntry<T> : ConfigEntryBase
    {
        public T Value { get { throw null; } set { } }
    }

    public class ConfigFile
    {
        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description) { throw null; }
    }
}

namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class BepInPlugin : Attribute
    {
        public BepInPlugin(string GUID, string Name, string Version) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class BepInProcess : Attribute
    {
        public BepInProcess(string ProcessName) { }
    }

    public static class Paths
    {
        public static string ConfigPath { get { throw null; } }
    }

    public abstract class BaseUnityPlugin : MonoBehaviour
    {
        protected BepInEx.Logging.ManualLogSource Logger { get { throw null; } }
        public BepInEx.Configuration.ConfigFile Config { get { throw null; } }
    }
}
