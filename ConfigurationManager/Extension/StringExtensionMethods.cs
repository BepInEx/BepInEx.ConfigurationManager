using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConfigurationManager.Extension
{
    internal static class StringExtensionMethods
    { 
        public static string AppendZero(this string s)
        {
            if (s.Contains("."))
            {
                return s;
            }
            else
            {
                return s + ".0";
            }
        }

        public static string AppendZeroIfFloat(this string s, Type type)
        {
            if (type == typeof(float)
                || type == typeof(double)
                || type == typeof(decimal))
            {
                return s.AppendZero();
            }
            else
            {
                return s;
            }
        }
    }
}
