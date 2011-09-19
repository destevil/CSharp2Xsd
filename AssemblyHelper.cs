// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AssemblyHelper.cs" company="7HillApps">
//   Copyright 2011 7HillApps
// </copyright>
//
// <summary>
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//   http://www.apache.org/licenses/LICENSE-2.0
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace SevenHillApps.Csharp2Xsd
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Assembly related helper functions
    /// </summary>
    internal class AssemblyHelper
    {
        /// <summary>
        /// Our assembly
        /// </summary>
        private readonly Lazy<Assembly> assembly;
        
        /// <summary>
        /// Restrict to listed types (not implemented)
        /// </summary>
        private readonly Lazy<Type[]> types;

        /// <summary>
        /// our assembly's name
        /// </summary>
        private string assemblyName;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyHelper"/> class.
        /// </summary>
        /// <param name="verbose">if set to <c>true</c> [v mode].</param>
        public AssemblyHelper(bool verbose)
        {
            this.VerboseMode = verbose;
            this.SearchDirs = new List<string>();
            this.SearchTypes = new List<string>();

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve +=
                this.CurrentDomainReflectionOnlyAssemblyResolve;

            this.assembly = new Lazy<Assembly>(() => Assembly.ReflectionOnlyLoadFrom(this.AssemblyName));

            this.types = new Lazy<Type[]>(
                () =>
                    {
                        try
                        {
                            return this.assembly.Value.GetTypes();
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

        /// <summary>
        /// Gets or sets the name of the assembly.
        /// </summary>
        /// <value>
        /// The name of the assembly.
        /// </value>
        public string AssemblyName
        {
            get
            {
                return this.assemblyName;
            }

            set
            {
                this.assemblyName = value;
                var dir = Path.GetDirectoryName(value);
                this.SearchDirs.Add(dir);
            }
        }

        /// <summary>
        /// Gets the types.
        /// </summary>
        public Type[] Types
        {
            get { return this.types.Value; }
        }

        /// <summary>
        /// Gets or sets the search dirs.
        /// </summary>
        /// <value>
        /// The search dirs.
        /// </value>
        public List<string> SearchDirs { get; set; }

        /// <summary>
        /// Gets or sets the search types.
        /// </summary>
        /// <value>
        /// The search types.
        /// </value>
        public List<string> SearchTypes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether [verbose mode].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [verbose mode]; otherwise, <c>false</c>.
        /// </value>
        protected bool VerboseMode { get; set; }

        /// <summary>
        /// Gets the types with specified attribute.
        /// </summary>
        /// <param name="attrib">The attrib.</param>
        /// <returns>type array</returns>
        public Type[] GetTypesWithAttribute(Type attrib)
        {
            return this.Types.Where(t => t.GetCustomAttributeDataOfType(attrib) != null).ToArray();
        }

        /// <summary>
        /// Handles the ReflectionOnlyAssemblyResolve event of the CurrentDomain control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="System.ResolveEventArgs"/> instance containing the event data.</param>
        /// <returns>assembly, if found, else none</returns>
        private Assembly CurrentDomainReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (this.VerboseMode)
            {
                Console.WriteLine("WARNING: Attempting to resolve {0}", args.Name);
            }

            var items = args.Name.Split(new[] { ',' });

            foreach (var fileName in this.SearchDirs.Select(
                d => string.Format("{0}\\{1}.dll", d, items[0])).Where(File.Exists))
            {
                try
                {
                    return Assembly.ReflectionOnlyLoadFrom(fileName);
                }
// ReSharper disable EmptyGeneralCatchClause
                catch (Exception e)
// ReSharper restore EmptyGeneralCatchClause
                {
                    Console.WriteLine("ERROR: {0}", e.Message);
                }
            }

            return null;
        }
    }
}