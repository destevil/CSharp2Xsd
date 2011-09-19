// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="7HillApps">
//  Copyright 2011 7HillApps
// </copyright>
// <summary>
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//    http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SevenHillApps.Csharp2Xsd
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Xml;
    using System.Xml.Schema;

    using Mono.Options;

    /// <summary>
    /// Entry point
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Gets or sets types to emit output for. Restrict output to these types only. Not implemented yet.
        /// </summary>
        /// <value>
        /// The types.
        /// </value>
        private static List<string> Types { get; set; }

        /// <summary>
        /// Gets or sets the dirs to search for additional dependancies
        /// </summary>
        /// <value>
        /// The dir.
        /// </value>
        private static List<string> Dir { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [verbose mode].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [verbose mode]; otherwise, <c>false</c>.
        /// </value>
        private static bool VerboseMode { get; set; }

        /// <summary>
        /// Gets or sets the assemblies which will be scanned for types to emit
        /// </summary>
        /// <value>
        /// The assemblies.
        /// </value>
        private static List<string> Assemblies { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to overwrite existing output files
        /// </summary>
        /// <value>
        ///   <c>true</c> if [overwrite mode]; otherwise, <c>false</c>.
        /// </value>
        private static bool OverwriteMode { get; set; }

        /// <summary>
        /// Gets or sets the root list -- XSD will contain element nodes for these types
        /// </summary>
        /// <value>
        /// The root list.
        /// </value>
        private static List<string> RootList { get; set; }

        /// <summary>
        /// Gets or sets the out dir -- output will be generated here
        /// </summary>
        /// <value>
        /// The out dir.
        /// </value>
        private static string OutDir { get; set; }

        /// <summary>
        /// Entry point
        /// </summary>
        /// <param name="args">The args.</param>
        private static void Main(string[] args)
        {
            Assemblies = new List<string>();
            Types = new List<string>();
            Dir = new List<string>();
            RootList = new List<string>();

            OptionSet p = null;
            p = new OptionSet().Add("v", "Verbose mode", v => VerboseMode = true).Add(
                "f|force", "Overwrite schema if exists", str => OverwriteMode = true).Add(
                    "a=|assembly=",
                    "Generate schemas for one or more types in this assembly",
                    str => Assemblies.Add(str)).Add(
                        "d=|directory=", "Search for dependencies in this directory", str => Dir.Add(str)).Add(
                            "o=|output=",
                            "Generate schema in this directory. By default, schemas are generated in the same directory as the assembly",
                            str => OutDir = str).Add(
                                "r=|root=", "Emit element node at schema root for this type", str => RootList.Add(str))
                // ReSharper disable AccessToModifiedClosure
                .Add("h|help", "This message", str => Help(p))
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
                    var asHelper = new AssemblyHelper(VerboseMode) { AssemblyName = name };

                    asHelper.SearchDirs.AddRange(Dir);
                    asHelper.SearchTypes.AddRange(Types);

                    if (VerboseMode)
                    {
                        Console.WriteLine("Types in assembly");
                        foreach (var t in asHelper.Types)
                        {
                            Console.WriteLine("\t{0}", t);
                        }
                    }

                    // look for classes with datacontract attribute
                    var contractList = asHelper.GetTypesWithAttribute(typeof(DataContractAttribute));
                    if (VerboseMode)
                    {
                        Console.WriteLine("Types with [DataContract] attribute");
                        foreach (var t in contractList)
                        {
                            Console.WriteLine("\t{0}", t);
                        }
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

                    // iterate through custom list until all of them are emitted
                    while (customTypeList.Count > 0)
                    {
                        var customList = new List<XmlQualifiedName>();
                        foreach (var c in customTypeList)
                        {
                            if (emittedTypeList.Contains(c.Name))
                            {
                                continue;
                            }

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
                            customList.AddRange(s1.CustomTypes);
                        }

                        customTypeList = customList;
                    }

                    // root elements
                    if (RootList.Count > 0)
                    {
                        var schema = new XmlSchema();
                        foreach (var s in RootList)
                        {
                            var e = new XmlSchemaElement { Name = s, SchemaTypeName = new XmlQualifiedName(s) };
                            schema.Items.Add(e);
                        }

                        set.Add(schema);
                    }

                    set.Compile();

                    var compiled = new XmlSchema();
                    foreach (XmlSchema s in set.Schemas())
                    {
                        foreach (var x in s.Items)
                        {
                            compiled.Items.Add(x);
                        }
                    }

                    if (OutDir == null)
                    {
                        OutDir = Path.GetDirectoryName(name);
                    }

                    // write output to assemblyname.xsd
                    var xsdName = OutDir + "\\" + Path.GetFileNameWithoutExtension(name) + ".xsd";

                    if (VerboseMode)
                    {
                        Console.WriteLine("Generating schema file: {0}", xsdName);
                    }

                    if (File.Exists(xsdName) && !OverwriteMode)
                    {
                        Console.WriteLine("ERROR: " + xsdName + " exists. Use /f option to overwrite");
                    }
                    else
                    {
                        compiled.Write(new StreamWriter(xsdName));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("ERROR: " + e.Message);
                }
            }
        }

        /// <summary>
        /// Print usage / help information
        /// </summary>
        /// <param name="p">The p.</param>
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

        /// <summary>
        /// Validation handler for XSD
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Xml.Schema.ValidationEventArgs"/> instance containing the event data.</param>
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter",
            Justification = "Reviewed. Suppression is OK here.")]
        private static void set_ValidationEventHandler(object sender, ValidationEventArgs e)
        {
            switch (e.Severity)
            {
                case XmlSeverityType.Error:
                    Console.Write("ERROR: ");
                    break;
                case XmlSeverityType.Warning:
                    Console.Write("WARNING: ");
                    break;
            }

            Console.WriteLine(e.Message);
        }
    }
}