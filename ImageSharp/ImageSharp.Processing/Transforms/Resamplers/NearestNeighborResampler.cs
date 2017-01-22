﻿// <copyright file="NearestNeighborResampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing
{
    /// <summary>
    /// The function implements the nearest neighbor algorithm. This uses an unscaled filter
    /// which will select the closest pixel to the new pixels position.
    /// </summary>
    public class NearestNeighborResampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 1;

        /// <inheritdoc/>
        public float GetValue(float x)
        {
            return x;
        }
    }
}
