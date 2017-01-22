﻿// <copyright file="ScharrProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// The Scharr operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Sobel_operator#Alternative_operators"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    public class ScharrProcessor<TColor> : EdgeDetector2DProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The horizontal gradient operator.
        /// </summary>
        private static readonly float[][] ScharrX = new float[3][]
        {
            new float[] { -3, 0, 3 },
            new float[] { -10, 0, 10 },
            new float[] { -3, 0, 3 }
        };

        /// <summary>
        /// The vertical gradient operator.
        /// </summary>
        private static readonly float[][] ScharrY = new float[3][]
        {
            new float[] { 3, 10, 3 },
            new float[] { 0, 0, 0 },
            new float[] { -3, -10, -3 }
        };

        /// <inheritdoc/>
        public override float[][] KernelX => ScharrX;

        /// <inheritdoc/>
        public override float[][] KernelY => ScharrY;
    }
}