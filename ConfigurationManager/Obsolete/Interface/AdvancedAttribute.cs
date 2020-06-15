using System;

namespace BepInEx
{
    [Obsolete("Pass string \"Advanced\" as a tag instead")]
    public class AdvancedAttribute : Attribute
    {
        public AdvancedAttribute(bool isAdvanced) => IsAdvanced = isAdvanced;

        public bool IsAdvanced { get; }
    }
}