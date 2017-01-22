﻿// <copyright file="Color.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Globalization;
    using System.Numerics;

    /// <summary>
    /// Packed pixel type containing four 8-bit unsigned normalized values ranging from 0 to 255.
    /// The color components are stored in red, green, blue, and alpha order.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public partial struct Color : IPackedPixel<uint>, IEquatable<Color>
    {
        /// <summary>
        /// The shift count for the red component
        /// </summary>
        private const int RedShift = 0;

        /// <summary>
        /// The shift count for the green component
        /// </summary>
        private const int GreenShift = 8;

        /// <summary>
        /// The shift count for the blue component
        /// </summary>
        private const int BlueShift = 16;

        /// <summary>
        /// The shift count for the alpha component
        /// </summary>
        private const int AlphaShift = 24;

        /// <summary>
        /// The maximum byte value.
        /// </summary>
        private static readonly Vector4 MaxBytes = new Vector4(255);

        /// <summary>
        /// The half vector value.
        /// </summary>
        private static readonly Vector4 Half = new Vector4(0.5F);

        /// <summary>
        /// The packed value.
        /// </summary>
        private uint packedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        public Color(byte r, byte g, byte b, byte a = 255)
        {
            this.packedValue = Pack(r, g, b, a);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="hex">
        /// The hexadecimal representation of the combined color components arranged
        /// in rgb, rgba, rrggbb, or rrggbbaa format to match web syntax.
        /// </param>
        public Color(string hex)
        {
            Guard.NotNullOrEmpty(hex, nameof(hex));

            hex = ToRgbaHex(hex);

            if (hex == null || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out this.packedValue))
            {
                throw new ArgumentException("Hexadecimal string is not in the correct format.", nameof(hex));
            }

            // Order parsed from hex string will be backwards, so reverse it.
            this.packedValue = Pack(this.A, this.B, this.G, this.R);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="r">The red component.</param>
        /// <param name="g">The green component.</param>
        /// <param name="b">The blue component.</param>
        /// <param name="a">The alpha component.</param>
        public Color(float r, float g, float b, float a = 1)
        {
            this.packedValue = Pack(r, g, b, a);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        public Color(Vector3 vector)
        {
            this.packedValue = Pack(ref vector);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector containing the components for the packed vector.
        /// </param>
        public Color(Vector4 vector)
        {
            this.packedValue = Pack(ref vector);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="packed">
        /// The packed value.
        /// </param>
        public Color(uint packed)
        {
            this.packedValue = packed;
        }

        /// <summary>
        /// Gets or sets the red component.
        /// </summary>
        public byte R
        {
            get
            {
                return (byte)(this.packedValue >> RedShift);
            }

            set
            {
                this.packedValue = this.packedValue & 0xFFFFFF00 | (uint)value << RedShift;
            }
        }

        /// <summary>
        /// Gets or sets the green component.
        /// </summary>
        public byte G
        {
            get
            {
                return (byte)(this.packedValue >> GreenShift);
            }

            set
            {
                this.packedValue = this.packedValue & 0xFFFF00FF | (uint)value << GreenShift;
            }
        }

        /// <summary>
        /// Gets or sets the blue component.
        /// </summary>
        public byte B
        {
            get
            {
                return (byte)(this.packedValue >> BlueShift);
            }

            set
            {
                this.packedValue = this.packedValue & 0xFF00FFFF | (uint)value << BlueShift;
            }
        }

        /// <summary>
        /// Gets or sets the alpha component.
        /// </summary>
        public byte A
        {
            get
            {
                return (byte)(this.packedValue >> AlphaShift);
            }

            set
            {
                this.packedValue = this.packedValue & 0x00FFFFFF | (uint)value << AlphaShift;
            }
        }

        /// <inheritdoc/>
        public uint PackedValue
        {
            get
            {
                return this.packedValue;
            }

            set
            {
                this.packedValue = value;
            }
        }

        /// <summary>
        /// Compares two <see cref="Color"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Color"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Color"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Color left, Color right)
        {
            return left.packedValue == right.packedValue;
        }

        /// <summary>
        /// Compares two <see cref="Color"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Color"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Color"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Color left, Color right)
        {
            return left.packedValue != right.packedValue;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="hex">
        /// The hexadecimal representation of the combined color components arranged
        /// in rgb, rgba, rrggbb, or rrggbbaa format to match web syntax.
        /// </param>
        /// <returns>
        /// The <see cref="Color"/>.
        /// </returns>
        public static Color FromHex(string hex)
        {
            return new Color(hex);
        }

        /// <inheritdoc/>
        public void PackFromBytes(byte x, byte y, byte z, byte w)
        {
            this.packedValue = Pack(x, y, z, w);
        }

        /// <summary>
        /// Converts the value of this instance to a hexadecimal string.
        /// </summary>
        /// <returns>A hexadecimal string representation of the value.</returns>
        public string ToHex()
        {
            uint hexOrder = Pack(this.A, this.B, this.G, this.R);
            return hexOrder.ToString("X8");
        }

        /// <inheritdoc/>
        public void ToXyzBytes(byte[] bytes, int startIndex)
        {
            bytes[startIndex] = this.R;
            bytes[startIndex + 1] = this.G;
            bytes[startIndex + 2] = this.B;
        }

        /// <inheritdoc/>
        public void ToXyzwBytes(byte[] bytes, int startIndex)
        {
            bytes[startIndex] = this.R;
            bytes[startIndex + 1] = this.G;
            bytes[startIndex + 2] = this.B;
            bytes[startIndex + 3] = this.A;
        }

        /// <inheritdoc/>
        public void ToZyxBytes(byte[] bytes, int startIndex)
        {
            bytes[startIndex] = this.B;
            bytes[startIndex + 1] = this.G;
            bytes[startIndex + 2] = this.R;
        }

        /// <inheritdoc/>
        public void ToZyxwBytes(byte[] bytes, int startIndex)
        {
            bytes[startIndex] = this.B;
            bytes[startIndex + 1] = this.G;
            bytes[startIndex + 2] = this.R;
            bytes[startIndex + 3] = this.A;
        }

        /// <inheritdoc/>
        public void PackFromVector4(Vector4 vector)
        {
            this.packedValue = Pack(ref vector);
        }

        /// <inheritdoc/>
        public Vector4 ToVector4()
        {
            return new Vector4(this.R, this.G, this.B, this.A) / MaxBytes;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return (obj is Color) && this.Equals((Color)obj);
        }

        /// <inheritdoc/>
        public bool Equals(Color other)
        {
            return this.packedValue == other.packedValue;
        }

        /// <summary>
        /// Gets a string representation of the packed vector.
        /// </summary>
        /// <returns>A string representation of the packed vector.</returns>
        public override string ToString()
        {
            return this.ToVector4().ToString();
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.packedValue.GetHashCode();
        }

        /// <summary>
        /// Packs a <see cref="Vector4"/> into a uint.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        /// <returns>The <see cref="uint"/> containing the packed values.</returns>
        private static uint Pack(ref Vector4 vector)
        {
            vector = Vector4.Clamp(vector, Vector4.Zero, Vector4.One);
            vector *= MaxBytes;
            vector += Half;
            return (uint)(((byte)vector.X << RedShift)
                        | ((byte)vector.Y << GreenShift)
                        | ((byte)vector.Z << BlueShift)
                        | (byte)vector.W << AlphaShift);
        }

        /// <summary>
        /// Packs a <see cref="Vector3"/> into a uint.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        /// <returns>The <see cref="uint"/> containing the packed values.</returns>
        private static uint Pack(ref Vector3 vector)
        {
            Vector4 value = new Vector4(vector, 1);
            return Pack(ref value);
        }

        /// <summary>
        /// Packs the four floats into a <see cref="uint"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        /// <returns>The <see cref="uint"/></returns>
        private static uint Pack(float x, float y, float z, float w)
        {
            Vector4 value = new Vector4(x, y, z, w);
            return Pack(ref value);
        }

        /// <summary>
        /// Packs the four floats into a <see cref="uint"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        /// <returns>The <see cref="uint"/></returns>
        private static uint Pack(byte x, byte y, byte z, byte w)
        {
            return (uint)(x << RedShift | y << GreenShift | z << BlueShift | w << AlphaShift);
        }

        /// <summary>
        /// Converts the specified hex value to an rrggbbaa hex value.
        /// </summary>
        /// <param name="hex">The hex value to convert.</param>
        /// <returns>
        /// A rrggbbaa hex value.
        /// </returns>
        private static string ToRgbaHex(string hex)
        {
            hex = hex.StartsWith("#") ? hex.Substring(1) : hex;

            if (hex.Length == 8)
            {
                return hex;
            }

            if (hex.Length == 6)
            {
                return hex + "FF";
            }

            if (hex.Length < 3 || hex.Length > 4)
            {
                return null;
            }

            string red = char.ToString(hex[0]);
            string green = char.ToString(hex[1]);
            string blue = char.ToString(hex[2]);
            string alpha = hex.Length == 3 ? "F" : char.ToString(hex[3]);

            return red + red + green + green + blue + blue + alpha + alpha;
        }
    }
}
