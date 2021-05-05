using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GameStuff
{
    class TaitoLegends2
    {
        class TL2FileInfo
        {
            public string name;
            public uint offset;
            public uint size;
            public long entryPos;

            public TL2FileInfo(string theName)
            {
                name = theName;
            }
        }

        static void ExplodeTaitoLegends2()
        {
            // File format
            // Header {
            // int noOfFiles
            // int startOfFileNames
            // int sizeOfFileNameSection
            // int startOfLocationInfo
            // } // end header
            // Null termiated names, the order of these are the same as the next
            // LocationInfo {
            // int fileSize
            // short startSector (file offset is * 0x800)
            // short unk (file/folder marker?)
            // long unk
            // }
            string outputLocation = @"C:\Users\Bob\Downloads\taitoLegends2\GameBIN";
            string inputGZH = @"I:\gamebin.gzh";
            List<TL2FileInfo> tl2Files = null;
            using (FileStream fs = File.OpenRead(inputGZH))
            using (BinaryReader br = new BinaryReader(fs))
            {
                int numFiles = br.ReadInt32();
                int fileNameStart = br.ReadInt32();
                int sizeOfFileNameSection = br.ReadInt32();
                int locationOffset = br.ReadInt32();

                tl2Files = new List<TL2FileInfo>(numFiles);

                br.BaseStream.Seek(fileNameStart, SeekOrigin.Begin);
                for (int i = 0; i < numFiles; ++i)
                {
                    string currentName = String.Empty;
                    char ch = Char.MaxValue;
                    while (ch != 0)
                    {
                        ch = br.ReadChar();
                        if (ch != 0)
                        {
                            currentName += ch;
                        }
                        else
                        {
                            currentName.TrimEnd(Char.MaxValue);
                            if (currentName != String.Empty)
                            {
                                TL2FileInfo fileInf = new TL2FileInfo(currentName);
                                tl2Files.Add(fileInf);
                                currentName = String.Empty;
                            }
                        }
                    }
                }
                br.BaseStream.Seek(locationOffset, SeekOrigin.Begin);
                for (int i = 0; i < numFiles; ++i)
                {
                    long entryPos = br.BaseStream.Position;
                    uint fileSize = br.ReadUInt32();
                    ushort sector = br.ReadUInt16();
                    short unk1 = br.ReadInt16();
                    long unk2 = br.ReadInt64();
                    tl2Files[i].offset = sector * 0x800u;
                    tl2Files[i].size = fileSize;
                    tl2Files[i].entryPos = entryPos;
                }
                foreach (TL2FileInfo file in tl2Files)
                {
                    //Console.WriteLine("{3:x} - Found {1} at {2:x} of size {0}", file.size, file.name, file.offset, file.entryPos);
                    Console.WriteLine("Writing {0} bytes of {1} from {2:x}", file.size, file.name, file.offset);
                    br.BaseStream.Seek((long)file.offset, SeekOrigin.Begin);
                    byte[] fileData = br.ReadBytes((int)file.size);
                    File.WriteAllBytes(Path.Combine(outputLocation, file.name), fileData);
                }
            }
        }
    }
}
