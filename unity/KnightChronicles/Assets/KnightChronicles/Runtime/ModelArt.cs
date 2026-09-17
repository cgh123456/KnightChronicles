using System;
using System.Collections.Generic;
using UnityEngine;

namespace KnightChronicles.Runtime
{
    /// <summary>Transparent orthographic art baked from editable Blender meshes.</summary>
    public static class ModelArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Missing = new HashSet<string>();
        [Serializable] private sealed class MotionMetadata { public int frameWidth; public float orthoScale; }
        private static readonly Dictionary<string, float> MotionUnits = new Dictionary<string, float>();
        /// <summary>Restore consistent model dimensions despite different atlas framing.</summary>
        public static float MotionPixelsPerUnit(string resourcePath)
        {
            if (MotionUnits.TryGetValue(resourcePath, out var units)) return units;
            var metadata = Resources.Load<TextAsset>(resourcePath);
            if (metadata == null) return 100;
            var info = JsonUtility.FromJson<MotionMetadata>(metadata.text);
            units = info != null && info.frameWidth > 0 && info.orthoScale > 0 ? info.frameWidth / info.orthoScale : 100;
            MotionUnits[resourcePath] = units; return units;
        }
        public static Sprite Sprite(string key)
        {
            if (Cache.TryGetValue(key, out var sprite)) return sprite;
            var texture = Resources.Load<Texture2D>("Art/World/" + key);
            if (texture == null)
            {
                if (Missing.Add(key)) Debug.LogWarning("ModelArt: missing baked model Art/World/" + key);
                return null;
            }
            sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = "Model_" + key; Cache.Add(key, sprite); return sprite;
        }
        /// <summary>Position is the image center; size is the exact world width/height.</summary>
        public static GameObject Place(string key, Transform parent, Vector2 pos, Vector2 size, int order)
        {
            var node = new GameObject("Model_" + key, typeof(SpriteRenderer));
            node.transform.SetParent(parent, false); node.transform.position = pos;
            var renderer = node.GetComponent<SpriteRenderer>(); renderer.sprite = Sprite(key); renderer.sortingOrder = order;
            if (renderer.sprite != null)
            {
                var bounds = renderer.sprite.bounds.size;
                node.transform.localScale = new Vector3(size.x / bounds.x, size.y / bounds.y, 1);
            }
            return node;
        }
    }
}
