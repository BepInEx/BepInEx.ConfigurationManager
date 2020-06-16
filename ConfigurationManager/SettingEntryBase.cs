// Made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using BepInEx;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

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
        public Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer { get; private set; }

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
        /// Force the "Reset" button to not be displayed, even if a valid DefaultValue is available. 
        /// </summary>
        public bool HideDefaultButton { get; protected set; }

        /// <summary>
        /// Force the setting name to not be displayed. Should only be used with a <see cref="CustomDrawer"/> to get more space.
        /// Can be used together with <see cref="HideDefaultButton"/> to gain even more space.
        /// </summary>
        public bool HideSettingName { get; protected set; }

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
        /// Is this setting advanced
        /// </summary>
        public bool? IsAdvanced { get; internal set; }

        /// <summary>
        /// Order of the setting on the settings list relative to other settings in a category. 0 by default, lower is higher on the list.
        /// </summary>
        public int Order { get; protected set; }

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

        /// <summary>
        /// Implementation of <see cref="Set"/>
        /// </summary>
        protected abstract void SetValue(object newVal);

        /// <summary>
        /// Custom converter from setting type to string for the textbox
        /// </summary>
        public Func<object, string> ObjToStr { get; internal set; }

        /// <summary>
        /// Custom converter from string to setting type for the textbox
        /// </summary>
        public Func<string, object> StrToObj { get; internal set; }

        private static readonly PropertyInfo[] _myProperties = typeof(SettingEntryBase).GetProperties(BindingFlags.Instance | BindingFlags.Public);

        internal void SetFromAttributes(object[] attribs, BaseUnityPlugin pluginInstance)
        {
            PluginInstance = pluginInstance;
            PluginInfo = pluginInstance?.Info.Metadata;

            if (attribs == null || attribs.Length == 0) return;

            foreach (var attrib in attribs)
            {
                switch (attrib)
                {
                    case null: break;

                    // Obsolete attributes from bepin4 -----------------------
#pragma warning disable 618 // Disable obsolete warning
                    case DisplayNameAttribute da:
                        DispName = da.DisplayName;
                        break;
                    case CategoryAttribute ca:
                        Category = ca.Category;
                        break;
                    case DescriptionAttribute de:
                        Description = de.Description;
                        break;
                    case DefaultValueAttribute def:
                        DefaultValue = def.Value;
                        break;
                    case ReadOnlyAttribute ro:
                        ReadOnly = ro.IsReadOnly;
                        break;
                    case BrowsableAttribute bro:
                        Browsable = bro.Browsable;
                        break;
                    case AdvancedAttribute adv:
                        IsAdvanced = adv.IsAdvanced;
                        break;
                    case AcceptableValueListAttribute oldAcceptableValList:
                        AcceptableValues = oldAcceptableValList.GetAcceptableValues(pluginInstance);
                        break;
                    case AcceptableValueRangeAttribute oldAcceptableValRange:
                        AcceptableValueRange = new KeyValuePair<object, object>(oldAcceptableValRange.MinValue, oldAcceptableValRange.MaxValue);
                        ShowRangeAsPercent = oldAcceptableValRange.ShowAsPercentage;
                        break;
                    case CustomSettingDrawAttribute oldCustomDraw:
                        CustomDrawer = _ => oldCustomDraw.Run(PluginInstance);
                        break;
#pragma warning restore 618

                    // Obsolete attributes from early bepin5 -----------------------
                    case Action<SettingEntryBase> newCustomDraw:
                        CustomDrawer = _ => newCustomDraw(this);
                        break;
                    case string str:
                        switch (str)
                        {
                            case "ReadOnly": ReadOnly = true; break;
                            case "Browsable": Browsable = true; break;
                            case "Unbrowsable": case "Hidden": Browsable = false; break;
                            case "Advanced": IsAdvanced = true; break;
                        }
                        break;

                    // Copy attributes from a specially formatted object, currently recommended
                    default:
                        var attrType = attrib.GetType();
                        if (attrType.Name == "ConfigurationManagerAttributes")
                        {
                            var otherFields = attrType.GetFields(BindingFlags.Instance | BindingFlags.Public);
                            foreach (var propertyPair in _myProperties.Join(otherFields, my => my.Name, other => other.Name, (my, other) => new { my, other }))
                            {
                                try
                                {
                                    var val = propertyPair.other.GetValue(attrib);
                                    if (val != null)
                                        propertyPair.my.SetValue(this, val, null);
                                }
                                catch (Exception ex)
                                {
                                    ConfigurationManager.Logger.LogWarning($"Failed to copy value {propertyPair.my.Name} from provided tag object {attrType.FullName} - " + ex.Message);
                                }
                            }
                            break;
                        }
                        return;
                }
            }
        }
    }
}
