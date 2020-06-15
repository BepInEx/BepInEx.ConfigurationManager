using System;
using BepInEx;
using BepInEx.Configuration;
using ConfigurationManager.Utilities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

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
                var type = plugin.GetType();

                var pluginInfo = plugin.Info.Metadata;

                if (type.GetCustomAttributes(typeof(BrowsableAttribute), false).Cast<BrowsableAttribute>()
                    .Any(x => !x.Browsable))
                {
                    modsWithoutSettings.Add(pluginInfo.Name);
                    continue;
                }

                var detected = new List<SettingEntryBase>();

                detected.AddRange(GetPluginConfig(plugin).Cast<SettingEntryBase>());

                detected.AddRange(LegacySettingSearcher.GetLegacyPluginConfig(plugin).Cast<SettingEntryBase>());

                detected.RemoveAll(x => x.Browsable == false);

                if (!detected.Any())
                    modsWithoutSettings.Add(pluginInfo.Name);

                // Allow to enable/disable plugin if it uses any update methods ------
                if (showDebug && type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(x => _updateMethodNames.Contains(x.Name)))
                {
                    // todo make a different class for it and fix access modifiers?
                    var enabledSetting = LegacySettingEntry.FromNormalProperty(plugin, type.GetProperty("enabled"), pluginInfo, plugin);
                    enabledSetting.DispName = "!Allow plugin to run on every frame";
                    enabledSetting.Description = "Disabling this will disable some or all of the plugin's functionality.\nHooks and event-based functionality will not be disabled.\nThis setting will be lost after game restart.";
                    enabledSetting.IsAdvanced = true;
                    detected.Add(enabledSetting);
                }

                if (detected.Any())
                {
#pragma warning disable 618 // Disable obsolete warning
                    var isAdvancedPlugin = type.GetCustomAttributes(typeof(AdvancedAttribute), false).Cast<AdvancedAttribute>().Any(x => x.IsAdvanced);
#pragma warning restore 618
                    if (isAdvancedPlugin)
                        detected.ForEach(entry => entry.IsAdvanced = true);

                    results = results.Concat(detected);
                }
            }
        }

        /// <summary>
        /// Bepinex 5 config
        /// </summary>
        private static IEnumerable<SettingEntryBase> GetBepInExCoreConfig()
        {
            var coreConfigProp = typeof(ConfigFile).GetProperty("CoreConfig", BindingFlags.Static | BindingFlags.NonPublic);
            if (coreConfigProp == null) throw new ArgumentNullException(nameof(coreConfigProp));

            var coreConfig = (ConfigFile)coreConfigProp.GetValue(null, null);
            var bepinMeta = new BepInPlugin("BepInEx", "BepInEx", typeof(BepInEx.Bootstrap.Chainloader).Assembly.GetName().Version.ToString());

            return coreConfig.GetConfigEntries()
                .Select(x => new ConfigSettingEntry(x, null) { IsAdvanced = true, PluginInfo = bepinMeta })
                .Cast<SettingEntryBase>();
        }

        /// <summary>
        /// Used by bepinex 5 plugins
        /// </summary>
        private static IEnumerable<ConfigSettingEntry> GetPluginConfig(BaseUnityPlugin plugin)
        {
            return plugin.Config.GetConfigEntries().Select(x => new ConfigSettingEntry(x, plugin));
        }
    }
}