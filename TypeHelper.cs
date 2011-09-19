// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TypeHelper.cs" company="7HillApps">
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
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Xml;
    using System.Xml.Schema;

    /// <summary>
    /// Generate XML for CLR primitives
    /// </summary>
    public static class Primitive2Xml
    {
        /// <summary>
        /// List of CLR / XSD types
        /// </summary>
        private static readonly Lazy<Dictionary<Type, XmlQualifiedName>> ClrType2Xml =
            // ReSharper restore InconsistentNaming
            new Lazy<Dictionary<Type, XmlQualifiedName>>(
                () =>
                    {
                        var d = new Dictionary<Type, XmlQualifiedName>();
                        d[typeof(string)] = new XmlQualifiedName("string", "http://www.w3.org/2001/XMLSchema");
                        d[typeof(String)] = new XmlQualifiedName("string", "http://www.w3.org/2001/XMLSchema");
                        d[typeof(int)] = new XmlQualifiedName("int", "http://www.w3.org/2001/XMLSchema");
                        d[typeof(Int32)] = new XmlQualifiedName("int", "http://www.w3.org/2001/XMLSchema");
                        d[typeof(bool)] = new XmlQualifiedName("boolean", "http://www.w3.org/2001/XMLSchema");
                        d[typeof(Boolean)] = new XmlQualifiedName("boolean", "http://www.w3.org/2001/XMLSchema");
                        d[typeof(float)] = new XmlQualifiedName("float", "http://www.w3.org/2001/XMLSchema");
                        d[typeof(double)] = new XmlQualifiedName("double", "http://www.w3.org/2001/XMLSchema");
                        d[typeof(Double)] = new XmlQualifiedName("double", "http://www.w3.org/2001/XMLSchema");
                        d[typeof(DateTime)] = new XmlQualifiedName("dateTime", "http://www.w3.org/2001/XMLSchema");
                        return d;
                    });

        /// <summary>
        /// extension method to return Xml QN for type
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>Xml QN</returns>
        public static XmlQualifiedName XmlName(this Type t)
        {
            return t != null && ClrType2Xml.Value.ContainsKey(t) ? ClrType2Xml.Value[t] : null;
        }

        /// <summary>
        /// XMLs the name.
        /// </summary>
        /// <param name="n3">The n3.</param>
        /// <returns>XML QN</returns>
        public static XmlQualifiedName XmlName(string n3)
        {
            var t = Type.GetType(n3);
            return t.XmlName();
        }
    }

    /// <summary>
    /// Type helpers for XML
    /// </summary>
    public static class Type2XmlHelper
    {
        /// <summary>
        /// Split type string 
        /// </summary>
        /// <param name="s">The s.</param>
        /// <param name="shortVersion">if set to <c>true</c> [short version].</param>
        /// <returns>type name</returns>
        public static string InternalType(string s, bool shortVersion)
        {
            var n = s.Split(new[] { '[', ',' });
            if (!shortVersion)
            {
                return n[2];
            }

            var n2 = n[2].Split(new[] { '.' });
            return n2[n2.Length - 1];
        }

        /// <summary>
        /// Gets the custom attribute data.
        /// </summary>
        /// <param name="attribList">A list.</param>
        /// <param name="attrib">The attrib.</param>
        /// <returns>attrib data</returns>
        public static CustomAttributeData GetCustomAttributeData(IList<CustomAttributeData> attribList, Type attrib)
        {
            return attribList == null
                       ? null
                       : (from a in attribList
                          let t1 = a.Constructor.ReflectedType
                          where t1.FullName == attrib.FullName
                          select a).FirstOrDefault();
        }


        /// <summary>
        /// Gets the type of the custom attribute data of. -- hillarious
        /// </summary>
        /// <param name="t">The t.</param>
        /// <param name="attrib">The attrib.</param>
        /// <returns>attrib data</returns>
        public static CustomAttributeData GetCustomAttributeDataOfType(this Type t, Type attrib)
        {
            return GetCustomAttributeData(t.GetCustomAttributesData(), attrib);
        }

        /// <summary>
        /// Gets the type of the custom attribute data of.
        /// </summary>
        /// <param name="p">The p.</param>
        /// <param name="attrib">The attrib.</param>
        /// <returns>attrib data</returns>
        public static CustomAttributeData GetCustomAttributeDataOfType(this PropertyInfo p, Type attrib)
        {
            return GetCustomAttributeData(p.GetCustomAttributesData(), attrib);
        }

        /// <summary>
        /// Schemas the key. Not used currently
        /// </summary>
        /// <returns>key a </returns>
        public static XmlSchemaKey SchemaKey()
        {
            var elementKey = new XmlSchemaKey
                                 {
                                     Name = "AccountKey",
                                     Selector = new XmlSchemaXPath
                                                    {
                                                        XPath = "r:parts/r:part"
                                                    }
                                 };

            {
                var field = new XmlSchemaXPath
                                {
                                    XPath = "@number"
                                };

                elementKey.Fields.Add(field);
            }

            return elementKey;
        }

        /// <summary>
        /// Gets the enum schema.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>schema info</returns>
        public static SchemaInfo GetEnumSchema(this Type t)
        {
            if (!t.IsEnum)
            {
                return null;
            }

            var schemaInfo = new SchemaInfo();
            var classType = new XmlSchemaSimpleType
                                {
                                    Name = t.Name
                                };

            var attribData = t.GetCustomAttributeDataOfType(typeof(DataContractAttribute));
            if (attribData.NamedArguments != null && attribData.NamedArguments.Count > 0)
            {
                foreach (var p1 in attribData.NamedArguments)
                {
                    switch (p1.MemberInfo.Name)
                    {
                        case "Namespace":
                            schemaInfo.Schema.TargetNamespace = p1.TypedValue.Value as string;
                            break;
                    }
                }
            }

            var content = new XmlSchemaSimpleTypeRestriction();
            classType.Content = content;

            content.BaseTypeName = typeof(string).XmlName();
            foreach (var e in t.GetEnumNames())
            {
                content.Facets.Add(new XmlSchemaEnumerationFacet
                                       {
                                           Value = e
                                       });
            }

            schemaInfo.Schema.Items.Add(classType);
            return schemaInfo;
        }

        /// <summary>
        /// Gets the class schema.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>schema info</returns>
        public static SchemaInfo GetClassSchema(this Type t)
        {
            var schemaInfo = new SchemaInfo();
            var classType = new XmlSchemaComplexType
                                {
                                    Name = t.Name
                                };

            var attribData = t.GetCustomAttributeDataOfType(typeof(DataContractAttribute));
            if (attribData.NamedArguments != null && attribData.NamedArguments.Count > 0)
            {
                foreach (var p1 in attribData.NamedArguments)
                {
                    switch (p1.MemberInfo.Name)
                    {
                        case "Namespace":
                            schemaInfo.Schema.TargetNamespace = p1.TypedValue.Value as string;
                            break;
                    }
                }
            }

            var sequence = new XmlSchemaSequence();

            classType.Particle = sequence;

            var propList = t.GetProperties();

            foreach (var p1 in propList)
            {
                var el = new XmlSchemaElement
                             {
                                 Name = p1.Name,
                                 MinOccurs = 0
                             };

                var xmlName = p1.PropertyType.XmlName();
                if (xmlName != null)
                {
                    el.SchemaTypeName = xmlName;
                }
                else
                {
                    if (p1.PropertyType.IsListType())
                    {
                        // what is this a list of?
                        if (p1.PropertyType.FullName != null)
                        {
                            var pr = Primitive2Xml.XmlName(InternalType(p1.PropertyType.FullName, false));
                            if (pr != null)
                            {
                                el.SchemaTypeName = new XmlQualifiedName("ArrayOf" + pr.Name);
                                schemaInfo.CustomTypes.Add(el.SchemaTypeName);
                            }
                            else
                            {
                                var schName = InternalType(p1.PropertyType.FullName, true);
                                el.SchemaTypeName = new XmlQualifiedName("ArrayOf" + schName);
                                schemaInfo.CustomTypes.Add(new XmlQualifiedName(schName));
                                schemaInfo.CustomTypes.Add(el.SchemaTypeName);
                            }
                        }
                    }
                    else if (p1.PropertyType.Name.Contains("Nullable"))
                    {
                        // what is this a nullable of?
                        if (p1.PropertyType.FullName != null)
                        {
                            var pr = Primitive2Xml.XmlName(InternalType(p1.PropertyType.FullName, false));
                            if (pr != null)
                            {
                                el.SchemaTypeName = pr;
                            }
                            else
                            {
                                var schName = InternalType(p1.PropertyType.FullName, true);
                                el.SchemaTypeName = new XmlQualifiedName(schName);
                                schemaInfo.CustomTypes.Add(el.SchemaTypeName);
                            }

                            el.IsNillable = true;
                        }
                    }
                    else
                    {
                        el.SchemaTypeName = new XmlQualifiedName(p1.PropertyType.Name);
                        schemaInfo.CustomTypes.Add(el.SchemaTypeName);
                    }
                }

                // get data member
                var attrData = p1.GetCustomAttributeDataOfType(typeof(DataMemberAttribute));
                if (attrData != null && attrData.NamedArguments != null
                    && attrData.NamedArguments.Count > 0)
                {
                    foreach (var pp in attrData.NamedArguments)
                    {
                        switch (pp.MemberInfo.Name)
                        {
                            case "IsRequired":
                                {
                                    var v = (bool)pp.TypedValue.Value;
                                    el.MinOccurs = v ? 1 : 0;
                                }

                                break;
                        }
                    }
                }

                sequence.Items.Add(el);
            }


            schemaInfo.Schema.Items.Add(classType);
            return schemaInfo;
        }

        /// <summary>
        /// Gets the list schema.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>schema info</returns>
        public static SchemaInfo GetListSchema(this Type t)
        {
            var schemaInfo = new SchemaInfo();
            var classType = new XmlSchemaComplexType
            {
                Name = "ArrayOf" + t.Name
            };

            var sequence = new XmlSchemaSequence();

            schemaInfo.Schema.Items.Add(classType);
            classType.Particle = sequence;
            sequence.Items.Add(new XmlSchemaElement
                                   {
                                       Name = t.Name,
                                       MinOccurs = 0,
                                       MaxOccursString = "unbounded",
                                       SchemaTypeName = new XmlQualifiedName(t.Name)
                                   });


            return schemaInfo;
        }

        /// <summary>
        /// Gets the schema.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <param name="isList">if set to <c>true</c> [is list].</param>
        /// <returns>the schema it is</returns>
        public static SchemaInfo GetSchema(this Type t, bool isList)
        {
            if (isList)
            {
                return t.GetListSchema();
            }

            if (t.IsClass)
            {
                return t.GetClassSchema();
            }

            return t.IsEnum ? t.GetEnumSchema() : null;
        }


        /// <summary>
        /// Gets the schema.
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>schema, am i?</returns>
        public static SchemaInfo GetSchema(this Type t)
        {
            return t.GetSchema(false);
        }

        /// <summary>
        /// Determines whether [is list type] [the specified t].
        /// </summary>
        /// <param name="t">The t.</param>
        /// <returns>
        ///   <c>true</c> if [is list type] [the specified t]; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsListType(this Type t)
        {
            return t.Name == typeof(List<>).Name || t.Name == typeof(ObservableCollection<>).Name
                   || t.Name == typeof(IList<>).Name || t.Name == typeof(ICollection<>).Name;
        }

        #region Nested type: SchemaInfo

        public class SchemaInfo
        {
            public SchemaInfo()
            {
                Schema = new XmlSchema();
                CustomTypes = new List<XmlQualifiedName>();
            }

            public XmlSchema Schema { get; set; }
            public List<XmlQualifiedName> CustomTypes { get; set; }
        }

        #endregion
    }
}