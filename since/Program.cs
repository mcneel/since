using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace since
{
    class Program
    {
        static void Main(string[] args)
        {
            const string pathOld = @"..\..\..\rhinocommon_versions\6\6.26\RhinoCommon.dll";
            const string sinceVersion = "7.0";
            string pathNew = $"..\\..\\..\\rhinocommon_versions\\{sinceVersion}\\RhinoCommon.dll";
            var newMembersTask = NewMembersAsync(pathOld, pathNew);

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
                var newMembers = newMembersTask.Result;
                foreach (var parsedItem in parsedItems)
                {
                    string signature = parsedItem.Signature.ToLower();
                    if (!newMembers.ContainsKey(signature))
                        continue;
                    newMembers[signature] = true;
                    countFoundInSource++;

                    if (!parsedItem.HasSinceTag())
                    {
                        int insertIndex = parsedItem.SinceInsertIndex();
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
                }
                if (modified)
                    System.IO.File.WriteAllText(sourceFile, text);
            }

            foreach (var kv in newMembersTask.Result)
            {
                if (kv.Value == false)
                {
                    Console.WriteLine($"Missed {kv.Key}");
                }
            }

            Console.WriteLine($"{newMembersTask.Result.Keys.Count} new items");
            Console.WriteLine($"{countFoundInSource} found in source");

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

        static Task<Dictionary<string, bool>> NewMembersAsync(string oldAssemblyPath, string newAssemblyPath)
        {
            return Task.Run(() =>
            {
                var oldDict = AssemblyItems(oldAssemblyPath);
                var newDict = AssemblyItems(newAssemblyPath);

                Dictionary<string, bool> newItems = new Dictionary<string, bool>();
                foreach (var key in newDict.Keys)
                {
                    if (!oldDict.ContainsKey(key))
                        newItems[key] = false;
                }
                return newItems;
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

        static Dictionary<string, bool> AssemblyItems(string path)
        {
            Dictionary<string, bool> items = new Dictionary<string, bool>();
            if (string.IsNullOrEmpty(path))
                return items;
            System.Reflection.Assembly assembly = System.Reflection.Assembly.LoadFrom(path);
            Type[] types = assembly.GetExportedTypes();
            foreach (var type in types)
            {
                if (type.IsEnum)
                {
                    string key = type.FullName.ToString().ToLower().Replace("+", ".");
                    items[key] = false;
                    continue;
                }

                var methods = type.GetMethods();
                foreach (var method in methods)
                {
                    if (method.DeclaringType != type)
                        continue;
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
                    items[key] = false;
                }

                var constructors = type.GetConstructors();
                foreach (var constructor in constructors)
                {
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
                    items[key] = false;
                }
            }
            return items;
        }
    }
}
