﻿// <copyright file="BinaryThresholdProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// An <see cref="IImageProcessor{TColor}"/> to perform binary threshold filtering against an
    /// <see cref="Image"/>. The image will be converted to grayscale before thresholding occurs.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public class BinaryThresholdProcessor<TColor> : ImageProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryThresholdProcessor{TColor}"/> class.
        /// </summary>
        /// <param name="threshold">The threshold to split the image. Must be between 0 and 1.</param>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="threshold"/> is less than 0 or is greater than 1.
        /// </exception>
        public BinaryThresholdProcessor(float threshold)
        {
            // TODO: Check limit.
            Guard.MustBeBetweenOrEqualTo(threshold, 0, 1, nameof(threshold));
            this.Value = threshold;

            TColor upper = default(TColor);
            upper.PackFromVector4(Color.White.ToVector4());
            this.UpperColor = upper;

            TColor lower = default(TColor);
            lower.PackFromVector4(Color.Black.ToVector4());
            this.LowerColor = lower;
        }

        /// <summary>
        /// Gets the threshold value.
        /// </summary>
        public float Value { get; }

        /// <summary>
        /// Gets or sets the color to use for pixels that are above the threshold.
        /// </summary>
        public TColor UpperColor { get; set; }

        /// <summary>
        /// Gets or sets the color to use for pixels that fall below the threshold.
        /// </summary>
        public TColor LowerColor { get; set; }

        /// <inheritdoc/>
        protected override void BeforeApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            new GrayscaleBt709Processor<TColor>().Apply(source, sourceRectangle);
        }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TColor> source, Rectangle sourceRectangle)
        {
            float threshold = this.Value;
            TColor upper = this.UpperColor;
            TColor lower = this.LowerColor;

            int startY = sourceRectangle.Y;
            int endY = sourceRectangle.Bottom;
            int startX = sourceRectangle.X;
            int endX = sourceRectangle.Right;

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
                            TColor color = sourcePixels[offsetX, offsetY];

                            // Any channel will do since it's Grayscale.
                            sourcePixels[offsetX, offsetY] = color.ToVector4().X >= threshold ? upper : lower;
                        }
                    });
            }
        }
    }
}