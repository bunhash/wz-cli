using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using MapleLib.WzLib;
using MapleLib.WzLib.Serialization;
using MapleLib.WzLib.Util;

namespace wz
{
    class Archiver
    {
        private WzFile wzFile;
        private WzDirectory root;
        private string filename;

        public Archiver(string filename, short gameVersion, WzMapleVersion targetVersion)
        {
            this.filename = filename;
            this.wzFile = new WzFile(gameVersion, targetVersion);
            this.wzFile.WzDirectory.Name = Path.GetFileName(this.filename);
            this.root = wzFile.WzDirectory;
        }

        public void AddImage(string path, string filename, WzMapleVersion version)
        {
            WzDirectory parent = CreateDirectory(path.Split('/'));
            if (parent == null)
                throw new DirectoryNotFoundException("Parent directory does not exist");
            string imageName = Path.GetFileName(filename);
            WzImgDeserializer loader = new WzImgDeserializer(false);
            bool success;
            WzImage image = loader.WzImageFromIMGFile(filename, WzTool.GetIvByMapleVersion(version), imageName, out success);
            if (!success)
                throw new Exception("Error parsing WzImage");
            parent.AddImage(image);
        }

        public void Archive()
        {
            this.wzFile.SaveToDisk(this.filename, this.wzFile.MapleVersion);
        }

        internal WzDirectory CreateDirectory(string[] path)
        {
            if (path.Length == 0)
                return null;
            if (!root.Name.Equals(path[0]))
                throw new DirectoryNotFoundException("Root directory mismatch: " + path[0]);
            WzDirectory focus = root;
            bool createDirectory = true;
            for (int i = 1; i < path.Length; ++i)
            {
                foreach (WzDirectory child in focus.WzDirectories)
                {
                    if (child.Name.Equals(path[i]))
                    {
                        focus = child;
                        createDirectory = false;
                        break;
                    }
                }
                if (createDirectory)
                {
                    WzDirectory nextDir = new WzDirectory(path[i], this.wzFile);
                    focus.AddDirectory(nextDir);
                    focus = nextDir;
                }
            }
            return focus;
        }
    }
}
