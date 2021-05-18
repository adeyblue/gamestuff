using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace GameStuff
{
    class WWFWrestlemania
    {
        const int NAME_LEN = 0xc;

        static void DumpData(StringBuilder sb, BinaryReader br, int numBytes)
        {
            bool doneOne = false;
            for (int i = 0; i < numBytes; ++i)
            {
                if ((i & 15) == 0)
                {
                    if (doneOne)
                    {
                        sb.AppendLine();
                    }
                    doneOne = true;
                }
                sb.AppendFormat("{0:X2} ", br.ReadByte());
            }
            br.BaseStream.Seek(-numBytes, SeekOrigin.Current);
        }

        // palette table format (0x1a size)
        // 0 - name
        // 0xb - unk
        // 0xc - num colours (2-byte colours)
        // 0xe - palette offset
        static List<ushort[]> ReadPalettes(BinaryReader br, int paletteOffset, int numPalettes, string outDir)
        {
            const int PALETTE_DATA_SIZE = 0x1a;
            if (numPalettes < 1)
            {
                throw new InvalidDataException("Invalid image");
            }
            StringBuilder sb = new StringBuilder();
            List<ushort[]> palettes = new List<ushort[]>(numPalettes);
            long curPos = br.BaseStream.Position;
            br.BaseStream.Seek(paletteOffset, SeekOrigin.Begin);
            sb.AppendFormat("Found {0} palettes:", numPalettes);
            sb.AppendLine();
            for (int i = 0; i < numPalettes; ++i)
            {
                long preName = br.BaseStream.Position;
                string name = ReadString(br, NAME_LEN);
                br.BaseStream.Seek(preName, SeekOrigin.Begin);
                Console.WriteLine("\tReading palette: {0}", name);
                sb.AppendLine(name);
                DumpData(sb, br, PALETTE_DATA_SIZE);
                sb.AppendLine();
                br.ReadBytes(0xc);
                short numColours = br.ReadInt16();
                int palOffset = br.ReadInt32();
                if (i < (numPalettes - 1))
                {
                    br.BaseStream.Seek(8, SeekOrigin.Current);
                }
                long afterPos = br.BaseStream.Position;
                br.BaseStream.Seek(palOffset, SeekOrigin.Begin);
                ushort[] colours = new ushort[numColours];
                byte[] colourData = br.ReadBytes(numColours * sizeof(ushort));
                Buffer.BlockCopy(colourData, 0, colours, 0, colourData.Length);
                for (int j = 0; j < colours.Length; ++j)
                {
                    colours[j] = (ushort)(colours[j] |= 0x8000);
                }
                br.BaseStream.Seek(afterPos, SeekOrigin.Begin);
                palettes.Add(colours);
            }
            br.BaseStream.Seek(paletteOffset, SeekOrigin.Begin);
            File.WriteAllText(Path.Combine(outDir, "palettes.txt"), sb.ToString());
            return palettes;
        }

        static string ReadString(BinaryReader br, int len)
        {
            byte[] nameBytes = br.ReadBytes(len);
            if (nameBytes.Length == 0)
            {
                return String.Empty;
            }
            int i = 0;
            for (; i < len; ++i)
            {
                if (nameBytes[i] == 0)
                {
                    break;
                }
            }
            for (; i < len; ++i)
            {
                nameBytes[i] = 0;
            }
            return Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
        }

        class Image
        {
            public string Name { get; private set; }
            public short Width { get; private set; }
            public short Height { get; private set; }
            public short CanvasWidth { get; private set; }
            public short CanvasHeight { get; private set; }
            public byte[] ImageData { get; private set; }
            public ushort[] Palette { get; private set; }

            public Image(string name_, short width_, short height_, byte[] fileData_, ushort[] palette_)
            {
                // chop off the leading ! so that any that are part of a sequence
                // are collated mext to the rest that don't start with a !
                name_ = name_.TrimStart('!');
                Name = name_;
                CanvasWidth = Width = width_;
                CanvasHeight = Height = height_;
                ImageData = fileData_;
                Palette = palette_;
            }

            public void UpdateCanvasSize(short width, short height)
            {
                CanvasHeight = height;
                CanvasWidth = width;
            }

            public override string ToString()
            {
                return String.Format("{0} - {1}x{2} ({3}x{4})", Name, Width, Height, CanvasWidth, CanvasHeight);
            }
        }

        static Color FromRGB555(ushort value)
        {
            int b = value & 0x1f;
            int g = (value >> 5) & 0x1f;
            int r = (value >> 10) & 0x1f;
            const float scaleFactor = 0xff / (float)0x1f;
            return Color.FromArgb((int)(r * scaleFactor), (int)(g * scaleFactor), (int)(b * scaleFactor));
        }

        static Color FindTopLeftColour(byte[] imgData, ushort[] palette)
        {
            int numColours = palette.Length;
            foreach(byte b in imgData)
            {
                if(b < numColours)
                {
                    return FromRGB555(palette[b]);
                }
            }
            return Color.Transparent;
        }

        static void SaveImages(List<Image> images, string outDir)
        {
            PixelFormat pf = PixelFormat.Format16bppArgb1555;
            foreach (Image im in images)
            {
                int canvasWidth = im.CanvasWidth;
                int canvasHeight = im.CanvasHeight;
                Rectangle lockRect = new Rectangle(0, 0, canvasWidth, canvasHeight);
                string imName = im.Name;
                string imgDir = Path.Combine(outDir, imName);
                Console.WriteLine("\tProcessing image {0}", imName);
                Debug.WriteLine(String.Format("Processing image {0}", imName));
                int imWidth = im.Width;
                byte[] imageData = im.ImageData;
                short[] rowColours = new short[imWidth];
                int stride = (imWidth + 3) & ~3;
                ushort[] palette = im.Palette;
                int numColours = palette.Length;
                int imageDataRowIndex = 0;
                using (Bitmap bmBack = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb))
                {
                    int startRow = canvasHeight - im.Height;
                    int startX = (canvasWidth - imWidth) / 2;
                    Color tlColour = FindTopLeftColour(imageData, palette);
                    using (Graphics g = Graphics.FromImage(bmBack))
                    {
                        g.Clear(tlColour);
                    }
                    using (Bitmap bm = bmBack.Clone(lockRect, pf))
                    {
                        BitmapData bmData = bm.LockBits(lockRect, ImageLockMode.WriteOnly, pf);
                        int bmStride = bmData.Stride;
                        IntPtr pBmDataPtr = new IntPtr(bmData.Scan0.ToInt64() + (startRow * bmStride) + (startX * sizeof(ushort)));
                        for (int y = 0; y < im.Height; ++y)
                        {
                            for (int x = 0; x < imWidth; ++x)
                            {
                                byte colourIndex = imageData[imageDataRowIndex + x];
                                if (colourIndex >= palette.Length)
                                {
                                    Debug.WriteLine(String.Format("\tRequest for colour id {0} outside of palette bounds {1}", colourIndex, palette.Length));
                                    continue;
                                }
                                rowColours[x] = (short)palette[colourIndex];
                            }
                            Marshal.Copy(rowColours, 0, pBmDataPtr, imWidth);
                            pBmDataPtr = new IntPtr(pBmDataPtr.ToInt64() + bmStride);
                            imageDataRowIndex += stride;
                        }
                        bm.UnlockBits(bmData);
                        string fileName = Path.Combine(outDir, String.Format("{0}.png", im.Name));
                        bm.Save(fileName, ImageFormat.Png);
                    }
                }
            }
        }

        static string GetGoodSequenceNamePart(string name)
        {
            // check if this name ends with two numbers, if so, return the unnumbered stem
            // otherwise fail since it isn't part of a sequence (that we can automatically detect)
            if (name.Length < 3) return null;
            string lastTwo = name.Substring(name.Length - 2);
            if (Char.IsDigit(lastTwo[0]) && Char.IsDigit(lastTwo[1]))
            {
                return name.Substring(0, name.Length - 2);
            }
            return null;
        }

        static void AmendSequenceCanvasSize(List<Image> thisSequence)
        {
            short widest = 0, tallest = 0;
            foreach(Image im in thisSequence)
            {
                widest = Math.Max(widest, im.Width);
                tallest = Math.Max(tallest, im.Height);
            }
            foreach(Image im in thisSequence)
            {
                im.UpdateCanvasSize(widest, tallest);
            }
        }

        static void ResizeSequences(List<Image> images)
        {
            string lastPart = null;
            List<Image> thisSequence = new List<Image>();
            images.Sort((x, y) => { return x.Name.CompareTo(y.Name); });
            foreach (Image im in images)
            {
                string thisPart = GetGoodSequenceNamePart(im.Name);
                if (thisPart != lastPart)
                {
                    if (lastPart != null)
                    {
                        if (thisSequence.Count > 1)
                        {
                            AmendSequenceCanvasSize(thisSequence);
                        }
                    }
                    thisSequence.Clear();
                    thisSequence.Add(im);
                    lastPart = thisPart;
                }
                else
                {
                    thisSequence.Add(im);
                }
            }
            if (lastPart != null)
            {
                if (thisSequence.Count > 1)
                {
                    AmendSequenceCanvasSize(thisSequence);
                }
            }
        }

        // File header format
        // 0 = num images
        // 0x2 - number of palettes + 3? (ie 4 when there's one palette, 0x10 when there's 13)
        // 0x4 - file table offset
        // 0x8 - 
        //
        // File table offset format (0x32 size)
        // 0 - name (until null)
        // 0x16 - width (image stride is next multiple of 4)
        // 0x18 - height
        // 0x1a - palette + 3
        // 0x1c - file data offset - 8bpp, 
        static void ProcessImage(string imgFile, string outDir)
        {
            const int IMAGE_DATA_SIZE = 0x32;
            StringBuilder sb = new StringBuilder();
            byte[] fileData = File.ReadAllBytes(imgFile);
            if(fileData.Length == 0)
            {
                return;
            }
            MemoryStream ms = new MemoryStream(fileData);
            List<ushort[]> palettes;
            List<Image> images;
            string imgFileName = Path.GetFileName(imgFile);
            Console.WriteLine("Processing {0}", imgFileName);
            string imgOutDir = Path.Combine(outDir, imgFileName);
            Directory.CreateDirectory(imgOutDir);
            using (BinaryReader br = new BinaryReader(ms))
            {
                int numImages = br.ReadInt16();
                images = new List<Image>(numImages);
                int numPalettes = br.ReadInt16() - 3;
                int fileTableOffset = br.ReadInt32();
                int palettesOffset = fileTableOffset + (numImages * 0x32);
                int fileSize = fileData.Length;
                palettes = ReadPalettes(br, palettesOffset, numPalettes, imgOutDir);
                br.BaseStream.Seek(fileTableOffset, SeekOrigin.Begin);
                for (int i = 0; i < numImages; ++i)
                {
                    long preName = br.BaseStream.Position;
                    string imName = ReadString(br, NAME_LEN);
                    br.BaseStream.Seek(preName, SeekOrigin.Begin);
                    sb.AppendLine(imName);
                    imName = imName.Replace('/', '_');
                    DumpData(sb, br, IMAGE_DATA_SIZE);
                    sb.AppendLine();
                    br.ReadBytes(0x16);
                    short width = br.ReadInt16();
                    short height = br.ReadInt16();
                    int palIndex = br.ReadInt16() - 3;
                    int stride = (width + 3) & ~3;
                    int dataOffset = br.ReadInt32();
                    byte[] imgData = new byte[stride * height];
                    br.ReadBytes(0x32 - 0x20);
                    Buffer.BlockCopy(fileData, dataOffset, imgData, 0, imgData.Length);
                    images.Add(new Image(imName, width, height, imgData, palettes[palIndex]));
                }
            }
            ResizeSequences(images);
            File.WriteAllText(Path.Combine(imgOutDir, "images.txt"), sb.ToString());
            SaveImages(images, imgOutDir);
        }

        static void ProcessDir(string inDir, string outDir)
        {
            if(File.Exists(inDir))
            {
                ProcessImage(inDir, outDir);
                return;
            }
            string[] childDirs = Directory.GetDirectories(inDir);
            foreach (string childDir in childDirs)
            {
                string dirName = Path.GetDirectoryName(childDir);
                ProcessDir(childDir, Path.Combine(outDir, dirName));
            }
            string[] dirImgs = Directory.GetFiles(inDir, "*.img");
            foreach (string imgFile in dirImgs)
            {
                try
                {
                    ProcessImage(imgFile, outDir);
                }
                catch(InvalidDataException)
                {
                    Console.WriteLine("{0} is an invalid img", imgFile);
                }
            }
        }

        static void Main(string[] args)
        {
            if ((args.Length < 2) || (!(Directory.Exists(args[0])) && (Directory.Exists(args[1]))))
            {
                if (!(File.Exists(args[0]) && Directory.Exists(args[1])))
                {
                    Console.WriteLine(
                        "Usage: WWFImgExtract <WWFDir> <OutDir>{0}" +
                        "{0}" +
                        "<WWFDir> needs to be where the WWF Wrestlemania source code is{0}" +
                        "<OutDir> is where to save the images{0}",
                        Environment.NewLine
                    );
                    return;
                }
            }
            ProcessDir(args[0], args[1]);
        }
    }
}
