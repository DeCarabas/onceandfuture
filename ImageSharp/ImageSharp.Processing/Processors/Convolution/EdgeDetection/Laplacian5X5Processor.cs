﻿// <copyright file="Laplacian5X5Processor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// The Laplacian 5 x 5 operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator"/>
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType", Justification = "We want to use only one instance of each array field for each generic type.")]
    public class Laplacian5X5Processor<TColor> : EdgeDetectorProcessor<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The 2d gradient operator.
        /// </summary>
        private static readonly float[][] Laplacian5X5XY =
        {
            new float[] { -1, -1, -1, -1, -1 },
            new float[] { -1, -1, -1, -1, -1 },
            new float[] { -1, -1, 24, -1, -1 },
            new float[] { -1, -1, -1, -1, -1 },
            new float[] { -1, -1, -1, -1, -1 }
        };

        /// <inheritdoc/>
        public override float[][] KernelXY => Laplacian5X5XY;
    }
}