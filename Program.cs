using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;
using Mono.Options;

namespace SevenHillApps.Csharp2Xsd
{
    internal class Program
    {
        private static List<string> Types { get; set; }
        private static List<string> Dir { get; set; }
        private static bool VerboseMode { get; set; }
        private static List<string> Assemblies { get; set; }
        private static bool OverwriteMode { get; set; }
        private static List<string> RootList { get; set; }

        private static void Main(string[] args)
        {
            Assemblies = new List<string>();
            Types = new List<string>();
            Dir = new List<string>();
            RootList = new List<string>();

            OptionSet p = null;
            p = new OptionSet()
                .Add("v", "Verbose mode", v => VerboseMode = true)
                .Add("f|force", "Overwrite schema if exists", str => OverwriteMode = true)
                .Add("a=|assembly=",
                     "Generate schemas for one or more types in this assembly",
                     str => Assemblies.Add(str))
                .Add("d=|directory=",
                     "Search for dependencies in this directory",
                     str => Dir.Add(str))
                .Add("o=|output=","Generate schema in this directory. By default, schemas are generated in the same directory as the assembly",str=>OutDir= str)
                .Add("r=|root=", "Emit element node at schema root for this type", str => RootList.Add(str))
// ReSharper disable AccessToModifiedClosure
                .Add("h|help", "This message", str=> Help(p))
// ReSharper restore AccessToModifiedClosure
                .Add("t=|type=", "TBD: Emit only specified types", str => Types.Add(str));

            p.Parse(args);

            if (Assemblies.Count == 0)
            {
                Help(p);
                return;
            }


            foreach (var name in Assemblies)
            {
                Console.WriteLine("Processing: " + name);

                try
                {
                    var asHelper = new AssemblyHelper(VerboseMode)
                                       {
                                           AssemblyName = name
                                       };

                    asHelper.SearchDirs.AddRange(Dir);
                    asHelper.SearchTypes.AddRange(Types);

                    if (VerboseMode)
                    {
                        Console.WriteLine("Types in assembly");
                        foreach (var t in asHelper.Types)
                            Console.WriteLine("\t{0}", t);
                    }

                    //look for classes with datacontract attribute
                    var contractList = asHelper.GetTypesWithAttribute(typeof (DataContractAttribute));
                    if (VerboseMode)
                    {
                        Console.WriteLine("Types with [DataContract] attribute");
                        foreach (var t in contractList)
                            Console.WriteLine("\t{0}", t);
                    }

                    var customTypeList = new List<XmlQualifiedName>();
                    var emittedTypeList = new List<string>();
                    var set = new XmlSchemaSet();
                    set.ValidationEventHandler += set_ValidationEventHandler;
                    foreach (var t in contractList)
                    {
                        var si = t.GetSchema();
                        set.Add(si.Schema);
                        emittedTypeList.Add(t.Name);
                        customTypeList.AddRange(si.CustomTypes);
                    }

                    //iterate through custom list until all of them are emitted
                    while (customTypeList.Count > 0)
                    {
                        var cList = new List<XmlQualifiedName>();
                        foreach (var c in customTypeList)
                        {
                            if (emittedTypeList.Contains(c.Name))
                                continue;

                            var name1 = c.Name;
                            var isList = false;

                            if (name1.StartsWith("ArrayOf"))
                            {
                                name1 = name1.Substring(7);
                                isList = true;
                            }

                            var t1 = contractList.FirstOrDefault(x => x.Name == name1);
                            if (t1 == null)
                            {
                                Console.WriteLine("ERROR: Type {0} is not defined", name1);
                                continue;
                            }

                            var s1 = t1.GetSchema(isList);
                            set.Add(s1.Schema);
                            emittedTypeList.Add(t1.Name);
                            cList.AddRange(s1.CustomTypes);
                        }
                        customTypeList = cList;
                    }

                    //root elements
                    if (RootList.Count > 0)
                    {
                        var schema = new XmlSchema();
                        foreach (var s in RootList)
                        {
                            var e = new XmlSchemaElement
                                        {
                                            Name = s,
                                            SchemaTypeName = new XmlQualifiedName(s)
                                        };
                            schema.Items.Add(e);
                        }
                        set.Add(schema);
                    }

                    set.Compile();

                    var compiled = new XmlSchema();
                    foreach (XmlSchema s in set.Schemas())
                    {
                        foreach (var x in s.Items)
                            compiled.Items.Add(x);
                    }

                    if (OutDir == null)
                        OutDir = Path.GetDirectoryName(name);

                    //write output to assemblyname.xsd
                    var xsdName = OutDir + "\\" + Path.GetFileNameWithoutExtension(name) + ".xsd";
                    
                    if (VerboseMode)
                        Console.WriteLine("Generating schema file: {0}", xsdName); 
   
                    if (File.Exists(xsdName) && !OverwriteMode)
                        Console.WriteLine("ERROR: " + xsdName + " exists. Use /f option to overwrite");
                    else
                        compiled.Write(new StreamWriter(xsdName));
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERROR: " + e.Message);
                }
            }
        }

        private static string OutDir { get; set; }

        private static void Help(OptionSet p)
        {
            Console.WriteLine(
@"Generates XML schemas for types in a CLR assembly.
The following custom attributes must be set for types 
to be emitted in the schema:
     * Classes / enums must have the [DataContract] attribute
     * Properties in types must have [DataMember] attribute

Notes:
    * Enum members are emitted as strings with enumerator restrictions. 
        The [EnumMember] custom attribute is ignored.
    * For properties, [DataMember(IsRequired = false)] emits minOccurs=0
    * For properties, [DataMember(IsRequired = true )] emits minOccurs=1
    * For properties, using Nillable<> or ? emits nillable=true
    * List<TypeABC> is emitted as 'ArrayOfTypeABC'. TypeABC must be defined
        elsewhere in the assembly (or a CLR primitive type).
    * Currently, only List<>, ObservableCollection<>, Collection<> generate
        array types.

If a type isn't found, an attempt is made to load it from the 
optinal types directory. An error is printed on stdout if a type 
cannot be found anywhere.

");
            Console.WriteLine("Usage: CSharp2Xsd <option> ..., where options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        private static void set_ValidationEventHandler(object sender, ValidationEventArgs e)
        {
            switch (e.Severity)
            {
            case XmlSeverityType.Error: Console.Write("ERROR: ");
                    break;
            case XmlSeverityType.Warning: Console.Write("WARNING: ");
                    break;
            }
            Console.WriteLine(e.Message);
        }
    }
}