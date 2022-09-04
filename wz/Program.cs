using System;
using System.Collections.Generic;
using System.IO;
using Mono.Options;
using MapleLib.WzLib;
using MapleLib.Helpers;
using MapleLib.WzLib.Serialization;

namespace wz
{
    class Program
    {
        private static bool VERBOSE = false;
        private static bool IMAGES_ONLY = true;
        private static WzMapleVersion WZ_VERSION = WzMapleVersion.BMS;

        static void Main(string[] argv)
        {
            bool printHelp = false;
            string filename = null;
            bool doExtract = false;
            bool doServer = false;
            bool doCreate = false;
            string targetDirectory = null;
            string gameVersion = null;
            bool fileIsImage = false;

            OptionSet p = new OptionSet() {
                {
                    "h|help",  "print this message",
                    v => printHelp = v != null
                },
                {
                    "v|verbose", "increase debug message verbosity",
                    v => VERBOSE = v != null
                },
                {
                    "x|extract", "extract the provided file",
                    v => doExtract = v != null
                },
                {
                    "s|server", "extract the provided files into private server XML",
                    v => doServer = v != null
                },
                {
                    "c|create", "create the provided file",
                    v => doCreate = v != null
                },
                {
                    "i|image", "create/extract image file",
                    v => fileIsImage = v != null
                },
                {
                    "f|file=", "the WZ {FILE}",
                    v => filename = v
                },
                {
                    "d|directory=", "extract to {DIR}",
                    v => targetDirectory = v
                },
                {
                    "g|gameversion=", "game {VERSION} to mark as",
                    v => gameVersion = v
                },
                {
                    "legacy", "use legacy GMS format",
                    v => {
                        if (v != null)
                            WZ_VERSION = WzMapleVersion.GMS;
                    }
                },
                {
                    "deep", "extract all images files",
                    v => IMAGES_ONLY = v == null
                },
            };

            // Parse command line arguments
            List<string> args;
            try
            {
                args = p.Parse(argv);
            }
            catch (OptionException e)
            {
                Console.Write("wz: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `wz --help' for more information.");
                return;
            }

            // Print help
            if (printHelp)
            {
                PrintHelp(p);
                return;
            }

            // Sanity checks
            if (!doCreate && !doExtract && !doServer)
            {
                Console.WriteLine("wz: Must supply a command");
                Console.WriteLine("Try `wz --help' for more information.");
                return;
            }

            if (doCreate && doExtract || doCreate && doServer || doExtract && doServer)
            {
                Console.WriteLine("wz: Must supply only one command");
                Console.WriteLine("Try `wz --help' for more information.");
                return;
            }

            if (String.IsNullOrEmpty(filename))
            {
                Console.WriteLine("wz: Must supply a WZ file");
                Console.WriteLine("Try `wz --help' for more information.");
                return;
            }
            
            // EXTRACT
            if (doExtract)
            {
                if (!String.IsNullOrEmpty(gameVersion))
                {
                    Console.WriteLine("wz: Can only supply a game version when archiving");
                    Console.WriteLine("Try `wz --help' for more information.");
                    return;
                }
                args.Insert(0, filename);
                foreach (string fname in args)
                    if (!File.Exists(fname))
                    {
                        Console.WriteLine("wz: `{0}` does not exist", fname);
                        Console.WriteLine("Try `wz --help' for more information.");
                        return;
                    }
                if (String.IsNullOrEmpty(targetDirectory))
                    targetDirectory = ".";
                if (fileIsImage)
                    foreach (string fname in args)
                        try
                        {
                            if (VERBOSE)
                                Console.WriteLine(fname);
                            (new ImageExtractor(fname, WZ_VERSION)).Extract(targetDirectory);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error ({0}): {1}", fname, e);
                        }
                else
                    foreach (string fname in args)
                        try
                        {
                            Extract(fname, targetDirectory);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Error ({0}): {1}", fname, e);
                        }
            }

            // SERVER
            else if (doServer)
            {
                args.Insert(0, filename);
                foreach (string fname in args)
                    if (!File.Exists(fname))
                    {
                        Console.WriteLine("wz: `{0}` does not exist", fname);
                        Console.WriteLine("Try `wz --help' for more information.");
                        return;
                    }
                if (String.IsNullOrEmpty(targetDirectory))
                    targetDirectory = ".";
                WzXmlSerializer.formattingInfo.NumberDecimalSeparator = ",";
                WzXmlSerializer.formattingInfo.NumberGroupSeparator = ".";
                foreach (string fname in args)
                    try
                    {
                        string path = Path.Combine(targetDirectory, Path.GetFileName(fname));
                        WzClassicXmlSerializer serializer = new WzClassicXmlSerializer(0, LineBreak.None, false);
                        WzFile wzFile = new WzFile(fname, WZ_VERSION);
                        string errorMessage;
                        if (!wzFile.ParseWzFile(out errorMessage))
                        {
                            Console.WriteLine("Error: {0}", errorMessage);
                            continue;
                        }
                        if (VERBOSE)
                            Console.WriteLine("Extracting {0}", path);
                        serializer.SerializeFile(wzFile, path);
                        wzFile.Dispose();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error ({0}): {1}", fname, e);
                    }
            }

            // CREATE
            else if (doCreate)
            {
                if (!String.IsNullOrEmpty(targetDirectory))
                {
                    Console.WriteLine("wz: Can only supply a directory when extracting");
                    Console.WriteLine("Try `wz --help' for more information.");
                    return;
                }
                if (fileIsImage)
                {
                    if (!String.IsNullOrEmpty(gameVersion))
                    {
                        Console.WriteLine("wz: Can only supply a game version when archiving");
                        Console.WriteLine("Try `wz --help' for more information.");
                        return;
                    }
                    if (args.Count == 0)
                    {
                        Console.WriteLine("wz: Must provide targets to image");
                        Console.WriteLine("Try `wz --help' for more information.");
                        return;
                    }
                    BuildImage(filename, args);
                }
                else
                {
                    if (String.IsNullOrEmpty(gameVersion))
                    {
                        Console.WriteLine("wz: Must supply a game version when archiving");
                        Console.WriteLine("Try `wz --help' for more information.");
                        return;
                    }
                    if (args.Count != 1)
                    {
                        Console.WriteLine("wz: Must provide a single directory to archve");
                        Console.WriteLine("Try `wz --help' for more information.");
                        return;
                    }
                    if (!Directory.Exists(args[0]))
                    {
                        Console.WriteLine("wz: `{0}` does not exist", args[0]);
                        Console.WriteLine("Try `wz --help' for more information.");
                        return;
                    }
                    short gVersion = 0;
                    try
                    {
                        gVersion = Int16.Parse(gameVersion);
                    }
                    catch
                    {
                        Console.WriteLine("wz: `{0}` is not a number", gameVersion);
                        Console.WriteLine("Try `wz --help' for more information.");
                        return;
                    }
                    BuildArchive(filename, gVersion, args[0]);
                }
                        
            }

            if (VERBOSE && ErrorLogger.ErrorsPresent())
            {
                Console.WriteLine("ERRORS");
                ErrorLogger.WriteToStream(Console.Out);
            }
        }

        private static void PrintHelp(OptionSet p)
        {
            Console.WriteLine("Usage: wz [OPTIONS] -xf  WZFILE...");
            Console.WriteLine("       wz [OPTIONS] -xif IMAGEFILE...");
            Console.WriteLine("       wz [OPTIONS] -sf  WZFILE...");
            Console.WriteLine("       wz [OPTIONS] -g VERSION -cf WZFILE DIRECTORY");
            Console.WriteLine("       wz [OPTIONS] -cif IMAGEFILE XMLFILE...");
            Console.WriteLine("       wz [OPTIONS] -cif IMAGEFILE DIRECTORY");
            Console.WriteLine();
            p.WriteOptionDescriptions(Console.Out);
        }

        private static void Extract(string filename, string targetDirectory)
        {
            Extractor extractor = new Extractor(filename, WZ_VERSION);
            foreach ((ulong num, WzImage wzimg, string directory) in extractor.GetImageIterator())
            {
                string dirPath = Path.Combine(targetDirectory, directory);
                if (VERBOSE)
                    Console.WriteLine(Path.Combine(dirPath, wzimg.Name));
                if (IMAGES_ONLY)
                    extractor.SaveImage(wzimg, dirPath);
                else
                    extractor.ExtractImage(wzimg, dirPath);
            }
        }

        private static void BuildImage(string filename, List<string> targets)
        {
            Imager imager = new Imager(Path.GetFileName(filename));
            foreach (string target in targets)
            {
                if (Directory.Exists(target))
                {
                    DirectoryInfo d = new DirectoryInfo(target);
                    foreach (FileInfo fi in d.EnumerateFiles("*.xml"))
                    {
                        if (VERBOSE)
                            Console.WriteLine(fi.FullName);
                        imager.AddXML(fi.FullName);
                    }
                }
                else if (File.Exists(target))
                {
                    if (VERBOSE)
                        Console.WriteLine(target);
                    imager.AddXML(target);
                }
                else
                {
                    Console.WriteLine("Error: {0} does not exit", target);
                    return;
                }
            }
            imager.Build(filename, WZ_VERSION);
        }

        private static void BuildArchive(string filename, short gameVersion, string target)
        {
            // Initialize archiver and stack
            Archiver archiver = new Archiver(filename, gameVersion, WZ_VERSION);
            DirectoryInfo root = new DirectoryInfo(target);
            Stack<(string, DirectoryInfo)> frontier = new Stack<(string, DirectoryInfo)>();
            frontier.Push((root.Name + ".wz", root));

            // Recurse through directory
            while (frontier.Count > 0)
            {
                (string path, DirectoryInfo di) = frontier.Pop();
                if (VERBOSE)
                    Console.WriteLine(path);
                foreach (DirectoryInfo child in di.EnumerateDirectories())
                    frontier.Push((path + "/" + child.Name, child));
                foreach (FileInfo image in di.EnumerateFiles("*.img"))
                {
                    if (VERBOSE)
                        Console.WriteLine(path + "/" + image.Name);
                    archiver.AddImage(path, image.FullName, WZ_VERSION);
                }
            }

            archiver.Archive();
        }
    }
}
