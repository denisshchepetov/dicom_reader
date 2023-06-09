﻿#if UVR_USE_SIMPLEITK
using UnityEngine;
using System;
using itk.simple;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using openDicom.Image;
using System.Drawing;

namespace UnityVolumeRendering
{
    /// <summary>
    /// SimpleITK-based DICOM importer.
    /// </summary>
    public class SimpleITKImageFileImporter : IImageFileImporter
    {
        public VolumeDataset Import(string filePath)
        {
            float[] pixelData = null;
            VectorUInt32 size = null;
            VectorDouble spacing = null;

            VolumeDataset volumeDataset = new VolumeDataset();

            ImportInternal(volumeDataset, pixelData, size, spacing, filePath);

            return volumeDataset;
        }
        public async Task<VolumeDataset> ImportAsync(string filePath)
        {
            float[] pixelData = null;
            VectorUInt32 size = null;
            VectorDouble spacing = null;

            // Create dataset
            VolumeDataset volumeDataset = new VolumeDataset();

            await Task.Run(() => ImportInternal(volumeDataset,pixelData,size,spacing,filePath));

            return volumeDataset;
        }
        private void ImportInternal(VolumeDataset volumeDataset, float[] pixelData,VectorUInt32 size,VectorDouble spacing,string filePath)
        {
            ImageFileReader reader = new ImageFileReader();

            reader.SetFileName(filePath);

            Image image = reader.Execute();

            // Cast to 32-bit float
            image = SimpleITK.Cast(image, PixelIDValueEnum.sitkFloat32);

            size = image.GetSize();

            int numPixels = 1;
            for (int dim = 0; dim < image.GetDimension(); dim++)
                numPixels *= (int)size[dim];

            // Read pixel data
            pixelData = new float[numPixels];
            IntPtr imgBuffer = image.GetBufferAsFloat();
            Marshal.Copy(imgBuffer, pixelData, 0, numPixels);
            spacing = image.GetSpacing();


            volumeDataset.data = pixelData;
            volumeDataset.dimX = (int)size[0];
            volumeDataset.dimY = (int)size[1];
            volumeDataset.dimZ = (int)size[2];
            volumeDataset.datasetName = "test";
            volumeDataset.filePath = filePath;
            volumeDataset.scaleX = (float)(spacing[0] * size[0]);
            volumeDataset.scaleY = (float)(spacing[1] * size[1]);
            volumeDataset.scaleZ = (float)(spacing[2] * size[2]);

            volumeDataset.FixDimensions();
        }
    }
}
#endif
