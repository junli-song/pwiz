/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.IO;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results.RemoteApi.Unifi;

namespace pwiz.Skyline.Model.Results
{
    public abstract class MsDataFileUri : Immutable, IComparable
    {
        public abstract FilePathAndSampleId GetLocation(); // Gets full path potentially with WIFF sample info, but no centroiding or combine_ims hint decorations
        public abstract string GetFilePath(); // Gets full path without any WIFF sample info, and no centroiding or combine_ims hint decorations
        public abstract string GetFileName(); // Gets file name without path info, or any WIFF sample info, and no centroiding or combine_ims hint decorations
        public abstract string GetFileNameWithoutExtension();
        public abstract override string ToString();
        public abstract DateTime GetFileLastWriteTime();
        public abstract string GetSampleName();
        public abstract int GetSampleIndex();
        public abstract string GetExtension();
        public abstract MsDataFileUri ToLower();
        public abstract MsDataFileUri Normalize();
        public abstract LockMassParameters GetLockMassParameters();
        public abstract bool IsWatersLockmassCorrectionCandidate();
        /// <summary>
        /// Returns a copy of itself with updated lockmass parameters
        /// </summary>
        public abstract MsDataFileUri ChangeLockMassParameters(LockMassParameters lockMassParameters);
        public abstract bool GetCentroidMs1();
        public abstract bool GetCentroidMs2();
        public abstract bool GetCombineIonMobilitySpectra();
        /// <summary>
        /// Returns a copy of itself with updated centroiding parameters
        /// </summary>
        public abstract MsDataFileUri ChangeCentroiding(bool centroidMS1, bool centroidMS2);

        public abstract MsDataFileUri ChangeCombineIonMobilitySpectra(bool combineIonMobilitySpectra);

        public MsDataFileUri ChangeParameters(SrmDocument doc, LockMassParameters lockMassParameters)
        {
            return doc.Settings.TransitionSettings.FullScan.ApplySettings(ChangeLockMassParameters(lockMassParameters));
        }

        public string GetSampleOrFileName()
        {
            return GetSampleName() ?? GetFileNameWithoutExtension();
        }

        public static MsDataFileUri Parse(string url)
        {
            if (url.StartsWith(UnifiUrl.UrlPrefix))
            {
                return new UnifiUrl(url);
            }
            return new MsDataFilePath(SampleHelp.GetPathFilePart(url), 
                SampleHelp.GetPathSampleNamePart(url), 
                SampleHelp.GetPathSampleIndexPart(url),
                SampleHelp.GetLockmassParameters(url),
                SampleHelp.GetCentroidMs1(url),
                SampleHelp.GetCentroidMs2(url),
                SampleHelp.GetCombineIonMobilitySpectra(url)); 
        }

        public abstract MsDataFileImpl OpenMsDataFile(bool simAsSpectra, int preferOnlyMsLevel, IEnumerable<MsDataFileImpl.PrecursorMzAndIonMobilityWindow> precursorMzAndIonMobilityWindows, bool ignoreZeroIntensityPoints);
        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }
            var msDataFileUri = (MsDataFileUri) obj;
            return string.CompareOrdinal(ToString(), msDataFileUri.ToString());
        }
    }

    public class FilePathAndSampleId : IEquatable<FilePathAndSampleId>
    {

        public FilePathAndSampleId(string filePath, string sampleName = null, int sampleIndex = -1)
        {
            FilePath = filePath;
            SampleName = sampleName;
            SampleIndex = sampleIndex;
        }

        public FilePathAndSampleId(FilePathAndSampleId other)
        {
            FilePath = other.FilePath;
            SampleName = other.SampleName;
            SampleIndex = other.SampleIndex;
        }

        public string FilePath { get; private set; }
        public string SampleName { get; private set; }
        public int SampleIndex { get; private set; }

        public FilePathAndSampleId SetFilePath(string pathToCheck)
        {
            return Equals(FilePath, pathToCheck) ? this : new FilePathAndSampleId(pathToCheck, SampleName, SampleIndex);
        }
        
        public override string ToString()
        {
            return SampleHelp.EncodePath(FilePath, SampleName, SampleIndex,null, false, false, false);
        }

        public string GetFileNameWithoutExtension()
        {
            return Path.GetFileNameWithoutExtension(FilePath);
        }
        public string GetSampleOrFileName()
        {
            return string.IsNullOrEmpty(SampleName) ? GetFileNameWithoutExtension() : SampleName;
        }

        public string GetSampleName()
        {
            return SampleName;
        }
        
        public string GetFilePath()
        {
            return FilePath;
        }

        public string GetFileName()
        {
            return Path.GetFileName(FilePath);
        }

        public string GetExtension()
        {
            return Path.GetExtension(FilePath);
        }


        public bool Equals(FilePathAndSampleId other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return FilePath == other.FilePath &&
                   SampleName == other.SampleName &&
                   SampleIndex == other.SampleIndex;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((FilePathAndSampleId) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = FilePath != null ? FilePath.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (SampleName != null ? SampleName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ SampleIndex;
                return hashCode;
            }
        }

        public static bool operator ==(FilePathAndSampleId left, FilePathAndSampleId right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(FilePathAndSampleId left, FilePathAndSampleId right)
        {
            return !Equals(left, right);
        }
    }


    public class MsDataFilePath : MsDataFileUri
    {
        public static readonly MsDataFilePath EMPTY = new MsDataFilePath(string.Empty);
        public MsDataFilePath(string filePath, LockMassParameters lockMassParameters = null, 
            bool centroidMs1=false, bool centroidMs2=false, bool combineIonMobilitySpectra = false)
            : this(new FilePathAndSampleId(filePath), lockMassParameters, centroidMs1, centroidMs2, combineIonMobilitySpectra)
        {
        }
        public MsDataFilePath(string filePath, string sampleName, int sampleIndex, LockMassParameters lockMassParameters = null,
            bool centroidMs1 = false, bool centroidMs2 = false, bool combineIonMobilitySpectra = false)
            : this(new FilePathAndSampleId(filePath, sampleName, sampleIndex), lockMassParameters, centroidMs1, centroidMs2, combineIonMobilitySpectra)
        {
        }
        public MsDataFilePath(FilePathAndSampleId filePathAndSampleId, LockMassParameters lockMassParameters = null,
            bool centroidMs1 = false, bool centroidMs2 = false, bool combineIonMobilitySpectra = false)
        {
            FilePathAndSampleId = filePathAndSampleId;
            LockMassParameters = lockMassParameters ?? LockMassParameters.EMPTY;
            CentroidMs1 = centroidMs1;
            CentroidMs2 = centroidMs2;
            CombineIonMobilitySpectra = combineIonMobilitySpectra;
        }

        protected MsDataFilePath(MsDataFilePath msDataFilePath)
        {
            FilePathAndSampleId = msDataFilePath.FilePathAndSampleId;
            LockMassParameters = msDataFilePath.LockMassParameters;
            CentroidMs1 = msDataFilePath.CentroidMs1;
            CentroidMs2 = msDataFilePath.CentroidMs2;
            CombineIonMobilitySpectra = msDataFilePath.CombineIonMobilitySpectra;
        }

        public FilePathAndSampleId FilePathAndSampleId { get; private set; }

        public string FilePath { get { return FilePathAndSampleId.FilePath; } }

        public MsDataFilePath SetFilePath(string filePath)
        {
            if (FilePathAndSampleId.FilePath.Equals(filePath))
                return this;
            return new MsDataFilePath(this){FilePathAndSampleId = new FilePathAndSampleId(filePath, FilePathAndSampleId.SampleName, FilePathAndSampleId.SampleIndex)};
        }

        public MsDataFilePath ChangeFilePathAndSampleId(FilePathAndSampleId id)
        {
            return Equals(FilePathAndSampleId, id)
                ? this
                : new MsDataFilePath(id, LockMassParameters, CentroidMs1, CentroidMs2, CombineIonMobilitySpectra);
        }

        public string SampleName { get {return FilePathAndSampleId.SampleName;} }
        public int SampleIndex { get { return FilePathAndSampleId.SampleIndex;} }
        public LockMassParameters LockMassParameters { get; private set; }
        public bool CentroidMs1 { get; private set; }
        public bool CentroidMs2 { get; private set; }
        public bool CombineIonMobilitySpectra { get; private set; } // When true, ask for IMS data in 3-array format

        public override FilePathAndSampleId GetLocation()
        {
            return FilePathAndSampleId;
        }

        public override string GetFilePath()
        {
            return FilePath;
        }

        public override string GetFileNameWithoutExtension()
        {
            return FilePathAndSampleId.GetFileNameWithoutExtension();
        }

        public override string GetExtension()
        {
            return FilePathAndSampleId.GetExtension();
        }

        public override string GetFileName()
        {
            return FilePathAndSampleId.GetFileName();
        }

        public override int GetSampleIndex()
        {
            return SampleIndex;
        }

        public override bool IsWatersLockmassCorrectionCandidate()
        {
            string filePath = GetFilePath();
            // Has to be a Waters .raw file, not just an mzML translation of one
            if (String.IsNullOrEmpty(filePath))
                return false; // Not even a file
            if (!GetFilePath().ToLowerInvariant().EndsWith(@".raw"))
                return false; // Return without even opening the file
            if (!Directory.Exists(filePath))
                return false; // Thermo .raw is a file, Waters .raw is actually a directory
            try
            {
                using (var f = new MsDataFileImpl(filePath))
                    return f.IsWatersLockmassCorrectionCandidate;
            }
            catch (Exception)
            {
                return false; // whatever that was, it wasn't a Waters lockmass file
            }
        }

        public override LockMassParameters GetLockMassParameters()
        {
            return LockMassParameters;
        }

        public override MsDataFileUri ChangeLockMassParameters(LockMassParameters lockMassParameters)
        {
            return new MsDataFilePath(FilePathAndSampleId, lockMassParameters, CentroidMs1, CentroidMs2);
        }

        public override bool GetCentroidMs1()
        {
            return CentroidMs1;
        }

        public override bool GetCentroidMs2()
        {
            return CentroidMs2;
        }

        public override MsDataFileUri ChangeCentroiding(bool centroidMS1, bool centroidMS2)
        {
            return new MsDataFilePath(FilePathAndSampleId, LockMassParameters, centroidMS1, centroidMS2);
        }

        public override bool GetCombineIonMobilitySpectra()
        {
            return CombineIonMobilitySpectra;
        }

        public override MsDataFileUri ChangeCombineIonMobilitySpectra(bool combineIonMobilitySpectra)
        {
            return Equals(combineIonMobilitySpectra, CombineIonMobilitySpectra) ? this : new MsDataFilePath(FilePathAndSampleId, LockMassParameters, CentroidMs1, CentroidMs2, combineIonMobilitySpectra);
        }

        public override string ToString()
        {
            return SampleHelp.EncodePath(FilePath, SampleName, SampleIndex, LockMassParameters, CentroidMs1, CentroidMs2, CombineIonMobilitySpectra);
        }

        public override DateTime GetFileLastWriteTime()
        {
            return File.GetLastWriteTime(FilePath);
        }

        public override string GetSampleName()
        {
            return SampleName;
        }

        public override MsDataFileUri ToLower()
        {
            return SetFilePath(FilePath.ToLower());
        }

        public override MsDataFileUri Normalize()
        {
            return SetFilePath(Path.GetFullPath(FilePath));
        }

        protected bool Equals(MsDataFilePath other)
        {
            if (!Equals(FilePathAndSampleId, other.FilePathAndSampleId))
                return false; // For ease of debugging
            if (!(CentroidMs1 == other.CentroidMs1 &&
                  CentroidMs2 == other.CentroidMs2 &&
                  CombineIonMobilitySpectra == other.CombineIonMobilitySpectra &&
                  LockMassParameters.Equals(other.LockMassParameters)))
            {
                if (CombineIonMobilitySpectra != other.CombineIonMobilitySpectra) // TODO(bspratt) remove this diagnostic
                {
                    var stack = Environment.StackTrace;
                    if (!stack.Contains(@"CalcCachedFlags") &&
                        !stack.Contains(@"ChromatogramSet.ChangeMSDataFilePaths") &&
                        !stack.Contains(@"ChromFileInfo.Equals") &&
                        !stack.Contains(@"MeasuredResults.RequiresCacheUpdate"))
                    {
                        Console.WriteLine();
                        //Console.WriteLine(@"CombineIonMobilitySpectra diff {0}", stack);
                    }
                }

                return false; // For ease of debugging
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType())
                return false;
            return Equals((MsDataFilePath) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = FilePathAndSampleId.GetHashCode();
                hashCode = (hashCode*397) ^ (LockMassParameters != null ? LockMassParameters.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (CentroidMs1 ? 1 : 0);
                hashCode = (hashCode*397) ^ (CentroidMs2 ? 1 : 0);
                hashCode = (hashCode*397) ^ (CombineIonMobilitySpectra ? 1 : 0);
                return hashCode;
            }
        }

        public override MsDataFileImpl OpenMsDataFile(bool simAsSpectra, int preferOnlyMsLevel, IEnumerable<MsDataFileImpl.PrecursorMzAndIonMobilityWindow> precursorMzAndIonMobilityWindows, bool ignoreZeroIntensityPoints)
        {
            var inFile = new MsDataFileImpl(FilePath, Math.Max(SampleIndex, 0), LockMassParameters, simAsSpectra,
                requireVendorCentroidedMS1: CentroidMs1, requireVendorCentroidedMS2: CentroidMs2,
                ignoreZeroIntensityPoints: ignoreZeroIntensityPoints, preferOnlyMsLevel: preferOnlyMsLevel,
                combineIonMobilitySpectra: CombineIonMobilitySpectra,
                precursorMzAndIonMobilityWindows:precursorMzAndIonMobilityWindows);

            if (CombineIonMobilitySpectra && !(inFile.HasIonMobilitySpectra && inFile.HasCombinedIonMobilitySpectra)) // Did we get the processing we asked for?
            {
                CombineIonMobilitySpectra = false; 
            }

            return inFile;
        }
    }
}
