// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Drawing;
using System.Runtime.ConstrainedExecution;
using UnityVolumeRendering;
using static UnityVolumeRendering.DICOMImporter;

internal class Program
{
    public enum Direction
    {
        Front,
        Side,
        Top
    };

    static void SaveBitmaps(ScanSet dataset, string name)
    {
        var bmpY = dataset.dimY;

        foreach (var hw in windowOptions)
        {
            for (int z = 0; z < dataset.dimZ; z++)
            {
                var bmp = new System.Drawing.Bitmap(dataset.dimX, dataset.dimY);
                var sliceOffset = dataset.dimX * dataset.dimY * z;
                //var sliceOffsetVertical = bmpY * (z / numTilesX);
                for (int j = 0; j < dataset.dimY; j++)
                {
                    for (int i = 0; i < dataset.dimX; i++)
                    {
                        //var sliceOffsetHorizontal = (dataset.dimX * z) % (dataset.dimX * numTilesX);
                        var floatVal = dataset.data[sliceOffset + j * dataset.dimX + i];
                        int pixelVal = GetPixelValue(floatVal, hw);
                        bmp.SetPixel(i, j, System.Drawing.Color.FromArgb(pixelVal, pixelVal, pixelVal));
                    }
                }

                var root = "c:\\dev\\data\\tmp\\images\\";
                bmp.Save($"{root}{name}_{hw.name}_{z}.png", System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }

    //static void SaveBitmapMap(ScanSet dataset, string name)
    //{
    //    const int numTilesX = 10;
    //    var bmpY = dataset.dimY;
    //    int bmpHeight = bmpY * (dataset.dimZ / numTilesX + ((dataset.dimZ % numTilesX > 0) ? 1 : 0));
    //
    //    var bmp = new System.Drawing.Bitmap(dataset.dimX * numTilesX, bmpHeight);
    //
    //    for (int z = 0; z < dataset.dimZ; z++)
    //    {
    //        var sliceOffset = dataset.dimX * dataset.dimY * z;
    //        var sliceOffsetVertical = bmpY * (z / numTilesX);
    //        for (int j = 0; j < dataset.dimY; j++)
    //        {
    //            for (int i = 0; i < dataset.dimX; i++)
    //            {
    //                var sliceOffsetHorizontal = (dataset.dimX * z) % (dataset.dimX * numTilesX);
    //                var floatVal = dataset.data[sliceOffset + j * dataset.dimX + i];
    //
    //                int pixelVal = GetPixelValue(floatVal);
    //
    //                int x = i + sliceOffsetHorizontal;
    //                int y = j + sliceOffsetVertical;
    //
    //                bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(pixelVal, pixelVal, pixelVal));
    //            }
    //        }
    //    }
    //
    //    if (true /*debug*/)
    //    {
    //        for (int y = 0; y < dataset.dimY; ++y)
    //        {
    //            for (int i = 0; i < dataset.dimX; i += (int)(dataset.dimX / 8.0f))
    //            {
    //                bmp.SetPixel(i, y, System.Drawing.Color.FromArgb(255, 255, 255));
    //            }
    //
    //            bmp.SetPixel(0, y, System.Drawing.Color.FromArgb(255, 255, 255));
    //            bmp.SetPixel(dataset.dimX / 2, y, System.Drawing.Color.FromArgb(255, 255, 255));
    //            bmp.SetPixel(dataset.dimX - 1, y, System.Drawing.Color.FromArgb(255, 255, 255));
    //        }
    //        for (int x = 0; x < dataset.dimX; ++x)
    //        {
    //            for (int i = 0; i < dataset.dimY; i += (int)(dataset.dimY / 8.0f))
    //            {
    //                bmp.SetPixel(x, i, System.Drawing.Color.FromArgb(255, 255, 255));
    //            }
    //
    //            bmp.SetPixel(x, 0, System.Drawing.Color.FromArgb(255, 255, 255));
    //            bmp.SetPixel(x, dataset.dimY/2, System.Drawing.Color.FromArgb(255, 255, 255));
    //            bmp.SetPixel(x, dataset.dimY-1, System.Drawing.Color.FromArgb(255, 255, 255));
    //        }
    //    }
    //
    //    var root = "c:\\dev\\data\\tmp\\";
    //    bmp.Save($"{root}{name}.png", System.Drawing.Imaging.ImageFormat.Png);
    //    using(var wr = new StreamWriter($"{root}\\{name}.txt"))
    //    {
    //        //wr.WriteLine($"x: {dataset.minX/1000.0f}, {dataset.maxX / 1000.0f}, {dataset.scaleX / 1000.0f}, {(dataset.maxX - dataset.minX) / 2.0f / 1000.0f}");
    //        //wr.WriteLine($"y: {dataset.minY / 1000.0f}, {dataset.maxY / 1000.0f}, {dataset.scaleY / 1000.0f}, {(dataset.maxY - dataset.minY) / 2.0f / 1000.0f}");
    //        //wr.WriteLine($"z: {dataset.minZ / 1000.0f}, {dataset.maxZ / 1000.0f}, {dataset.scaleZ / 1000.0f}, {(dataset.maxZ - dataset.minZ) / 2.0f / 1000.0f}");
    //        wr.WriteLine($"v0: {dataset.v0.x},{dataset.v0.y},{dataset.v0.z}");
    //        wr.WriteLine($"sz: {dataset.scaleX},{dataset.scaleY},{dataset.scaleZ}");
    //
    //        wr.WriteLine($"l: {dataset.minLocation}, {dataset.maxLocation}");
    //    }
    //}

    public static List<HounsfieldWindow> windowOptions = new List<HounsfieldWindow>()
    {
        new HounsfieldWindow( "brain", 40, 80 ),
        new HounsfieldWindow( "subdural", 100, 300 ),
        new HounsfieldWindow( "stroke", 32, 8),
        new HounsfieldWindow("temporal bones", 700, 4000),
        new HounsfieldWindow("soft tissues", 20, 400)
    };

    public class HounsfieldWindow
    {
        public HounsfieldWindow(string Name, int Level, int Window)
        {
            name = Name;
            level = Level;
            window = Window;
        }

        public string name;
        public int level;
        public int window;
    }

    private static int GetPixelValue(float floatVal, HounsfieldWindow hounsfieldWindow)
    {

        //-1024.0f, 3071.0f
        float lowBound = hounsfieldWindow.level - hounsfieldWindow.window / 2.0f;
        float highBound = hounsfieldWindow.level + hounsfieldWindow.window / 2.0f;
        int pixelVal = (int)(((floatVal - lowBound) / (highBound - lowBound)) * 255);
        if (pixelVal > 255) pixelVal = 255;
        if (pixelVal < 0) pixelVal = 0;
        return pixelVal;
    }

    static public void Save(string directoryPath, string stripName, Direction dir)
    {
        // Find all DICOM files in directory
        IEnumerable<string> fileCandidates = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(p => p.EndsWith(".dcm", StringComparison.InvariantCultureIgnoreCase) || p.EndsWith(".dicom", StringComparison.InvariantCultureIgnoreCase) || p.EndsWith(".dicm", StringComparison.InvariantCultureIgnoreCase));

        var importer = new DICOMImporter();
        IEnumerable<IImageSequenceSeries> seriesList = importer.LoadSeries(fileCandidates);
        foreach (IImageSequenceSeries series in seriesList)
        {
            ScanSet dataset = importer.ImportSeries(series);
            if (dataset != null)
            {
                SaveBitmaps(dataset, stripName);
            }
        }
    }

    struct PathNamePair
    {
        public PathNamePair(string path, Direction dir)
        {
            Path = path;
            Dir = dir;
        }
        public string Path;
        public Direction Dir;
    }

    static string NameFromDirection(Direction dir)
    {
        switch (dir)
        {
            case Direction.Top: return "top";
            case Direction.Front: return "front";
            case Direction.Side: return "side";
        }

        return null;
    }

    private static void Main(string[] args)
    {
        //var dataHQ = new List<PathNamePair>() { new PathNamePair( @"C:\Users\shepe\Downloads\Head^DE_HEAD (Adult) C220889 3\Head^DE_HEAD (Adult) C220889 3\DICOM\HEAD   1.0  Hr40  2  F_0.5", "top" ),
        //    new PathNamePair( @"C:\Users\shepe\Downloads\Head^DE_HEAD (Adult) C220889 3\Head^DE_HEAD (Adult) C220889 3\DICOM\HEAD   3.0  MPR  cor  F_0.5", "front"),
        //    new PathNamePair( @"C:\Users\shepe\Downloads\Head^DE_HEAD (Adult) C220889 3\Head^DE_HEAD (Adult) C220889 3\DICOM\HEAD   3.0  MPR  sag  F_0.5", "side") };

        var data = new List<PathNamePair>() { new PathNamePair( @"C:\dev\data\Ax, Sag, Core\CT HEAD_BRAIN W_O CON Axial Schwartzbauer 3_13_23\CT HEAD\BRAIN W\O CON Axial Schwartzbauer 3_13_23\DICOM\AX MPR, iDose (3)", Direction.Top ),
            new PathNamePair( @"C:\dev\data\Ax, Sag, Core\CT HEAD_BRAIN W_O CON Coronal Scwhartzbauer 3_13_23\CT HEAD\BRAIN W\O CON Coronal Scwhartzbauer 3_13_23\DICOM\COR, iDose (3)", Direction.Front),
            new PathNamePair( @"C:\dev\data\Ax, Sag, Core\CT HEAD_BRAIN W_O CON Sagittal Schwartzbauer 3_13_23\CT HEAD\BRAIN W\O CON Sagittal Schwartzbauer 3_13_23\DICOM\SAG MPR, iDose (3)", Direction.Side) };

        foreach (var val in data)
        {
            if (!Directory.Exists(val.Path))
            {
                throw new Exception($"Path not found ${val.Path}");
            }
            Save(val.Path, NameFromDirection(val.Dir), val.Dir);
        }

        Console.WriteLine("Hello, World!");
    }
}
