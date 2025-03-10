using System.Collections.Generic;
using UnityEngine;

namespace ConfigurationManager.Utilities
{
    /// <summary>
    /// Simple static texture pool that reuses Texture2D objects
    /// to reduce overhead of creating/destroying them repeatedly.
    /// </summary>
    internal static class TexturePool
    {
        // Key: (width, height, format, mipChain)
        // Value: Stack of available (unused) textures with the above specs
        private static readonly Dictionary<TextureKey, Stack<Texture2D>> _texturePool = new Dictionary<TextureKey, Stack<Texture2D>>();

        /// <summary>
        /// Retrieve a pooled texture (or create a new one) matching the desired specs.
        /// </summary>
        public static Texture2D GetTexture2D(int width, int height, TextureFormat format, bool mipChain)
        {
            TextureKey key = new TextureKey(width, height, format, mipChain);

            if (_texturePool.TryGetValue(key, out Stack<Texture2D> stack) && stack.Count > 0)
            {
                // Reuse existing texture
                Texture2D tex = stack.Pop();
                // Clear out any leftover pixels if you want. Often not necessary:
                //   tex.SetPixels32(new Color32[width * height]);
                //   tex.Apply(false);
                return tex;
            }
            else
            {
                // Pool is empty or doesn't exist yet: create new
                return new Texture2D(width, height, format, mipChain);
            }
        }

        /// <summary>
        /// Return a texture to the pool.
        /// </summary>
        public static void ReleaseTexture2D(Texture2D tex)
        {
            if (tex == null) return;
            TextureKey key = new TextureKey(tex.width, tex.height, tex.format, tex.mipmapCount > 1);

            if (!_texturePool.ContainsKey(key))
                _texturePool[key] = new Stack<Texture2D>();

            // Optionally clear data or re-initialize texture if needed:
            // tex.SetPixels32(new Color32[tex.width * tex.height]);
            // tex.Apply(false);

            if (_texturePool[key].Count < 20)
            {
                _texturePool[key].Push(tex);
            }
            else
            {
                // Pool is full: destroy the texture
                UnityEngine.Object.Destroy(tex);
            }
        }

        public static void ClearAll()
        {
            foreach (var kvp in _texturePool)
            {
                var stack = kvp.Value;
                while (stack.Count > 0)
                {
                    var tex = stack.Pop();
                    if (tex != null)
                    {
                        // Now explicitly destroy so it doesn’t pile up.
                        // If you want to handle IL2CPP or different Unity versions,
                        // make sure you destroy properly:
                        UnityEngine.Object.Destroy(tex);
                    }
                }
            }

            _texturePool.Clear();
        }
    }

    internal struct TextureKey
    {
        public int Width;
        public int Height;
        public TextureFormat Format;
        public bool MipChain;

        public TextureKey(int w, int h, TextureFormat f, bool m)
        {
            Width = w;
            Height = h;
            Format = f;
            MipChain = m;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Width.GetHashCode();
                hash = hash * 31 + Height.GetHashCode();
                hash = hash * 31 + Format.GetHashCode();
                hash = hash * 31 + MipChain.GetHashCode();
                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is TextureKey)) return false;
            var other = (TextureKey)obj;
            return Width == other.Width
                   && Height == other.Height
                   && Format == other.Format
                   && MipChain == other.MipChain;
        }
    }
}