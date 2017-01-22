﻿// <copyright file="ExifValue.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Represent the value of the EXIF profile.
    /// </summary>
    public sealed class ExifValue : IEquatable<ExifValue>
    {
        /// <summary>
        /// The exif value.
        /// </summary>
        private object exifValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExifValue"/> class
        /// by making a copy from another exif value.
        /// </summary>
        /// <param name="other">The other exif value, where the clone should be made from.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="other"/> is null.</exception>
        public ExifValue(ExifValue other)
        {
            Guard.NotNull(other, nameof(other));

            this.DataType = other.DataType;
            this.IsArray = other.IsArray;
            this.Tag = other.Tag;

            if (!other.IsArray)
            {
                this.exifValue = other.exifValue;
            }
            else
            {
                Array array = (Array)other.exifValue;
                this.exifValue = array.Clone();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExifValue"/> class.
        /// </summary>
        /// <param name="tag">The tag.</param>
        /// <param name="dataType">The data type.</param>
        /// <param name="isArray">Whether the value is an array.</param>
        internal ExifValue(ExifTag tag, ExifDataType dataType, bool isArray)
        {
            this.Tag = tag;
            this.DataType = dataType;
            this.IsArray = isArray;

            if (dataType == ExifDataType.Ascii)
            {
                this.IsArray = false;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExifValue"/> class.
        /// </summary>
        /// <param name="tag">The tag.</param>
        /// <param name="dataType">The data type.</param>
        /// <param name="value">The value.</param>
        /// <param name="isArray">Whether the value is an array.</param>
        internal ExifValue(ExifTag tag, ExifDataType dataType, object value, bool isArray)
          : this(tag, dataType, isArray)
        {
            this.exifValue = value;
        }

        /// <summary>
        /// Gets the data type of the exif value.
        /// </summary>
        public ExifDataType DataType
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether the value is an array.
        /// </summary>
        public bool IsArray
        {
            get;
        }

        /// <summary>
        /// Gets the tag of the exif value.
        /// </summary>
        public ExifTag Tag
        {
            get;
        }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        public object Value
        {
            get
            {
                return this.exifValue;
            }

            set
            {
                this.CheckValue(value);
                this.exifValue = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the EXIF value has a value.
        /// </summary>
        internal bool HasValue
        {
            get
            {
                if (this.exifValue == null)
                {
                    return false;
                }

                if (this.DataType == ExifDataType.Ascii)
                {
                    return ((string)this.exifValue).Length > 0;
                }

                return true;
            }
        }

        /// <summary>
        /// Gets the length of the EXIF value
        /// </summary>
        internal int Length
        {
            get
            {
                if (this.exifValue == null)
                {
                    return 4;
                }

                int size = (int)(GetSize(this.DataType) * this.NumberOfComponents);

                return size < 4 ? 4 : size;
            }
        }

        /// <summary>
        /// Gets the number of components.
        /// </summary>
        internal int NumberOfComponents
        {
            get
            {
                if (this.DataType == ExifDataType.Ascii)
                {
                    return Encoding.UTF8.GetBytes((string)this.exifValue).Length;
                }

                if (this.IsArray)
                {
                    return ((Array)this.exifValue).Length;
                }

                return 1;
            }
        }

        /// <summary>
        /// Compares two <see cref="ExifValue"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="ExifValue"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="ExifValue"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(ExifValue left, ExifValue right)
        {
            return ExifValue.Equals(left, right);
        }

        /// <summary>
        /// Compares two <see cref="ExifValue"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="ExifValue"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="ExifValue"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(ExifValue left, ExifValue right)
        {
            return !ExifValue.Equals(left, right);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return this.Equals(obj as ExifValue);
        }

        /// <inheritdoc />
        public bool Equals(ExifValue other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return
              this.Tag == other.Tag &&
              this.DataType == other.DataType &&
              object.Equals(this.exifValue, other.exifValue);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.GetHashCode(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.exifValue == null)
            {
                return null;
            }

            if (this.DataType == ExifDataType.Ascii)
            {
                return (string)this.exifValue;
            }

            if (!this.IsArray)
            {
                return this.ToString(this.exifValue);
            }

            StringBuilder sb = new StringBuilder();
            foreach (object value in (Array)this.exifValue)
            {
                sb.Append(this.ToString(value));
                sb.Append(" ");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Creates a new <see cref="ExifValue"/>
        /// </summary>
        /// <param name="tag">The tag.</param>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The <see cref="ExifValue"/>.
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// Thrown if the tag is not supported.
        /// </exception>
        internal static ExifValue Create(ExifTag tag, object value)
        {
            Guard.IsFalse(tag == ExifTag.Unknown, nameof(tag), "Invalid Tag");

            ExifValue exifValue;
            Type type = value?.GetType();
            if (type != null && type.IsArray)
            {
                type = type.GetElementType();
            }

            switch (tag)
            {
                case ExifTag.ImageDescription:
                case ExifTag.Make:
                case ExifTag.Model:
                case ExifTag.Software:
                case ExifTag.DateTime:
                case ExifTag.Artist:
                case ExifTag.HostComputer:
                case ExifTag.Copyright:
                case ExifTag.DocumentName:
                case ExifTag.PageName:
                case ExifTag.InkNames:
                case ExifTag.TargetPrinter:
                case ExifTag.ImageID:
                case ExifTag.MDLabName:
                case ExifTag.MDSampleInfo:
                case ExifTag.MDPrepDate:
                case ExifTag.MDPrepTime:
                case ExifTag.MDFileUnits:
                case ExifTag.SEMInfo:
                case ExifTag.SpectralSensitivity:
                case ExifTag.DateTimeOriginal:
                case ExifTag.DateTimeDigitized:
                case ExifTag.SubsecTime:
                case ExifTag.SubsecTimeOriginal:
                case ExifTag.SubsecTimeDigitized:
                case ExifTag.FaxSubaddress:
                case ExifTag.OffsetTime:
                case ExifTag.OffsetTimeOriginal:
                case ExifTag.OffsetTimeDigitized:
                case ExifTag.SecurityClassification:
                case ExifTag.ImageHistory:
                case ExifTag.ImageUniqueID:
                case ExifTag.OwnerName:
                case ExifTag.SerialNumber:
                case ExifTag.LensMake:
                case ExifTag.LensModel:
                case ExifTag.LensSerialNumber:
                case ExifTag.GDALMetadata:
                case ExifTag.GDALNoData:
                case ExifTag.GPSLatitudeRef:
                case ExifTag.GPSLongitudeRef:
                case ExifTag.GPSSatellites:
                case ExifTag.GPSStatus:
                case ExifTag.GPSMeasureMode:
                case ExifTag.GPSSpeedRef:
                case ExifTag.GPSTrackRef:
                case ExifTag.GPSImgDirectionRef:
                case ExifTag.GPSMapDatum:
                case ExifTag.GPSDestLatitudeRef:
                case ExifTag.GPSDestLongitudeRef:
                case ExifTag.GPSDestBearingRef:
                case ExifTag.GPSDestDistanceRef:
                case ExifTag.GPSDateStamp:
                    exifValue = new ExifValue(tag, ExifDataType.Ascii, true);
                    break;

                case ExifTag.ClipPath:
                case ExifTag.VersionYear:
                case ExifTag.XMP:
                case ExifTag.CFAPattern2:
                case ExifTag.TIFFEPStandardID:
                case ExifTag.XPTitle:
                case ExifTag.XPComment:
                case ExifTag.XPAuthor:
                case ExifTag.XPKeywords:
                case ExifTag.XPSubject:
                case ExifTag.GPSVersionID:
                    exifValue = new ExifValue(tag, ExifDataType.Byte, true);
                    break;
                case ExifTag.FaxProfile:
                case ExifTag.ModeNumber:
                case ExifTag.GPSAltitudeRef:
                    exifValue = new ExifValue(tag, ExifDataType.Byte, false);
                    break;

                case ExifTag.FreeOffsets:
                case ExifTag.FreeByteCounts:
                case ExifTag.ColorResponseUnit:
                case ExifTag.TileOffsets:
                case ExifTag.SMinSampleValue:
                case ExifTag.SMaxSampleValue:
                case ExifTag.JPEGQTables:
                case ExifTag.JPEGDCTables:
                case ExifTag.JPEGACTables:
                case ExifTag.StripRowCounts:
                case ExifTag.IntergraphRegisters:
                case ExifTag.TimeZoneOffset:
                    exifValue = new ExifValue(tag, ExifDataType.Long, true);
                    break;
                case ExifTag.SubfileType:
                case ExifTag.SubIFDOffset:
                case ExifTag.GPSIFDOffset:
                case ExifTag.T4Options:
                case ExifTag.T6Options:
                case ExifTag.XClipPathUnits:
                case ExifTag.YClipPathUnits:
                case ExifTag.ProfileType:
                case ExifTag.CodingMethods:
                case ExifTag.T82ptions:
                case ExifTag.JPEGInterchangeFormat:
                case ExifTag.JPEGInterchangeFormatLength:
                case ExifTag.MDFileTag:
                case ExifTag.StandardOutputSensitivity:
                case ExifTag.RecommendedExposureIndex:
                case ExifTag.ISOSpeed:
                case ExifTag.ISOSpeedLatitudeyyy:
                case ExifTag.ISOSpeedLatitudezzz:
                case ExifTag.FaxRecvParams:
                case ExifTag.FaxRecvTime:
                case ExifTag.ImageNumber:
                    exifValue = new ExifValue(tag, ExifDataType.Long, false);
                    break;

                case ExifTag.WhitePoint:
                case ExifTag.PrimaryChromaticities:
                case ExifTag.YCbCrCoefficients:
                case ExifTag.ReferenceBlackWhite:
                case ExifTag.PixelScale:
                case ExifTag.IntergraphMatrix:
                case ExifTag.ModelTiePoint:
                case ExifTag.ModelTransform:
                case ExifTag.GPSLatitude:
                case ExifTag.GPSLongitude:
                case ExifTag.GPSTimestamp:
                case ExifTag.GPSDestLatitude:
                case ExifTag.GPSDestLongitude:
                    exifValue = new ExifValue(tag, ExifDataType.Rational, true);
                    break;
                case ExifTag.XPosition:
                case ExifTag.YPosition:
                case ExifTag.XResolution:
                case ExifTag.YResolution:
                case ExifTag.BatteryLevel:
                case ExifTag.ExposureTime:
                case ExifTag.FNumber:
                case ExifTag.MDScalePixel:
                case ExifTag.CompressedBitsPerPixel:
                case ExifTag.ApertureValue:
                case ExifTag.MaxApertureValue:
                case ExifTag.SubjectDistance:
                case ExifTag.FocalLength:
                case ExifTag.FlashEnergy2:
                case ExifTag.FocalPlaneXResolution2:
                case ExifTag.FocalPlaneYResolution2:
                case ExifTag.ExposureIndex2:
                case ExifTag.Humidity:
                case ExifTag.Pressure:
                case ExifTag.Acceleration:
                case ExifTag.FlashEnergy:
                case ExifTag.FocalPlaneXResolution:
                case ExifTag.FocalPlaneYResolution:
                case ExifTag.ExposureIndex:
                case ExifTag.DigitalZoomRatio:
                case ExifTag.LensInfo:
                case ExifTag.GPSAltitude:
                case ExifTag.GPSDOP:
                case ExifTag.GPSSpeed:
                case ExifTag.GPSTrack:
                case ExifTag.GPSImgDirection:
                case ExifTag.GPSDestBearing:
                case ExifTag.GPSDestDistance:
                    exifValue = new ExifValue(tag, ExifDataType.Rational, false);
                    break;

                case ExifTag.BitsPerSample:
                case ExifTag.MinSampleValue:
                case ExifTag.MaxSampleValue:
                case ExifTag.GrayResponseCurve:
                case ExifTag.ColorMap:
                case ExifTag.ExtraSamples:
                case ExifTag.PageNumber:
                case ExifTag.TransferFunction:
                case ExifTag.Predictor:
                case ExifTag.HalftoneHints:
                case ExifTag.SampleFormat:
                case ExifTag.TransferRange:
                case ExifTag.DefaultImageColor:
                case ExifTag.JPEGLosslessPredictors:
                case ExifTag.JPEGPointTransforms:
                case ExifTag.YCbCrSubsampling:
                case ExifTag.CFARepeatPatternDim:
                case ExifTag.IntergraphPacketData:
                case ExifTag.ISOSpeedRatings:
                case ExifTag.SubjectArea:
                case ExifTag.SubjectLocation:
                    exifValue = new ExifValue(tag, ExifDataType.Short, true);
                    break;
                case ExifTag.OldSubfileType:
                case ExifTag.Compression:
                case ExifTag.PhotometricInterpretation:
                case ExifTag.Thresholding:
                case ExifTag.CellWidth:
                case ExifTag.CellLength:
                case ExifTag.FillOrder:
                case ExifTag.Orientation:
                case ExifTag.SamplesPerPixel:
                case ExifTag.PlanarConfiguration:
                case ExifTag.GrayResponseUnit:
                case ExifTag.ResolutionUnit:
                case ExifTag.CleanFaxData:
                case ExifTag.InkSet:
                case ExifTag.NumberOfInks:
                case ExifTag.DotRange:
                case ExifTag.Indexed:
                case ExifTag.OPIProxy:
                case ExifTag.JPEGProc:
                case ExifTag.JPEGRestartInterval:
                case ExifTag.YCbCrPositioning:
                case ExifTag.Rating:
                case ExifTag.RatingPercent:
                case ExifTag.ExposureProgram:
                case ExifTag.Interlace:
                case ExifTag.SelfTimerMode:
                case ExifTag.SensitivityType:
                case ExifTag.MeteringMode:
                case ExifTag.LightSource:
                case ExifTag.FocalPlaneResolutionUnit2:
                case ExifTag.SensingMethod2:
                case ExifTag.Flash:
                case ExifTag.ColorSpace:
                case ExifTag.FocalPlaneResolutionUnit:
                case ExifTag.SensingMethod:
                case ExifTag.CustomRendered:
                case ExifTag.ExposureMode:
                case ExifTag.WhiteBalance:
                case ExifTag.FocalLengthIn35mmFilm:
                case ExifTag.SceneCaptureType:
                case ExifTag.GainControl:
                case ExifTag.Contrast:
                case ExifTag.Saturation:
                case ExifTag.Sharpness:
                case ExifTag.SubjectDistanceRange:
                case ExifTag.GPSDifferential:
                    exifValue = new ExifValue(tag, ExifDataType.Short, false);
                    break;

                case ExifTag.Decode:
                    exifValue = new ExifValue(tag, ExifDataType.SignedRational, true);
                    break;
                case ExifTag.ShutterSpeedValue:
                case ExifTag.BrightnessValue:
                case ExifTag.ExposureBiasValue:
                case ExifTag.AmbientTemperature:
                case ExifTag.WaterDepth:
                case ExifTag.CameraElevationAngle:
                    exifValue = new ExifValue(tag, ExifDataType.SignedRational, false);
                    break;

                case ExifTag.JPEGTables:
                case ExifTag.OECF:
                case ExifTag.ExifVersion:
                case ExifTag.ComponentsConfiguration:
                case ExifTag.MakerNote:
                case ExifTag.UserComment:
                case ExifTag.FlashpixVersion:
                case ExifTag.SpatialFrequencyResponse:
                case ExifTag.SpatialFrequencyResponse2:
                case ExifTag.Noise:
                case ExifTag.CFAPattern:
                case ExifTag.DeviceSettingDescription:
                case ExifTag.ImageSourceData:
                case ExifTag.GPSProcessingMethod:
                case ExifTag.GPSAreaInformation:
                    exifValue = new ExifValue(tag, ExifDataType.Undefined, true);
                    break;
                case ExifTag.FileSource:
                case ExifTag.SceneType:
                    exifValue = new ExifValue(tag, ExifDataType.Undefined, false);
                    break;

                case ExifTag.StripOffsets:
                case ExifTag.TileByteCounts:
                case ExifTag.ImageLayer:
                    exifValue = CreateNumber(tag, type, true);
                    break;
                case ExifTag.ImageWidth:
                case ExifTag.ImageLength:
                case ExifTag.TileWidth:
                case ExifTag.TileLength:
                case ExifTag.BadFaxLines:
                case ExifTag.ConsecutiveBadFaxLines:
                case ExifTag.PixelXDimension:
                case ExifTag.PixelYDimension:
                    exifValue = CreateNumber(tag, type, false);
                    break;

                default:
                    throw new NotSupportedException();
            }

            exifValue.Value = value;
            return exifValue;
        }

        /// <summary>
        /// Gets the size in bytes of the given data type.
        /// </summary>
        /// <param name="dataType">The data type.</param>
        /// <returns>
        /// The <see cref="uint"/>.
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// Thrown if the type is unsupported.
        /// </exception>
        internal static uint GetSize(ExifDataType dataType)
        {
            switch (dataType)
            {
                case ExifDataType.Ascii:
                case ExifDataType.Byte:
                case ExifDataType.SignedByte:
                case ExifDataType.Undefined:
                    return 1;
                case ExifDataType.Short:
                case ExifDataType.SignedShort:
                    return 2;
                case ExifDataType.Long:
                case ExifDataType.SignedLong:
                case ExifDataType.SingleFloat:
                    return 4;
                case ExifDataType.DoubleFloat:
                case ExifDataType.Rational:
                case ExifDataType.SignedRational:
                    return 8;
                default:
                    throw new NotSupportedException(dataType.ToString());
            }
        }

        /// <summary>
        /// Returns an EXIF value with a numeric type for the given tag.
        /// </summary>
        /// <param name="tag">The tag.</param>
        /// <param name="type">The numeric type.</param>
        /// <param name="isArray">Whether the value is an array.</param>
        /// <returns>
        /// The <see cref="ExifValue"/>.
        /// </returns>
        private static ExifValue CreateNumber(ExifTag tag, Type type, bool isArray)
        {
            if (type == null || type == typeof(ushort))
            {
                return new ExifValue(tag, ExifDataType.Short, isArray);
            }

            if (type == typeof(short))
            {
                return new ExifValue(tag, ExifDataType.SignedShort, isArray);
            }

            if (type == typeof(uint))
            {
                return new ExifValue(tag, ExifDataType.Long, isArray);
            }

            return new ExifValue(tag, ExifDataType.SignedLong, isArray);
        }

        /// <summary>
        /// Checks the value type of the given object.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <exception cref="NotSupportedException">
        /// Thrown if the object type is not supported.
        /// </exception>
        private void CheckValue(object value)
        {
            if (value == null)
            {
                return;
            }

            Type type = value.GetType();

            if (this.DataType == ExifDataType.Ascii)
            {
                Guard.IsTrue(type == typeof(string), nameof(value), "Value should be a string.");
                return;
            }

            if (type.IsArray)
            {
                Guard.IsTrue(this.IsArray, nameof(value), "Value should not be an array.");
                type = type.GetElementType();
            }
            else
            {
                Guard.IsFalse(this.IsArray, nameof(value), "Value should not be an array.");
            }

            switch (this.DataType)
            {
                case ExifDataType.Byte:
                    Guard.IsTrue(type == typeof(byte), nameof(value), $"Value should be a byte{(this.IsArray ? " array." : ".")}");
                    break;
                case ExifDataType.DoubleFloat:
                    Guard.IsTrue(type == typeof(double), nameof(value), $"Value should be a double{(this.IsArray ? " array." : ".")}");
                    break;
                case ExifDataType.Long:
                    Guard.IsTrue(type == typeof(uint), nameof(value), $"Value should be an unsigned int{(this.IsArray ? " array." : ".")}");
                    break;
                case ExifDataType.Rational:
                    Guard.IsTrue(type == typeof(Rational), nameof(value), $"Value should be a Rational{(this.IsArray ? " array." : ".")}");
                    break;
                case ExifDataType.Short:
                    Guard.IsTrue(type == typeof(ushort), nameof(value), $"Value should be an unsigned short{(this.IsArray ? " array." : ".")}");
                    break;
                case ExifDataType.SignedByte:
                    Guard.IsTrue(type == typeof(sbyte), nameof(value), $"Value should be a signed byte{(this.IsArray ? " array." : ".")}");
                    break;
                case ExifDataType.SignedLong:
                    Guard.IsTrue(type == typeof(int), nameof(value), $"Value should be an int{(this.IsArray ? " array." : ".")}");
                    break;
                case ExifDataType.SignedRational:
                    Guard.IsTrue(type == typeof(SignedRational), nameof(value), $"Value should be a SignedRational{(this.IsArray ? " array." : ".")}");
                    break;
                case ExifDataType.SignedShort:
                    Guard.IsTrue(type == typeof(short), nameof(value), $"Value should be a short{(this.IsArray ? " array." : ".")}");
                    break;
                case ExifDataType.SingleFloat:
                    Guard.IsTrue(type == typeof(float), nameof(value), $"Value should be a float{(this.IsArray ? " array." : ".")}");
                    break;
                case ExifDataType.Undefined:
                    Guard.IsTrue(type == typeof(byte), nameof(value), "Value should be a byte array.");
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Converts the object value of this instance to its equivalent string representation
        /// </summary>
        /// <param name="value">The value</param>
        /// <returns>The <see cref="string"/></returns>
        private string ToString(object value)
        {
            string description = ExifTagDescriptionAttribute.GetDescription(this.Tag, value);
            if (description != null)
            {
                return description;
            }

            switch (this.DataType)
            {
                case ExifDataType.Ascii:
                    return (string)value;
                case ExifDataType.Byte:
                    return ((byte)value).ToString("X2", CultureInfo.InvariantCulture);
                case ExifDataType.DoubleFloat:
                    return ((double)value).ToString(CultureInfo.InvariantCulture);
                case ExifDataType.Long:
                    return ((uint)value).ToString(CultureInfo.InvariantCulture);
                case ExifDataType.Rational:
                    return ((Rational)value).ToString(CultureInfo.InvariantCulture);
                case ExifDataType.Short:
                    return ((ushort)value).ToString(CultureInfo.InvariantCulture);
                case ExifDataType.SignedByte:
                    return ((sbyte)value).ToString("X2", CultureInfo.InvariantCulture);
                case ExifDataType.SignedLong:
                    return ((int)value).ToString(CultureInfo.InvariantCulture);
                case ExifDataType.SignedRational:
                    return ((Rational)value).ToString(CultureInfo.InvariantCulture);
                case ExifDataType.SignedShort:
                    return ((short)value).ToString(CultureInfo.InvariantCulture);
                case ExifDataType.SingleFloat:
                    return ((float)value).ToString(CultureInfo.InvariantCulture);
                case ExifDataType.Undefined:
                    return ((byte)value).ToString("X2", CultureInfo.InvariantCulture);
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="exif">
        /// The instance of <see cref="ExifValue"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(ExifValue exif)
        {
            int hashCode = exif.Tag.GetHashCode() ^ exif.DataType.GetHashCode();
            return hashCode ^ exif.exifValue?.GetHashCode() ?? hashCode;
        }
    }
}