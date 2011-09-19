using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SevenHillApps.Csharp2Xsd
{
    internal class AssemblyHelper
    {
        private readonly Lazy<Assembly> m_assembly;
        private readonly Lazy<Type[]> m_types;
        private string m_assemblyName;

        public AssemblyHelper(bool vMode)
        {
            VerboseMode = vMode;
            SearchDirs = new List<string>();
            SearchTypes = new List<string>();

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve +=
                CurrentDomain_ReflectionOnlyAssemblyResolve;

            m_assembly = new Lazy<Assembly>(() => Assembly.ReflectionOnlyLoadFrom(AssemblyName));

            m_types = new Lazy<Type[]>(
                () =>
                    {
                        try
                        {
                            return m_assembly.Value.GetTypes();
                        }
                        catch (ReflectionTypeLoadException e)
                        {
                            var list = e.LoaderExceptions;
                            foreach (var e1 in list)
                            {
                                Console.WriteLine(e1.Message);
                            }
                            throw;
                        }
                    });
        }

        protected bool VerboseMode { get; set; }

        public string AssemblyName
        {
            get { return m_assemblyName; }
            set
            {
                m_assemblyName = value;
                var dir = Path.GetDirectoryName(value);
                SearchDirs.Add(dir);
            }
        }


        public Type[] Types
        {
            get { return m_types.Value; }
        }

        public List<string> SearchDirs { get; set; }

        public List<string> SearchTypes { get; set; }

        private Assembly CurrentDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (VerboseMode)
                Console.WriteLine("WARNING: Attempting to resolve {0}", args.Name);

            var items = args.Name.Split(new[] {','});

            foreach (var fName in SearchDirs.Select(
                d => String.Format("{0}\\{1}.dll", d, items[0])).Where(File.Exists))
            {
                try
                {
                    return Assembly.ReflectionOnlyLoadFrom(fName);
                }
// ReSharper disable EmptyGeneralCatchClause
                catch (Exception e)
// ReSharper restore EmptyGeneralCatchClause
                {
                    Console.WriteLine("ERROR: {0}", e.Message );
                }
            }

            return null;
        }


        public Type[] GetTypesWithAttribute(Type attrib) { return Types.Where(t => t.GetCustomAttributeDataOfType(attrib) != null).ToArray(); }
    }
}