using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Shenmunity
{
    public class Model
    {
        BinaryReader m_reader;
        int m_textureStart;
        long m_base;

        public Dictionary<uint, Node> m_nodes = new Dictionary<uint, Node>();

        public List<Texture> m_textures = new List<Texture>();

        public class Face
        {
            public int m_texture;
            public List<FaceVert> m_faceVerts = new List<FaceVert>();
        }

        public class FaceVert
        {
            public int m_vertIndex;
            public Vector2 m_uv;
        }

        public class Texture
        {
            public PVRType m_type;

            public uint m_width;
            public uint m_height;

            public Color[] m_texels;
        }


        public enum PVRType
        {
            ARGB1555,// (bilevel translucent alpha 0,255)
            RGB565, //(no translucent)
            ARGB4444, //(translucent alpha 0-255)
            YUV442,
            Bump,
            Bit4,
            Bit8,
        }

        enum PVRFormat
        {
            SQUARE_TWIDDLED = 1,
            SQUARE_TWIDDLED_MIPMAP = 2,
            VQ = 3,
            VQ_MIPMAP = 4,
            CLUT_TWIDDLED_8BIT = 5,
            CLUT_TWIDDLED_4BIT = 6,
            DIRECT_TWIDDLED_8BIT = 7,
            DIRECT_TWIDDLED_4BIT = 8,
            RECTANGLE = 9,
            RECTANGULAR_STRIDE = 0xb,
            RECTANGULAR_TWIDDLED = 0xd,
            SMALL_VQ = 0x10,
            SMALL_VQ_MIPMAP = 0x11,
            SQUARE_TWIDDLED_MIPMAP_2 = 0x12,
        }

        public class Node
        {
            public Node(BinaryReader br)
            {
                nodeId = br.ReadUInt32();
                meshData = br.ReadUInt32();
                rotX = br.ReadSingle();
                rotY = br.ReadSingle();
                rotZ = br.ReadSingle();
                scaleX = br.ReadSingle();
                scaleY = br.ReadSingle();
                scaleZ = br.ReadSingle();
                x = br.ReadSingle();
                y = br.ReadSingle();
                z = br.ReadSingle();
                child = br.ReadUInt32();
                next = br.ReadUInt32();
                up = br.ReadUInt32();
                objectName = br.ReadUInt32();
                unknown5 = br.ReadUInt32();
                nextObject = br.ReadUInt32();
            }

            uint nodeId;
            public uint meshData;
            public float rotX;
            public float rotY;
            public float rotZ;
            public float scaleX, scaleY, scaleZ;
            public float x, y, z;
            public uint child;
            public uint next;
            public uint up;
            uint objectName; //4 bytes
            uint unknown5;
            public uint nextObject;

                    public int m_totalFaceVerts = 0;

        public List<Face> m_faces = new List<Face>();
        
        public List<Vector3> m_pos = new List<Vector3>();
        public List<Vector3> m_norm = new List<Vector3>();
        }

        class ObjectFooter
        {
            public ObjectFooter(BinaryReader br)
            {
                empty = br.ReadUInt32();
                endOfPreviousFaces = br.ReadUInt32();
                verticesNumber = br.ReadInt32();
                endOfPreviousFooter = br.ReadUInt32();
                f1 = br.ReadSingle();
                f2 = br.ReadSingle();
                f3 = br.ReadSingle();
                f4 = br.ReadSingle();
            }

            uint empty;
            public uint endOfPreviousFaces;
            public int verticesNumber;
            public uint endOfPreviousFooter;
            public float f1;
            public float f2;
            public float f3;
            public float f4;
        }

        class FaceHeader
        {
            public FaceHeader(BinaryReader br)
            {
                p0 = br.ReadInt16();
                mustBe16 = br.ReadInt16();
                p2 = br.ReadInt16();
                p3 = br.ReadInt16();
                p4 = br.ReadInt16();
                p5 = br.ReadInt16();
                p6 = br.ReadInt16();
                p7 = br.ReadInt16();
                p8 = br.ReadInt16();
                p9 = br.ReadInt16();
                p10 = br.ReadInt16();
                textureNumber = br.ReadInt16();
                p12 = br.ReadInt16();
                p13 = br.ReadInt16();
                p14 = br.ReadInt16();
                blockSize = br.ReadInt16();
                p16 = br.ReadInt16();
            }

            public short p0;
            public short mustBe16;
            public short p2;
            public short p3;
            public short p4;
            public short p5;
            public short p6;
            public short p7;
            public short p8;
            public short p9;
            public short p10;
            public short textureNumber;
            public short p12;
            public short p13;
            public short p14;
            public short blockSize;
            public short p16;
        }

        public Model(string path)
        {
            int len = 0;

            using (m_reader = TACReader.GetBytes(path, out len))
            {
                m_base = m_reader.BaseStream.Seek(0, SeekOrigin.Current);

                var bytes = m_reader.ReadBytes(len);
                File.WriteAllBytes("model.bin", bytes);

                Seek(-len, SeekOrigin.Current);

                uint pos = ReadHeader();
                GetNode(pos);

                Seek(m_textureStart, SeekOrigin.Begin);
                ReadTextures();
            }
        }

        Node GetNode(uint pos)
        {
            if (m_nodes.ContainsKey(pos))
                return m_nodes[pos];

            Seek(pos, SeekOrigin.Begin);

            Node obj = ReadObjectHeader();
            if (obj.meshData != 0)
            {
                Seek(obj.meshData, SeekOrigin.Begin);
                var footer = new ObjectFooter(m_reader);
                Seek(footer.endOfPreviousFooter + 16, SeekOrigin.Begin);
                ReadFaces(obj, footer.endOfPreviousFaces);
                Seek(footer.endOfPreviousFaces, SeekOrigin.Begin);
                ReadVertices(obj, footer.verticesNumber);
            }
            m_nodes[pos] = obj;
            if (obj.child != 0)
            {
                GetNode(obj.child);
            }
            if (obj.next != 0)
            {
                GetNode(obj.next);
            }
            if (obj.up != 0)
            {
                GetNode(obj.up);
            }

            return obj;
        }

        void Seek(long pos, SeekOrigin origin)
        {
            if(origin == SeekOrigin.Current)
            {
                m_reader.BaseStream.Seek(pos, SeekOrigin.Current);
            }
            else if(origin == SeekOrigin.Begin)
            {
                m_reader.BaseStream.Seek(pos + m_base, SeekOrigin.Begin);
            }
        }

        long GetPos()
        {
            return m_reader.BaseStream.Seek(0, SeekOrigin.Current) - m_base;
        }
        

        Node ReadObjectHeader()
        {
            return new Node(m_reader);
        }

        uint ReadHeader()
        {
            m_reader.BaseStream.Seek(4, SeekOrigin.Current);
            m_textureStart = m_reader.ReadInt32();
            return m_reader.ReadUInt32();
        }

        void ReadVertices(Node node, int count)
        {
            for(int i = 0; i < count; i++)
            {
                Vector3 pos;
                pos.x = m_reader.ReadSingle();
                pos.y = m_reader.ReadSingle();
                pos.z = m_reader.ReadSingle();
                node.m_pos.Add(pos);

                Vector3 norm;
                norm.x = m_reader.ReadSingle();
                norm.y = m_reader.ReadSingle();
                norm.z = m_reader.ReadSingle();
                node.m_norm.Add(norm);
            }
        }

        void ReadFaces(Node node, UInt32 end)
        {
            while(GetPos() < end)
            {
                var faceHeader = new FaceHeader(m_reader);
                if (faceHeader.mustBe16 != 0x10)
                    break;

                long blockEnd = GetPos() + faceHeader.blockSize - 4;

                while (GetPos() < blockEnd)
                {
                    var face = new Face();
                    face.m_texture = faceHeader.textureNumber;

                    node.m_faces.Add(face);

                    var data = m_reader.ReadUInt16();
                    for (int i = 0; i < 0xffff - data + 1; i++)
                    {
                        var fv = new FaceVert();
                        fv.m_vertIndex = m_reader.ReadInt16();
                        fv.m_uv.x = (float)(m_reader.ReadInt16() & 0x3ff) / 0x3ff;
                        fv.m_uv.y = (float)(m_reader.ReadInt16() & 0x3ff) / 0x3ff;
                        face.m_faceVerts.Add(fv);
                        node.m_totalFaceVerts++;
                    }
                }
            }
        }

        class PVRT
        {
            public PVRT(BinaryReader br)
            {
                len = br.ReadUInt32();
                type = (PVRType)br.ReadByte();
                format = (PVRFormat)br.ReadByte();
                br.BaseStream.Seek(2, SeekOrigin.Current);
                width = br.ReadUInt16();
                height = br.ReadUInt16();

                if(format == PVRFormat.VQ)
                {
                    var palette = new Color[1024];
                    for(int i = 0; i < palette.Length; i++)
                    {
                        palette[i] = ReadColor(br);
                    }
                    var bytes = new byte[width * height / 4];
                    for (int i = 0; i < width * height / 4; i++)
                    {
                        bytes[i] = br.ReadByte();
                    }
                    DecodeVQ(bytes, palette);
                }
                else if(type == PVRType.RGB565 || type == PVRType.ARGB1555 || type == PVRType.ARGB4444)
                {
                    texels = new Color[width * height];
                    for(int i = 0; i < width * height; i++)
                    {
                        texels[i] = ReadColor(br);
                    }
                }
            }

            void DecodeVQ(byte[] source, Color[] palette)
            {
                int[] swizzleMap = new int[width];

                for (int i = 0; i < width; i++)
                {
                    swizzleMap[i] = 0;

                    for (int j = 0, k = 1; k <= i; j++, k <<= 1)
                    {
                        swizzleMap[i] |= (i & k) << j;
                    }
                }

                texels = new Color[width * height];

                for (int y = 0; y < height; y += 2)
                {
                    for (int x = 0; x < width; x += 2)
                    {
                        int index = (source[(swizzleMap[x >> 1] << 1) | swizzleMap[y >> 1]]) * 4;

                        for (int x2 = 0; x2 < 2; x2++)
                        {
                            for (int y2 = 0; y2 < 2; y2++)
                            {
                                long destinationIndex = ((y + y2) * width) + (x + x2);

                                texels[destinationIndex] = palette[index];

                                index++;
                            }
                        }
                    }
                }
            }

            private void Unswizzle()
            {
                int[] swizzleMap = new int[width];

                for (int i = 0; i < width; i++)
                {
                    swizzleMap[i] = 0;

                    for (int j = 0, k = 1; k <= i; j++, k <<= 1)
                    {
                        swizzleMap[i] |= (i & k) << j;
                    }
                }

                var newTexels = new Color[width * height];

                for (int y = 0; y < height; y+=2)
                {
                    for (int x = 0; x < width; x+=2)
                    {
                        int index = (swizzleMap[x >> 1] << 1) | swizzleMap[y >> 1];

                        for (int x2 = 0; x2 < 2; x2++)
                        {
                            for (int y2 = 0; y2 < 2; y2++)
                            {
                                long destinationIndex = ((y + y2) * width) + (x + x2);

                                newTexels[destinationIndex] = texels[index];

                                index++;
                            }
                        }
                    }
                }

                texels = newTexels;
            }

            float Comp(ushort val, int shift, int bits)
            {
                return (float)((val >> shift) & ((1 << bits) - 1)) / ((1 << bits) - 1);
            }

            Color ReadColor(BinaryReader br)
            {
                switch (type)
                {
                    case PVRType.RGB565:
                        {
                            ushort val = br.ReadUInt16();
                            return new Color(Comp(val, 11, 5), Comp(val, 5, 6), Comp(val, 0, 5));
                        }
                    case PVRType.ARGB1555:
                        {
                            ushort val = br.ReadUInt16();
                            return new Color(Comp(val, 10, 5), Comp(val, 5, 5), Comp(val, 0, 5), Comp(val, 15, 1));
                        }
                    case PVRType.ARGB4444:
                        {
                            ushort val = br.ReadUInt16();
                            return new Color(Comp(val, 8, 4), Comp(val, 4, 4), Comp(val, 0, 4), Comp(val, 12, 4));
                        }
                }
                return Color.magenta;
            }

            public uint len;
            public PVRType type;
            public PVRFormat format;
            public uint width;
            public uint height;

            public Color[] texels;
        }


        void ReadTextures()
        {
            Seek(4, SeekOrigin.Current); //"TEXD"
            Seek(4, SeekOrigin.Current); //Unknown
            int numberTextures = m_reader.ReadInt32();

            for(int i = 0; i < numberTextures; i++)
            {
                Seek(4, SeekOrigin.Current); //"TEXN"
                long end = GetPos() + m_reader.ReadInt32() - 4;
                Seek(4, SeekOrigin.Current); //unknown (address?)
                Seek(4, SeekOrigin.Current); //Unknown (address?)
                Seek(4, SeekOrigin.Current); //"GBIX"
                long gbixLen = m_reader.ReadInt32();
                long gbix = m_reader.ReadInt64();

                var pvrt = new PVRT(m_reader);

                var tex = new Texture();
                tex.m_type = pvrt.type;
                tex.m_width = pvrt.width;
                tex.m_height = pvrt.height;
                tex.m_texels = pvrt.texels;
                m_textures.Add(tex);

                Seek(end, SeekOrigin.Begin);
            }
        }
    }
}
