using System;
using System.IO;
using openDicom.Registry;
using openDicom.File;
using openDicom.DataStructure.DataSet;
using openDicom.DataStructure;
using System.Collections.Generic;
using openDicom.Image;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Collections;
using System.Data;

namespace UnityVolumeRendering
{
    /// <summary>
    /// DICOM importer.
    /// Reads a 3D DICOM dataset from a list of DICOM files.
    /// </summary>
    public class DICOMImporter
    {
        public class Vector3
        {
            public Vector3(float x, float y, float z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }

            public float x;
            public float y;
            public float z;

            public Vector3 Subtract(Vector3 v)
            {
                return new Vector3(x - v.x, y - v.y, z - v.z);
            }

            public Vector3 Normalize()
            {
                float d = (float) Math.Sqrt(x * x + y * y + z * z);
                return new Vector3(x / d, y / d, z / d);
            }

            public float DotProduct(Vector3 v)
            {
                return x*v.x + y*v.y + z*v.z;
            }
        }
        public class DICOMSliceFile : IImageSequenceFile
        {
            public AcrNemaFile file;
            public string filePath;
            public float location = 0;
            public Vector3 position = new Vector3(0.0f , 0.0f, 0.0f);
            public float intercept = 0.0f;
            public float slope = 1.0f;
            public float pixelSpacing = 0.0f;
            public string seriesUID = "";
            public bool missingLocation = false;

            public string GetFilePath()
            {
                return filePath;
            }
        }

        public class DICOMSeries : IImageSequenceSeries
        {
            public List<DICOMSliceFile> dicomFiles = new List<DICOMSliceFile>();

            public IEnumerable<IImageSequenceFile> GetFiles()
            {
                return dicomFiles;
            }
        }

        private int iFallbackLoc = 0;

        public IEnumerable<IImageSequenceSeries> LoadSeries(IEnumerable<string> fileCandidates)
        {
            DataElementDictionary dataElementDictionary = new DataElementDictionary();
            UidDictionary uidDictionary = new UidDictionary();

            // Split parsed DICOM files into series (by DICOM series UID)
            Dictionary<string, DICOMSeries> seriesByUID = new Dictionary<string, DICOMSeries>();

            // Load .dic files
            var data = File.ReadAllBytes("c:\\dev\\data\\dicom-elements-2007.dic");
            dataElementDictionary.LoadFromMemory(new MemoryStream(data), DictionaryFileFormat.BinaryFile);
            data = File.ReadAllBytes("c:\\dev\\data\\dicom-uids-2007.dic");
            uidDictionary.LoadFromMemory(new MemoryStream(data), DictionaryFileFormat.BinaryFile);

            // Load all DICOM files
            LoadSeriesInternal(fileCandidates, seriesByUID);

            Console.WriteLine($"Loaded {seriesByUID.Count} DICOM series");

            return new List<DICOMSeries>(seriesByUID.Values);
        }
        /*public async Task<IEnumerable<IImageSequenceSeries>> LoadSeriesAsync(IEnumerable<string> fileCandidates)
        {
            DataElementDictionary dataElementDictionary = new DataElementDictionary();
            UidDictionary uidDictionary = new UidDictionary();

            // Split parsed DICOM files into series (by DICOM series UID)
            Dictionary<string, DICOMSeries> seriesByUID = new Dictionary<string, DICOMSeries>();

            //LoadSeriesFromResourcesInternal(dataElementDictionary, uidDictionary);

            await Task.Run(()=> LoadSeriesInternal(fileCandidates, seriesByUID));

            Console.WriteLine($"Loaded {seriesByUID.Count} DICOM series");


            return new List<DICOMSeries>(seriesByUID.Values);
        }*/

        public class ScanSet
        {
            public string datasetName;
            public int dimX;
            public int dimY;
            public int dimZ;
            public float[] data;

            public Vector3 v0;

            public float scaleX;
            public float scaleY;
            public float scaleZ;

            public float minLocation;
            public float maxLocation;
        };

        private void LoadSeriesInternal(IEnumerable<string> fileCandidates, Dictionary<string, DICOMSeries> seriesByUID)
        {
            // Load all DICOM files
            List<DICOMSliceFile> slices = new List<DICOMSliceFile>();

            IEnumerable<string> sortedFiles = fileCandidates.OrderBy(
                s => s);

            /*
             * {
                    var fileName = Path.GetFileNameWithoutExtension(s);
                    if (int.TryParse(fileName, out int res))
                    {
                        return res;
                    }
                    return 0;
                }
             */

            foreach (string filePath in sortedFiles)
            {
                DICOMSliceFile sliceFile = ReadDICOMFile(filePath);
                if (sliceFile != null)
                {
                    if (sliceFile.file.PixelData.IsJpeg)
                        System.Console.WriteLine("DICOM with JPEG not supported by importer. Please enable SimpleITK from volume rendering import settings.");
                    else
                        slices.Add(sliceFile);
                }
            }

            foreach (DICOMSliceFile file in slices)
            {
                if (!seriesByUID.ContainsKey(file.seriesUID))
                {
                    seriesByUID.Add(file.seriesUID, new DICOMSeries());
                }
                seriesByUID[file.seriesUID].dicomFiles.Add(file);
            }
        }

        public ScanSet ImportSeries(IImageSequenceSeries series)
        {
            DICOMSeries dicomSeries = (DICOMSeries)series;
            List<DICOMSliceFile> files = dicomSeries.dicomFiles;

            if (files.Count <= 1)
            {
                Console.WriteLine("Insufficient number of slices.");
                return null;
            }

            // Create dataset
            ScanSet dataset = new ScanSet();

            ImportSeriesInternal(files, dataset);

            return dataset;
        }

        public void ImportSeriesInternal(List<DICOMSliceFile> files, ScanSet dataset)
        {
            // Check if the series is missing the slice location tag
            bool needsCalcLoc = false;
            foreach (DICOMSliceFile file in files)
            {
                needsCalcLoc |= file.missingLocation;
            }

            // Calculate slice location from "Image Position" (0020,0032)
            // TODO is this still needed?
            if (needsCalcLoc)
                CalcSliceLocFromPos(files);

            // Sort files by slice location
            files.Sort((DICOMSliceFile a, DICOMSliceFile b) => { return a.location.CompareTo(b.location); });

            Console.WriteLine($"Importing {files.Count} DICOM slices");

            dataset.datasetName = Path.GetFileName(files[0].filePath);
            dataset.dimX = files[0].file.PixelData.Columns;
            dataset.dimY = files[0].file.PixelData.Rows;
            dataset.dimZ = files.Count;
            int dimension = dataset.dimX * dataset.dimY * dataset.dimZ;
            dataset.data = new float[dimension];
            Console.WriteLine($"Dims: {dataset.dimX}x{dataset.dimY}x{dataset.dimZ}");

            dataset.v0 = files[0].position;

            for (int iSlice = 0; iSlice < files.Count; iSlice++)
            {
                DICOMSliceFile slice = files[iSlice];
                PixelData pixelData = slice.file.PixelData;
                int[] pixelArr = ToPixelArray(pixelData);
                if (pixelArr == null) // This should not happen
                    pixelArr = new int[pixelData.Rows * pixelData.Columns];

                for (int iRow = 0; iRow < pixelData.Rows; iRow++)
                {
                    for (int iCol = 0; iCol < pixelData.Columns; iCol++)
                    {
                        int pixelIndex = (iRow * pixelData.Columns) + iCol;
                        int dataIndex = (iSlice * pixelData.Columns * pixelData.Rows) + (iRow * pixelData.Columns) + iCol;

                        int pixelValue = pixelArr[pixelIndex];

                        // TODO what's this?
                        float hounsfieldValue = pixelValue * slice.slope + slice.intercept;
                        dataset.data[dataIndex] = Math.Clamp(hounsfieldValue, -1024.0f, 3071.0f);
                    }
                }
            }

            if (files[0].pixelSpacing > 0.0f)
            {
                // beware that pixelSpacing might be different between X and Y axis
                dataset.scaleX = files[0].pixelSpacing * dataset.dimX;
                dataset.scaleY = files[0].pixelSpacing * dataset.dimY;
                dataset.scaleZ = Math.Abs(files[files.Count - 1].location - files[0].location);
                dataset.minLocation = files[0].location;
                dataset.maxLocation = files[files.Count - 1].location;
            }

            // this is for volumetric data
            //dataset.FixDimensions();
        }

        private DICOMSliceFile ReadDICOMFile(string filePath)
        {
            AcrNemaFile file = LoadFile(filePath);

            if (file != null && file.HasPixelData)
            {
                DICOMSliceFile slice = new DICOMSliceFile();
                slice.file = file;
                slice.filePath = filePath;

                Tag locTag = new Tag("(0020,1041)");
                Tag posTag = new Tag("(0020,0032)");
                Tag interceptTag = new Tag("(0028,1052)");
                Tag slopeTag = new Tag("(0028,1053)");
                Tag pixelSpacingTag = new Tag("(0028,0030)");
                Tag seriesUIDTag = new Tag("(0020,000E)");

                // Read location (optional)
                if (file.DataSet.Contains(locTag))
                {
                    DataElement elemLoc = file.DataSet[locTag];
                    slice.location = (float)Convert.ToDouble(elemLoc.Value[0]);
                    if (file.DataSet.Contains(posTag))
                    {
                        elemLoc = file.DataSet[posTag];
                        slice.position = new Vector3(
                        (float)Convert.ToDouble(elemLoc.Value[0]),
                        (float)Convert.ToDouble(elemLoc.Value[1]),
                        (float)Convert.ToDouble(elemLoc.Value[2]));
                    }
                }
                // If no location tag, read position tag (will need to calculate location afterwards)
                else if (file.DataSet.Contains(posTag))
                {
                    DataElement elemLoc = file.DataSet[posTag];
                    slice.position = new Vector3(
                    (float)Convert.ToDouble(elemLoc.Value[0]),
                    (float)Convert.ToDouble(elemLoc.Value[1]),
                    (float)Convert.ToDouble(elemLoc.Value[2]));
                    slice.missingLocation = true;
                }
                else
                {
                    Console.WriteLine($"Missing location/position tag in file: {filePath}.\n The file will not be imported correctly.");
                    // Fallback: use counter as location
                    slice.location = (float)iFallbackLoc++;
                }
                
                // Read intercept
                if (file.DataSet.Contains(interceptTag))
                {
                    DataElement elemIntercept = file.DataSet[interceptTag];
                    slice.intercept = (float)Convert.ToDouble(elemIntercept.Value[0]);
                }
                else
                    Console.WriteLine($"The file {filePath} is missing the intercept element. As a result, the default transfer function might not look good.");
                
                // Read slope
                if (file.DataSet.Contains(slopeTag))
                {
                    DataElement elemSlope = file.DataSet[slopeTag];
                    slice.slope = (float)Convert.ToDouble(elemSlope.Value[0]);
                }
                else
                    Console.WriteLine($"The file {filePath} is missing the intercept element. As a result, the default transfer function might not look good.");
                
                // Read pixel spacing
                if (file.DataSet.Contains(pixelSpacingTag))
                {
                    DataElement elemPixelSpacing = file.DataSet[pixelSpacingTag];
                    slice.pixelSpacing = (float)Convert.ToDouble(elemPixelSpacing.Value[0]);
                }

                // Read series UID
                if (file.DataSet.Contains(seriesUIDTag))
                {
                    DataElement elemSeriesUID = file.DataSet[seriesUIDTag];
                    slice.seriesUID = Convert.ToString(elemSeriesUID.Value[0]);
                }

                return slice;
            }
            return null;
        }
        
        private AcrNemaFile LoadFile(string filePath)
        {
             AcrNemaFile file = null;
            try
            {
                if (DicomFile.IsDicomFile(filePath))
                    file = new DicomFile(filePath, false);
                else if (AcrNemaFile.IsAcrNemaFile(filePath))
                    file = new AcrNemaFile(filePath, false);
                else
                    Console.WriteLine("Selected file is neither a DICOM nor an ACR-NEMA file.");
            }
            catch (Exception dicomFileException)
            {
                Console.WriteLine($"Problems processing the DICOM file {filePath} :\n {dicomFileException}");
                return null;
            }
            return file;
        }

        private static int[] ToPixelArray(PixelData pixelData)
        {
            int[] intArray;
            if (pixelData.Data.Value.IsSequence)
            {
                Sequence sq = (Sequence)pixelData.Data.Value[0];
                intArray = new int[sq.Count];
                for (int i = 0; i < sq.Count; i++)
                    intArray[i] = Convert.ToInt32(sq[i].Value[0]);
                return intArray;
            }
            else if (pixelData.Data.Value.IsArray)
            {
                byte[][] bytesArray = pixelData.ToBytesArray();
                if (bytesArray != null && bytesArray.Length > 0)
                {
                    byte[] bytes = bytesArray[0];

                    int cellSize = pixelData.BitsAllocated / 8;
                    int pixelCount = bytes.Length / cellSize;

                    intArray = new int[pixelCount];
                    int pixelIndex = 0;

                    // Byte array for a single cell/pixel value
                    byte[] cellData = new byte[cellSize];
                    for(int iByte = 0; iByte < bytes.Length; iByte++)
                    {
                        // Collect bytes for one cell (sample)
                        int index = iByte % cellSize;
                        cellData[index] = bytes[iByte];
                        // We have collected enough bytes for one cell => convert and add it to pixel array
                        if (index == cellSize - 1)
                        {
                            int cellValue = 0;
                            if (pixelData.BitsAllocated == 8)
                                cellValue = cellData[0];
                            else if (pixelData.BitsAllocated == 16)
                                cellValue = BitConverter.ToInt16(cellData, 0);
                            else if (pixelData.BitsAllocated == 32)
                                cellValue = BitConverter.ToInt32(cellData, 0);
                            else
                                Console.WriteLine("Invalid format!");

                            intArray[pixelIndex] = cellValue;
                            pixelIndex++;
                        }
                    }
                    return intArray;
                }
                else
                    return null;
            }
            else
            {
                Console.WriteLine("Pixel array is invalid");
                return null;
            }
        }

        private void CalcSliceLocFromPos(List<DICOMSliceFile> slices)
        {
            // We use the first slice as a starting point (a), andthe normalised vector (v) between the first and second slice as a direction.
            Vector3 v = slices[1].position.Subtract(slices[0].position).Normalize();
            Vector3 a = slices[0].position;
            slices[0].location = 0.0f;

            for(int i = 1; i < slices.Count; i++)
            {
                // Calculate the vector between a and p (ap) and dot it with v to get the distance along the v vector (distance when projected onto v)
                Vector3 p = slices[i].position;
                Vector3 ap = p.Subtract(a);
                float dot = ap.DotProduct(v);
                slices[i].location = dot;
                slices[i].missingLocation = false;
            }
        }
    }
}
