﻿// <copyright file="KayyaliProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// The Kayyali operator filter.
    /// <see href="http://edgedetection.webs.com/"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    public class KayyaliProcessor<TColor> : EdgeDetector2DProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The horizontal gradient operator.
        /// </summary>
        private static readonly float[][] KayyaliX =
        {
            new float[] { 6, 0, -6 },
            new float[] { 0, 0, 0 },
            new float[] { -6, 0, 6 }
        };

        /// <summary>
        /// The vertical gradient operator.
        /// </summary>
        private static readonly float[][] KayyaliY =
        {
            new float[] { -6, 0, 6 },
            new float[] { 0, 0, 0 },
            new float[] { 6, 0, -6 }
        };

        /// <inheritdoc/>
        public override float[][] KernelX => KayyaliX;

        /// <inheritdoc/>
        public override float[][] KernelY => KayyaliY;
    }
}
