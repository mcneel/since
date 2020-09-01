using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml;

namespace CppSince
{
    class Program
    {
        static void Main(string[] args)
        {
            var v7Dictionary = ParsedMember.BuildMemberDictionary(@"C:\dev\github\mcneel\since\cpp_versions\7.0\index.xml");
            var v6Dictionary = ParsedMember.BuildMemberDictionary(@"C:\dev\github\mcneel\since\cpp_versions\6.0\index.xml");
            var sb = new System.Text.StringBuilder();
            foreach(var kv in v6Dictionary)
            {
                if(!v7Dictionary.ContainsKey(kv.Key) &&
                   !kv.Value.IsPrivate)
                {
                    sb.AppendLine(kv.Value.Signature);
                }
            }
            System.IO.File.WriteAllText("v7removed.txt", sb.ToString());

            sb = new System.Text.StringBuilder();
            foreach (var kv in v7Dictionary)
            {
                if (!v6Dictionary.ContainsKey(kv.Key))
                {
                    sb.AppendLine(kv.Value.Signature);
                }
            }
            System.IO.File.WriteAllText("v7added.txt", sb.ToString());
        }
    }

    class ParsedMember
    {
        string _name;
        public ParsedMember Parent { get; set; }
        public string Kind { get; set; }
        public string Name
        {
            get { return _name; }
            set 
            {
                if (value.StartsWith("@"))
                    _name = "";
                else
                    _name = value;
            }
        }
        public string RefId { get; set; }

        public string Signature
        {
            get
            {
                string s = $"{Kind}::{Name}";
                if (Parent != null)
                    s = Parent.Signature + "::" + s;
                return s;
            }
        }

        public string XmlDirectory { get; set; }

        bool? _isPrivate = null;
        public bool IsPrivate
        {
            get
            {
                if (!_isPrivate.HasValue)
                {
                    var p = Parent;
                    if (p.Parent != null)
                        p = p.Parent;
                    string xmlfile = System.IO.Path.Combine(XmlDirectory, p.RefId + ".xml");
                    string content = System.IO.File.ReadAllText(xmlfile);
                    int index = content.IndexOf(RefId);
                    int protIndex = content.IndexOf("prot", index);
                    int gtIndex = content.IndexOf(">", index);
                    if (gtIndex < protIndex)
                        throw new Exception();
                    content = content.Substring(protIndex + "prot=\"".Length);
                    _isPrivate = content.StartsWith("private");
                }
                return _isPrivate.Value;
            }
        }

        public static Dictionary<string, ParsedMember> BuildMemberDictionary(string path)
        {
            string directory = System.IO.Path.GetDirectoryName(path);
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Parse;

            using (var reader = XmlReader.Create(path, settings))
            {
                Dictionary<string, ParsedMember> dict = new Dictionary<string, ParsedMember>();
                ParsedMember parentMember = null;
                reader.MoveToContent();
                // Parse the file and display each of the nodes.
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            if (reader.Name.Equals("compound"))
                            {
                                string refid = reader.GetAttribute("refid");
                                string kind = reader.GetAttribute("kind");
                                while (reader.NodeType != XmlNodeType.Text)
                                    reader.Read();
                                parentMember = new ParsedMember()
                                {
                                    XmlDirectory = directory,
                                    Kind = kind,
                                    Name = reader.Value,
                                    RefId = refid
                                };
                            }
                            else if (reader.Name.Equals("member"))
                            {
                                string refid = reader.GetAttribute("refid");
                                string kind = reader.GetAttribute("kind");
                                while (reader.NodeType != XmlNodeType.Text)
                                    reader.Read();

                                if (parentMember != null && parentMember.Kind.Equals("enum"))
                                {
                                    if (!kind.Equals("enumvalue"))
                                        parentMember = parentMember.Parent;
                                }

                                var member = new ParsedMember()
                                {
                                    XmlDirectory = directory,
                                    Parent = parentMember,
                                    RefId = refid,
                                    Kind = kind,
                                    Name = reader.Value
                                };

                                if (kind.Equals("enum"))
                                {
                                    parentMember = member;
                                }

                                if (!kind.Equals("define") && !kind.Equals("friend"))
                                    dict[member.Signature] = member;
                            }
                            break;
                        case XmlNodeType.EndElement:
                            if (reader.Name.Equals("compound"))
                                parentMember = null;
                            break;
                        default:
                            break;
                    }
                }

                return dict;
            }
        }
    }
}
