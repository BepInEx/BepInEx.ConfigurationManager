using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using ConfigurationManager.Utilities;

namespace ConfigurationManager
{
    internal static class LegacySettingSearcher
    {
        private static readonly Type _bepin4BaseSettingType = Type.GetType("BepInEx4.ConfigWrapper`1, BepInEx.BepIn4Patcher, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", false);

        /// <summary>
        /// Used by bepinex 4 plugins
        /// </summary>
        public static IEnumerable<LegacySettingEntry> GetLegacyPluginConfig(BaseUnityPlugin plugin)
        {
            if (_bepin4BaseSettingType == null)
                return Enumerable.Empty<LegacySettingEntry>();

            var type = plugin.GetType();
            var pluginInfo = plugin.Info.Metadata;

            // Config wrappers ------

            var settingProps = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FilterBrowsable(true, true);

            var settingFields = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(f => !f.IsSpecialName)
                .FilterBrowsable(true, true)
                .Select(f => new FieldToPropertyInfoWrapper(f));

            var settingEntries = settingProps.Concat(settingFields.Cast<PropertyInfo>())
                .Where(x => x.PropertyType.IsSubclassOfRawGeneric(_bepin4BaseSettingType));

            var results = settingEntries.Select(x => LegacySettingEntry.FromConfigWrapper(plugin, x, pluginInfo, plugin)).Where(x => x != null);

            // Config wrappers static ------

            var settingStaticProps = type
                .GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FilterBrowsable(true, true);

            var settingStaticFields = type.GetFields(BindingFlags.Static | BindingFlags.Public)
                .Where(f => !f.IsSpecialName)
                .FilterBrowsable(true, true)
                .Select(f => new FieldToPropertyInfoWrapper(f));

            var settingStaticEntries = settingStaticProps.Concat(settingStaticFields.Cast<PropertyInfo>())
                .Where(x => x.PropertyType.IsSubclassOfRawGeneric(_bepin4BaseSettingType));

            results = results.Concat(settingStaticEntries.Select(x => LegacySettingEntry.FromConfigWrapper(null, x, pluginInfo, plugin)).Where(x => x != null));

            // Normal properties ------

            bool IsPropSafeToShow(PropertyInfo p) => p.GetSetMethod()?.IsPublic == true && (p.PropertyType.IsValueType || p.PropertyType == typeof(string));

            var normalPropsSafeToShow = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(IsPropSafeToShow)
                .FilterBrowsable(true, true)
                .Where(x => !x.PropertyType.IsSubclassOfRawGeneric(_bepin4BaseSettingType));

            var normalPropsWithBrowsable = type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FilterBrowsable(true, false)
                .Where(x => !x.PropertyType.IsSubclassOfRawGeneric(_bepin4BaseSettingType));

            var normalProps = normalPropsSafeToShow.Concat(normalPropsWithBrowsable).Distinct();

            results = results.Concat(normalProps.Select(x => LegacySettingEntry.FromNormalProperty(plugin, x, pluginInfo, plugin)).Where(x => x != null));

            // Normal static properties ------

            var normalStaticPropsSafeToShow = type
                .GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(IsPropSafeToShow)
                .FilterBrowsable(true, true)
                .Where(x => !x.PropertyType.IsSubclassOfRawGeneric(_bepin4BaseSettingType));

            var normalStaticPropsWithBrowsable = type.GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .FilterBrowsable(true, false)
                .Where(x => !x.PropertyType.IsSubclassOfRawGeneric(_bepin4BaseSettingType));

            var normalStaticProps = normalStaticPropsSafeToShow.Concat(normalStaticPropsWithBrowsable).Distinct();

            results = results.Concat(normalStaticProps.Select(x => LegacySettingEntry.FromNormalProperty(null, x, pluginInfo, plugin)).Where(x => x != null));

            return results;
        }
    }
}