﻿// <copyright file="WuQuantizer.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Quantizers
{
    using System;
    using System.Buffers;
    using System.Numerics;
    using System.Threading.Tasks;

    /// <summary>
    /// An implementation of Wu's color quantizer with alpha channel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Based on C Implementation of Xiaolin Wu's Color Quantizer (v. 2)
    /// (see Graphics Gems volume II, pages 126-133)
    /// (<see href="http://www.ece.mcmaster.ca/~xwu/cq.c"/>).
    /// </para>
    /// <para>
    /// This adaptation is based on the excellent JeremyAnsel.ColorQuant by Jérémy Ansel
    /// <see href="https://github.com/JeremyAnsel/JeremyAnsel.ColorQuant"/>
    /// </para>
    /// <para>
    /// Algorithm: Greedy orthogonal bipartition of RGB space for variance minimization aided by inclusion-exclusion tricks.
    /// For speed no nearest neighbor search is done. Slightly better performance can be expected by more sophisticated
    /// but more expensive versions.
    /// </para>
    /// </remarks>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    public sealed class WuQuantizer<TColor> : IQuantizer<TColor>
        where TColor : struct, IPackedPixel, IEquatable<TColor>
    {
        /// <summary>
        /// The index bits.
        /// </summary>
        private const int IndexBits = 6;

        /// <summary>
        /// The index alpha bits.
        /// </summary>
        private const int IndexAlphaBits = 3;

        /// <summary>
        /// The index count.
        /// </summary>
        private const int IndexCount = (1 << IndexBits) + 1;

        /// <summary>
        /// The index alpha count.
        /// </summary>
        private const int IndexAlphaCount = (1 << IndexAlphaBits) + 1;

        /// <summary>
        /// The table length.
        /// </summary>
        private const int TableLength = IndexCount * IndexCount * IndexCount * IndexAlphaCount;

        /// <summary>
        /// The long array pool.
        /// </summary>
        private static readonly ArrayPool<long> LongPool = ArrayPool<long>.Create(TableLength, 25);

        /// <summary>
        /// The double array pool.
        /// </summary>
        private static readonly ArrayPool<double> DoublePool = ArrayPool<double>.Create(TableLength, 5);

        /// <summary>
        /// The byte array pool.
        /// </summary>
        private static readonly ArrayPool<byte> BytePool = ArrayPool<byte>.Create(TableLength, 5);

        /// <summary>
        /// Moment of <c>P(c)</c>.
        /// </summary>
        private readonly long[] vwt;

        /// <summary>
        /// Moment of <c>r*P(c)</c>.
        /// </summary>
        private readonly long[] vmr;

        /// <summary>
        /// Moment of <c>g*P(c)</c>.
        /// </summary>
        private readonly long[] vmg;

        /// <summary>
        /// Moment of <c>b*P(c)</c>.
        /// </summary>
        private readonly long[] vmb;

        /// <summary>
        /// Moment of <c>a*P(c)</c>.
        /// </summary>
        private readonly long[] vma;

        /// <summary>
        /// Moment of <c>c^2*P(c)</c>.
        /// </summary>
        private readonly double[] m2;

        /// <summary>
        /// Color space tag.
        /// </summary>
        private readonly byte[] tag;

        /// <summary>
        /// A buffer for storing pixels
        /// </summary>
        private readonly byte[] rgbaBuffer = new byte[4];

        /// <summary>
        /// Initializes a new instance of the <see cref="WuQuantizer{TColor}"/> class.
        /// </summary>
        public WuQuantizer()
        {
            this.vwt = LongPool.Rent(TableLength);
            this.vmr = LongPool.Rent(TableLength);
            this.vmg = LongPool.Rent(TableLength);
            this.vmb = LongPool.Rent(TableLength);
            this.vma = LongPool.Rent(TableLength);
            this.m2 = DoublePool.Rent(TableLength);
            this.tag = BytePool.Rent(TableLength);
        }

        /// <inheritdoc/>
        public QuantizedImage<TColor> Quantize(ImageBase<TColor> image, int maxColors)
        {
            Guard.NotNull(image, nameof(image));

            int colorCount = maxColors.Clamp(1, 256);

            this.Clear();

            using (PixelAccessor<TColor> imagePixels = image.Lock())
            {
                this.Build3DHistogram(imagePixels);
                this.Get3DMoments();

                Box[] cube;
                this.BuildCube(out cube, ref colorCount);

                return this.GenerateResult(imagePixels, colorCount, cube);
            }
        }

        /// <summary>
        /// Gets an index.
        /// </summary>
        /// <param name="r">The red value.</param>
        /// <param name="g">The green value.</param>
        /// <param name="b">The blue value.</param>
        /// <param name="a">The alpha value.</param>
        /// <returns>The index.</returns>
        private static int GetPaletteIndex(int r, int g, int b, int a)
        {
            return (r << ((IndexBits * 2) + IndexAlphaBits)) + (r << (IndexBits + IndexAlphaBits + 1))
                   + (g << (IndexBits + IndexAlphaBits)) + (r << (IndexBits * 2)) + (r << (IndexBits + 1))
                   + (g << IndexBits) + ((r + g + b) << IndexAlphaBits) + r + g + b + a;
        }

        /// <summary>
        /// Computes sum over a box of any given statistic.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="moment">The moment.</param>
        /// <returns>The result.</returns>
        private static double Volume(Box cube, long[] moment)
        {
            return moment[GetPaletteIndex(cube.R1, cube.G1, cube.B1, cube.A1)]
                   - moment[GetPaletteIndex(cube.R1, cube.G1, cube.B1, cube.A0)]
                   - moment[GetPaletteIndex(cube.R1, cube.G1, cube.B0, cube.A1)]
                   + moment[GetPaletteIndex(cube.R1, cube.G1, cube.B0, cube.A0)]
                   - moment[GetPaletteIndex(cube.R1, cube.G0, cube.B1, cube.A1)]
                   + moment[GetPaletteIndex(cube.R1, cube.G0, cube.B1, cube.A0)]
                   + moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A1)]
                   - moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A0)]
                   - moment[GetPaletteIndex(cube.R0, cube.G1, cube.B1, cube.A1)]
                   + moment[GetPaletteIndex(cube.R0, cube.G1, cube.B1, cube.A0)]
                   + moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A1)]
                   - moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A0)]
                   + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A1)]
                   - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A0)]
                   - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A1)]
                   + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A0)];
        }

        /// <summary>
        /// Computes part of Volume(cube, moment) that doesn't depend on r1, g1, or b1 (depending on direction).
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="moment">The moment.</param>
        /// <returns>The result.</returns>
        private static long Bottom(Box cube, int direction, long[] moment)
        {
            switch (direction)
            {
                // Red
                case 0:
                    return -moment[GetPaletteIndex(cube.R0, cube.G1, cube.B1, cube.A1)]
                           + moment[GetPaletteIndex(cube.R0, cube.G1, cube.B1, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A1)]
                           - moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A1)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A0)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A1)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A0)];

                // Green
                case 1:
                    return -moment[GetPaletteIndex(cube.R1, cube.G0, cube.B1, cube.A1)]
                           + moment[GetPaletteIndex(cube.R1, cube.G0, cube.B1, cube.A0)]
                           + moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A1)]
                           - moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A1)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A0)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A1)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A0)];

                // Blue
                case 2:
                    return -moment[GetPaletteIndex(cube.R1, cube.G1, cube.B0, cube.A1)]
                           + moment[GetPaletteIndex(cube.R1, cube.G1, cube.B0, cube.A0)]
                           + moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A1)]
                           - moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A1)]
                           - moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A0)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A1)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A0)];

                // Alpha
                case 3:
                    return -moment[GetPaletteIndex(cube.R1, cube.G1, cube.B1, cube.A0)]
                           + moment[GetPaletteIndex(cube.R1, cube.G1, cube.B0, cube.A0)]
                           + moment[GetPaletteIndex(cube.R1, cube.G0, cube.B1, cube.A0)]
                           - moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, cube.G1, cube.B1, cube.A0)]
                           - moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A0)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A0)];

                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        /// <summary>
        /// Computes remainder of Volume(cube, moment), substituting position for r1, g1, or b1 (depending on direction).
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="position">The position.</param>
        /// <param name="moment">The moment.</param>
        /// <returns>The result.</returns>
        private static long Top(Box cube, int direction, int position, long[] moment)
        {
            switch (direction)
            {
                // Red
                case 0:
                    return moment[GetPaletteIndex(position, cube.G1, cube.B1, cube.A1)]
                           - moment[GetPaletteIndex(position, cube.G1, cube.B1, cube.A0)]
                           - moment[GetPaletteIndex(position, cube.G1, cube.B0, cube.A1)]
                           + moment[GetPaletteIndex(position, cube.G1, cube.B0, cube.A0)]
                           - moment[GetPaletteIndex(position, cube.G0, cube.B1, cube.A1)]
                           + moment[GetPaletteIndex(position, cube.G0, cube.B1, cube.A0)]
                           + moment[GetPaletteIndex(position, cube.G0, cube.B0, cube.A1)]
                           - moment[GetPaletteIndex(position, cube.G0, cube.B0, cube.A0)];

                // Green
                case 1:
                    return moment[GetPaletteIndex(cube.R1, position, cube.B1, cube.A1)]
                           - moment[GetPaletteIndex(cube.R1, position, cube.B1, cube.A0)]
                           - moment[GetPaletteIndex(cube.R1, position, cube.B0, cube.A1)]
                           + moment[GetPaletteIndex(cube.R1, position, cube.B0, cube.A0)]
                           - moment[GetPaletteIndex(cube.R0, position, cube.B1, cube.A1)]
                           + moment[GetPaletteIndex(cube.R0, position, cube.B1, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, position, cube.B0, cube.A1)]
                           - moment[GetPaletteIndex(cube.R0, position, cube.B0, cube.A0)];

                // Blue
                case 2:
                    return moment[GetPaletteIndex(cube.R1, cube.G1, position, cube.A1)]
                           - moment[GetPaletteIndex(cube.R1, cube.G1, position, cube.A0)]
                           - moment[GetPaletteIndex(cube.R1, cube.G0, position, cube.A1)]
                           + moment[GetPaletteIndex(cube.R1, cube.G0, position, cube.A0)]
                           - moment[GetPaletteIndex(cube.R0, cube.G1, position, cube.A1)]
                           + moment[GetPaletteIndex(cube.R0, cube.G1, position, cube.A0)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, position, cube.A1)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, position, cube.A0)];

                // Alpha
                case 3:
                    return moment[GetPaletteIndex(cube.R1, cube.G1, cube.B1, position)]
                           - moment[GetPaletteIndex(cube.R1, cube.G1, cube.B0, position)]
                           - moment[GetPaletteIndex(cube.R1, cube.G0, cube.B1, position)]
                           + moment[GetPaletteIndex(cube.R1, cube.G0, cube.B0, position)]
                           - moment[GetPaletteIndex(cube.R0, cube.G1, cube.B1, position)]
                           + moment[GetPaletteIndex(cube.R0, cube.G1, cube.B0, position)]
                           + moment[GetPaletteIndex(cube.R0, cube.G0, cube.B1, position)]
                           - moment[GetPaletteIndex(cube.R0, cube.G0, cube.B0, position)];

                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        /// <summary>
        /// Clears the tables.
        /// </summary>
        private void Clear()
        {
            Array.Clear(this.vwt, 0, TableLength);
            Array.Clear(this.vmr, 0, TableLength);
            Array.Clear(this.vmg, 0, TableLength);
            Array.Clear(this.vmb, 0, TableLength);
            Array.Clear(this.vma, 0, TableLength);
            Array.Clear(this.m2, 0, TableLength);
            Array.Clear(this.tag, 0, TableLength);
        }

        /// <summary>
        /// Builds a 3-D color histogram of <c>counts, r/g/b, c^2</c>.
        /// </summary>
        /// <param name="pixels">The pixel accessor.</param>
        private void Build3DHistogram(PixelAccessor<TColor> pixels)
        {
            for (int y = 0; y < pixels.Height; y++)
            {
                for (int x = 0; x < pixels.Width; x++)
                {
                    // Colors are expected in r->g->b->a format
                    pixels[x, y].ToXyzwBytes(this.rgbaBuffer, 0);

                    byte r = this.rgbaBuffer[0];
                    byte g = this.rgbaBuffer[1];
                    byte b = this.rgbaBuffer[2];
                    byte a = this.rgbaBuffer[3];

                    int inr = r >> (8 - IndexBits);
                    int ing = g >> (8 - IndexBits);
                    int inb = b >> (8 - IndexBits);
                    int ina = a >> (8 - IndexAlphaBits);

                    int ind = GetPaletteIndex(inr + 1, ing + 1, inb + 1, ina + 1);

                    this.vwt[ind]++;
                    this.vmr[ind] += r;
                    this.vmg[ind] += g;
                    this.vmb[ind] += b;
                    this.vma[ind] += a;
                    this.m2[ind] += (r * r) + (g * g) + (b * b) + (a * a);
                }
            }
        }

        /// <summary>
        /// Converts the histogram into moments so that we can rapidly calculate the sums of the above quantities over any desired box.
        /// </summary>
        private void Get3DMoments()
        {
            long[] volume = ArrayPool<long>.Shared.Rent(IndexCount * IndexAlphaCount);
            long[] volumeR = ArrayPool<long>.Shared.Rent(IndexCount * IndexAlphaCount);
            long[] volumeG = ArrayPool<long>.Shared.Rent(IndexCount * IndexAlphaCount);
            long[] volumeB = ArrayPool<long>.Shared.Rent(IndexCount * IndexAlphaCount);
            long[] volumeA = ArrayPool<long>.Shared.Rent(IndexCount * IndexAlphaCount);
            double[] volume2 = ArrayPool<double>.Shared.Rent(IndexCount * IndexAlphaCount);

            long[] area = ArrayPool<long>.Shared.Rent(IndexAlphaCount);
            long[] areaR = ArrayPool<long>.Shared.Rent(IndexAlphaCount);
            long[] areaG = ArrayPool<long>.Shared.Rent(IndexAlphaCount);
            long[] areaB = ArrayPool<long>.Shared.Rent(IndexAlphaCount);
            long[] areaA = ArrayPool<long>.Shared.Rent(IndexAlphaCount);
            double[] area2 = ArrayPool<double>.Shared.Rent(IndexAlphaCount);

            try
            {
                for (int r = 1; r < IndexCount; r++)
                {
                    Array.Clear(volume, 0, IndexCount * IndexAlphaCount);
                    Array.Clear(volumeR, 0, IndexCount * IndexAlphaCount);
                    Array.Clear(volumeG, 0, IndexCount * IndexAlphaCount);
                    Array.Clear(volumeB, 0, IndexCount * IndexAlphaCount);
                    Array.Clear(volumeA, 0, IndexCount * IndexAlphaCount);
                    Array.Clear(volume2, 0, IndexCount * IndexAlphaCount);

                    for (int g = 1; g < IndexCount; g++)
                    {
                        Array.Clear(area, 0, IndexAlphaCount);
                        Array.Clear(areaR, 0, IndexAlphaCount);
                        Array.Clear(areaG, 0, IndexAlphaCount);
                        Array.Clear(areaB, 0, IndexAlphaCount);
                        Array.Clear(areaA, 0, IndexAlphaCount);
                        Array.Clear(area2, 0, IndexAlphaCount);

                        for (int b = 1; b < IndexCount; b++)
                        {
                            long line = 0;
                            long lineR = 0;
                            long lineG = 0;
                            long lineB = 0;
                            long lineA = 0;
                            double line2 = 0;

                            for (int a = 1; a < IndexAlphaCount; a++)
                            {
                                int ind1 = GetPaletteIndex(r, g, b, a);

                                line += this.vwt[ind1];
                                lineR += this.vmr[ind1];
                                lineG += this.vmg[ind1];
                                lineB += this.vmb[ind1];
                                lineA += this.vma[ind1];
                                line2 += this.m2[ind1];

                                area[a] += line;
                                areaR[a] += lineR;
                                areaG[a] += lineG;
                                areaB[a] += lineB;
                                areaA[a] += lineA;
                                area2[a] += line2;

                                int inv = (b * IndexAlphaCount) + a;

                                volume[inv] += area[a];
                                volumeR[inv] += areaR[a];
                                volumeG[inv] += areaG[a];
                                volumeB[inv] += areaB[a];
                                volumeA[inv] += areaA[a];
                                volume2[inv] += area2[a];

                                int ind2 = ind1 - GetPaletteIndex(1, 0, 0, 0);

                                this.vwt[ind1] = this.vwt[ind2] + volume[inv];
                                this.vmr[ind1] = this.vmr[ind2] + volumeR[inv];
                                this.vmg[ind1] = this.vmg[ind2] + volumeG[inv];
                                this.vmb[ind1] = this.vmb[ind2] + volumeB[inv];
                                this.vma[ind1] = this.vma[ind2] + volumeA[inv];
                                this.m2[ind1] = this.m2[ind2] + volume2[inv];
                            }
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<long>.Shared.Return(volume);
                ArrayPool<long>.Shared.Return(volumeR);
                ArrayPool<long>.Shared.Return(volumeG);
                ArrayPool<long>.Shared.Return(volumeB);
                ArrayPool<long>.Shared.Return(volumeA);
                ArrayPool<double>.Shared.Return(volume2);

                ArrayPool<long>.Shared.Return(area);
                ArrayPool<long>.Shared.Return(areaR);
                ArrayPool<long>.Shared.Return(areaG);
                ArrayPool<long>.Shared.Return(areaB);
                ArrayPool<long>.Shared.Return(areaA);
                ArrayPool<double>.Shared.Return(area2);
            }
        }

        /// <summary>
        /// Computes the weighted variance of a box cube.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <returns>The <see cref="double"/>.</returns>
        private double Variance(Box cube)
        {
            double dr = Volume(cube, this.vmr);
            double dg = Volume(cube, this.vmg);
            double db = Volume(cube, this.vmb);
            double da = Volume(cube, this.vma);

            double xx =
                this.m2[GetPaletteIndex(cube.R1, cube.G1, cube.B1, cube.A1)]
                - this.m2[GetPaletteIndex(cube.R1, cube.G1, cube.B1, cube.A0)]
                - this.m2[GetPaletteIndex(cube.R1, cube.G1, cube.B0, cube.A1)]
                + this.m2[GetPaletteIndex(cube.R1, cube.G1, cube.B0, cube.A0)]
                - this.m2[GetPaletteIndex(cube.R1, cube.G0, cube.B1, cube.A1)]
                + this.m2[GetPaletteIndex(cube.R1, cube.G0, cube.B1, cube.A0)]
                + this.m2[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A1)]
                - this.m2[GetPaletteIndex(cube.R1, cube.G0, cube.B0, cube.A0)]
                - this.m2[GetPaletteIndex(cube.R0, cube.G1, cube.B1, cube.A1)]
                + this.m2[GetPaletteIndex(cube.R0, cube.G1, cube.B1, cube.A0)]
                + this.m2[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A1)]
                - this.m2[GetPaletteIndex(cube.R0, cube.G1, cube.B0, cube.A0)]
                + this.m2[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A1)]
                - this.m2[GetPaletteIndex(cube.R0, cube.G0, cube.B1, cube.A0)]
                - this.m2[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A1)]
                + this.m2[GetPaletteIndex(cube.R0, cube.G0, cube.B0, cube.A0)];

            return xx - (((dr * dr) + (dg * dg) + (db * db) + (da * da)) / Volume(cube, this.vwt));
        }

        /// <summary>
        /// We want to minimize the sum of the variances of two sub-boxes.
        /// The sum(c^2) terms can be ignored since their sum over both sub-boxes
        /// is the same (the sum for the whole box) no matter where we split.
        /// The remaining terms have a minus sign in the variance formula,
        /// so we drop the minus sign and maximize the sum of the two terms.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="first">The first position.</param>
        /// <param name="last">The last position.</param>
        /// <param name="cut">The cutting point.</param>
        /// <param name="wholeR">The whole red.</param>
        /// <param name="wholeG">The whole green.</param>
        /// <param name="wholeB">The whole blue.</param>
        /// <param name="wholeA">The whole alpha.</param>
        /// <param name="wholeW">The whole weight.</param>
        /// <returns>The <see cref="double"/>.</returns>
        private double Maximize(Box cube, int direction, int first, int last, out int cut, double wholeR, double wholeG, double wholeB, double wholeA, double wholeW)
        {
            long baseR = Bottom(cube, direction, this.vmr);
            long baseG = Bottom(cube, direction, this.vmg);
            long baseB = Bottom(cube, direction, this.vmb);
            long baseA = Bottom(cube, direction, this.vma);
            long baseW = Bottom(cube, direction, this.vwt);

            double max = 0.0;
            cut = -1;

            for (int i = first; i < last; i++)
            {
                double halfR = baseR + Top(cube, direction, i, this.vmr);
                double halfG = baseG + Top(cube, direction, i, this.vmg);
                double halfB = baseB + Top(cube, direction, i, this.vmb);
                double halfA = baseA + Top(cube, direction, i, this.vma);
                double halfW = baseW + Top(cube, direction, i, this.vwt);

                double temp;

                if (Math.Abs(halfW) < Constants.Epsilon)
                {
                    continue;
                }

                temp = ((halfR * halfR) + (halfG * halfG) + (halfB * halfB) + (halfA * halfA)) / halfW;

                halfR = wholeR - halfR;
                halfG = wholeG - halfG;
                halfB = wholeB - halfB;
                halfA = wholeA - halfA;
                halfW = wholeW - halfW;

                if (Math.Abs(halfW) < Constants.Epsilon)
                {
                    continue;
                }

                temp += ((halfR * halfR) + (halfG * halfG) + (halfB * halfB) + (halfA * halfA)) / halfW;

                if (temp > max)
                {
                    max = temp;
                    cut = i;
                }
            }

            return max;
        }

        /// <summary>
        /// Cuts a box.
        /// </summary>
        /// <param name="set1">The first set.</param>
        /// <param name="set2">The second set.</param>
        /// <returns>Returns a value indicating whether the box has been split.</returns>
        private bool Cut(Box set1, Box set2)
        {
            double wholeR = Volume(set1, this.vmr);
            double wholeG = Volume(set1, this.vmg);
            double wholeB = Volume(set1, this.vmb);
            double wholeA = Volume(set1, this.vma);
            double wholeW = Volume(set1, this.vwt);

            int cutr;
            int cutg;
            int cutb;
            int cuta;

            double maxr = this.Maximize(set1, 0, set1.R0 + 1, set1.R1, out cutr, wholeR, wholeG, wholeB, wholeA, wholeW);
            double maxg = this.Maximize(set1, 1, set1.G0 + 1, set1.G1, out cutg, wholeR, wholeG, wholeB, wholeA, wholeW);
            double maxb = this.Maximize(set1, 2, set1.B0 + 1, set1.B1, out cutb, wholeR, wholeG, wholeB, wholeA, wholeW);
            double maxa = this.Maximize(set1, 3, set1.A0 + 1, set1.A1, out cuta, wholeR, wholeG, wholeB, wholeA, wholeW);

            int dir;

            if ((maxr >= maxg) && (maxr >= maxb) && (maxr >= maxa))
            {
                dir = 0;

                if (cutr < 0)
                {
                    return false;
                }
            }
            else if ((maxg >= maxr) && (maxg >= maxb) && (maxg >= maxa))
            {
                dir = 1;
            }
            else if ((maxb >= maxr) && (maxb >= maxg) && (maxb >= maxa))
            {
                dir = 2;
            }
            else
            {
                dir = 3;
            }

            set2.R1 = set1.R1;
            set2.G1 = set1.G1;
            set2.B1 = set1.B1;
            set2.A1 = set1.A1;

            switch (dir)
            {
                // Red
                case 0:
                    set2.R0 = set1.R1 = cutr;
                    set2.G0 = set1.G0;
                    set2.B0 = set1.B0;
                    set2.A0 = set1.A0;
                    break;

                // Green
                case 1:
                    set2.G0 = set1.G1 = cutg;
                    set2.R0 = set1.R0;
                    set2.B0 = set1.B0;
                    set2.A0 = set1.A0;
                    break;

                // Blue
                case 2:
                    set2.B0 = set1.B1 = cutb;
                    set2.R0 = set1.R0;
                    set2.G0 = set1.G0;
                    set2.A0 = set1.A0;
                    break;

                // Alpha
                case 3:
                    set2.A0 = set1.A1 = cuta;
                    set2.R0 = set1.R0;
                    set2.G0 = set1.G0;
                    set2.B0 = set1.B0;
                    break;
            }

            set1.Volume = (set1.R1 - set1.R0) * (set1.G1 - set1.G0) * (set1.B1 - set1.B0) * (set1.A1 - set1.A0);
            set2.Volume = (set2.R1 - set2.R0) * (set2.G1 - set2.G0) * (set2.B1 - set2.B0) * (set2.A1 - set2.A0);

            return true;
        }

        /// <summary>
        /// Marks a color space tag.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="label">A label.</param>
        private void Mark(Box cube, byte label)
        {
            for (int r = cube.R0 + 1; r <= cube.R1; r++)
            {
                for (int g = cube.G0 + 1; g <= cube.G1; g++)
                {
                    for (int b = cube.B0 + 1; b <= cube.B1; b++)
                    {
                        for (int a = cube.A0 + 1; a <= cube.A1; a++)
                        {
                            this.tag[GetPaletteIndex(r, g, b, a)] = label;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds the cube.
        /// </summary>
        /// <param name="cube">The cube.</param>
        /// <param name="colorCount">The color count.</param>
        private void BuildCube(out Box[] cube, ref int colorCount)
        {
            cube = new Box[colorCount];
            double[] vv = new double[colorCount];

            for (int i = 0; i < colorCount; i++)
            {
                cube[i] = new Box();
            }

            cube[0].R0 = cube[0].G0 = cube[0].B0 = cube[0].A0 = 0;
            cube[0].R1 = cube[0].G1 = cube[0].B1 = IndexCount - 1;
            cube[0].A1 = IndexAlphaCount - 1;

            int next = 0;

            for (int i = 1; i < colorCount; i++)
            {
                if (this.Cut(cube[next], cube[i]))
                {
                    vv[next] = cube[next].Volume > 1 ? this.Variance(cube[next]) : 0.0;
                    vv[i] = cube[i].Volume > 1 ? this.Variance(cube[i]) : 0.0;
                }
                else
                {
                    vv[next] = 0.0;
                    i--;
                }

                next = 0;

                double temp = vv[0];
                for (int k = 1; k <= i; k++)
                {
                    if (vv[k] > temp)
                    {
                        temp = vv[k];
                        next = k;
                    }
                }

                if (temp <= 0.0)
                {
                    colorCount = i + 1;
                    break;
                }
            }
        }

        /// <summary>
        /// Generates the quantized result.
        /// </summary>
        /// <param name="imagePixels">The image pixels.</param>
        /// <param name="colorCount">The color count.</param>
        /// <param name="cube">The cube.</param>
        /// <returns>The result.</returns>
        private QuantizedImage<TColor> GenerateResult(PixelAccessor<TColor> imagePixels, int colorCount, Box[] cube)
        {
            TColor[] pallette = new TColor[colorCount];
            byte[] pixels = new byte[imagePixels.Width * imagePixels.Height];
            int width = imagePixels.Width;
            int height = imagePixels.Height;

            for (int k = 0; k < colorCount; k++)
            {
                this.Mark(cube[k], (byte)k);

                double weight = Volume(cube[k], this.vwt);

                if (Math.Abs(weight) > Constants.Epsilon)
                {
                    float r = (float)(Volume(cube[k], this.vmr) / weight);
                    float g = (float)(Volume(cube[k], this.vmg) / weight);
                    float b = (float)(Volume(cube[k], this.vmb) / weight);
                    float a = (float)(Volume(cube[k], this.vma) / weight);

                    TColor color = default(TColor);
                    color.PackFromVector4(new Vector4(r, g, b, a) / 255F);
                    pallette[k] = color;
                }
            }

            Parallel.For(
                0,
                height,
                imagePixels.ParallelOptions,
                y =>
                {
                    byte[] rgba = ArrayPool<byte>.Shared.Rent(4);
                    for (int x = 0; x < width; x++)
                    {
                        // Expected order r->g->b->a
                        imagePixels[x, y].ToXyzwBytes(rgba, 0);

                        int r = rgba[0] >> (8 - IndexBits);
                        int g = rgba[1] >> (8 - IndexBits);
                        int b = rgba[2] >> (8 - IndexBits);
                        int a = rgba[3] >> (8 - IndexAlphaBits);

                        int ind = GetPaletteIndex(r + 1, g + 1, b + 1, a + 1);
                        pixels[(y * width) + x] = this.tag[ind];
                    }

                    ArrayPool<byte>.Shared.Return(rgba);
                });

            // Cleanup
            LongPool.Return(this.vwt);
            LongPool.Return(this.vmr);
            LongPool.Return(this.vmg);
            LongPool.Return(this.vmb);
            LongPool.Return(this.vma);
            DoublePool.Return(this.m2);
            BytePool.Return(this.tag);

            return new QuantizedImage<TColor>(width, height, pallette, pixels);
        }
    }
}