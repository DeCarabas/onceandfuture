﻿// <copyright file="ImageMaths.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Provides common mathematical methods.
    /// </summary>
    internal static class ImageMaths
    {
        /// <summary>
        /// Returns the absolute value of a 32-bit signed integer. Uses bit shifting to speed up the operation.
        /// </summary>
        /// <param name="x">
        /// A number that is greater than <see cref="int.MinValue"/>, but less than or equal to <see cref="int.MaxValue"/>
        /// </param>
        /// <returns>The <see cref="int"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FastAbs(int x)
        {
            return (x ^ (x >> 31)) - (x >> 31);
        }

        /// <summary>
        /// Returns how many bits are required to store the specified number of colors.
        /// Performs a Log2() on the value.
        /// </summary>
        /// <param name="colors">The number of colors.</param>
        /// <returns>
        /// The <see cref="int"/>
        /// </returns>
        public static int GetBitsNeededForColorDepth(int colors)
        {
            return (int)Math.Ceiling(Math.Log(colors, 2));
        }

        /// <summary>
        /// Implementation of 1D Gaussian G(x) function
        /// </summary>
        /// <param name="x">The x provided to G(x).</param>
        /// <param name="sigma">The spread of the blur.</param>
        /// <returns>The Gaussian G(x)</returns>
        public static float Gaussian(float x, float sigma)
        {
            const float Numerator = 1.0f;
            float denominator = (float)(Math.Sqrt(2 * Math.PI) * sigma);

            float exponentNumerator = -x * x;
            float exponentDenominator = (float)(2 * Math.Pow(sigma, 2));

            float left = Numerator / denominator;
            float right = (float)Math.Exp(exponentNumerator / exponentDenominator);

            return left * right;
        }

        /// <summary>
        /// Returns the result of a B-C filter against the given value.
        /// <see href="http://www.imagemagick.org/Usage/filter/#cubic_bc"/>
        /// </summary>
        /// <param name="x">The value to process.</param>
        /// <param name="b">The B-Spline curve variable.</param>
        /// <param name="c">The Cardinal curve variable.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        public static float GetBcValue(float x, float b, float c)
        {
            float temp;

            if (x < 0F)
            {
                x = -x;
            }

            temp = x * x;
            if (x < 1F)
            {
                x = ((12 - (9 * b) - (6 * c)) * (x * temp)) + ((-18 + (12 * b) + (6 * c)) * temp) + (6 - (2 * b));
                return x / 6F;
            }

            if (x < 2F)
            {
                x = ((-b - (6 * c)) * (x * temp)) + (((6 * b) + (30 * c)) * temp) + (((-12 * b) - (48 * c)) * x) + ((8 * b) + (24 * c));
                return x / 6F;
            }

            return 0F;
        }

        /// <summary>
        /// Gets the result of a sine cardinal function for the given value.
        /// </summary>
        /// <param name="x">The value to calculate the result for.</param>
        /// <returns>
        /// The <see cref="float"/>.
        /// </returns>
        public static float SinC(float x)
        {
            if (Math.Abs(x) > Constants.Epsilon)
            {
                x *= (float)Math.PI;
                return Clean((float)Math.Sin(x) / x);
            }

            return 1.0f;
        }

        /// <summary>
        /// Returns the given degrees converted to radians.
        /// </summary>
        /// <param name="degrees">The angle in degrees.</param>
        /// <returns>
        /// The <see cref="float"/> representing the degree as radians.
        /// </returns>
        public static float DegreesToRadians(float degrees)
        {
            return degrees * (float)(Math.PI / 180);
        }

        /// <summary>
        /// Gets the bounding <see cref="Rectangle"/> from the given points.
        /// </summary>
        /// <param name="topLeft">
        /// The <see cref="Point"/> designating the top left position.
        /// </param>
        /// <param name="bottomRight">
        /// The <see cref="Point"/> designating the bottom right position.
        /// </param>
        /// <returns>
        /// The bounding <see cref="Rectangle"/>.
        /// </returns>
        public static Rectangle GetBoundingRectangle(Point topLeft, Point bottomRight)
        {
            return new Rectangle(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
        }

        /// <summary>
        /// Gets the bounding <see cref="Rectangle"/> from the given matrix.
        /// </summary>
        /// <param name="rectangle">The source rectangle.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>
        /// The <see cref="Rectangle"/>.
        /// </returns>
        public static Rectangle GetBoundingRectangle(Rectangle rectangle, Matrix3x2 matrix)
        {
            Vector2 leftTop = Vector2.Transform(new Vector2(rectangle.Left, rectangle.Top), matrix);
            Vector2 rightTop = Vector2.Transform(new Vector2(rectangle.Right, rectangle.Top), matrix);
            Vector2 leftBottom = Vector2.Transform(new Vector2(rectangle.Left, rectangle.Bottom), matrix);
            Vector2 rightBottom = Vector2.Transform(new Vector2(rectangle.Right, rectangle.Bottom), matrix);

            Vector2[] allCorners = { leftTop, rightTop, leftBottom, rightBottom };
            float extentX = allCorners.Select(v => v.X).Max() - allCorners.Select(v => v.X).Min();
            float extentY = allCorners.Select(v => v.Y).Max() - allCorners.Select(v => v.Y).Min();
            return new Rectangle(0, 0, (int)extentX, (int)extentY);
        }

        /// <summary>
        /// Finds the bounding rectangle based on the first instance of any color component other
        /// than the given one.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="bitmap">The <see cref="Image"/> to search within.</param>
        /// <param name="componentValue">The color component value to remove.</param>
        /// <param name="channel">The <see cref="RgbaComponent"/> channel to test against.</param>
        /// <returns>
        /// The <see cref="Rectangle"/>.
        /// </returns>
        public static Rectangle GetFilteredBoundingRectangle<TColor>(ImageBase<TColor> bitmap, float componentValue, RgbaComponent channel = RgbaComponent.B)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            Point topLeft = default(Point);
            Point bottomRight = default(Point);

            Func<PixelAccessor<TColor>, int, int, float, bool> delegateFunc;

            // Determine which channel to check against
            switch (channel)
            {
                case RgbaComponent.R:
                    delegateFunc = (pixels, x, y, b) => Math.Abs(pixels[x, y].ToVector4().X - b) > Constants.Epsilon;
                    break;

                case RgbaComponent.G:
                    delegateFunc = (pixels, x, y, b) => Math.Abs(pixels[x, y].ToVector4().Y - b) > Constants.Epsilon;
                    break;

                case RgbaComponent.B:
                    delegateFunc = (pixels, x, y, b) => Math.Abs(pixels[x, y].ToVector4().Z - b) > Constants.Epsilon;
                    break;

                default:
                    delegateFunc = (pixels, x, y, b) => Math.Abs(pixels[x, y].ToVector4().W - b) > Constants.Epsilon;
                    break;
            }

            Func<PixelAccessor<TColor>, int> getMinY = pixels =>
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (delegateFunc(pixels, x, y, componentValue))
                        {
                            return y;
                        }
                    }
                }

                return 0;
            };

            Func<PixelAccessor<TColor>, int> getMaxY = pixels =>
            {
                for (int y = height - 1; y > -1; y--)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (delegateFunc(pixels, x, y, componentValue))
                        {
                            return y;
                        }
                    }
                }

                return height;
            };

            Func<PixelAccessor<TColor>, int> getMinX = pixels =>
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (delegateFunc(pixels, x, y, componentValue))
                        {
                            return x;
                        }
                    }
                }

                return 0;
            };

            Func<PixelAccessor<TColor>, int> getMaxX = pixels =>
            {
                for (int x = width - 1; x > -1; x--)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (delegateFunc(pixels, x, y, componentValue))
                        {
                            return x;
                        }
                    }
                }

                return height;
            };

            using (PixelAccessor<TColor> bitmapPixels = bitmap.Lock())
            {
                topLeft.Y = getMinY(bitmapPixels);
                topLeft.X = getMinX(bitmapPixels);
                bottomRight.Y = (getMaxY(bitmapPixels) + 1).Clamp(0, height);
                bottomRight.X = (getMaxX(bitmapPixels) + 1).Clamp(0, width);
            }

            return GetBoundingRectangle(topLeft, bottomRight);
        }

        /// <summary>
        /// Ensures that any passed double is correctly rounded to zero
        /// </summary>
        /// <param name="x">The value to clean.</param>
        /// <returns>
        /// The <see cref="float"/>
        /// </returns>.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Clean(float x)
        {
            if (Math.Abs(x) < Constants.Epsilon)
            {
                return 0F;
            }

            return x;
        }
    }
}
