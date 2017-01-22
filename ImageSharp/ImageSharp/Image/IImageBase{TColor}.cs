﻿// <copyright file="IImageBase{TColor}.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    /// <summary>
    /// Encapsulates the basic properties and methods required to manipulate images in varying formats.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public interface IImageBase<TColor> : IImageBase
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// Gets the pixels as an array of the given packed pixel format.
        /// </summary>
        TColor[] Pixels { get; }

        /// <summary>
        /// Sets the size of the pixel array of the image to the given width and height.
        /// </summary>
        /// <param name="width">The new width of the image. Must be greater than zero.</param>
        /// <param name="height">The new height of the image. Must be greater than zero.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown if either <paramref name="width"/> or <paramref name="height"/> are less than or equal to 0.
        /// </exception>
        void InitPixels(int width, int height);

        /// <summary>
        /// Sets the pixel array of the image to the given value.
        /// </summary>
        /// <param name="width">The new width of the image. Must be greater than zero.</param>
        /// <param name="height">The new height of the image. Must be greater than zero.</param>
        /// <param name="pixels">The array with pixels. Must be a multiple of the width and height.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown if either <paramref name="width"/> or <paramref name="height"/> are less than or equal to 0.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown if the <paramref name="pixels"/> length is not equal to Width * Height.
        /// </exception>
        void SetPixels(int width, int height, TColor[] pixels);

        /// <summary>
        /// Sets the pixel array of the image to the given value, creating a copy of
        /// the original pixels.
        /// </summary>
        /// <param name="width">The new width of the image. Must be greater than zero.</param>
        /// <param name="height">The new height of the image. Must be greater than zero.</param>
        /// <param name="pixels">The array with pixels. Must be a multiple of four times the width and height.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Thrown if either <paramref name="width"/> or <paramref name="height"/> are less than or equal to 0.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown if the <paramref name="pixels"/> length is not equal to Width * Height.
        /// </exception>
        void ClonePixels(int width, int height, TColor[] pixels);

        /// <summary>
        /// Locks the image providing access to the pixels.
        /// <remarks>
        /// It is imperative that the accessor is correctly disposed off after use.
        /// </remarks>
        /// </summary>
        /// <returns>The <see cref="PixelAccessor{TColor}"/></returns>
        PixelAccessor<TColor> Lock();
    }

    /// <summary>
    /// Encapsulates the basic properties and methods required to manipulate images.
    /// </summary>
    public interface IImageBase
    {
        /// <summary>
        /// Gets the <see cref="Rectangle"/> representing the bounds of the image.
        /// </summary>
        Rectangle Bounds { get; }

        /// <summary>
        /// Gets or sets the quality of the image. This affects the output quality of lossy image formats.
        /// </summary>
        int Quality { get; set; }

        /// <summary>
        /// Gets or sets the frame delay for animated images.
        /// If not 0, this field specifies the number of hundredths (1/100) of a second to
        /// wait before continuing with the processing of the Data Stream.
        /// The clock starts ticking immediately after the graphic is rendered.
        /// </summary>
        int FrameDelay { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowable width in pixels.
        /// </summary>
        int MaxWidth { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowable height in pixels.
        /// </summary>
        int MaxHeight { get; set; }

        /// <summary>
        /// Gets the width in pixels.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the height in pixels.
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Gets the pixel ratio made up of the width and height.
        /// </summary>
        double PixelRatio { get; }
    }
}