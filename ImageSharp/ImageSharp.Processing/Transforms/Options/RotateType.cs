﻿// <copyright file="RotateType.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing
{
    /// <summary>
    /// Provides enumeration over how the image should be rotated.
    /// </summary>
    public enum RotateType
    {
        /// <summary>
        /// Do not rotate the image.
        /// </summary>
        None,

        /// <summary>
        /// Rotate the image by 90 degrees clockwise.
        /// </summary>
        Rotate90 = 90,

        /// <summary>
        /// Rotate the image by 180 degrees clockwise.
        /// </summary>
        Rotate180 = 180,

        /// <summary>
        /// Rotate the image by 270 degrees clockwise.
        /// </summary>
        Rotate270 = 270
    }
}
