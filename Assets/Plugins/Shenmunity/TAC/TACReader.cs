using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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
            new string[] { "MDP7", "MDC7", "HRCM"}, //MODEL,
            new string[] { "DTPK" },//SND
            new string[] {  "GBIX", "TEXN" }//PVR
        };

        public class TACEntry
        {
            public string m_path;
            public string m_name;
            public int m_offset;
            public int m_length;
        }

        static Dictionary<string, string> s_sources = new Dictionary<string, string>
        {
            { "Shenmue", "sm1/archives/dx11/data" },
            //{ "Shenmue2", "sm2/archives/dx11/data" },
        };

        static Dictionary<string, Dictionary<string, TACEntry>> m_files;
        static Dictionary<FileType, List<TACEntry>> m_byType;

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
            return s_shenmuePath + "/" + s_sources[p[0]] + "/" + p[1] + ".tac";
        }

        static public TACEntry GetEntry(string path)
        {
            string[] p = path.Split('/');
            return GetFiles()[GetTAC(path)][p[2]];
        }

        static public void SaveNames()
        {
            using (var file = File.CreateText("Names.txt"))
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
            if(!File.Exists("Names.txt"))
            {
                return;
            }
            using (var file = File.OpenText("Names.txt"))
            {
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    var ps = line.Split(new char[] { ' ' }, 2);
                    GetEntry(ps[0]).m_name = ps[1];
                }
            }
        }

        static public BinaryReader GetBytes(string path, out int length)
        {
            GetFiles();

            return GetBytes(GetTAC(path), GetEntry(path), out length);
        }

        static BinaryReader GetBytes(string file, TACEntry e, out int length)
        {
            var br = new BinaryReader(new FileStream(file, FileMode.Open));
            br.BaseStream.Seek(e.m_offset, SeekOrigin.Begin);
            length = e.m_length;
            return br;
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
            m_files[tac] = dir;

            shortForm = shortForm + "/" + Path.GetFileNameWithoutExtension(tac);

            using (BinaryReader reader = new BinaryReader(new FileStream(tadFile, FileMode.Open)))
            {
                //skip header
                reader.BaseStream.Seek(72, SeekOrigin.Current);

                while (true)
                {
                    var r = new TACEntry();

                    reader.BaseStream.Seek(4, SeekOrigin.Current); //skip padding (at file begin)
                    r.m_offset = BitConverter.ToInt32(reader.ReadBytes(4), 0);
                    reader.BaseStream.Seek(4, SeekOrigin.Current); //skip padding
                    r.m_length = BitConverter.ToInt32(reader.ReadBytes(4), 0);
                    reader.BaseStream.Seek(4, SeekOrigin.Current); //skip padding
                    var hash = BitConverter.ToString(reader.ReadBytes(4)).Replace("-", "");
                    reader.BaseStream.Seek(8, SeekOrigin.Current); //skip padding

                    r.m_path = shortForm + "/" + hash;

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
        
        static void BuildTypes()
        {
            m_byType = new Dictionary<FileType, List<TACEntry>>();

            foreach (var tac in m_files.Keys)
            {
                using (BinaryReader reader = new BinaryReader(new FileStream(tac, FileMode.Open)))
                {
                    foreach(var e in m_files[tac].Values)
                    {
                        reader.BaseStream.Seek(e.m_offset, SeekOrigin.Begin);
                        string type = Encoding.ASCII.GetString(reader.ReadBytes(4));
                        for(int i = 0; i < (int)FileType.COUNT; i++)
                        {
                            foreach(var id in s_identifier[i])
                            {
                                if(string.Compare(id, 0, type, 0, id.Length) == 0)
                                {
                                    GetFiles((FileType)i).Add(e);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}