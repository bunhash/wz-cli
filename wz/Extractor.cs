using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;

namespace wz
{
    class Extractor
    {
        private WzFile wzFile;
        private WzObject root;

        public ulong Size { get; private set; }

        public Extractor(string filename, WzMapleVersion version)
        {
            this.wzFile = new WzFile(filename, version);
            string parseErrorMessage = string.Empty;
            bool res = this.wzFile.ParseWzFile(out parseErrorMessage);
            if (!res)
                throw new FileLoadException(parseErrorMessage);

            // Root directory
            this.root = this.wzFile.WzDirectory;
            this.root.Name = StripEnd(root.Name, ".wz");
            this.Size = Extractor.CountImages(this.root);
        }

        internal static ulong CountImages(WzObject wzobj)
        {
            ulong size = 0;
            if (wzobj is WzDirectory)
            {
                foreach (WzDirectory dir in ((WzDirectory)wzobj).WzDirectories)
                    size += Extractor.CountImages(dir);
                foreach (WzImage img in ((WzDirectory)wzobj).WzImages)
                    size += 1;
            }
            return size;
        }

        public void ExtractAll(string targetDirectory = ".")
        {
            foreach ((ulong num, WzImage wzimg, string directory) in this.GetImageIterator())
                this.ExtractImage(wzimg, Path.Combine(targetDirectory, directory));
        }

        public void ExtractImageFiles(string targetDirectory = ".")
        {
            foreach ((ulong num, WzImage wzimg, string directory) in this.GetImageIterator())
                this.SaveImage(wzimg, Path.Combine(targetDirectory, directory));
        }

        public IEnumerable<(ulong, WzImage, string)> GetImageIterator()
        {
            Stack<(WzObject, string)> frontier = new Stack<(WzObject, string)>();
            ulong count = 0;
            frontier.Push((this.root, string.Empty));
            while (frontier.Count > 0)
            {
                (WzObject wzobj, string directory) = frontier.Pop();
                if (wzobj is WzDirectory)
                {
                    foreach (WzDirectory dir in ((WzDirectory)wzobj).WzDirectories)
                        frontier.Push((dir, Path.Combine(directory, wzobj.Name)));
                    foreach (WzImage img in ((WzDirectory)wzobj).WzImages)
                        yield return (count++, img, Path.Combine(directory, wzobj.Name));
                }
            }
        }

        public void SaveImage(WzImage wzimg, string targetDirectory)
        {
            string filename = Path.Combine(targetDirectory, wzimg.Name);
            Directory.CreateDirectory(targetDirectory);
            WzBinaryWriter writer = new WzBinaryWriter(File.Create(filename), WzTool.GetIvByMapleVersion(this.wzFile.MapleVersion));
            wzimg.SaveImage(writer);
            wzimg.UnparseImage();
        }

        public void ExtractImage(WzImage wzimg, string targetDirectory)
        {
            string directory = Path.Combine(targetDirectory, StripEnd(wzimg.Name, ".img"));
            ImageExtractor extractor = new ImageExtractor(wzimg);
            extractor.Extract(directory);
        }

        internal static string StripEnd(string source, string pattern)
        {
            if (source.EndsWith(pattern))
                return source.Substring(0, source.Length - pattern.Length);
            return source;
        }
    }

    class ImageExtractor
    {
        private static string RESOURCES = "res";
        private WzImage wzImage;

        public bool Verbose { get; set; }

        public ImageExtractor(string filename, WzMapleVersion version)
        {
            this.wzImage = new WzImage(Path.GetFileName(filename), File.OpenRead(filename), version);
        }

        public ImageExtractor(string name, Stream stream, WzMapleVersion version)
        {
            this.wzImage = new WzImage(name, stream, version);
        }

        public ImageExtractor(WzImage wzImage)
        {
            this.wzImage = wzImage;
        }

        public void Extract(string targetDirectory = ".")
        {
            Directory.CreateDirectory(targetDirectory);
            this.wzImage.ParseImage();
            foreach (WzImageProperty prop in this.wzImage.WzProperties)
            {
                try
                {
                    string filename = Path.Combine(targetDirectory, prop.Name + ".xml");
                    using (TextWriter tw = new StreamWriter(File.Create(filename)))
                    {
                        XDocument doc = new XDocument(
                            new XDeclaration("1.0", "utf-8", "yes"),
                            BuildImagePropertyXML(prop, targetDirectory, prop.Name));
                        doc.Save(tw);
                    }
                }
                catch (ArgumentException e)
                {
                    Console.WriteLine("Error: ArgumentException - {0}", e.Message);
                }
            }
            this.wzImage.UnparseImage();
        }

        internal static XElement BuildImagePropertyXML(WzImageProperty prop, string directory, string resPrefix)
        {
            XElement res = CreateXMLElement(prop, directory, resPrefix);
            if (res == null)
                throw new FormatException("Invalid WzObject type");
            if (prop.WzProperties != null)
                foreach (WzImageProperty child in prop.WzProperties)
                    res.Add(BuildImagePropertyXML(child, directory, resPrefix + "-" + child.Name));
            return res;
        }

        internal static XElement CreateXMLElement(WzImageProperty prop, string directory, string resPrefix)
        {
            if (prop is WzSubProperty imgdirProp)
            {
                XElement imgdirElement = new XElement("imgdir");
                imgdirElement.SetAttributeValue("name", imgdirProp.Name);
                return imgdirElement;
            }
            else if (prop is WzShortProperty shortProp)
            {
                XElement shortElement = new XElement("short");
                shortElement.SetAttributeValue("name", shortProp.Name);
                shortElement.SetAttributeValue("value", shortProp.Value);
                return shortElement;
            }
            else if (prop is WzIntProperty intProp)
            {
                XElement intElement = new XElement("int");
                intElement.SetAttributeValue("name", intProp.Name);
                intElement.SetAttributeValue("value", intProp.Value);
                return intElement;
            }
            else if (prop is WzLongProperty longProp)
            {
                XElement longElement = new XElement("long");
                longElement.SetAttributeValue("name", longProp.Name);
                longElement.SetAttributeValue("value", longProp.Value);
                return longElement;
            }
            else if (prop is WzFloatProperty floatProp)
            {
                XElement floatElement = new XElement("float");
                floatElement.SetAttributeValue("name", floatProp.Name);
                floatElement.SetAttributeValue("value", floatProp.Value);
                return floatElement;
            }
            else if (prop is WzDoubleProperty doubleProp)
            {
                XElement doubleElement = new XElement("double");
                doubleElement.SetAttributeValue("name", doubleProp.Name);
                doubleElement.SetAttributeValue("value", doubleProp.Value);
                return doubleElement;
            }
            else if (prop is WzStringProperty stringProp)
            {
                XElement stringElement = new XElement("string");
                stringElement.SetAttributeValue("name", stringProp.Name);
                stringElement.SetAttributeValue("value", stringProp.Value);
                return stringElement;
            }
            else if (prop is WzUOLProperty uolProp)
            {
                XElement uolElement = new XElement("uol");
                uolElement.SetAttributeValue("name", uolProp.Name);
                uolElement.SetAttributeValue("value", uolProp.Value);
                return uolElement;
            }
            else if (prop is WzVectorProperty vectorProp)
            {
                XElement vectorElement = new XElement("vector");
                vectorElement.SetAttributeValue("name", vectorProp.Name);
                vectorElement.SetAttributeValue("x", vectorProp.X.Value);
                vectorElement.SetAttributeValue("y", vectorProp.Y.Value);
                return vectorElement;
            }
            else if (prop is WzConvexProperty extendedProp)
            {
                XElement extendedElement = new XElement("extended");
                extendedElement.SetAttributeValue("name", extendedProp.Name);
                return extendedElement;
            }
            else if (prop is WzCanvasProperty canvasProp)
            {
                // Create node
                XElement canvasElement = new XElement("canvas");
                canvasElement.SetAttributeValue("name", canvasProp.Name);

                bool savePng = !canvasProp.HaveInlinkProperty() && !canvasProp.HaveOutlinkProperty();
                foreach (WzImageProperty child in canvasProp.WzProperties)
                    if (child is WzStringProperty stringChild)
                        // PNG source is elsewhere...
                        if (stringChild.Name.Equals("source"))
                            savePng = false;
                if (savePng)
                {
                    // Save resource
                    string res = Path.Combine(directory, RESOURCES);
                    Directory.CreateDirectory(res);
                    string pngName = resPrefix + ".png";
                    using (FileStream stream = File.Create(Path.Combine(res, pngName)))
                        canvasProp.PngProperty.GetImage(false).Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    canvasElement.SetAttributeValue("src", RESOURCES + "/" + pngName);
                    canvasElement.SetAttributeValue("format", canvasProp.PngProperty.Format);
                }

                return canvasElement;
            }
            else if (prop is WzBinaryProperty soundProp)
            {
                // Save resource
                string res = Path.Combine(directory, RESOURCES);
                Directory.CreateDirectory(res);
                string soundName = resPrefix + ".mp3";
                soundProp.SaveToFile(Path.Combine(res, soundName));

                // Create node
                XElement soundElement = new XElement("sound");
                soundElement.SetAttributeValue("name", soundProp.Name);
                /*
                soundElement.SetAttributeValue("length", soundProp.Length);
                soundElement.SetAttributeValue("header", Convert.ToBase64String(soundProp.Header));
                soundElement.SetAttributeValue("data", Convert.ToBase64String(soundProp.GetBytes()));
                */
                soundElement.SetAttributeValue("src", RESOURCES + "/" + soundName);
                return soundElement;
            }
            else if (prop is WzLuaProperty luaProp) // probably added on v188 gms?
            {
                // Save resource
                string res = Path.Combine(directory, RESOURCES);
                Directory.CreateDirectory(res);
                string luaName = resPrefix + ".lua";
                using (TextWriter stream = new StreamWriter(File.Create(Path.Combine(res, luaName))))
                    stream.Write(luaProp.ToString());

                // Create node
                XElement luaElement = new XElement("lua");
                luaElement.SetAttributeValue("name", luaProp.Name);
                luaElement.SetAttributeValue("src", RESOURCES + "/" + luaName);
                return luaElement;
            }
            else if (prop is WzNullProperty nullProp)
            {
                XElement nullElement = new XElement("null");
                nullElement.SetAttributeValue("name", nullProp.Name);
                return nullElement;
            }
            return null;
        }
    }
}
