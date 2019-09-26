// Made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using BepInEx;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BepInEx.Configuration;

namespace ConfigurationManager
{
    /// <summary>
    /// Class representing all data about a setting collected by ConfigurationManager.
    /// </summary>
    public abstract class SettingEntryBase
    {
        /// <summary>
        /// List of values this setting can take
        /// </summary>
        public object[] AcceptableValues { get; protected set; }

        /// <summary>
        /// Range of the values this setting can take
        /// </summary>
        public KeyValuePair<object, object> AcceptableValueRange { get; protected set; }

        /// <summary>
        /// Should the setting be shown as a percentage (only applies to value range settings)
        /// </summary>
        public bool? ShowRangeAsPercent { get; protected set; }

        /// <summary>
        /// Custom setting draw action
        /// </summary>
        public Action<SettingEntryBase> CustomDrawer { get; private set; }

        /// <summary>
        /// Show this setting in the settings screen at all? If false, don't show.
        /// </summary>
        public bool? Browsable { get; protected set; }

        /// <summary>
        /// Category the setting is under. Null to be directly under the plugin.
        /// </summary>
        public string Category { get; protected set; }

        /// <summary>
        /// If set, a "Default" button will be shown next to the setting to allow resetting to default.
        /// </summary>
        public object DefaultValue { get; protected set; }

        /// <summary>
        /// Optional description shown when hovering over the setting
        /// </summary>
        public string Description { get; protected internal set; }

        /// <summary>
        /// Name of the setting
        /// </summary>
        public virtual string DispName { get; protected internal set; }

        /// <summary>
        /// Plugin this setting belongs to
        /// </summary>
        public BepInPlugin PluginInfo { get; protected internal set; }

        /// <summary>
        /// Only allow showing of the value. False whenever possible by default.
        /// </summary>
        public bool? ReadOnly { get; protected set; }

        /// <summary>
        /// Type of the variable
        /// </summary>
        public abstract Type SettingType { get; }

        /// <summary>
        /// Instance of the plugin that owns this setting
        /// </summary>
        public BaseUnityPlugin PluginInstance { get; private set; }

        /// <summary>
        /// Instance of the <see cref="ConfigWrapper{T}"/> that holds this setting. 
        /// Null if setting is not in a ConfigWrapper.
        /// </summary>
        public object Wrapper { get; internal set; }

        /// <summary>
        /// Is this setting advanced
        /// </summary>
        public bool? IsAdvanced { get; internal set; }

        /// <summary>
        /// Get the value of this setting
        /// </summary>
        public abstract object Get();

        /// <summary>
        /// Set the value of this setting
        /// </summary>
        public void Set(object newVal)
        {
            if (ReadOnly != true)
                SetValue(newVal);
        }

        protected abstract void SetValue(object newVal);

        /// <summary>
        /// Custom converter from setting type to string for the textbox
        /// </summary>
        public Func<object, string> ObjToStr { get; internal set; }

        /// <summary>
        /// Custom converter from string to setting type for the textbox
        /// </summary>
        public Func<string, object> StrToObj { get; internal set; }

        protected void SetFromAttributes(object[] attribs, BaseUnityPlugin pluginInstance)
        {
            PluginInstance = pluginInstance;
            PluginInfo = pluginInstance?.Info.Metadata;

            if (attribs == null || attribs.Length == 0) return;

            DispName = attribs.OfType<DisplayNameAttribute>().FirstOrDefault()?.DisplayName;
            Category = attribs.OfType<CategoryAttribute>().FirstOrDefault()?.Category;
            Description = attribs.OfType<DescriptionAttribute>().FirstOrDefault()?.Description;
            DefaultValue = attribs.OfType<DefaultValueAttribute>().FirstOrDefault()?.Value;

            var acc = attribs.OfType<AcceptableValueBaseAttribute>().FirstOrDefault();
            if (acc is AcceptableValueListAttribute accList)
                AcceptableValues = accList.GetAcceptableValues(pluginInstance);
            else if (acc is AcceptableValueRangeAttribute accRange)
            {
                AcceptableValueRange = new KeyValuePair<object, object>(accRange.MinValue, accRange.MaxValue);
                ShowRangeAsPercent = accRange.ShowAsPercentage;
            }

            var customSettingDrawAttribute = attribs.OfType<CustomSettingDrawAttribute>().FirstOrDefault();
            if (customSettingDrawAttribute != null) CustomDrawer = x => customSettingDrawAttribute.Run(x.PluginInstance);
            else CustomDrawer = attribs.OfType<Action<SettingEntryBase>>().FirstOrDefault();

            bool HasStringValue(string val)
            {
                return attribs.OfType<string>().Contains(val, StringComparer.OrdinalIgnoreCase);
            }

            if (HasStringValue("ReadOnly")) ReadOnly = true;
            else ReadOnly = attribs.OfType<ReadOnlyAttribute>().FirstOrDefault()?.IsReadOnly;

            if (HasStringValue("Browsable")) Browsable = true;
            else if (HasStringValue("Unbrowsable") || HasStringValue("Hidden")) Browsable = false;
            else Browsable = attribs.OfType<BrowsableAttribute>().FirstOrDefault()?.Browsable;

            if (HasStringValue("Advanced")) IsAdvanced = true;
            else IsAdvanced = attribs.OfType<AdvancedAttribute>().FirstOrDefault()?.IsAdvanced;
        }
    }
}
