using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConfigurationManager.Config
{
    /// <summary>
    /// User-defined configuration
    /// </summary>
    [Serializable]
    public class UserConfig
    {
        public int ScreenWidth;

        /// <summary>
        /// Whether to use custom screens
        /// </summary>
        public bool UseCustomScreen;

        public int FontSize;

        /// <summary>
        /// Whether multilingual is used
        /// </summary>
        public bool UseLangs;
    }
}
