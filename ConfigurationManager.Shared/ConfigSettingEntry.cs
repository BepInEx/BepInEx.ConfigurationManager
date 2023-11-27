﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;

#if IL2CPP
using BaseUnityPlugin = BepInEx.PluginInfo;
#endif

namespace ConfigurationManager
{
    internal sealed class ConfigSettingEntry : SettingEntryBase
    {
        public ConfigEntryBase Entry { get; }

        public ConfigSettingEntry(ConfigEntryBase entry, BaseUnityPlugin owner)
        {
            Entry = entry;

            DispName = entry.Definition.Key;
            Category = entry.Definition.Section;
            Description = entry.Description?.Description;

            var converter = TomlTypeConverter.GetConverter(entry.SettingType);
            if (converter != null)
            {
                ObjToStr = o => converter.ConvertToString(o, entry.SettingType);
                StrToObj = s => converter.ConvertToObject(s, entry.SettingType);
            }

            var values = entry.Description?.AcceptableValues;
            if (values != null)
                GetAcceptableValues(values);

            DefaultValue = entry.DefaultValue;

            SetFromAttributes(entry.Description?.Tags, owner);
        }

        private void GetAcceptableValues(AcceptableValueBase values)
        {
            var t = values.GetType();
            var listProp = t.GetProperty(nameof(AcceptableValueList<bool>.AcceptableValues), BindingFlags.Instance | BindingFlags.Public);
            if (listProp != null)
            {
                AcceptableValues = ((IEnumerable)listProp.GetValue(values, null)).Cast<object>().ToArray();
            }
            else
            {
                var minProp = t.GetProperty(nameof(AcceptableValueRange<bool>.MinValue), BindingFlags.Instance | BindingFlags.Public);
                if (minProp != null)
                {
                    var maxProp = t.GetProperty(nameof(AcceptableValueRange<bool>.MaxValue), BindingFlags.Instance | BindingFlags.Public);
                    if (maxProp == null) throw new ArgumentNullException(nameof(maxProp));
                    AcceptableValueRange = new KeyValuePair<object, object>(minProp.GetValue(values, null), maxProp.GetValue(values, null));
                    ShowRangeAsPercent = (AcceptableValueRange.Key.Equals(0) || AcceptableValueRange.Key.Equals(1)) && AcceptableValueRange.Value.Equals(100) ||
                                         AcceptableValueRange.Key.Equals(0f) && AcceptableValueRange.Value.Equals(1f);
                }
            }
        }

        public override Type SettingType => Entry.SettingType;

        public override object Get()
        {
            return Entry.BoxedValue;
        }

        protected override void SetValue(object newVal)
        {
            Entry.BoxedValue = newVal;
        }
    }
}