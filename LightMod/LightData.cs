﻿using Microsoft.Xna.Framework;

namespace LightMod
{
    public class LightData
    {
        public Color color;
        public int textureIndex;
        public string texturePath;
        public int textureFrames;
        public int frameWidth;
        public float frameSeconds;
        public float radius;
        public XY offset;
        public bool isLamp;
    }

    public class XY
    {
        public float X;
        public float Y;
    }
}