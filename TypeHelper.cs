using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;

namespace SevenHillApps.Csharp2Xsd
{
    public static class Primitive2Xml
    {
        // ReSharper disable InconsistentNaming

        public static Lazy<Dictionary<Type, XmlQualifiedName>> ClrType2Xml =
            // ReSharper restore InconsistentNaming
            new Lazy<Dictionary<Type, XmlQualifiedName>>(
                () =>
                    {
                        var d = new Dictionary<Type, XmlQualifiedName>();

                        d[typeof (string)] = new XmlQualifiedName("string", "http://www.w3.org/2001/XMLSchema");
                        d[typeof (String)] = new XmlQualifiedName("string", "http://www.w3.org/2001/XMLSchema");
                        d[typeof (int)] = new XmlQualifiedName("int", "http://www.w3.org/2001/XMLSchema");
                        d[typeof (Int32)] = new XmlQualifiedName("int", "http://www.w3.org/2001/XMLSchema");
                        d[typeof (bool)] = new XmlQualifiedName("boolean", "http://www.w3.org/2001/XMLSchema");
                        d[typeof (Boolean)] = new XmlQualifiedName("boolean", "http://www.w3.org/2001/XMLSchema");
                        d[typeof (float)] = new XmlQualifiedName("float", "http://www.w3.org/2001/XMLSchema");
                        d[typeof (double)] = new XmlQualifiedName("double", "http://www.w3.org/2001/XMLSchema");
                        d[typeof (Double)] = new XmlQualifiedName("double", "http://www.w3.org/2001/XMLSchema");
                        d[typeof (DateTime)] = new XmlQualifiedName("dateTime", "http://www.w3.org/2001/XMLSchema");

                        return d;
                    });

        public static XmlQualifiedName XmlName(this Type t) { return t != null && ClrType2Xml.Value.ContainsKey(t) ? ClrType2Xml.Value[t] : null; }

        public static XmlQualifiedName XmlName(string n3)
        {
            var t = Type.GetType(n3);
            return t.XmlName();
        }
    }

    public static class Type2XmlHelper
    {
        public static string InternalType(string s, bool shortVersion)
        {
            var n = s.Split(new[] {'[', ','});
            if (!shortVersion)
                return n[2];
            var n2 = n[2].Split(new[] {'.'});
            return n2[n2.Length - 1];
        }

        public static CustomAttributeData GetCustomAttributeData(IList<CustomAttributeData> aList, Type attrib)
        {
            return aList == null
                       ? null
                       : (from a in aList
                          let t1 = a.Constructor.ReflectedType
                          where t1.FullName == attrib.FullName
                          select a).FirstOrDefault();
        }


        public static CustomAttributeData GetCustomAttributeDataOfType(this Type t, Type attrib) { return GetCustomAttributeData(t.GetCustomAttributesData(), attrib); }
        public static CustomAttributeData GetCustomAttributeDataOfType(this PropertyInfo p, Type attrib) { return GetCustomAttributeData(p.GetCustomAttributesData(), attrib); }

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

        public static SchemaInfo GetEnumSchema(this Type t)
        {
            if (!t.IsEnum)
                return null;

            var schemaInfo = new SchemaInfo();
            var classType = new XmlSchemaSimpleType
                                {
                                    Name = t.Name
                                };

            var attribData = t.GetCustomAttributeDataOfType(typeof (DataContractAttribute));
            if (attribData.NamedArguments != null && attribData.NamedArguments.Count > 0)
            {
                foreach (var p1 in attribData.NamedArguments)
                    switch (p1.MemberInfo.Name)
                    {
                        case "Namespace":
                            schemaInfo.Schema.TargetNamespace = p1.TypedValue.Value as string;
                            break;
                    }
            }

            var content = new XmlSchemaSimpleTypeRestriction();
            classType.Content = content;

            content.BaseTypeName = typeof (string).XmlName();
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

        public static SchemaInfo GetClassSchema(this Type t)
        {
            var schemaInfo = new SchemaInfo();
            var classType = new XmlSchemaComplexType
                                {
                                    Name = t.Name
                                };


            var attribData = t.GetCustomAttributeDataOfType(typeof (DataContractAttribute));
            if (attribData.NamedArguments != null && attribData.NamedArguments.Count > 0)
            {
                foreach (var p1 in attribData.NamedArguments)
                    switch (p1.MemberInfo.Name)
                    {
                        case "Namespace":
                            schemaInfo.Schema.TargetNamespace = p1.TypedValue.Value as string;
                            break;
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
                    el.SchemaTypeName = xmlName;
                else
                {
                    if (p1.PropertyType.IsListType())
                    {
                        //el.MaxOccursString = "unbounded";
                        //what is this a list of?
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
                                var sName = InternalType(p1.PropertyType.FullName, true);
                                el.SchemaTypeName = new XmlQualifiedName("ArrayOf" + sName);
                                schemaInfo.CustomTypes.Add(new XmlQualifiedName(sName));
                                schemaInfo.CustomTypes.Add(el.SchemaTypeName);
                            }
                        }
                    }
                    else if (p1.PropertyType.Name.Contains("Nullable"))
                    {
                        //what is this a nullable of?
                        if (p1.PropertyType.FullName != null)
                        {
                            var pr = Primitive2Xml.XmlName(InternalType(p1.PropertyType.FullName, false));
                            if (pr != null)
                            {
                                el.SchemaTypeName = pr;
                            }
                            else
                            {
                                var sName = InternalType(p1.PropertyType.FullName, true);
                                el.SchemaTypeName = new XmlQualifiedName(sName);
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


                //get data member
                var aData = p1.GetCustomAttributeDataOfType(typeof (DataMemberAttribute));
                if (aData != null
                    && aData.NamedArguments != null
                    && aData.NamedArguments.Count > 0)
                {
                    foreach (var pp in aData.NamedArguments)
                        switch (pp.MemberInfo.Name)
                        {
                            case "IsRequired":
                                {
                                    var v = (bool) pp.TypedValue.Value;
                                    el.MinOccurs = v ? 1 : 0;
                                }
                                break;
                        }
                }

                sequence.Items.Add(el);
            }


            schemaInfo.Schema.Items.Add(classType);
            return schemaInfo;
        }

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

        public static SchemaInfo GetSchema(this Type t, bool isList)
        {
            if (isList)
                return t.GetListSchema();

            if (t.IsClass)
                return t.GetClassSchema();

            return t.IsEnum ? t.GetEnumSchema() : null;
        }


        public static SchemaInfo GetSchema(this Type t) { return t.GetSchema(false); }

        private static bool IsListType(this Type t)
        {
            return t.Name == typeof (List<>).Name ||
                   t.Name == typeof (ObservableCollection<>).Name ||
                   t.Name == typeof (IList<>).Name ||
                   t.Name == typeof (ICollection<>).Name;
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