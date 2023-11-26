using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using ConfigurationManager.Utilities;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;

namespace ConfigurationManager
{
    internal static class SettingSearcher
    {
        private static readonly ICollection<string> _updateMethodNames = new[]
        {
            "Update",
            "FixedUpdate",
            "LateUpdate",
            "OnGUI"
        };

        public static void CollectSettings(out IEnumerable<SettingEntryBase> results, out List<string> modsWithoutSettings, bool showDebug)
        {
            modsWithoutSettings = new List<string>();
            try
            {
                results = GetBepInExCoreConfig();
            }
            catch (Exception ex)
            {
                results = Enumerable.Empty<SettingEntryBase>();
                ConfigurationManager.Logger.LogError(ex);
            }

            foreach (var plugin in Utils.FindPlugins())
            {
                var type = plugin.Instance.GetType();

                bool advanced = false;
                if (type.GetCustomAttributes(typeof(BrowsableAttribute), false).Cast<BrowsableAttribute>()
                    .Any(x => !x.Browsable))
                {
                    var metadata = plugin.Metadata;

                    if (ConfigurationManager.Instance.OverrideHotkey || metadata.GUID != ConfigurationManager.GUID)
                    {
                        modsWithoutSettings.Add(metadata.Name);
                        continue;
                    }
                    advanced = true;
                }

                var detected = GetPluginConfig(plugin).Cast<SettingEntryBase>().ToList();

                detected.RemoveAll(x => x.Browsable == false);

                if (detected.Count == 0 || advanced)
                    detected.ForEach(x => x.IsAdvanced = true);

                // Allow to enable/disable plugin if it uses any update methods ------
                if (showDebug)
                {
                    var pluginAssembly = type.Assembly;
                    var behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()
                        .Where(behaviour => behaviour.GetType().Assembly == pluginAssembly);
                    foreach (var behaviour in behaviours)
                    {
                        var behaviourType = behaviour.GetType();
                        if (!behaviourType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(m => _updateMethodNames.Contains(m.Name)))
                            continue;
                        PropertyInfo property = behaviourType.GetProperty(nameof(MonoBehaviour.enabled));
                        PropertySettingEntry enabledSetting = new PropertySettingEntry(behaviour, property, plugin);
                        enabledSetting.DispName = "!Allow plugin to run on every frame";
                        enabledSetting.Description = "Disabling this will disable some or all of the plugin's functionality.\nHooks and event-based functionality will not be disabled.\nThis setting will be lost after game restart.";
                        enabledSetting.IsAdvanced = true;
                        detected.Add(enabledSetting);
                        break;
                    }
                }

                if (detected.Count > 0)
                    results = results.Concat(detected);
            }
        }

        /// <summary>
        /// Get entries for all core BepInEx settings
        /// </summary>
        private static IEnumerable<SettingEntryBase> GetBepInExCoreConfig()
        {
            var bepinMeta = new BepInPlugin(nameof(BepInEx), nameof(BepInEx), typeof(BepInEx.Bootstrap.BaseChainloader<BasePlugin>).Assembly.GetName().Version.ToString());

            return ConfigFile.CoreConfig.Select(kvp => (SettingEntryBase)new ConfigSettingEntry(kvp.Value, null) { IsAdvanced = true, PluginInfo = bepinMeta });
        }

        /// <summary>
        /// Get entries for all settings of a plugin
        /// </summary>
        private static IEnumerable<ConfigSettingEntry> GetPluginConfig(PluginInfo plugin)
        {
            var basePlugin = plugin.Instance as BasePlugin;
            return basePlugin != null
                ? basePlugin.Config.Select(kvp => new ConfigSettingEntry(kvp.Value, plugin))
                : Enumerable.Empty<ConfigSettingEntry>();
        }
    }
}