using System;
using System.Collections.Generic;
using BepInEx;

namespace ConfigurationManager.Utilities
{
    internal static class PluginSettingsDataPool
    {
        private static readonly Stack<ConfigurationManager.PluginSettingsData> _pool = new Stack<ConfigurationManager.PluginSettingsData>();

        public static ConfigurationManager.PluginSettingsData Get(BepInPlugin info, List<ConfigurationManager.PluginSettingsData.PluginSettingsGroupData> categories, string website)
        {
            ConfigurationManager.PluginSettingsData data;
            if (_pool.Count > 0)
            {
                data = _pool.Pop();
                data.Info = info;
                data.Categories = categories;
                data.Website = website;
                data.Height = 0;
            }
            else
            {
                data = new ConfigurationManager.PluginSettingsData
                {
                    Info = info,
                    Categories = categories,
                    Website = website,
                    Height = 0,
                };
            }

            return data;
        }

        public static void Release(ConfigurationManager.PluginSettingsData data)
        {
            if (data == null)
                return;

            data.Info = null;
            data.Categories = null;
            data.Website = null;
            data.Height = 0;
            _pool.Push(data);
        }

        public static void ClearAll()
        {
            foreach (ConfigurationManager.PluginSettingsData pluginSettingsData in _pool)
            {
                pluginSettingsData.Info = null;
                pluginSettingsData.Categories = null;
                pluginSettingsData.Website = null;
                pluginSettingsData.Height = 0;
            }

            _pool.Clear();
        }
    }
}