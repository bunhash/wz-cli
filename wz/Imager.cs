using System;
using System.IO;
using System.Xml.Linq;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;

namespace wz
{
    class Imager
    {
        private WzImage wzImage;

        public Imager(string name)
        {
            this.wzImage = new WzImage(name);
        }

        public void Build(string filename, WzMapleVersion version)
        {
            this.wzImage.Changed = true;
            using (FileStream stream = File.Open(filename, FileMode.OpenOrCreate))
                using (WzBinaryWriter writer = new WzBinaryWriter(stream, WzTool.GetIvByMapleVersion(version)))
                    this.wzImage.SaveImage(writer, true);
        }

        public void AddXML(string filename)
        {
            this.wzImage.AddProperty(XMLReader.Parse(filename));
        }

        public void AddDirectory(string directory)
        {
            DirectoryInfo d = new DirectoryInfo(directory);
            foreach (FileInfo f in d.GetFiles("*.xml"))
                AddXML(f.FullName);
        }
    }

    class XMLReader
    {
        public static WzImageProperty Parse(string filename)
        {
            /*
            try
            {
            */
                XDocument doc = XDocument.Load(filename);
                XElement root = doc.Element("imgdir");
                if (root == null)
                    throw new FormatException("Expected non-null root element");
                WzImageProperty prop = ParseXMLElement(root, Path.GetDirectoryName(filename));
                return prop;
                /*
            } catch (Exception e)
            {
                Console.WriteLine("{0}: {1}", filename, e.Message);
                throw e;
            }
            */
        }

        internal static WzImageProperty ParseXMLElement(XElement e, string directory)
        {
            if (e.Name == null)
                throw new FormatException("property: expected tag name");
            string name = e.Name.ToString();

            // All attributes have a name
            XAttribute nameAttr = e.Attribute("name");
            if (nameAttr == null)
                throw new FormatException("property: expected name attribute");

            // Parse type-specific information
            if (name.Equals("imgdir"))
            {
                WzSubProperty imgdirProp = new WzSubProperty(nameAttr.Value);
                foreach (XElement child in e.Elements())
                    imgdirProp.AddProperty(ParseXMLElement(child, directory));
                return imgdirProp;
            }
            else if (name.Equals("short"))
            {
                XAttribute shortAttr = e.Attribute("value");
                if (shortAttr == null)
                    throw new FormatException("short: expected value");
                return new WzShortProperty(nameAttr.Value, Int16.Parse(shortAttr.Value));
            }
            else if (name.Equals("int"))
            {
                XAttribute intAttr = e.Attribute("value");
                if (intAttr == null)
                    throw new FormatException("int: expected value");
                return new WzIntProperty(nameAttr.Value, Int32.Parse(intAttr.Value));
            }
            else if (name.Equals("long"))
            {
                XAttribute longAttr = e.Attribute("value");
                if (longAttr == null)
                    throw new FormatException("long: expected value");
                return new WzLongProperty(nameAttr.Value, Int64.Parse(longAttr.Value));
            }
            else if (name.Equals("float"))
            {
                XAttribute floatAttr = e.Attribute("value");
                if (floatAttr == null)
                    throw new FormatException("float: expected value");
                return new WzFloatProperty(nameAttr.Value, Single.Parse(floatAttr.Value));
            }
            else if (name.Equals("double"))
            {
                XAttribute doubleAttr = e.Attribute("value");
                if (doubleAttr == null)
                    throw new FormatException("double: expected value");
                return new WzDoubleProperty(nameAttr.Value, Double.Parse(doubleAttr.Value));
            }
            else if (name.Equals("string"))
            {
                XAttribute stringAttr = e.Attribute("value");
                if (stringAttr == null)
                    throw new FormatException("string: expected value");
                return new WzStringProperty(nameAttr.Value, stringAttr.Value);
            }
            else if (name.Equals("uol"))
            {
                XAttribute uolAttr = e.Attribute("value");
                if (uolAttr == null)
                    throw new FormatException("uol: expected value");
                WzUOLProperty uolProp = new WzUOLProperty(nameAttr.Value, uolAttr.Value);
                /*
                foreach (XElement child in e.Elements())
                    uolProp.WzProperties.Add(ParseXMLElement(child, directory));
                */
                return uolProp;
            }
            else if (name.Equals("vector"))
            {
                XAttribute vectorXAttr = e.Attribute("x");
                XAttribute vectorYAttr = e.Attribute("y");
                if (vectorXAttr == null || vectorYAttr == null)
                    throw new FormatException("vector: expected x/y values");
                return new WzVectorProperty(nameAttr.Value, Int32.Parse(vectorXAttr.Value), Int32.Parse(vectorYAttr.Value));
            }
            else if (name.Equals("extended"))
            {
                WzConvexProperty imgdirProp = new WzConvexProperty(nameAttr.Value);
                foreach (XElement child in e.Elements())
                    imgdirProp.AddProperty(ParseXMLElement(child, directory));
                return imgdirProp;
            }
            else if (name.Equals("canvas"))
            {
                // Create property
                WzCanvasProperty canvasProp = new WzCanvasProperty(nameAttr.Value);

                // Load resource (if available)
                XAttribute srcAttr = e.Attribute("src");
                XAttribute dataAttr = e.Attribute("data");
                if (srcAttr != null)
                {
                    XAttribute formatAttr = e.Attribute("format");
                    if (formatAttr == null)
                        throw new FormatException("canvas: expected format attribute");
                    string resource = Path.Combine(directory, srcAttr.Value);
                    canvasProp.PngProperty = new WzPngProperty();
                    canvasProp.PngProperty.SetImage((System.Drawing.Bitmap)System.Drawing.Image.FromFile(resource, true), Int32.Parse(formatAttr.Value));
                }
                else
                    Console.WriteLine("Warning: A canvas without a source image may not work in legacy versions");

                // Load children
                foreach (XElement child in e.Elements())
                    canvasProp.AddProperty(ParseXMLElement(child, directory));
                return canvasProp;
            }
            else if (name.Equals("sound"))
            {
                XAttribute srcAttr = e.Attribute("src");
                if (srcAttr == null)
                    throw new FormatException("sound: expected src value");
                string resource = Path.Combine(directory, srcAttr.Value);
                if (!File.Exists(resource))
                    throw new FileNotFoundException("sound: MP3 file not found");
                return new WzBinaryProperty(nameAttr.Value, resource);
            }
            else if (name.Equals("lua")) // probably added on v188 gms?
            {
                XAttribute srcAttr = e.Attribute("src");
                if (srcAttr == null)
                    throw new FormatException("lua: expected src value");
                string resource = Path.Combine(directory, srcAttr.Value);
                if (!File.Exists(resource))
                    throw new FileNotFoundException("lua: Lua script not found");
                return new WzLuaProperty(nameAttr.Value, resource);
            }
            else if (name.Equals("null"))
            {
                return new WzNullProperty(nameAttr.Value);
            }
            throw new FormatException("Invalid XML tag: " + name);
        }
    }
}
