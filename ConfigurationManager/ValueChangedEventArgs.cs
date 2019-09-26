// Made by MarC0 / ManlyMarco
// Copyright 2018 GNU General Public License v3.0

using System;

namespace ConfigurationManager
{
    /// <summary>
    /// Arguments representing a change in value
    /// </summary>
    public sealed class ValueChangedEventArgs<TValue> : EventArgs
    {
        public ValueChangedEventArgs(TValue newValue)
        {
            NewValue = newValue;
        }
        public TValue NewValue { get; }
    }
}
