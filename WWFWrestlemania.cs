using System;
using System.Collections.Generic;
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

        static bool IsValidPaletteName(string name)
        {
            if (name.IndexOf("_P") != -1)
            {
                return true;
            }
            int len = name.Length;
            int firstNonAlpha = len;
            for (int i = 0; i < len; ++i)
            {
                char ch = name[i];
                if (!(
                        (ch >= 'A' && ch <= 'Z') ||
                        (ch >= 'a' && ch <= 'z') ||
                        (ch >= '0' && ch <= '9') ||
                        (ch == '_' || ch == '-')
                    )
                )
                {
                    firstNonAlpha = i;
                    break;
                }
            }
            return firstNonAlpha > 5;
        }

        // palette table format (0x1a size)
        // 0 - name
        // 0xb - unk
        // 0xc - num colours (2-byte colours)
        // 0xe - palette offset
        static List<ushort[]> ReadPalettes(BinaryReader br, int paletteOffset, int numPalettes, string outDir)
        {
            const int PALETTE_DATA_SIZE = 0x1a;
            StringBuilder sb = new StringBuilder();
            List<ushort[]> palettes = new List<ushort[]>(numPalettes);
            long curPos = br.BaseStream.Position;
            br.BaseStream.Seek(paletteOffset, SeekOrigin.Begin);
            for (int i = 0; i < numPalettes; ++i)
            {
                long preName = br.BaseStream.Position;
                string name = ReadString(br, NAME_LEN);
                br.BaseStream.Seek(preName, SeekOrigin.Begin);
                if (!IsValidPaletteName(name))
                {
                    break;
                }
                Console.WriteLine("\tReading palette: {0}", name);
                sb.AppendLine(name);
                DumpData(sb, br, PALETTE_DATA_SIZE);
                sb.AppendLine();
                br.ReadBytes(0xc);
                short numColours = br.ReadInt16();
                int palOffset = br.ReadInt32();
                if(i < (numPalettes - 1))
                {
                    br.BaseStream.Seek(8, SeekOrigin.Current);
                }
                long afterPos = br.BaseStream.Position;
                br.BaseStream.Seek(palOffset, SeekOrigin.Begin);
                ushort[] colours = new ushort[numColours];
                byte[] colourData = br.ReadBytes(numColours * sizeof(ushort));
                Buffer.BlockCopy(colourData, 0, colours, 0, colourData.Length);
                for(int j = 0; j < colours.Length; ++j)
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
            if(nameBytes.Length == 0)
            {
                return String.Empty;
            }
            int i = 0;
            for(; i < len; ++i)
            {
                if(nameBytes[i] == 0)
                {
                    break;
                }
            }
            for(; i < len; ++i)
            {
                nameBytes[i] = 0;
            }
            return Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
        }

        class Image
        {
            public string Name { get; private set; }
            public short Width {get; private set;}
            public short Height {get; private set;}
            public byte[] ImageData {get; private set;}
            public List<ushort[]> PotentialPalettes { get; private set; }

            public Image(string name_, short width_, short height_, byte[] fileData_, List<ushort[]> palettes_)
            {
                Name = name_;
                Width = width_;
                Height = height_;
                ImageData = fileData_;
                PotentialPalettes = palettes_;
            }
        }

        static Color FromRGB555(ushort value)
        {
            int r = value & 0x1f;
            int g = (value >> 5) & 0x1f;
            int b = (value >> 10) & 0x1f;
            const float scaleFactor = 0xff / 0x1f;
            return Color.FromArgb((int)(r * scaleFactor), (int)(g * scaleFactor), (int)(b * scaleFactor));
        }

        static void SaveImages(short canvasWidth, short canvasHeight, List<Image> images, string outDir)
        {
            Rectangle lockRect = new Rectangle(0, 0, canvasWidth, canvasHeight);
            PixelFormat pf = PixelFormat.Format16bppArgb1555;
            foreach (Image im in images)
            {
                string imName = im.Name;
                string imgDir = Path.Combine(outDir, imName);
                Console.WriteLine("\tProcessing image {0}", imName);
                int imWidth = im.Width;
                byte[] imageData = im.ImageData;
                short[] rowColours = new short[imWidth];
                GCHandle rowColoursHandle = GCHandle.Alloc(rowColours, GCHandleType.Pinned);
                IntPtr rowColoursSrc = rowColoursHandle.AddrOfPinnedObject();
                int stride = (imWidth + 3) & ~3;
                int palIndex = 0;
                foreach(ushort[] palette in im.PotentialPalettes)
                {
                    int numColours = palette.Length;
                    int imageDataRowIndex = 0;
                    using (Bitmap bmBack = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb))
                    {
                        int startRow = canvasHeight - im.Height;
                        int startX = (canvasWidth - imWidth) / 2;
                        byte tlIndex = imageData[0];
                        if(numColours <= tlIndex)
                        {
                            goto nextPalette;
                        }
                        //Color tlColour = FromRGB555(palette[tlIndex]);
                        //using (Graphics g = Graphics.FromImage(bmBack))
                        //{
                        //    g.Clear(Color.Transparent);
                        //}
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
                                    // since we don't know which palette goes with which image
                                    // and the palettes have different number of colours in them
                                    // if we get an out of bounds colour, then this is obviously a
                                    // bad palette for this image
                                    if (colourIndex >= numColours)
                                    {
                                        goto nextPalette;
                                    }
                                    rowColours[x] = (short)palette[colourIndex];
                                }
                                Marshal.Copy(rowColours, 0, pBmDataPtr, imWidth);
                                pBmDataPtr = new IntPtr(pBmDataPtr.ToInt64() + bmStride);
                                imageDataRowIndex += stride;
                            }
                            bm.UnlockBits(bmData);
                            string fileName = Path.Combine(outDir, String.Format("{0}-p{1}.png", im.Name, palIndex));
                            bm.Save(fileName, ImageFormat.Png);
                        }
                    }
                    ++palIndex;
                nextPalette:
                    ;
                }
                rowColoursHandle.Free();
            }
        }

        // File table offset format (0x32 size)
        // 0 - name (until null)
        // 0x16 - width (image stride is next multiple of 4)
        // 0x18 - height
        // 0x1a - palette? 
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
            short widest = 0;
            short tallest = 0;
            string imgFileName = Path.GetFileName(imgFile);
            Console.WriteLine("Processing {0}", imgFileName);
            string imgOutDir = Path.Combine(outDir, imgFileName);
            Directory.CreateDirectory(imgOutDir);
            using (BinaryReader br = new BinaryReader(ms))
            {
                int numImages = br.ReadInt16();
                images = new List<Image>(numImages);
                int numUnk = br.ReadInt16();
                int fileTableOffset = br.ReadInt32();
                int palettesOffset = fileTableOffset + (numImages * 0x32);
                int fileSize = fileData.Length;
                int numPalettes = (fileSize - palettesOffset) / 0x12;
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
                    int stride = (width + 3) & ~3;
                    short unk = br.ReadInt16();
                    int dataOffset = br.ReadInt32();
                    byte[] imgData = new byte[stride * height];
                    br.ReadBytes(0x32 - 0x20);
                    Buffer.BlockCopy(fileData, dataOffset, imgData, 0, imgData.Length);
                    images.Add(new Image(imName, width, height, imgData, palettes));
                    if (width > widest) widest = width;
                    if (height > tallest) tallest = height;
                }
            }
            File.WriteAllText(Path.Combine(imgOutDir, "images.txt"), sb.ToString());
            SaveImages(widest, tallest, images, imgOutDir);
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
                ProcessImage(imgFile, outDir);
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
