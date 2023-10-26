using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ConfigurationManager.Config
{
    /// <summary>
    /// Multilingual
    /// </summary>
    [Serializable]
    public class Langs
    {
        [SerializeField]
        public string En;
        [SerializeField]
        public string Other;
    }
}
