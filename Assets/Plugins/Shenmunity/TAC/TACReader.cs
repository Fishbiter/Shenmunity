using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Shenmunity
{
    public class TACReader
    {
        public static string s_shenmuePath;

        public enum FileType
        {
            TEXTURE,
            IDX,
            AFS,
            WAV,
            DXBC,
            PAKS,
            PAKF,
            MODEL,
            SND,
            PVR,
            PAWN,
            CHRT,
            SCN3,

            COUNT
        }

        static string[][] s_identifier = new string[(int)FileType.COUNT][]
        {
            new string[] { "DDS" }, //TEXTURE,
            new string[] { "IDX" }, //IDX,
            new string[] { "AFS" }, //AFS,
            new string[] { "RIFF" }, //WAV,
            new string[] { "DXBC" }, //DXBC
            new string[] { "PAKS" }, //PAKS
            new string[] { "PAKF" }, //PAKF
            new string[] { "MDP7", "MDC7", "HRCM", "CHRM", "MAPM",
            //    "MDOX", //unlikely to be model data (maybe texture?)
            //    "MDLX",
            //    "MDCX",
            //    "MDPX"
            }, //MODEL,
            new string[] { "DTPK" },//SND
            new string[] { "GBIX", "TEXN" },//PVR
            new string[] { "PAWN" },//PAWN
            new string[] { "CHRT" },
            new string[] { "SCN3" },
        };

        public struct TextureEntry
        {
            public TACEntry m_file;
            public long m_postion;
        };

        static Dictionary<string, TextureEntry> s_textureLib = new Dictionary<string, TextureEntry>();
        static string s_textureNamespace = "";

        public class TACEntry
        {
            public string m_path;
            public string m_name;
            public string m_type;
            public FileType m_fileType;
            public uint m_offset;
            public uint m_length;
            public bool m_zipped;
            public TACEntry m_parent;

            public List<TACEntry> m_children;
        }

        static Dictionary<string, string> s_sources = new Dictionary<string, string>
        {
            { "Shenmue", "sm1/archives/dx11/data" },
           // { "Shenmue2", "sm2/archives/dx11/data" },
        };

        static string s_namesFile = "Assets/Plugins/Shenmunity/Names.txt";

        static Dictionary<string, Dictionary<string, TACEntry>> m_files;
        static Dictionary<string, string> m_tacToFilename = new Dictionary<string, string>();
        static Dictionary<FileType, List<TACEntry>> m_byType;
        static Dictionary<string, int> m_unknownTypes;
        static Dictionary<string, List<TACEntry>> m_modelToTAC = new Dictionary<string, List<TACEntry>>();
        static Dictionary<TACEntry, Byte[]> m_gzipCache = new Dictionary<TACEntry, Byte[]>();


        static public List<TACEntry> GetFiles(FileType type)
        {
            GetFiles();

            if (!m_byType.ContainsKey(type))
            {
                m_byType[type] = new List<TACEntry>();
            }

            return m_byType[type];
        }

        static void FindShenmue()
        {
            var steamPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null);
            if(string.IsNullOrEmpty(steamPath))
            {
                throw new FileNotFoundException("Couldn't find steam registry keys HKEY_CURRENT_USER\\Software\\Valve\\Steam\\SteamPath");
            }

            steamPath += "/" + "SteamApps";

            var libraryPaths = new List<string>();
            libraryPaths.Add(steamPath + "/common");

            var otherPathsFile = File.OpenText(steamPath + "/libraryfolders.vdf");
            string line;
            int libIndex = 1;
            while((line = otherPathsFile.ReadLine()) != null)
            {
                string[] param = line.Split('\t').Where(x => !string.IsNullOrEmpty(x)).ToArray();
                if(param[0] == "\"" + libIndex + "\"")
                {
                    libIndex++;
                    libraryPaths.Add(param[1].Replace("\"", "").Replace("\\\\", "/") + "/steamapps/common");
                }
            }

            foreach(var path in libraryPaths)
            {
                var smpath = path + "/" + "SMLaunch";
                if (Directory.Exists(smpath + "/" + s_sources["Shenmue"]))
                {
                    s_shenmuePath = smpath;
                    break;
                }
            }

            if(string.IsNullOrEmpty(s_shenmuePath))
            {
                throw new FileNotFoundException("Couldn't find shenmue installation in any steam library dir");
            }
        }

        static public string GetTAC(string path)
        {
            string[] p = path.Split('/');
            return s_shenmuePath + "/" + s_sources[p[0]] + "/" + m_tacToFilename[p[0]+"/"+p[1]];
        }

        static public TACEntry GetEntry(string path)
        {
            string[] p = path.Split('/');

            string tac = p[0] + "/" + p[1];

            if (GetFiles().ContainsKey(tac))
            {
                var tacContents = GetFiles()[tac];
                if(tacContents.ContainsKey(p[2]))
                {
                    return tacContents[p[2]];
                }
            }

            return null;
        }

        static public void SetTextureNamespace(string path)
        {
            s_textureNamespace = path;
        }

        static public void SaveNames()
        {
            using (var file = File.CreateText(s_namesFile))
            {
                foreach (var tac in GetFiles().Keys)
                {
                    foreach (var entry in m_files[tac].Values)
                    {
                        if (!string.IsNullOrEmpty(entry.m_name))
                        {
                            file.WriteLine(string.Format("{0} {1}", entry.m_path, entry.m_name));
                        }
                    }
                }
            }
        }

        static public void LoadNames()
        {
            if(!File.Exists(s_namesFile))
            {
                return;
            }
            using (var file = File.OpenText(s_namesFile))
            {
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    var ps = line.Split(new char[] { ' ' }, 2);
                    var e = GetEntry(ps[0]);
                    if (e != null)
                    {
                        e.m_name = ps[1];
                    }
                }
            }
        }

        static public BinaryReader GetBytes(string path, out uint length)
        {
            GetFiles();

            return GetBytes(GetTAC(path), GetEntry(path), out length);
        }

        static BinaryReader GetBytes(string file, TACEntry e, out uint length)
        {
            return new BinaryReader(new DebugStream(GetStream(file, e, out length)));
        }

        static Stream GetStream(string file, TACEntry e, out uint length)
        { 
            if(e.m_parent != null && e.m_parent.m_zipped)
            {
                var parent = GetStream(file, e.m_parent, out length);
                return new SubStream(parent, e.m_offset, e.m_length);
            }
            else
            {
                Stream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                stream = new SubStream(stream, e.m_offset, e.m_length);
                length = e.m_length;

                if(e.m_zipped)
                {
                    byte[] bytes;

                    if (m_gzipCache.ContainsKey(e))
                    {
                        bytes = m_gzipCache[e];
                    }
                    else
                    {
                        stream.Seek(-4, SeekOrigin.End);
                        length = new BinaryReader(stream).ReadUInt32();
                        stream.Seek(0, SeekOrigin.Begin);

                        var gzip = new GZipStream(stream, CompressionMode.Decompress);
                        bytes = new byte[length];
                        gzip.Read(bytes, 0, (int)length);

                        m_gzipCache[e] = bytes;
                    }

                    stream = new MemoryStream(bytes);
                }

                return stream;
            }
        }


        static void BuildFiles()
        {
            FindShenmue();

            m_files = new Dictionary<string, Dictionary<string, TACEntry>>();

            foreach(var s in s_sources)
            {
                BuildFilesInDirectory(s.Key, s.Value);
            }

            BuildTypes();

            LoadNames();
        }

        static void BuildFilesInDirectory(string shortForm, string dir)
        {
            string root = s_shenmuePath + "/" + dir;
            foreach (var fi in Directory.GetFiles(root))
            {
                if(fi.EndsWith(".tac"))
                {
                    BuildFilesInTAC(shortForm, fi.Replace("\\", "/"));
                }
            }
        }

        static void BuildFilesInTAC(string shortForm, string tac)
        {
            //Load tad file
            string tadFile = Path.ChangeExtension(tac, ".tad");

            var dir = new Dictionary<string, TACEntry>();

            string tacName = Path.GetFileNameWithoutExtension(tadFile);
            tacName = shortForm + "/" + tacName.Substring(0, tacName.IndexOf("_")); //remove hash (these change per release)
            
            m_files[tacName] = dir;
            m_tacToFilename[tacName] = Path.GetFileName(tac);

            using (BinaryReader reader = new BinaryReader(new FileStream(tadFile, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                //skip header
                reader.BaseStream.Seek(72, SeekOrigin.Current);

                while (true)
                {
                    var r = new TACEntry();

                    reader.BaseStream.Seek(4, SeekOrigin.Current); //skip padding (at file begin)
                    r.m_offset = reader.ReadUInt32();
                    reader.BaseStream.Seek(4, SeekOrigin.Current); //skip padding
                    r.m_length = reader.ReadUInt32();
                    reader.BaseStream.Seek(4, SeekOrigin.Current); //skip padding
                    var hash = BitConverter.ToString(reader.ReadBytes(4)).Replace("-", "");
                    reader.BaseStream.Seek(8, SeekOrigin.Current); //skip padding

                    r.m_path = tacName + "/" + hash;

                    dir[hash] = r;

                    if (reader.BaseStream.Position >= reader.BaseStream.Length) break; //TODO: check the missing values at EOF
                }
            }
        }

        static Dictionary<string, Dictionary<string, TACEntry>> GetFiles()
        {
            if (m_files == null)
            {
                BuildFiles();
            }
            return m_files;
        }
        
        static public void ExtractFile(TACEntry entry)
        {
            uint len;
            var br = GetBytes(entry.m_path, out len);
            var path = Directory.GetCurrentDirectory();
            path += "/" + entry.m_path + "." + entry.m_type;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, br.ReadBytes((int)len));
        }

        static void BuildTypes()
        {
            m_byType = new Dictionary<FileType, List<TACEntry>>();
            m_unknownTypes = new Dictionary<string, int>();

            foreach (var tac in m_files.Keys)
            {
                using (BinaryReader rawReader = new BinaryReader(new FileStream(GetTAC(tac), FileMode.Open, FileAccess.Read, FileShare.Read)))
                {
                    foreach (var e in m_files[tac].Values.ToArray())
                    {
                        var reader = rawReader;
                        reader.BaseStream.Seek(e.m_offset, SeekOrigin.Begin);
                        var header = reader.ReadBytes(4);
                        string type = Encoding.ASCII.GetString(header);

                        bool zipped = header[0] == 0x1f && header[1] == 0x8b;
                        if (zipped)
                        {
                            reader.BaseStream.Seek(-4, SeekOrigin.Current);
                            reader = new BinaryReader(new GzipWithSeek(reader.BaseStream, CompressionMode.Decompress));
                            header = reader.ReadBytes(4);
                            type = Encoding.ASCII.GetString(header);

                            e.m_zipped = true;
                        }

                        e.m_type = type;

                        AddEntryToType(type, e);

                        if (type == "PAKS")
                        {
                            ReadPAKS(tac, e, reader);
                        }
                        else if(type == "PAKF")
                        {
                            ReadPAKF(tac, e, reader);
                        }
                    }
                }
            }
        }

        static void AddEntryToType(string type, TACEntry e)
        {
            for (int i = 0; i < (int)FileType.COUNT; i++)
            {
                foreach (var id in s_identifier[i])
                {
                    if (string.Compare(id, 0, type, 0, id.Length) == 0)
                    {
                        GetFiles((FileType)i).Add(e);
                        e.m_name = type;
                        e.m_fileType = (FileType)i;
                        return;
                    }
                }
            }

            if (!m_unknownTypes.ContainsKey(type))
                m_unknownTypes[type] = 0;
            m_unknownTypes[type]++;
        }

        static void ReadPAKS(string tac, TACEntry parent, BinaryReader r)
        {
            uint paksSize = r.ReadUInt32();
            uint c1 = r.ReadUInt32();
            uint c2 = r.ReadUInt32();

            ReadIPAC(tac, parent, r);
        }

        static void ReadPAKF(string tac, TACEntry parent, BinaryReader r)
        {
            uint pakfSize = r.ReadUInt32();
            uint c1 = r.ReadUInt32();
            uint numTextures = r.ReadUInt32();
            string magic = null;

            if(numTextures > 0)
            {
                do
                {
                    long blockStart = r.BaseStream.Position;
                    magic = Encoding.ASCII.GetString(r.ReadBytes(4));
                    long end = blockStart + r.ReadUInt32();
                    switch (magic)
                    {
                        case "DUMY":
                            break;
                        case "TEXN":
                            uint number = r.ReadUInt32();
                            string name = Encoding.ASCII.GetString(r.ReadBytes(4)) + number;
                            var texEntry = new TextureEntry();
                            texEntry.m_file = parent;
                            texEntry.m_postion = blockStart + 8;
                            if(!parent.m_zipped)
                            {
                                texEntry.m_postion -= parent.m_offset;
                            }

                            s_textureLib[name] = texEntry;
                            s_textureLib[parent.m_path + name] = texEntry;
                            break;

                    }
                    if (magic[0] == 0 || magic == "IPAC")
                    {
                        break;
                    }
                    r.BaseStream.Seek(end, SeekOrigin.Begin);
                }
                while (true);
            }
            r.BaseStream.Seek((parent.m_zipped ? 0 : parent.m_offset) + pakfSize, SeekOrigin.Begin);
            ReadIPAC(tac, parent, r);
        }

        static void ReadIPAC(string tac, TACEntry parent, BinaryReader r)
        {
            var parentHash = parent.m_path.Split('/')[2];

            long basePos = r.BaseStream.Position;

            string magic = Encoding.ASCII.GetString(r.ReadBytes(4));
            if (magic != "IPAC")
            {
                return;
            }
            uint size1 = r.ReadUInt32();
            uint num = r.ReadUInt32();
            uint size2 = r.ReadUInt32();

            r.BaseStream.Seek(size1 - 16, SeekOrigin.Current);

            parent.m_children = new List<TACEntry>();

            for (int i = 0; i < num; i++)
            {
                string fn = Encoding.ASCII.GetString(r.ReadBytes(8)).Trim('\0');
                string ext = Encoding.ASCII.GetString(r.ReadBytes(4)).Trim('\0');
                uint ofs = r.ReadUInt32();
                uint length = r.ReadUInt32();

                TACEntry newE = new TACEntry();

                newE.m_path = parent.m_path + "_" + fn;
                newE.m_name = fn + "." + ext;
                newE.m_offset = (uint)(ofs + basePos);
                if (!parent.m_zipped)
                {
                    newE.m_offset -= parent.m_offset;
                }
                newE.m_length = length;
                newE.m_parent = parent;
                newE.m_type = ext;

                string hash = parentHash + "_" + fn;
                int fnIndex = 1;

                while (m_files[tac].ContainsKey(hash))
                {
                    hash = parentHash + "_" + fn + fnIndex;
                    newE.m_path = parent.m_path + "_" + fn + fnIndex;
                    fnIndex++;
                }

                m_files[tac].Add(hash, newE);
                parent.m_children.Add(newE);
                AddEntryToType(ext, newE);

                if (newE.m_fileType == FileType.MODEL)
                {
                    if (!m_modelToTAC.ContainsKey(fn))
                    {
                        m_modelToTAC[fn] = new List<TACEntry>();
                    }
                    m_modelToTAC[fn].Add(parent);
                }

                //ExtractFile(newE);
            }
        }

        static public BinaryReader GetTextureAddress(string name)
        {
            var e = s_textureLib.ContainsKey(s_textureNamespace + name) ? s_textureLib[s_textureNamespace + name] : s_textureLib[name];
            uint len = 0;
            var br = GetBytes(e.m_file.m_path, out len);
            br.BaseStream.Seek(e.m_postion, SeekOrigin.Current);
            return br;
        }

        //AFAICT pakf->paks joining is probably done by filename. Since we don't have filenames (curse you TAC) just find PAKS that contain the entities we're after...
        static public List<TACEntry> GetPAKSCandidates(IEnumerable<string> models)
        {
            List<TACEntry> candidates = null;
            foreach (var model in models)
            {
                if (!m_modelToTAC.ContainsKey(model))
                    continue;

                var list = m_modelToTAC[model];
                if (candidates == null)
                {
                    candidates = list.ToList();
                }
                else
                {
                    candidates.RemoveAll(x => !list.Contains(x));
                }
                if (candidates.Count == 0)
                {
                    return candidates;
                }
            }
            return candidates;
        }
    }
}