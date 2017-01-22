﻿// <copyright file="ImageBase{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// The base class of all images. Encapsulates the basic properties and methods required to manipulate
    /// images in different pixel formats.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    [DebuggerDisplay("Image: {Width}x{Height}")]
    public abstract class ImageBase<TColor> : IImageBase<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The image pixels
        /// </summary>
        private TColor[] pixelBuffer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBase{TColor}"/> class.
        /// </summary>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        protected ImageBase(Configuration configuration = null)
        {
            this.Configuration = configuration ?? Configuration.Default;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBase{TColor}"/> class.
        /// </summary>
        /// <param name="width">The width of the image in pixels.</param>
        /// <param name="height">The height of the image in pixels.</param>
        /// <param name="configuration">
        /// The configuration providing initialization code which allows extending the library.
        /// </param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown if either <paramref name="width"/> or <paramref name="height"/> are less than or equal to 0.
        /// </exception>
        protected ImageBase(int width, int height, Configuration configuration = null)
        {
            this.Configuration = configuration ?? Configuration.Default;
            this.InitPixels(width, height);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageBase{TColor}"/> class.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="ImageBase{TColor}"/> to create this instance from.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown if the given <see cref="ImageBase{TColor}"/> is null.
        /// </exception>
        protected ImageBase(ImageBase<TColor> other)
        {
            Guard.NotNull(other, nameof(other), "Other image cannot be null.");

            this.Width = other.Width;
            this.Height = other.Height;
            this.CopyProperties(other);

            // Copy the pixels. Unsafe.CopyBlock gives us a nice speed boost here.
            this.pixelBuffer = new TColor[this.Width * this.Height];
            using (PixelAccessor<TColor> sourcePixels = other.Lock())
            using (PixelAccessor<TColor> target = this.Lock())
            {
                sourcePixels.CopyTo(target);
            }
        }

        /// <inheritdoc/>
        public int MaxWidth { get; set; } = int.MaxValue;

        /// <inheritdoc/>
        public int MaxHeight { get; set; } = int.MaxValue;

        /// <inheritdoc/>
        public TColor[] Pixels => this.pixelBuffer;

        /// <inheritdoc/>
        public int Width { get; private set; }

        /// <inheritdoc/>
        public int Height { get; private set; }

        /// <inheritdoc/>
        public double PixelRatio => (double)this.Width / this.Height;

        /// <inheritdoc/>
        public Rectangle Bounds => new Rectangle(0, 0, this.Width, this.Height);

        /// <inheritdoc/>
        public int Quality { get; set; }

        /// <inheritdoc/>
        public int FrameDelay { get; set; }

        /// <summary>
        /// Gets the configuration providing initialization code which allows extending the library.
        /// </summary>
        public Configuration Configuration { get; private set; }

        /// <inheritdoc/>
        public void InitPixels(int width, int height)
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));

            this.Width = width;
            this.Height = height;
            this.pixelBuffer = new TColor[width * height];
        }

        /// <inheritdoc/>
        public void SetPixels(int width, int height, TColor[] pixels)
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));
            Guard.NotNull(pixels, nameof(pixels));

            if (pixels.Length != width * height)
            {
                throw new ArgumentException("Pixel array must have the length of Width * Height.");
            }

            this.Width = width;
            this.Height = height;
            this.pixelBuffer = pixels;
        }

        /// <inheritdoc/>
        public void ClonePixels(int width, int height, TColor[] pixels)
        {
            Guard.MustBeGreaterThan(width, 0, nameof(width));
            Guard.MustBeGreaterThan(height, 0, nameof(height));
            Guard.NotNull(pixels, nameof(pixels));

            if (pixels.Length != width * height)
            {
                throw new ArgumentException("Pixel array must have the length of Width * Height.");
            }

            this.Width = width;
            this.Height = height;

            // Copy the pixels. TODO: use Unsafe.Copy.
            this.pixelBuffer = new TColor[pixels.Length];
            Array.Copy(pixels, this.pixelBuffer, pixels.Length);
        }

        /// <inheritdoc/>
        public virtual PixelAccessor<TColor> Lock()
        {
            return new PixelAccessor<TColor>(this);
        }

        /// <summary>
        /// Copies the properties from the other <see cref="ImageBase{TColor}"/>.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="ImageBase{TColor}"/> to copy the properties from.
        /// </param>
        protected void CopyProperties(ImageBase<TColor> other)
        {
            this.Configuration = other.Configuration;
            this.Quality = other.Quality;
            this.FrameDelay = other.FrameDelay;
        }
    }
}