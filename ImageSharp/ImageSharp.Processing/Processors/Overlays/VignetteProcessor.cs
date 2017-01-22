﻿// <copyright file="VignetteProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageProcessor{TColor}"/> that applies a radial vignette effect to an <see cref="Image{TColor}"/>.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class VignetteProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="VignetteProcessor{TColor}"/> class.
        /// </summary>
        public VignetteProcessor()
        {
            TColor color = default(TColor);
            color.PackFromVector4(Color.Black.ToVector4());
            this.VignetteColor = color;
        }

        /// <summary>
        /// Gets or sets the vignette color to apply.
        /// </summary>
        public TColor VignetteColor { get; set; }

        /// <summary>
        /// Gets or sets the the x-radius.
        /// </summary>
        public float RadiusX { get; set; }

        /// <summary>
        /// Gets or sets the the y-radius.
        /// </summary>
        public float RadiusY { get; set; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;
            TColor vignetteColor = this.VignetteColor;
            Vector2 centre = Rectangle.Center(sourceRectangle).ToVector2();
            float rX = this.RadiusX > 0 ? Math.Min(this.RadiusX, sourceRectangle.Width * .5F) : sourceRectangle.Width * .5F;
            float rY = this.RadiusY > 0 ? Math.Min(this.RadiusY, sourceRectangle.Height * .5F) : sourceRectangle.Height * .5F;
            float maxDistance = (float)Math.Sqrt((rX * rX) + (rY * rY));

            // Align start/end positions.
            int minX = Math.Max(0, startX);
            int maxX = Math.Min(source.Width, endX);
            int minY = Math.Max(0, startY);
            int maxY = Math.Min(source.Height, endY);

            // Reset offset if necessary.
            if (minX > 0)
            {
                startX = 0;
            }

            if (minY > 0)
            {
                startY = 0;
            }

            using (PixelAccessor<TColor> sourcePixels = source.Lock())
            {
                Parallel.For(
                    minY,
                    maxY,
                    this.ParallelOptions,
                    y =>
                        {
                            int offsetY = y - startY;
                            for (int x = minX; x < maxX; x++)
                            {
                                int offsetX = x - startX;
                                float distance = Vector2.Distance(centre, new Vector2(offsetX, offsetY));
                                Vector4 sourceColor = sourcePixels[offsetX, offsetY].ToVector4();
                                TColor packed = default(TColor);
                                packed.PackFromVector4(Vector4BlendTransforms.PremultipliedLerp(sourceColor, vignetteColor.ToVector4(), .9F * (distance / maxDistance)));
                                sourcePixels[offsetX, offsetY] = packed;
                            }
                        });
            }
        }
    }
}