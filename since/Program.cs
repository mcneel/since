using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace since
{
    class Program
    {
        static void Main(string[] args)
        {
            //string[] directories = Directory.GetDirectories(@"..\..\..\rhinocommon_versions\6");

            const string pathOld = @"..\..\..\rhinocommon_versions\7.4\RhinoCommon.dll";
            const string sinceVersion = "7.5";
            string pathNew = $"..\\..\\..\\rhinocommon_versions\\{sinceVersion}\\RhinoCommon.dll";
            var modifiedMembersTask = ModifiedMembersAsync(pathOld, pathNew);
            bool fileWritten = false;
            int countFoundInSource = 0;
            string rhinocommonDirectory = @"C:\dev\github\mcneel\rhino\src4\DotNetSDK\rhinocommon\dotnet\";
            foreach (var sourceFile in AllSourceFiles(rhinocommonDirectory))
            {
                bool modified = false;
                //Console.WriteLine($"parse: {sourceFile}");
                string text = System.IO.File.ReadAllText(sourceFile);
                var parsedItems = SourceFileWalker.ParseSource(text);
                // Reverse so we walk backward through the source. This will allow us
                // to use proper character offets as we insert strings
                parsedItems.Reverse();
                var modifiedMembers = modifiedMembersTask.Result;
                foreach (var parsedItem in parsedItems)
                {
                    string signature = parsedItem.Signature.ToLower();

                    ReflectedItem modifiedMember;
                    if( !modifiedMembers.TryGetValue(signature, out modifiedMember))
                        continue;
                    modifiedMember.FoundInParsedCode = true;
                    countFoundInSource++;
                    //if (modifiedMember.AddedInNewVersion && modifiedMember.ObsoletedInNewVersion)
                    //    throw new Exception("This looks sketchy");

                    if (!parsedItem.HasSinceTag() && modifiedMember.AddedInNewVersion)
                    {
                        int insertIndex = parsedItem.TagInsertIndex();
                        StringBuilder since = new StringBuilder();
                        while (text[insertIndex - 1] == ' ')
                        {
                            since.Append(" ");
                            insertIndex--;
                        }
                        since.AppendLine($"/// <since>{sinceVersion}</since>");
                        text = text.Insert(insertIndex, since.ToString());
                        modified = true;
                    }
                    if (!parsedItem.HasDeprecatedTag() && modifiedMember.ObsoletedInNewVersion)
                    {
                        int insertIndex = parsedItem.TagInsertIndex();
                        StringBuilder deprecated = new StringBuilder();
                        while (text[insertIndex - 1] == ' ')
                        {
                            deprecated.Append(" ");
                            insertIndex--;
                        }
                        deprecated.AppendLine($"/// <deprecated>{sinceVersion}</deprecated>");
                        text = text.Insert(insertIndex, deprecated.ToString());
                        modified = true;

                    }
                }
                if (modified)
                {
                    System.IO.File.WriteAllText(sourceFile, text);
                    fileWritten = true;
                }
            }

            foreach (var kv in modifiedMembersTask.Result)
            {
                if (kv.Value.FoundInParsedCode == false)
                {
                    Console.WriteLine($"Missed {kv.Key}");
                }
            }

            Console.WriteLine($"{modifiedMembersTask.Result.Keys.Count} modified items");
            Console.WriteLine($"{countFoundInSource} found in source");
            Console.WriteLine($"Any files written = {fileWritten}");
            Console.ReadKey();
        }

        static IEnumerable<string> AllSourceFiles(string sourcePath)
        {
            foreach (string file in System.IO.Directory.EnumerateFiles(sourcePath, "*.cs", System.IO.SearchOption.AllDirectories))
            {
                string filename = System.IO.Path.GetFileName(file);
                if (filename.Equals(".cs") || filename.StartsWith("AutoNative"))
                    continue;
                if (file.Contains("\\obj\\"))
                    continue;
                yield return file;
            }
        }

        class ReflectedItem
        {
            public bool ObsoletedInNewVersion { get; set; } = false;
            public bool AddedInNewVersion { get; set; } = false;
            public bool FoundInParsedCode { get; set; } = false;
        }

        static Task<Dictionary<string, ReflectedItem>> ModifiedMembersAsync(string oldAssemblyPath, string newAssemblyPath)
        {
            return Task.Run(() =>
            {
                var oldDict = AssemblyItems(oldAssemblyPath);
                var newDict = AssemblyItems(newAssemblyPath);

                Dictionary<string, ReflectedItem> modifiedItems = new Dictionary<string, ReflectedItem>();
                foreach(var kvp in newDict)
                {
                    string key = kvp.Key;
                    bool isObsolete = kvp.Value;
                    ReflectedItem item = new ReflectedItem();

                    // if the item is not in oldDict, then it is new
                    if (!oldDict.ContainsKey(key))
                    {
                        item.AddedInNewVersion = true;
                        item.ObsoletedInNewVersion = kvp.Value;
                    }
                    else if(isObsolete)
                    {
                        bool obsoleteInOld = oldDict[key];
                        if (!obsoleteInOld)
                            item.ObsoletedInNewVersion = true;
                    }

                    if (item.ObsoletedInNewVersion || item.AddedInNewVersion)
                        modifiedItems.Add(key, item);
                }
                return modifiedItems;
            });
        }

        static string TweakTypeName(string name)
        {
            bool isArray = name.EndsWith("[]");
            if (isArray)
                name = name.Substring(0, name.Length - 2);
            if (name.Equals("Boolean"))
                name = "bool";
            if (name.Equals("Int32"))
                name = "int";
            if (name.Equals("UInt32"))
                name = "uint";
            if (name.Equals("Single"))
                name = "float";
            if (name.Equals("Int16"))
                name = "short";
            if (name.Equals("UInt16"))
                name = "ushort";
            if (isArray)
                name += "[]";

            return name;
        }

        /// <summary>
        /// Get all public items in an assembly along with true/false if they are obsolete
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        static Dictionary<string, bool> AssemblyItems(string path)
        {
            Dictionary<string, bool> items = new Dictionary<string, bool>();
            if (string.IsNullOrEmpty(path))
                return items;
            System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFrom(path);
            Type[] types = assembly.GetExportedTypes();
            foreach (var type in types)
            {
                bool typeIsObsolete = (type.GetCustomAttribute(typeof(ObsoleteAttribute)) != null);
                if (type.IsEnum)
                {
                    string key = type.FullName.ToString().ToLower().Replace("+", ".");
                    items[key] = typeIsObsolete;
                    continue;
                }
                var methods = type.GetMethods();
                foreach (var method in methods)
                {
                    if (method.DeclaringType != type)
                        continue;

                    bool methodIsObsolete = (method.GetCustomAttribute(typeof(ObsoleteAttribute)) != null);

                    StringBuilder signature = new StringBuilder($"{type.FullName}.{method.Name}");

                    if (method.IsSpecialName)
                    {
                        string name = method.Name;
                        if (name.StartsWith("get_") || name.StartsWith("set_") || name.StartsWith("add_"))
                            signature = new StringBuilder($"{type.FullName}.{name.Substring(4)}");
                        if (name.StartsWith("remove_"))
                            signature = new StringBuilder($"{type.FullName}.{name.Substring(7)}");
                        if (name.StartsWith("op_"))
                        {
                            switch (name)
                            {
                                case "op_Equality":
                                    name = "==";
                                    break;
                                case "op_Inequality":
                                    name = "!=";
                                    break;
                                case "op_Addition":
                                    name = "+";
                                    break;
                                case "op_Subtraction":
                                    name = "-";
                                    break;
                                case "op_LessThan":
                                    name = "<";
                                    break;
                                case "op_GreaterThan":
                                    name = ">";
                                    break;
                                case "op_Multiply":
                                    name = "*";
                                    break;
                                case "op_Division":
                                    name = "/";
                                    break;
                                case "op_LessThanOrEqual":
                                    name = "<=";
                                    break;
                                case "op_GreaterThanOrEqual":
                                    name = ">=";
                                    break;
                                default:
                                    break;
                            }
                            signature = new StringBuilder($"{type.FullName}.{name}");
                        }
                    }
                    else
                    {
                        signature.Append("(");
                        var parameters = method.GetParameters();
                        if (parameters.Length == 0)
                        {
                            if (method.Name.Equals("GetHashCode") || method.Name.Equals("ToString"))
                                continue;
                        }
                        if (parameters.Length == 1 && method.Name.Equals("Equals"))
                        {
                            if (parameters[0].ParameterType.Name.Equals("Object"))
                                continue;
                        }

                        for (int i = 0; i < parameters.Length; i++)
                        {
                            if (i > 0)
                                signature.Append(",");
                            var ptype = parameters[i].ParameterType;
                            string name = ptype.Name.Replace("&", "");
                            if (ptype.IsGenericType && ptype.GenericTypeArguments.Length > 0)
                            //              if (name.Equals("IEnumerable`1"))
                            {
                                int index = name.IndexOf("`");

                                name = TweakTypeName(name.Substring(0, index)) + "<";
                                for (int j = 0; j < ptype.GenericTypeArguments.Length; j++)
                                {
                                    if (j > 0)
                                        name += ",";
                                    name += TweakTypeName(ptype.GenericTypeArguments[j].Name);
                                }
                                name += ">";
                            }
                            else
                                name = TweakTypeName(name);

                            signature.Append(name);
                        }
                        signature.Append(")");
                    }

                    string key = signature.ToString().ToLower().Replace("+", ".");
                    items[key] = typeIsObsolete || methodIsObsolete;
                }

                var constructors = type.GetConstructors();
                foreach (var constructor in constructors)
                {
                    bool constructorIsObsolete = (constructor.GetCustomAttribute(typeof(ObsoleteAttribute)) != null);

                    StringBuilder signature = new StringBuilder($"{type.FullName}");
                    signature.Append("(");
                    var parameters = constructor.GetParameters();
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (i > 0)
                            signature.Append(",");
                        var ptype = parameters[i].ParameterType;
                        string name = ptype.Name.Replace("&", "");
                        if (ptype.IsGenericType && ptype.GenericTypeArguments.Length > 0)
                        //              if (name.Equals("IEnumerable`1"))
                        {
                            int index = name.IndexOf("`");

                            name = TweakTypeName(name.Substring(0, index)) + "<";
                            for (int j = 0; j < ptype.GenericTypeArguments.Length; j++)
                            {
                                if (j > 0)
                                    name += ",";
                                name += TweakTypeName(ptype.GenericTypeArguments[j].Name);
                            }
                            name += ">";
                        }
                        else
                            name = TweakTypeName(name);

                        signature.Append(name);
                    }
                    signature.Append(")");

                    string key = signature.ToString().ToLower().Replace("+", ".");
                    items[key] = typeIsObsolete || constructorIsObsolete;
                }
            }
            return items;
        }
    }
}
