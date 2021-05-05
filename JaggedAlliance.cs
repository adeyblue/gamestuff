using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace GameStuff
{
    class JaggedAlliance
    {
        // format of JaggedAlliance
        // header
        // 0 - 0x10 - this file name (null padded)
        // 0x10 - unk count
        // 0x14 - num of embedded files
        // 0x18-0x20 - nulls/zero fields
        // 
        // file entries (1 for each embedded files)
        // 0 - 0x10 - this file name (null padded)
        // 0x10 - data start offset (byte address in this file)
        // 0x14 - data size
        // 0x18-0x20 - nulls/zero fields
        //
        // file data
        static bool MyIsPathRooted(string path)
        {
            bool isPath = false;
            try
            {
                isPath = Path.IsPathRooted(path);
            }
            catch (Exception)
            { }
            return isPath;
        }

        static bool IsAllPrint(string s)
        {
            if ((!Char.IsLetterOrDigit(s[0])) || (s.Length <= 1))
            {
                return false;
            }
            for (int i = 1; i < s.Length; ++i)
            {
                char ch = s[i];
                if ((ch < 0x28) || (ch >= 0x7e) || (ch == '='))
                {
                    return false;
                }
            }
            return true;
        }

        public class StrCmpLogicalComparer : IComparer<string>
        {
            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
            static extern int StrCmpLogicalW(String x, String y);

            public int Compare(string x, string y)
            {
                return StrCmpLogicalW(x, y);
            }
        }

        static void RunFFMPEG(string args, byte[] data)
        {
            string ffmpeg = Environment.GetEnvironmentVariable("FFMPEG");
            Debug.WriteLine(String.Format("Calling FFMPEG with args: {0}", args));
            ProcessStartInfo psi = new ProcessStartInfo(ffmpeg, args);
            psi.RedirectStandardInput = (data != null) ? true : false;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.WorkingDirectory = Environment.CurrentDirectory;
            psi.CreateNoWindow = true;
            using (Process p = Process.Start(psi))
            {
                if (data != null)
                {
                    p.StandardInput.BaseStream.Write(data, 0, data.Length);
                    p.StandardInput.Close();
                }
                string err = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0)
                {
                    Debug.WriteLine(String.Format("FFMPEG failure output was: {0}", err));
                }
            }
        }

        static void OutputWave(string inFileName, string outputFile, string inExt, byte[] data, bool is11khz)
        {
            if (File.Exists(outputFile))
            //if (!inFileName.EndsWith("voc", StringComparison.OrdinalIgnoreCase))
            {
                return;
                //Console.Write("Going to overwrite file {0}, OK? (Y)es, (N)o: ", outputFile);
                //string inp = Console.ReadLine();
                //if (inp.Length == 0 || inp[0] == 'N' || inp[0] == 'n')
                //{
                //    Console.WriteLine("Not extracting");
                //    return;
                //}
            }
            string inFormatArgs;
            // chop off the dot
            if (inExt[0] != 'V')
            {
                string codecExt = String.Empty;
                if (inExt != "8")
                {
                    codecExt = "le";
                }
                // pcm_u8 or pcm_u16le depending on if the extension is .8 or .16
                inFormatArgs = String.Format("-f u{0}{1} -ar {2} -ac 1", inExt, codecExt, is11khz ? "11025" : "22050");
            }
            else
            {
                inFormatArgs = "-f voc";
            }
            string args = String.Format("-hide_banner {0} -vn -i - -f wav -c:a pcm_s16le -ac 1 -ar 22050 -y \"{1}\"", inFormatArgs, outputFile);
            RunFFMPEG(args, data);
        }

        static void OutputStillImage(string inFileName, string outputFile, string inExt, byte[] data)
        {
            if (File.Exists(outputFile))
            {
                return;
            }
            string tempName = String.Format("T:\\temp\\ja{0}.{1}", Thread.CurrentThread.ManagedThreadId, inExt);
            switch (inExt)
            {
                case "PCC":
                case "PCX":
                {
                    File.WriteAllBytes(tempName, data);
                }
                break;
                default:
                {
                    throw new Exception("Unknown still image type");
                }
                break;
            }
            string args = String.Format("-hide_banner -an -i \"{0}\" -c:v png -y \"{1}\"", tempName, outputFile);
            RunFFMPEG(args, null);
            File.Delete(tempName);
        }

        static void OutputGif(string inFileName, string outputFile, string inExt, byte[] data)
        {
            if (File.Exists(outputFile))
            {
                return;
            }
            string inFormat;
            switch (inExt)
            {
                case "FLC":
                {
                    inFormat = "-f flic";
                }
                break;
                default:
                {
                    throw new Exception("Unknown animated image type");
                }
                break;
            }
            string args = String.Format("-hide_banner {0} -an -i - -f gif -y \"{1}\"", inFormat, outputFile);
            RunFFMPEG(args, data);
        }

        static void ExplodeJaggedAlliance(string file)
        {
            string explodeDir = Path.Combine(Path.GetDirectoryName(file), "exp-" + Path.GetFileName(file));
            string lowerFile = file.ToLowerInvariant();
            using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(file)))
            {
                string fileName = Path.GetFileName(file);
                BinaryReader br = new BinaryReader(ms, Encoding.ASCII);
                string s = new string(br.ReadChars(0x10)).TrimEnd('\0');
                if (s.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
                {
                    Console.WriteLine("Not processing {0} due to header containing invalid file name chars", fileName);
                    return;
                }
                //if (s.Length == 0 || (!(s.Equals(fileName, StringComparison.OrdinalIgnoreCase) || MyIsPathRooted(s) || IsAllPrint(s))))
                //{
                //    Console.WriteLine("Skipping {0} because it didn't start with {1}", file, fileName);
                //    return;
                //}
                Console.WriteLine("Processing {0}...", file);
                br.BaseStream.Seek(0x10, SeekOrigin.Begin);
                int fileDataSpaces = br.ReadInt32(); // generally 0x50 which * 0x20 = 0xa00 which is where the first file usually starts
                int numFiles = br.ReadInt32() - 1; // this seems to include the header section we've already read too
                br.ReadInt64(); // unk/zeroes
                Directory.CreateDirectory(explodeDir);
                for (int i = 0; i < numFiles; ++i)
                {
                    string packedFileName = new string(br.ReadChars(0x10)).TrimEnd('\0');
                    int fileStartPos = br.ReadInt32();
                    int fileLength = br.ReadInt32();
                    br.ReadInt64(); // unk / zeroes
                    string outputFile;
                    if (packedFileName.Length == 0) continue;
                    if (packedFileName.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
                    {
                        Console.WriteLine("Created file name '{0}' has invalid chars in it", packedFileName);
                        break;
                    }
                    outputFile = Path.Combine(explodeDir, packedFileName);
                    long currentLoc = br.BaseStream.Position;
                    br.BaseStream.Seek(fileStartPos, SeekOrigin.Begin);
                    byte[] dataBytes = br.ReadBytes(fileLength);
                    string packExt = Path.GetExtension(packedFileName);
                    if (!String.IsNullOrEmpty(packExt))
                    {
                        packExt = packExt.Substring(1).ToUpperInvariant();
                    }
                    if (packExt == "8" || packExt == "16" || packExt == "VOC")
                    {
                        // the snds and wpro directories are 11khz, whereas everthing else are 22khz
                        OutputWave(packedFileName, Path.ChangeExtension(outputFile, "wav"), packExt, dataBytes, lowerFile.Contains("\\snds") || lowerFile.Contains("\\wpro"));
                    }
                    else if (packExt == "PCX" || packExt == "PCC")
                    {
                        OutputStillImage(packedFileName, Path.ChangeExtension(outputFile, "png"), packExt, dataBytes);
                    }
                    else if (packExt == "FLC")
                    {
                        OutputGif(packedFileName, Path.ChangeExtension(outputFile, "gif"), packExt, dataBytes);
                    }
                    else
                    {
                        File.WriteAllBytes(outputFile, dataBytes);
                    }
                    br.BaseStream.Seek(currentLoc, SeekOrigin.Begin);
                    Console.WriteLine("\tWrote {0}", packedFileName);
                }
            }
        }

        static void EnumDir(string dir)
        {
            string[] entries = Directory.GetFileSystemEntries(dir);
            foreach (string entry in entries)
            {
                if (Directory.Exists(entry))
                {
                    if (!Path.GetFileName(entry).StartsWith("exp-"))
                    {
                        EnumDir(entry);
                    }
                }
                else
                {
                    ExplodeJaggedAlliance(entry);
                }
            }
        }

        static void CreateSounds(string dir, string outDir)
        {
            string[] entries = Directory.GetDirectories(dir, "exp-*");
            foreach (string d in entries)
            {
                CreateSounds(d, outDir);
            }
            string[] wavs = Directory.GetFiles(dir, "*.wav");
            if (wavs.Length == 0)
            {
                return;
            }
            Console.WriteLine("Creating sounds in dir {0}", dir);
            Array.Sort(wavs, new StrCmpLogicalComparer());
            using (StreamWriter sw = new StreamWriter(Path.Combine(dir, "concat.txt")))
            {
                foreach (string wav in wavs)
                {
                    if (wav.Contains("silence")) continue;
                    sw.WriteLine("file '{0}'", Path.GetFileName(wav));
                    sw.WriteLine("file 'silence.wav'");
                }
            }
            string silence = Path.Combine(dir, "silence.wav");
            if (!File.Exists(silence))
            {
                File.Copy(@"T:\silence_s16le.wav", silence);
            }
            string outWav = Path.Combine(outDir, String.Format("{0}.wav", Path.GetFileName(dir)));
            //if(File.Exists(outWav))
            //{
            //    Console.WriteLine("the output file {0} already exists", outWav);
            //    return;
            //}
            string args = String.Format("-hide_banner -f concat -i concat.txt -c:a copy -y \"{0}\"", outWav);
            Environment.CurrentDirectory = dir;
            RunFFMPEG(args, null);
        }

        static string GetDuration(string fName)
        {
            string args = String.Format("-hide_banner -i \"{0}\"", fName);
            ProcessStartInfo psi = new ProcessStartInfo("C:\\test\\ffmpeg\\ffprobe.exe", args);
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.WorkingDirectory = Environment.CurrentDirectory;
            string output = null;
            using (Process p = Process.Start(psi))
            {
                output = p.StandardError.ReadToEnd();
                p.WaitForExit();
            }
            Match m = Regex.Match(output, @"Duration\: (\d+\:\d+:\d+\.\d+)");
            return m.Groups[1].Value;
        }

        static void MakeCharacterStaticVideos()
        {
            string outDir = @"C:\Users\Adrian\Downloads\Jagged Alliance\wholesounds";
            Environment.CurrentDirectory = @"C:\Users\Adrian\Downloads\Jagged Alliance\exp-characters";
            string[] dirs = Directory.GetDirectories(".");
            const string videoArgs = "-loop 1 -framerate 2 -i \"{0}\" -c:v libx264 -preset medium -tune stillimage -crf 18 -an -threads auto -shortest -t {1} -y -f mpegts \"{2}\"";
            foreach (string w in dirs)
            {
                string fileName = Path.GetFileName(w);
                string outFile = Path.Combine(outDir, String.Format("{0}.ts", fileName));
                if (!File.Exists(outFile))
                {
                    string wavFile = Path.Combine(outDir, w + ".wav");
                    string wavDuration = GetDuration(wavFile);
                    string fNameNum = Regex.Match(w, @"exp-(\d+)").Groups[1].Value;
                    int fileNum = Convert.ToInt32(fNameNum) + 1;
                    string inputImage = String.Format(@"Resized-dg_{0}.png", fileNum);
                    string vidArgs = String.Format(videoArgs, inputImage, wavDuration, Path.ChangeExtension(outFile, ".ts"));
                    RunFFMPEG(vidArgs, null);
                }
            }
        }

        // this is for original jagged alliance
        static void MakeOtherOriginalStaticVideos()
        {
            string outDir = @"C:\Users\Adrian\Downloads\Jagged Alliance\wholesounds";
            Environment.CurrentDirectory = outDir;
            string wav = Path.Combine(outDir, "exp-charprobes.wav");
            const string videoArgs = "-loop 1 -framerate 2 -i \"{0}\" -c:v libx264 -preset medium -tune stillimage -crf 18 -an -threads auto -shortest -t {1} -y -f mpegts \"{2}\"";
            if (!File.Exists(Path.ChangeExtension(wav, ".ts")))
            {
                string wavDuration = GetDuration(wav);
                string inputImage = @"C:\Users\Adrian\Downloads\Jagged Alliance\exp-charprobes\probebig.png";
                string vidArgs = String.Format(videoArgs, inputImage, wavDuration, Path.ChangeExtension(wav, ".ts"));
                RunFFMPEG(vidArgs, null);
            }
            wav = Path.Combine(outDir, "exp-intro.wav");
            if (!File.Exists(Path.ChangeExtension(wav, ".ts")))
            {
                string wavDuration = GetDuration(wav);
                string inputImage = @"C:\Users\Adrian\Downloads\Jagged Alliance\exp-intro\introbig.png";
                string vidArgs = String.Format(videoArgs, inputImage, wavDuration, Path.ChangeExtension(wav, ".ts"));
                RunFFMPEG(vidArgs, null);
            }
            string[] wavs = Directory.GetFiles(".", "exp-Items-*.wav");
            foreach (string w in wavs)
            {
                string fileName = Path.ChangeExtension(w, ".ts");
                if (!File.Exists(fileName))
                {
                    string wavDuration = GetDuration(w);
                    string inputImage = @"C:\Users\Adrian\Downloads\Jagged Alliance\exp-inventory\itemsbig.png";
                    string vidArgs = String.Format(videoArgs, inputImage, wavDuration, Path.ChangeExtension(w, ".ts"));
                    RunFFMPEG(vidArgs, null);
                }
            }
        }

        static void MakeGusStaticVideos()
        {
            Environment.CurrentDirectory = @"C:\Users\Adrian\Downloads\Jagged Alliance\wholesounds";
            string[] wavs = Directory.GetFiles(".", "exp-95*.wav");
            const string videoArgs = "-loop 1 -framerate 2 -i \"{0}\" -c:v libx264 -preset medium -tune stillimage -crf 18 -an -threads auto -shortest -t {1} -y -f mpegts \"{2}\"";
            foreach (string w in wavs)
            {
                string fileName = Path.GetFileNameWithoutExtension(w);
                if (!File.Exists(String.Format("{0}.ts", fileName)))
                {
                    string wavDuration = GetDuration(w);
                    string inputImage = @"C:\Users\Adrian\Downloads\Jagged Alliance\exp-gus\gusbig.png";
                    string vidArgs = String.Format(videoArgs, inputImage, wavDuration, Path.ChangeExtension(w, ".ts"));
                    RunFFMPEG(vidArgs, null);
                }
            }
        }

        static void MakeMickyStaticVideos()
        {
            Environment.CurrentDirectory = @"C:\Users\Adrian\Downloads\Jagged Alliance\wholesounds";
            string[] wavs = Directory.GetFiles(".", "exp-96*.wav");
            const string videoArgs = "-loop 1 -framerate 2 -i \"{0}\" -c:v libx264 -preset medium -tune stillimage -crf 18 -an -threads auto -shortest -t {1} -y -f mpegts \"{2}\"";
            foreach (string w in wavs)
            {
                string fileName = Path.GetFileNameWithoutExtension(w);
                if (!File.Exists(String.Format("{0}.ts", fileName)))
                {
                    string wavDuration = GetDuration(w);
                    string inputImage = @"C:\Users\Adrian\Downloads\Jagged Alliance\exp-micky\mickybig.png";
                    string vidArgs = String.Format(videoArgs, inputImage, wavDuration, Path.ChangeExtension(w, ".ts"));
                    RunFFMPEG(vidArgs, null);
                }
            }
        }

        static void MakeItemsStaticVideos()
        {
            Environment.CurrentDirectory = @"C:\Users\Adrian\Downloads\Jagged Alliance\wholesounds";
            string[] wavs = Directory.GetFiles(".", "exp-items-*.wav");
            const string videoArgs = "-loop 1 -framerate 2 -i \"{0}\" -c:v libx264 -preset medium -tune stillimage -crf 18 -an -threads auto -shortest -t {1} -y -f mpegts \"{2}\"";
            foreach (string w in wavs)
            {
                string fileName = Path.GetFileNameWithoutExtension(w);
                if (!File.Exists(String.Format("{0}.ts", fileName)))
                {
                    string wavDuration = GetDuration(w);
                    string inputImage = @"C:\Users\Adrian\Downloads\Jagged Alliance\exp-inventory\itemsbig.png";
                    string vidArgs = String.Format(videoArgs, inputImage, wavDuration, Path.ChangeExtension(w, ".ts"));
                    RunFFMPEG(vidArgs, null);
                }
            }
        }

        static DateTime DurationToSeconds(string dur)
        {
            string[] parts = dur.Split(':');
            ulong[] multiplicators = { 3600, 60 };
            ulong partDur = 0;
            for (int i = 0; i < parts.Length - 1; ++i)
            {
                partDur += ((Convert.ToUInt64(parts[i]) * multiplicators[i]) * 100);
            }
            string lastPart = parts[parts.Length - 1];
            int dotPos = lastPart.IndexOf('.');
            lastPart = lastPart.Remove(dotPos, 1);
            partDur += Convert.ToUInt64(lastPart);
            return new DateTime();
        }

        static string SecondsToHMS(ulong seconds)
        {
            ulong h = seconds / (3600 * 100);
            ulong s = seconds % 10000;
            seconds /= 10000;
            return null;
        }

        static List<string> ParseNames()
        {
            List<string> names = new List<string>(100);
#if DEADLY_GAMES
            string fileName = @"C:\Users\Adrian\Downloads\Jagged Alliance\EDT\PROF.EDT";
            int startPoint = 0x22;
            int between = 0x84;
            int toName = 0;
            int numMercs = 70;
#else // ofiginal jagged alliance
            string fileName = @"C:\Users\Adrian\Downloads\Jagged Alliance\EDT\NEWPROF.EDT";
            int startPoint = 0;
            int between = 0x5a;
            int toName = 0xa;
            int numMercs = 64;
#endif
            byte[] profFile = File.ReadAllBytes(fileName);
            MemoryStream ms = new MemoryStream(profFile);
            StringBuilder sb = new StringBuilder();
            using(BinaryReader br = new BinaryReader(ms, Encoding.ASCII))
            {
                ms.Seek(startPoint, SeekOrigin.Begin);
                for(int i = 0; i < numMercs; ++i)
                {
                    long nameStartLoc = br.BaseStream.Position;
                    br.BaseStream.Seek(toName, SeekOrigin.Current);
                    byte b;
                    while((b = br.ReadByte()) != 0)
                    {
                        sb.Append(Convert.ToChar(b));
                    }
                    names.Add(sb.ToString());
                    sb.Length = 0;
                    br.BaseStream.Seek(nameStartLoc + between, SeekOrigin.Begin);
                }
            }
            return names;
        }

        static void ConcatBigVideos()
        {
            Environment.CurrentDirectory = @"C:\Users\Adrian\Downloads\Jagged Alliance\wholesounds";
            string[] wavs = Directory.GetFiles(".", "*.wav");
            Array.Sort(wavs, new StrCmpLogicalComparer());
            DateTime totalTime = new DateTime();
            List<string> names = ParseNames();
#if DEADLY_GAMES
            names.Add("Gus");
            names.Add("Micky");
            names.Add("Inventory/Items");
#else
            names.Add("Jack"); // 64
            names.Add("Brenda"); // 65
            names.Add("Santino"); // 66
            names.Add("Wall Probes");
            names.Add("Intro");
            names.Add("Inventory/Items");
#endif
            int namesCount = 0;
            bool seenItems = false;
#if DEADLY_GAMES
            bool seenGus = false;
            bool seenMicky = false;
#endif
            using (StreamWriter posWriter = new StreamWriter("positions.txt"))
            {
                using (StreamWriter sw = new StreamWriter("wavconcat.txt"))
                {
                    foreach (string w in wavs)
                    {
                        if (w.Contains("whole")) continue;
                        string noExt = Path.GetFileNameWithoutExtension(w);
                        sw.WriteLine("file '{0}'", Path.GetFileName(w));
                        bool writeName = false;
#if DEADLY_GAMES
                        // Gus
                        if (noExt.StartsWith("exp-95"))
                        {
                            if (!seenGus)
                            {
                                writeName = seenGus = true;
                            }
                        }
                        // Micky
                        else if (noExt.StartsWith("exp-96"))
                        {
                            if (!seenMicky)
                            {
                                writeName = seenMicky = true;
                            }
                        }
                        else if (!Char.IsLetter(noExt[noExt.Length - 1]))
                        {
#endif
                            if (noExt.Contains("Items"))
                            {
                                if (!seenItems)
                                {
                                    writeName = true;
                                    seenItems = true;
                                }
                            }
                            else writeName = true;
#if DEADLY_GAMES
                        }
#endif
                        if (writeName)
                        {
                            string posStr = totalTime.ToString("H:mm:ss");
                            posWriter.WriteLine("{0}: {1}", names[namesCount], posStr);
                            ++namesCount;
                        }
                        string dur = GetDuration(w);
                        TimeSpan durSpan = TimeSpan.Parse(dur);
                        totalTime = totalTime.Add(durSpan);
                    }
                }
            }
            string[] ts = Directory.GetFiles(".", "*.ts");
            Array.Sort(ts, new StrCmpLogicalComparer());
            Console.WriteLine("Concatenating ts files...");
            using (FileStream fsTs = new FileStream("whole.ts", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                foreach (string t in ts)
                {
                    if (t.Contains("whole")) continue;
                    Console.WriteLine("\t{0}", Path.GetFileName(t));
                    byte[] fileData = File.ReadAllBytes(t);
                    fsTs.Write(fileData, 0, fileData.Length);
                }
            }
            if (!File.Exists("whole.wav"))
            {
                Console.WriteLine("Concatenating wav files...");
                string vidArgs = "-f concat -i wavconcat.txt -c:a copy -vn whole.wav";
                RunFFMPEG(vidArgs, null);
                Console.WriteLine("Converting audio...");
                vidArgs = "-i whole.wav -c:a aac -b:a 128k -threads 4 whole.aac";
                RunFFMPEG(vidArgs, null);
                Console.WriteLine("Combining audio and video");
                vidArgs = "-i whole.ts -i whole.aac -c copy whole.mp4";
                RunFFMPEG(vidArgs, null);
            }
        }

        static void JaggedMain(string[] args)
        {
            //EnumDir(@"C:\Users\Adrian\Downloads\Jagged Alliance\");
            //
            //string outDir = @"C:\Users\Adrian\Downloads\Jagged Alliance\wholesounds";
            //CreateSounds(@"C:\Users\Adrian\Downloads\Jagged Alliance\", outDir);
            //
            //MakeCharacterStaticVideos();
            //MakeOtherOriginalStaticVideos();
            //MakeGusStaticVideos();
            //MakeMickyStaticVideos();
            //MakeItemsStaticVideos();
            ConcatBigVideos();
        }
    }
}
