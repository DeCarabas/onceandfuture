﻿// <copyright file="PrewittProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// The Prewitt operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Prewitt_operator"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    public class PrewittProcessor<TColor> : EdgeDetector2DProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The horizontal gradient operator.
        /// </summary>
        private static readonly float[][] PrewittX =
        {
            new float[] { -1, 0, 1 },
            new float[] { -1, 0, 1 },
            new float[] { -1, 0, 1 }
        };

        /// <summary>
        /// The vertical gradient operator.
        /// </summary>
        private static readonly float[][] PrewittY =
        {
            new float[] { 1, 1, 1 },
            new float[] { 0, 0, 0 },
            new float[] { -1, -1, -1 }
        };

        /// <inheritdoc/>
        public override float[][] KernelX => PrewittX;

        /// <inheritdoc/>
        public override float[][] KernelY => PrewittY;
    }
}