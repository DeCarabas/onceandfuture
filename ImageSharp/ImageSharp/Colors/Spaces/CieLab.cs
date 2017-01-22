﻿// <copyright file="CieLab.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces
{
    using System;
    using System.ComponentModel;
    using System.Numerics;

    /// <summary>
    /// Represents an CIE LAB 1976 color.
    /// <see href="https://en.wikipedia.org/wiki/Lab_color_space"/>
    /// </summary>
    public struct CieLab : IEquatable<CieLab>, IAlmostEquatable<CieLab, float>
    {
        /// <summary>
        /// Represents a <see cref="CieLab"/> that has L, A, B values set to zero.
        /// </summary>
        public static readonly CieLab Empty = default(CieLab);

        /// <summary>
        /// Min range used for clamping
        /// </summary>
        private static readonly Vector3 VectorMin = new Vector3(0, -100, -100);

        /// <summary>
        /// Max range used for clamping
        /// </summary>
        private static readonly Vector3 VectorMax = new Vector3(100);

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private readonly Vector3 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLab"/> struct.
        /// </summary>
        /// <param name="l">The lightness dimension.</param>
        /// <param name="a">The a (green - magenta) component.</param>
        /// <param name="b">The b (blue - yellow) component.</param>
        public CieLab(float l, float a, float b)
            : this()
        {
            this.backingVector = Vector3.Clamp(new Vector3(l, a, b), VectorMin, VectorMax);
        }

        /// <summary>
        /// Gets the lightness dimension.
        /// <remarks>A value ranging between 0 (black), 100 (diffuse white) or higher (specular white).</remarks>
        /// </summary>
        public float L => this.backingVector.X;

        /// <summary>
        /// Gets the a color component.
        /// <remarks>Negative is green, positive magenta.</remarks>
        /// </summary>
        public float A => this.backingVector.Y;

        /// <summary>
        /// Gets the b color component.
        /// <remarks>Negative is blue, positive is yellow</remarks>
        /// </summary>
        public float B => this.backingVector.Z;

        /// <summary>
        /// Gets a value indicating whether this <see cref="CieLab"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="Color"/> to a
        /// <see cref="CieLab"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="Color"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="CieLab"/>.
        /// </returns>
        public static implicit operator CieLab(Color color)
        {
            // First convert to CIE XYZ
            Vector4 vector = color.ToVector4().Expand();
            float x = (vector.X * 0.4124F) + (vector.Y * 0.3576F) + (vector.Z * 0.1805F);
            float y = (vector.X * 0.2126F) + (vector.Y * 0.7152F) + (vector.Z * 0.0722F);
            float z = (vector.X * 0.0193F) + (vector.Y * 0.1192F) + (vector.Z * 0.9505F);

            // Now to LAB
            x /= 0.95047F;

            // y /= 1F;
            z /= 1.08883F;

            x = x > 0.008856F ? (float)Math.Pow(x, 0.3333333F) : ((903.3F * x) + 16F) / 116F;
            y = y > 0.008856F ? (float)Math.Pow(y, 0.3333333F) : ((903.3F * y) + 16F) / 116F;
            z = z > 0.008856F ? (float)Math.Pow(z, 0.3333333F) : ((903.3F * z) + 16F) / 116F;

            float l = Math.Max(0, (116F * y) - 16F);
            float a = 500F * (x - y);
            float b = 200F * (y - z);

            return new CieLab(l, a, b);
        }

        /// <summary>
        /// Compares two <see cref="CieLab"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieLab"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieLab"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(CieLab left, CieLab right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="CieLab"/> objects for inequality
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieLab"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieLab"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(CieLab left, CieLab right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.backingVector.GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.IsEmpty)
            {
                return "CieLab [Empty]";
            }

            return $"CieLab [ L={this.L:#0.##}, A={this.A:#0.##}, B={this.B:#0.##}]";
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is CieLab)
            {
                return this.Equals((CieLab)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(CieLab other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <inheritdoc/>
        public bool AlmostEquals(CieLab other, float precision)
        {
            Vector3 result = Vector3.Abs(this.backingVector - other.backingVector);

            return result.X <= precision
                && result.Y <= precision
                && result.Z <= precision;
        }
    }
}
