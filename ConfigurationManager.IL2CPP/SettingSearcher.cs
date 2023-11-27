using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using System.ComponentModel;
using System.Linq;
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

        // Search for instances of BaseUnityPlugin to also find dynamically loaded plugins. Doing this makes checking Chainloader.PluginInfos redundant.
        // Have to use FindObjectsOfType(Type) instead of FindObjectsOfType<T> because the latter is not available in some older unity versions.
        public static IReadOnlyList<PluginInfo> FindPlugins() => IL2CPPChainloader.Instance.Plugins.Values.Where(x => x.Instance is BasePlugin).ToList();

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

            foreach (var plugin in FindPlugins())
            {
                var type = plugin.Instance.GetType();

                bool advanced = false;
                if (type.GetCustomAttributes(typeof(BrowsableAttribute), false).Cast<BrowsableAttribute>()
                    .Any(x => !x.Browsable))
                {
                    var metadata = plugin.Metadata;

                    if (metadata.GUID != ConfigurationManager.GUID)
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
            var bepinMeta = new BepInPlugin(nameof(BepInEx), nameof(BepInEx), BepInEx.Paths.BepInExVersion.ToString().Split('+')[0]);

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